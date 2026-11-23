using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using H.NotifyIcon;
using Langent.App.Infrastructure;
using Langent.App.Services;

namespace Langent.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private TaskbarIcon? _trayIcon;
    private Logging? _log;
    private SingleInstanceSignalServer? _signalServer;
    private AppController? _controller;

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

        _trayIcon = CreateTrayIcon();

        _controller = new AppController(_log);
        _controller.Start();
    }

    private TaskbarIcon CreateTrayIcon()
    {
        var menu = new ContextMenu();
        var exitItem = new MenuItem { Header = "Beenden" };
        exitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(exitItem);

        var icon = new TaskbarIcon
        {
            ToolTipText = "Langent",
            ContextMenu = menu,
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
        };
        icon.ForceCreate();
        return icon;
    }

    private void OnSecondInstanceSignalled()
    {
        // SPEC §9: a second start should open the settings window. The window
        // arrives in M5; for now the hit is recorded so the behaviour is visible.
        _log?.Info("Zweiter Start erkannt (Signal über Named Pipe).");
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
