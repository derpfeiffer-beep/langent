using System.Diagnostics;
using System.Windows.Interop;
using Langent.App.Native;

namespace Langent.App.Services;

/// <summary>
/// Registers a message-only window with <c>AddClipboardFormatListener</c> and
/// raises <see cref="ClipboardUpdated"/> for every <c>WM_CLIPBOARDUPDATE</c>,
/// carrying a Stopwatch timestamp and the current Ctrl state (SPEC §4.1).
/// Must be created and disposed on the UI thread.
/// </summary>
public sealed class ClipboardWatcher : IDisposable
{
    private readonly Stopwatch _clock;
    private HwndSource? _source;

    /// <summary>Raised on the UI thread. Arguments: timestamp in ms, Ctrl held.</summary>
    public event Action<long, bool>? ClipboardUpdated;

    public ClipboardWatcher(Stopwatch clock) => _clock = clock;

    public void Start()
    {
        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("Langent.ClipboardWatcher")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = NativeMethods.HWND_MESSAGE,
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        NativeMethods.AddClipboardFormatListener(_source.Handle);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            var timestamp = _clock.ElapsedMilliseconds;
            var ctrlDown = NativeMethods.IsKeyDown(NativeMethods.VK_CONTROL);
            ClipboardUpdated?.Invoke(timestamp, ctrlDown);
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is null)
        {
            return;
        }

        NativeMethods.RemoveClipboardFormatListener(_source.Handle);
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }
}
