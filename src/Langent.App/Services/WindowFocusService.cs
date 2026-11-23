using System.Diagnostics;
using Langent.App.Native;

namespace Langent.App.Services;

/// <summary>
/// Remembers and reactivates the foreground window (SPEC §4.2 step 1, §4.3 steps 3–4).
/// </summary>
public sealed class WindowFocusService
{
    public readonly record struct ForegroundWindow(IntPtr Handle, string ProcessName);

    public ForegroundWindow GetForeground()
    {
        var handle = NativeMethods.GetForegroundWindow();
        return new ForegroundWindow(handle, GetProcessName(handle));
    }

    public bool IsAlive(IntPtr handle) => handle != IntPtr.Zero && NativeMethods.IsWindow(handle);

    public bool Activate(IntPtr handle) => NativeMethods.SetForegroundWindow(handle);

    private static string GetProcessName(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            NativeMethods.GetWindowThreadProcessId(handle, out var processId);
            if (processId == 0)
            {
                return string.Empty;
            }

            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName; // without ".exe"
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
