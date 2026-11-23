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
/// Orchestrates the Strg+C+C flow (SPEC §4.2) and app-level UI (translation window,
/// settings window, pause). Runs on the UI thread.
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly Logging _log;
    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly ITranslationProvider _provider;
    private readonly UsageLog _usageLog;
    private readonly ConnectionTester _connectionTester;
    private readonly ApiKeyResolver _apiKeyResolver;
    private readonly string _exePath;
    private readonly string _usageCsvPath;
    private readonly Dispatcher _dispatcher;
    private readonly Action _persistSettings;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DoubleCopyDetector _detector;
    private readonly ClipboardWatcher _watcher;
    private readonly ClipboardService _clipboard;
    private readonly WindowFocusService _windows = new();

    private AppProfiles _profiles;
    private TranslationWindow? _currentWindow;
    private SettingsWindow? _settingsWindow;
    private bool _paused;

    public AppController(
        Logging log,
        AppSettings settings,
        SettingsStore settingsStore,
        ITranslationProvider provider,
        UsageLog usageLog,
        ConnectionTester connectionTester,
        ApiKeyResolver apiKeyResolver,
        string exePath,
        string usageCsvPath,
        Dispatcher dispatcher)
    {
        _log = log;
        _settings = settings;
        _settingsStore = settingsStore;
        _provider = provider;
        _usageLog = usageLog;
        _connectionTester = connectionTester;
        _apiKeyResolver = apiKeyResolver;
        _exePath = exePath;
        _usageCsvPath = usageCsvPath;
        _dispatcher = dispatcher;
        _persistSettings = () => _settingsStore.Save(_settings);

        _detector = new DoubleCopyDetector(
            settings.Trigger.WindowMs, settings.Trigger.BurstMergeMs, settings.Trigger.DoubleCopyEnabled);
        _profiles = new AppProfiles(settings.ExcludedProcesses);
        _watcher = new ClipboardWatcher(_clock);
        _clipboard = new ClipboardService(_clock, _detector);
        _watcher.ClipboardUpdated += OnClipboardUpdated;
    }

    public bool IsPaused => _paused;

    public void Start()
    {
        _watcher.Start();
        _log.Info("Strg+C+C-Erkennung aktiv.");
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        ApplyDetectorEnabled();
        _log.Info(paused ? "Pausiert." : "Fortgesetzt.");
    }

    public void OpenSettings(bool keyMissingHint = false)
    {
        _dispatcher.Invoke(() =>
        {
            if (_settingsWindow is not null)
            {
                _settingsWindow.Activate();
                return;
            }

            var viewModel = new SettingsViewModel(
                _settings, _settingsStore, _usageLog, _connectionTester, _apiKeyResolver.Resolve,
                _usageCsvPath, keyMissingHint, OnSettingsSaved, () => _settingsWindow?.Close());
            var window = new SettingsWindow(viewModel);
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_settingsWindow, window))
                {
                    _settingsWindow = null;
                }
            };

            _settingsWindow = window;
            window.Show();
            window.Activate();
        });
    }

    public void OpenFreeInputWindow()
    {
        _dispatcher.Invoke(() =>
        {
            var foreground = _windows.GetForeground();
            OpenWindow(new TranslationContext("", HasSelection: false, foreground.Handle, Snapshot: null));
        });
    }

    private void OnSettingsSaved()
    {
        _detector.Configure(_settings.Trigger.WindowMs, _settings.Trigger.BurstMergeMs);
        ApplyDetectorEnabled();
        _profiles = new AppProfiles(_settings.ExcludedProcesses);
        AutostartService.Apply(_settings.Autostart, _exePath);
        _log.Info("Einstellungen gespeichert.");
    }

    private void ApplyDetectorEnabled()
        => _detector.Enabled = _settings.Trigger.DoubleCopyEnabled && !_paused;

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
            context, _provider, _settings, _usageLog, _clipboard, _windows, _log)
        {
            OpenSettingsRequested = () => OpenSettings(),
        };
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
