using System.Diagnostics;
using System.Windows.Threading;
using Langent.App.Services;
using Langent.App.Views;
using Langent.Core.Settings;
using Langent.Core.Translation;
using Langent.Core.Trigger;
using Langent.Core.Usage;

namespace Langent.App;

/// <summary>
/// Orchestrates the Strg+C+C flow (SPEC §4.2): on a trigger it reads and saves the
/// clipboard, remembers the foreground window, and opens the translation window
/// (SPEC §8.1). Replace/copy happen inside the window. Runs on the UI thread.
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly Logging _log;
    private readonly AppSettings _settings;
    private readonly ITranslationProvider _provider;
    private readonly UsageLog _usageLog;
    private readonly Action _persistSettings;
    private readonly Dispatcher _dispatcher;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DoubleCopyDetector _detector;
    private readonly AppProfiles _profiles;
    private readonly ClipboardWatcher _watcher;
    private readonly ClipboardService _clipboard;
    private readonly WindowFocusService _windows = new();

    private TranslationWindow? _currentWindow;

    public AppController(
        Logging log,
        AppSettings settings,
        ITranslationProvider provider,
        UsageLog usageLog,
        Action persistSettings,
        Dispatcher dispatcher)
    {
        _log = log;
        _settings = settings;
        _provider = provider;
        _usageLog = usageLog;
        _persistSettings = persistSettings;
        _dispatcher = dispatcher;

        _detector = new DoubleCopyDetector(
            settings.Trigger.WindowMs, settings.Trigger.BurstMergeMs, settings.Trigger.DoubleCopyEnabled);
        _profiles = new AppProfiles(settings.ExcludedProcesses);
        _watcher = new ClipboardWatcher(_clock);
        _clipboard = new ClipboardService(_clock, _detector);
        _watcher.ClipboardUpdated += OnClipboardUpdated;
    }

    /// <summary>Pause hook for the tray (M5).</summary>
    public DoubleCopyDetector Detector => _detector;

    public void Start()
    {
        _watcher.Start();
        _log.Info("Strg+C+C-Erkennung aktiv.");
    }

    private void OnClipboardUpdated(long timestamp, bool ctrlDown)
    {
        if (!_detector.OnClipboardUpdate(timestamp, ctrlDown))
        {
            return;
        }

        try
        {
            HandleTrigger();
        }
        catch (Exception ex)
        {
            _log.Error("Fehler beim Auslösen", ex);
        }
    }

    private void HandleTrigger()
    {
        var foreground = _windows.GetForeground();
        if (_profiles.IsExcluded(foreground.ProcessName))
        {
            _log.Info($"Auslöser ignoriert, Prozess ausgeschlossen: {foreground.ProcessName}");
            return;
        }

        var text = _clipboard.ReadUnicodeText() ?? string.Empty;
        var snapshot = _clipboard.Save();
        var hasSelection = !string.IsNullOrWhiteSpace(text);

        _log.Info($"Auslöser: {text.Length} Zeichen, Prozess {foreground.ProcessName}.");

        OpenWindow(new TranslationContext(text, hasSelection, foreground.Handle, snapshot));
    }

    private void OpenWindow(TranslationContext context)
    {
        CloseCurrent();

        var viewModel = new TranslationViewModel(
            context, _provider, _settings, _usageLog, _clipboard, _windows, _log);
        var window = new TranslationWindow(viewModel, _settings, _persistSettings);
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_currentWindow, window))
            {
                _currentWindow = null;
            }
        };

        _currentWindow = window;
        window.Show();
        window.Activate();
    }

    private void CloseCurrent()
    {
        if (_currentWindow is null)
        {
            return;
        }

        var window = _currentWindow;
        _currentWindow = null;
        window.Close();
    }

    public void Dispose()
    {
        _watcher.ClipboardUpdated -= OnClipboardUpdated;
        _watcher.Dispose();
    }
}
