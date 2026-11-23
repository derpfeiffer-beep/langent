using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Langent.App.Services;
using Langent.Core;
using Langent.Core.Settings;
using Langent.Core.Translation;
using Langent.Core.Usage;

namespace Langent.App.Views;

public sealed partial class PriceRow : ObservableObject
{
    public PriceRow(string model, decimal input, decimal output)
    {
        _model = model;
        _inputPerMTokUsd = input;
        _outputPerMTokUsd = output;
    }

    [ObservableProperty]
    private string _model;

    [ObservableProperty]
    private decimal _inputPerMTokUsd;

    [ObservableProperty]
    private decimal _outputPerMTokUsd;
}

public sealed partial class GlossaryRow : ObservableObject
{
    public GlossaryRow(string source, string target)
    {
        _source = source;
        _target = target;
    }

    [ObservableProperty]
    private string _source;

    [ObservableProperty]
    private string _target;
}

/// <summary>Backs the settings window (SPEC §8.3).</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly UsageLog _usageLog;
    private readonly ConnectionTester _tester;
    private readonly Func<string?> _resolveExistingKey;
    private readonly string _usageCsvPath;
    private readonly Action _onSaved;
    private readonly Action _requestClose;

    public SettingsViewModel(
        AppSettings settings,
        SettingsStore store,
        UsageLog usageLog,
        ConnectionTester tester,
        Func<string?> resolveExistingKey,
        string usageCsvPath,
        bool keyMissingHint,
        Action onSaved,
        Action requestClose)
    {
        _settings = settings;
        _store = store;
        _usageLog = usageLog;
        _tester = tester;
        _resolveExistingKey = resolveExistingKey;
        _usageCsvPath = usageCsvPath;
        _onSaved = onSaved;
        _requestClose = requestClose;

        Languages = new ObservableCollection<Language>(Core.Languages.All);
        ToneOptions =
        [
            new ToneOption(Tone.Formal, "Sie"),
            new ToneOption(Tone.Informal, "Du"),
            new ToneOption(Tone.Neutral, "Neutral"),
        ];

        _selectedTarget = Core.Languages.Find(settings.TargetLanguage) ?? Languages[0];
        _selectedSecondary = Core.Languages.Find(settings.SecondaryLanguage) ?? Languages[1];
        _selectedTone = ToneOptions.FirstOrDefault(t => t.Tone == settings.Tone) ?? ToneOptions[2];
        _autostart = settings.Autostart;
        _doubleCopyEnabled = settings.Trigger.DoubleCopyEnabled;
        _windowMs = settings.Trigger.WindowMs;

        _model = settings.Model;
        _qualityModel = settings.QualityModel;
        _customInstructions = settings.CustomInstructions;
        KeyMissingHint = keyMissingHint;
        KeyStatus = string.IsNullOrWhiteSpace(_resolveExistingKey()) ? "nicht gesetzt" : "gesetzt";

        Prices = [];
        foreach (var (model, price) in settings.Prices)
        {
            Prices.Add(new PriceRow(model, price.InputPerMTokUsd, price.OutputPerMTokUsd));
        }

        EnsurePriceRow(settings.Model);
        EnsurePriceRow(settings.QualityModel);

        Glossary = [];
        foreach (var entry in settings.Glossary)
        {
            Glossary.Add(new GlossaryRow(entry.Source, entry.Target));
        }

        _restoreClipboard = settings.RestoreClipboard;
        _excludedProcessesText = string.Join(Environment.NewLine, settings.ExcludedProcesses);
        _maxInputChars = settings.MaxInputChars;
        _monthlyBudget = settings.MonthlyBudgetUsd?.ToString(CultureInfo.InvariantCulture) ?? "";

        RefreshUsage();
    }

    public ObservableCollection<Language> Languages { get; }

    public ObservableCollection<ToneOption> ToneOptions { get; }

    public ObservableCollection<PriceRow> Prices { get; }

    public ObservableCollection<GlossaryRow> Glossary { get; }

    [ObservableProperty]
    private Language _selectedTarget;

    [ObservableProperty]
    private Language _selectedSecondary;

    [ObservableProperty]
    private ToneOption _selectedTone;

    [ObservableProperty]
    private bool _autostart;

    [ObservableProperty]
    private bool _doubleCopyEnabled;

    [ObservableProperty]
    private int _windowMs;

    [ObservableProperty]
    private string _model;

    [ObservableProperty]
    private string _qualityModel;

    [ObservableProperty]
    private string _customInstructions;

    [ObservableProperty]
    private bool _restoreClipboard;

    [ObservableProperty]
    private string _excludedProcessesText;

    [ObservableProperty]
    private int _maxInputChars;

    [ObservableProperty]
    private string _monthlyBudget;

    [ObservableProperty]
    private bool _keyMissingHint;

    [ObservableProperty]
    private string _keyStatus = "nicht gesetzt";

    [ObservableProperty]
    private string _testStatus = "";

    [ObservableProperty]
    private bool _testInProgress;

    public bool TestNotInProgress => !TestInProgress;

    partial void OnTestInProgressChanged(bool value) => OnPropertyChanged(nameof(TestNotInProgress));

    [ObservableProperty]
    private string _todayText = "";

    [ObservableProperty]
    private string _monthText = "";

    [ObservableProperty]
    private string _previousMonthText = "";

    /// <summary>Set by the window from the PasswordBox; empty means "leave unchanged".</summary>
    public string ApiKeyInput { get; set; } = "";

    private void EnsurePriceRow(string model)
    {
        if (Prices.All(p => !string.Equals(p.Model, model, StringComparison.OrdinalIgnoreCase)))
        {
            Prices.Add(new PriceRow(model, 0m, 0m));
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        TestInProgress = true;
        TestStatus = "Teste …";
        try
        {
            var key = string.IsNullOrWhiteSpace(ApiKeyInput) ? _resolveExistingKey() : ApiKeyInput;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var result = await _tester.TestAsync(key, Model, cts.Token).ConfigureAwait(true);
            TestStatus = result.Message;
        }
        finally
        {
            TestInProgress = false;
        }
    }

    [RelayCommand]
    private void OpenUsageCsv()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_usageCsvPath) { UseShellExecute = true });
        }
        catch (Exception)
        {
            TestStatus = "usage.csv ist noch nicht vorhanden.";
        }
    }

    [RelayCommand]
    private void Save()
    {
        _settings.TargetLanguage = SelectedTarget.Code;
        _settings.SecondaryLanguage = SelectedSecondary.Code;
        _settings.Tone = SelectedTone.Tone;
        _settings.Autostart = Autostart;
        _settings.Trigger.DoubleCopyEnabled = DoubleCopyEnabled;
        _settings.Trigger.WindowMs = WindowMs;
        _settings.Model = Model;
        _settings.QualityModel = QualityModel;
        _settings.CustomInstructions = CustomInstructions;
        _settings.RestoreClipboard = RestoreClipboard;
        _settings.MaxInputChars = MaxInputChars;

        _settings.Prices = Prices
            .Where(p => !string.IsNullOrWhiteSpace(p.Model))
            .GroupBy(p => p.Model, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => new ModelPrice(g.Last().InputPerMTokUsd, g.Last().OutputPerMTokUsd));

        _settings.Glossary = Glossary
            .Where(g => !string.IsNullOrWhiteSpace(g.Source))
            .Select(g => new GlossaryEntry(g.Source.Trim(), g.Target.Trim()))
            .ToList();

        _settings.ExcludedProcesses = ExcludedProcessesText
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        _settings.MonthlyBudgetUsd =
            decimal.TryParse(MonthlyBudget, NumberStyles.Number, CultureInfo.InvariantCulture, out var budget)
                ? budget
                : null;

        if (!string.IsNullOrEmpty(ApiKeyInput))
        {
            _settings.ApiKeyProtected = SecretProtector.Protect(ApiKeyInput);
        }

        _store.Save(_settings);
        _onSaved();
        _requestClose();
    }

    [RelayCommand]
    private void CancelDialog() => _requestClose();

    private void RefreshUsage()
    {
        var now = DateTime.Now;
        TodayText = Describe(_usageLog.TotalsForDay(now));
        MonthText = Describe(_usageLog.TotalsForMonth(now));
        PreviousMonthText = Describe(_usageLog.TotalsForMonth(now.AddMonths(-1)));
    }

    private static string Describe(UsageTotals totals)
        => $"{totals.Count} Übersetzungen · {totals.InputTokens}+{totals.OutputTokens} Tokens · " +
           $"${totals.CostUsd.ToString("0.######", CultureInfo.InvariantCulture)}";
}
