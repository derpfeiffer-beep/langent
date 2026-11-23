using System.Runtime.InteropServices;
using Langent.App.Native;

namespace Langent.App.Services;

/// <summary>Synthesises Strg+V via <c>SendInput</c> (SPEC §4.3 step 5).</summary>
public static class InputSimulator
{
    public static void SendCtrlV()
    {
        NativeMethods.INPUT[] inputs =
        [
            Key(NativeMethods.VK_CONTROL, keyUp: false),
            Key(NativeMethods.VK_V, keyUp: false),
            Key(NativeMethods.VK_V, keyUp: true),
            Key(NativeMethods.VK_CONTROL, keyUp: true),
        ];

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT Key(ushort virtualKey, bool keyUp) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = virtualKey,
                dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0,
            },
        },
    };
}
