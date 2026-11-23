using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Langent.App.Services;
using Langent.Core;
using Langent.Core.Settings;
using Langent.Core.Translation;
using Langent.Core.Usage;

namespace Langent.App.Views;

/// <summary>A choice in the source-language dropdown; a null <see cref="Code"/> means auto-detect.</summary>
public sealed partial class SourceOption : ObservableObject
{
    public SourceOption(string? code, string display)
    {
        Code = code;
        _display = display;
    }

    public string? Code { get; }

    [ObservableProperty]
    private string _display;
}

/// <summary>A tone choice with its German label.</summary>
public sealed record ToneOption(Tone Tone, string Label);

/// <summary>Everything the window needs to run and complete one translation.</summary>
public sealed record TranslationContext(
    string OriginalText,
    bool HasSelection,
    IntPtr ForegroundWindow,
    ClipboardSnapshot? Snapshot);

/// <summary>
/// Drives the translation window (SPEC §8.1): streaming, header controls, errors
/// (§5.6), replace/copy (§4.3/§4.4) and the footer. Runs on the UI thread.
/// </summary>
public sealed partial class TranslationViewModel : ObservableObject
{
    private readonly ITranslationProvider _provider;
    private readonly AppSettings _settings;
    private readonly UsageLog _usageLog;
    private readonly ClipboardService _clipboard;
    private readonly WindowFocusService _windows;
    private readonly Logging _log;

    private readonly IntPtr _foregroundWindow;
    private readonly ClipboardSnapshot? _snapshot;

    private CancellationTokenSource? _cts;
    private bool _initializing;

    public TranslationViewModel(
        TranslationContext context,
        ITranslationProvider provider,
        AppSettings settings,
        UsageLog usageLog,
        ClipboardService clipboard,
        WindowFocusService windows,
        Logging log)
    {
        _provider = provider;
        _settings = settings;
        _usageLog = usageLog;
        _clipboard = clipboard;
        _windows = windows;
        _log = log;

        _foregroundWindow = context.ForegroundWindow;
        _snapshot = context.Snapshot;

        _initializing = true;

        SourceOptions = [new SourceOption(null, "Automatisch")];
        foreach (var language in Languages.All)
        {
            SourceOptions.Add(new SourceOption(language.Code, language.GermanName));
        }

        _selectedSource = SourceOptions[0];

        TargetOptions = new ObservableCollection<Language>(Languages.All);
        _selectedTarget = Languages.Find(settings.TargetLanguage) ?? Languages.All[0];

        ToneOptions =
        [
            new ToneOption(Tone.Formal, "Sie"),
            new ToneOption(Tone.Informal, "Du"),
            new ToneOption(Tone.Neutral, "Neutral"),
        ];
        _selectedTone = ToneOptions.FirstOrDefault(t => t.Tone == settings.Tone) ?? ToneOptions[2];

        _originalText = context.OriginalText;
        _originalExpanded = !context.HasSelection;
        _originalEditable = !context.HasSelection;

        UpdateFooter();
        _initializing = false;
    }

    // Header
    public ObservableCollection<SourceOption> SourceOptions { get; }

    public ObservableCollection<Language> TargetOptions { get; }

    public ObservableCollection<ToneOption> ToneOptions { get; }

    [ObservableProperty]
    private SourceOption _selectedSource;

    [ObservableProperty]
    private Language _selectedTarget;

    [ObservableProperty]
    private ToneOption _selectedTone;

    [ObservableProperty]
    private bool _highQuality;

    // Original
    [ObservableProperty]
    private string _originalText;

    [ObservableProperty]
    private bool _originalExpanded;

    [ObservableProperty]
    private bool _originalEditable;

    // Translation
    [ObservableProperty]
    private string _translationText = "";

    [ObservableProperty]
    private bool _isStreaming;

    // Error / gate
    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _showMissingKeyAction;

    [ObservableProperty]
    private bool _needsLengthConfirmation;

