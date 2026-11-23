using System.Diagnostics;
using System.Windows.Threading;
using Langent.App.Services;
using Langent.Core.Trigger;

namespace Langent.App;

/// <summary>
/// Orchestrates the Strg+C+C flow (SPEC §4.2/§4.3). M1 is a risk spike: instead
/// of translating, the selected text is upper-cased and replaced directly, with
/// no window. Runs entirely on the UI thread (clipboard + SendInput need STA).
/// </summary>
public sealed class AppController : IDisposable
{
    private readonly Logging _log;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DoubleCopyDetector _detector = new();
    private readonly AppProfiles _profiles = new();
    private readonly ClipboardWatcher _watcher;
    private readonly ClipboardService _clipboard;
    private readonly WindowFocusService _windows = new();

    private bool _busy;

    public AppController(Logging log)
    {
        _log = log;
        _watcher = new ClipboardWatcher(_clock);
        _clipboard = new ClipboardService(_clock, _detector);
        _watcher.ClipboardUpdated += OnClipboardUpdated;
    }

    /// <summary>Pause hook for the tray (M5): toggling this stops/starts detection.</summary>
    public DoubleCopyDetector Detector => _detector;

    public void Start()
    {
        _watcher.Start();
        _log.Info("Strg+C+C-Erkennung aktiv.");
    }

    private void OnClipboardUpdated(long timestamp, bool ctrlDown)
    {
        if (!_detector.OnClipboardUpdate(timestamp, ctrlDown) || _busy)
        {
            return;
        }

        _ = HandleTriggerAsync();
    }

    private async Task HandleTriggerAsync()
    {
        _busy = true;
        try
        {
            var foreground = _windows.GetForeground();
            if (_profiles.IsExcluded(foreground.ProcessName))
            {
                _log.Info($"Auslöser ignoriert, Prozess ausgeschlossen: {foreground.ProcessName}");
                return;
            }

            var text = _clipboard.ReadUnicodeText();
            if (string.IsNullOrWhiteSpace(text))
            {
                _log.Info("Auslöser, aber kein Text markiert (M1: keine Aktion).");
                return;
            }

            var snapshot = _clipboard.Save();
            var replacement = text.ToUpperInvariant(); // M1 stand-in for translation

            _clipboard.WriteText(replacement);

            if (!_windows.IsAlive(foreground.Handle))
            {
                _log.Info("Ursprungsfenster weg (M1: Ersetzung liegt in der Zwischenablage).");
                return;
            }

            _windows.Activate(foreground.Handle);
            await Task.Delay(50).ConfigureAwait(true);

            InputSimulator.SendCtrlV();

            await Task.Delay(400).ConfigureAwait(true);
            _clipboard.Restore(snapshot);

            _log.Info($"Ersetzt: {text.Length} Zeichen, Prozess {foreground.ProcessName}.");
        }
        catch (Exception ex)
        {
            _log.Error("Fehler beim Ersetzen", ex);
        }
        finally
        {
            _busy = false;
        }
    }

    public void Dispose()
    {
        _watcher.ClipboardUpdated -= OnClipboardUpdated;
        _watcher.Dispose();
    }
}
