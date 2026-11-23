using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using H.NotifyIcon;
using Langent.App.Infrastructure;
using Langent.App.Services;
using Langent.Core.Infrastructure;
using Langent.Core.Settings;
using Langent.Core.Translation;
using Langent.Core.Usage;

namespace Langent.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private TaskbarIcon? _trayIcon;
    private MenuItem? _pauseItem;
    private Logging? _log;
    private SingleInstanceSignalServer? _signalServer;
    private AppController? _controller;
    private HttpClient? _httpClient;

    private UsageLog? _usageLog;
    private AppSettings? _settings;
    private ApiKeyResolver? _apiKeyResolver;

    private string _budgetMonth = "";
    private bool _budget80Notified;
    private bool _budget100Notified;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(initiallyOwned: true, SingleInstance.MutexName, out var createdNew);
        if (!createdNew)
        {
            SingleInstance.NotifyExistingInstance();
            Shutdown();
            return;
        }

        var paths = AppPaths.Resolve();
        _log = new Logging(paths.LogFilePath);
        _log.Info("Langent gestartet.");

        SetupGlobalExceptionHandling(_log);

        _signalServer = new SingleInstanceSignalServer(_log, OnSecondInstanceSignalled);
        _signalServer.Start();

        _controller = BuildController(_log);
        _trayIcon = CreateTrayIcon();

        RefreshTrayTooltip();

        var exePath = Environment.ProcessPath ?? string.Empty;
        AutostartService.EnsureCurrentPath(_settings!.Autostart, exePath);

        _controller.Start();

        if (string.IsNullOrWhiteSpace(_apiKeyResolver!.Resolve()))
        {
            _log.Info("Erster Start ohne API-Schlüssel: Einstellungen werden geöffnet.");
            _controller.OpenSettings(keyMissingHint: true);
        }
    }

    private AppController BuildController(Logging log)
    {
        var exeDirectory = AppContext.BaseDirectory;
        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppPaths.AppDataFolderName);

        var fileSystem = new FileSystem();
        var resolver = new FilePathResolver(new FileSystemDirectoryProbe());

        var settingsStore = new SettingsStore(fileSystem, resolver, exeDirectory, appDataDirectory);
        _settings = settingsStore.Load();

        var usagePath = resolver.ResolvePath(exeDirectory, appDataDirectory, UsageLog.FileName);
        _usageLog = new UsageLog(fileSystem, usagePath);
        _usageLog.Changed += OnUsageChanged;

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _apiKeyResolver = new ApiKeyResolver(() => _settings);
        var provider = new ClaudeTranslationProvider(_httpClient, _apiKeyResolver.Resolve);
        var connectionTester = new ConnectionTester(_httpClient);
        var exePath = Environment.ProcessPath ?? string.Empty;

        return new AppController(
            log, _settings, settingsStore, provider, _usageLog, connectionTester,
            _apiKeyResolver, exePath, usagePath, Dispatcher);
    }

    private TaskbarIcon CreateTrayIcon()
    {
        var menu = new ContextMenu();

        var translateItem = new MenuItem { Header = "Übersetzen …" };
        translateItem.Click += (_, _) => _controller?.OpenFreeInputWindow();
        menu.Items.Add(translateItem);

        _pauseItem = new MenuItem { Header = "Pausieren", IsCheckable = true };
        _pauseItem.Click += (_, _) => _controller?.SetPaused(_pauseItem!.IsChecked);
        menu.Items.Add(_pauseItem);

        var settingsItem = new MenuItem { Header = "Einstellungen" };
        settingsItem.Click += (_, _) => _controller?.OpenSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Beenden" };
        exitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(exitItem);

        var icon = new TaskbarIcon
        {
            ToolTipText = "Langent",
            ContextMenu = menu,
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
        };
        icon.TrayMouseDoubleClick += (_, _) => _controller?.OpenSettings();
        icon.ForceCreate();
        return icon;
    }

    private void OnUsageChanged() => Dispatcher.Invoke(() =>
    {
        RefreshTrayTooltip();
        CheckMonthlyBudget();
    });

    private void RefreshTrayTooltip()
    {
        if (_trayIcon is null || _usageLog is null)
        {
            return;
        }

        var now = DateTime.Now;
        var today = _usageLog.TotalsForDay(now).CostUsd;
        var month = _usageLog.TotalsForMonth(now).CostUsd;
        _trayIcon.ToolTipText = $"Langent · heute {Money(today)} · Monat {Money(month)}";
    }

    private void CheckMonthlyBudget()
    {
        if (_settings?.MonthlyBudgetUsd is not { } budget || budget <= 0 || _usageLog is null)
        {
            return;
        }

        var now = DateTime.Now;
        var monthKey = now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        if (monthKey != _budgetMonth)
        {
            _budgetMonth = monthKey;
            _budget80Notified = false;
            _budget100Notified = false;
        }

        var month = _usageLog.TotalsForMonth(now).CostUsd;

        if (!_budget100Notified && month >= budget)
        {
            _budget100Notified = true;
            _budget80Notified = true;
            Notify("Monatsbudget erreicht (100 %).");
        }
        else if (!_budget80Notified && month >= 0.8m * budget)
        {
            _budget80Notified = true;
            Notify("80 % des Monatsbudgets erreicht.");
        }
    }

    private void Notify(string message)
    {
        try
        {
            _trayIcon?.ShowNotification("Langent", message);
        }
        catch (Exception)
        {
            // Balloon is best-effort.
        }
    }

    private static string Money(decimal value)
        => "$" + value.ToString("0.######", CultureInfo.InvariantCulture);

    private void OnSecondInstanceSignalled()
    {
        _log?.Info("Zweiter Start erkannt: Einstellungen werden geöffnet.");
        _controller?.OpenSettings();
    }

    private void SetupGlobalExceptionHandling(Logging log)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            log.Error("Unbehandelte UI-Ausnahme", args.Exception);
            MessageBox.Show(
                "Ein unerwarteter Fehler ist aufgetreten. Langent läuft weiter.",
                "Langent",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                log.Error("Unbehandelte Ausnahme", ex);
            }
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _httpClient?.Dispose();
        _trayIcon?.Dispose();
        _signalServer?.Dispose();

        _log?.Info("Langent beendet.");

        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex was not owned (we exited as a second instance).
            }

            _mutex.Dispose();
        }

        base.OnExit(e);
    }
}