    [ObservableProperty]
    private string _footerText = "";

    /// <summary>Set by the window: hide, re-show and close hooks.</summary>
    public Action? HideWindow { get; set; }

    public Action? ShowWindow { get; set; }

    public Action? CloseWindow { get; set; }

    /// <summary>Set by the controller: open the settings window (MissingKey action).</summary>
    public Action? OpenSettingsRequested { get; set; }

    /// <summary>Called by the window after it is shown to begin the flow.</summary>
    public void Begin()
    {
        if (!_initializing && OriginalEditable)
        {
            // No selection: wait for the user to type and press "Neu übersetzen".
            return;
        }

        if (string.IsNullOrWhiteSpace(OriginalText))
        {
            OriginalEditable = true;
            OriginalExpanded = true;
            return;
        }

        if (OriginalText.Length > _settings.MaxInputChars)
        {
            NeedsLengthConfirmation = true;
            ErrorMessage = $"Der Text ist sehr lang ({OriginalText.Length} Zeichen).";
            return;
        }

        _ = StartTranslationAsync();
    }

    partial void OnSelectedSourceChanged(SourceOption value) => RestartIfRunning();

    partial void OnSelectedTargetChanged(Language value) => RestartIfRunning();

    partial void OnSelectedToneChanged(ToneOption value) => RestartIfRunning();

    partial void OnHighQualityChanged(bool value) => RestartIfRunning();

    private void RestartIfRunning()
    {
        if (_initializing)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(OriginalText) && OriginalText.Length <= _settings.MaxInputChars)
        {
            _ = StartTranslationAsync();
        }
    }

    [RelayCommand]
    private void TranslateAnyway()
    {
        NeedsLengthConfirmation = false;
        ErrorMessage = null;
        _ = StartTranslationAsync();
    }

    [RelayCommand]
    private void Retry()
    {
        HasError = false;
        ErrorMessage = null;
        _ = StartTranslationAsync();
    }

    [RelayCommand]
    private void Retranslate() => _ = StartTranslationAsync();

    [RelayCommand]
    private void ToggleOriginal() => OriginalExpanded = !OriginalExpanded;

    [RelayCommand]
    private void Swap()
    {
        _initializing = true;
        var oldTargetCode = SelectedTarget.Code;
        var newSourceCode = oldTargetCode;
        var newTargetCode = SelectedSource.Code ?? DetectedIso ?? SelectedTarget.Code;

        SelectedSource = SourceOptions.FirstOrDefault(o => o.Code == newSourceCode) ?? SourceOptions[0];
        SelectedTarget = Languages.Find(newTargetCode) ?? SelectedTarget;
        _initializing = false;

        RestartIfRunning();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (OpenSettingsRequested is not null)
        {
            OpenSettingsRequested.Invoke();
        }
        else
        {
            _log.Info("Einstellungen angefordert.");
        }
    }

    [RelayCommand]
    private async Task ReplaceAsync()
    {
        CancelCurrent();

        var text = TranslationText;
        _clipboard.WriteText(text);
        HideWindow?.Invoke();

        if (!_windows.IsAlive(_foregroundWindow))
        {
            ShowWindow?.Invoke();
            HasError = true;
            ErrorMessage = "Ursprungsfenster nicht mehr vorhanden, Text liegt in der Zwischenablage.";
            return;
        }

        _windows.Activate(_foregroundWindow);
        await Task.Delay(50).ConfigureAwait(true);
        InputSimulator.SendCtrlV();
        await Task.Delay(400).ConfigureAwait(true);

        if (_settings.RestoreClipboard)
        {
            _clipboard.Restore(_snapshot);
        }

        _log.Info($"Ersetzt: {text.Length} Zeichen.");
        CloseWindow?.Invoke();
    }

    [RelayCommand]
    private void Copy()
    {
        CancelCurrent();
        _clipboard.WriteText(TranslationText);
        _log.Info("Übersetzung in die Zwischenablage kopiert.");
        CloseWindow?.Invoke();
    }

    public void Cancel()
    {
        CancelCurrent();
    }

    private string? DetectedIso { get; set; }

    private async Task StartTranslationAsync()
    {
        CancelCurrent();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        HasError = false;
        ErrorMessage = null;
        ShowMissingKeyAction = false;
        NeedsLengthConfirmation = false;
        TranslationText = "";
        DetectedIso = null;
        IsStreaming = true;

        var model = HighQuality ? _settings.QualityModel : _settings.Model;
        var request = new TranslationRequest(
            OriginalText,
            SelectedTarget.Code,
            _settings.SecondaryLanguage,
            SelectedSource.Code,
            model,
            SelectedTone.Tone,
            _settings.Glossary,
            _settings.CustomInstructions);

        var gotAnything = false;
        try
        {
            await foreach (var update in _provider.TranslateAsync(request, ct).ConfigureAwait(true))
            {
                switch (update)
                {
                    case TranslationUpdate.SourceLanguageDetected d:
                        DetectedIso = d.IsoCode;
                        gotAnything = true;
                        UpdateAutoSourceDisplay();
                        break;
                    case TranslationUpdate.TextDelta t:
                        TranslationText += t.Text;
                        gotAnything = true;
                        break;
                    case TranslationUpdate.Completed c:
                        LogUsage(c.Model, c.InputTokens, c.OutputTokens, cancelled: false);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (gotAnything)
            {
                // SPEC §5.7: record the cancelled request. The provider surfaces usage
                // only on completion, so token counts are 0 here (documented limitation).
                LogUsage(model, 0, 0, cancelled: true);
            }
        }
        catch (TranslationException ex)
        {
            HasError = true;
            ErrorMessage = TranslationErrorText.For(ex);
            ShowMissingKeyAction = ex.Kind == ErrorKind.MissingKey;
            _log.Error($"Übersetzung fehlgeschlagen: {ex.Kind}", ex);
        }
        finally
        {
            IsStreaming = false;
        }
    }

    private void CancelCurrent()
    {
        if (_cts is null)
        {
            return;
        }

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _cts.Dispose();
        _cts = null;
    }

    private void UpdateAutoSourceDisplay()
    {
        var auto = SourceOptions[0];
        var name = DetectedIso is null ? "…" : Languages.Find(DetectedIso)?.GermanName ?? DetectedIso;
        auto.Display = $"Automatisch: {name}";
    }

    private void LogUsage(string model, int input, int output, bool cancelled)
    {
        var cost = CostCalculator.Compute(model, input, output, _settings.Prices);
        var sourceCode = DetectedIso ?? SelectedSource.Code ?? "";
        _usageLog.Append(new UsageEntry(
            DateTime.UtcNow, model, input, output, cost.CostUsd,
            OriginalText.Length, sourceCode, SelectedTarget.Code, cancelled));
        UpdateFooter(model, input, output, cost);
    }

    private void UpdateFooter(string? model = null, int input = 0, int output = 0, CostResult? cost = null)
    {
        var today = _usageLog.TotalsForDay(DateTime.Now).CostUsd;
        var month = _usageLog.TotalsForMonth(DateTime.Now).CostUsd;
        var source = DetectedIso ?? SelectedSource.Code ?? "?";

        if (model is null || cost is null)
        {
            FooterText = $"{source} → {SelectedTarget.Code} · heute {Money(today)} · Monat {Money(month)}";
            return;
        }

        var priceHint = cost.PriceMissing ? " · Preis fehlt" : "";
        FooterText =
            $"{source} → {SelectedTarget.Code} · {ModelNames.ShortName(model)} · {input}+{output} Tokens · " +
            $"{Money(cost.CostUsd)} · heute {Money(today)} · Monat {Money(month)}{priceHint}";
    }

    private static string Money(decimal value)
        => "$" + value.ToString("0.######", CultureInfo.InvariantCulture);
}
