using System.Runtime.InteropServices;
using System.Text;
using Langent.App.Native;

namespace Langent.App.Services;

/// <summary>
/// Protects a secret with DPAPI (<c>CurrentUser</c>) and Base64-encodes it (SPEC §6).
/// A value that cannot be decrypted (different PC/user) is treated as "no secret".
/// </summary>
public static class SecretProtector
{
    public static string? Protect(string plaintext)
    {
        var input = Encoding.UTF8.GetBytes(plaintext);
        var inHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
        var dataIn = new NativeMethods.DATA_BLOB { cbData = input.Length, pbData = inHandle.AddrOfPinnedObject() };

        try
        {
            if (!NativeMethods.CryptProtectData(ref dataIn, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    NativeMethods.CRYPTPROTECT_UI_FORBIDDEN, out var dataOut))
            {
                return null;
            }

            try
            {
                var output = new byte[dataOut.cbData];
                Marshal.Copy(dataOut.pbData, output, 0, dataOut.cbData);
                return Convert.ToBase64String(output);
            }
            finally
            {
                NativeMethods.LocalFree(dataOut.pbData);
            }
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            inHandle.Free();
        }
    }

    public static string? Unprotect(string? protectedBase64)
    {
        if (string.IsNullOrWhiteSpace(protectedBase64))
        {
            return null;
        }

        byte[] input;
        try
        {
            input = Convert.FromBase64String(protectedBase64);
        }
        catch (FormatException)
        {
            return null;
        }

        var inHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
        var dataIn = new NativeMethods.DATA_BLOB { cbData = input.Length, pbData = inHandle.AddrOfPinnedObject() };

        try
        {
            if (!NativeMethods.CryptUnprotectData(ref dataIn, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    NativeMethods.CRYPTPROTECT_UI_FORBIDDEN, out var dataOut))
            {
                return null;
            }

            try
            {
                var output = new byte[dataOut.cbData];
                Marshal.Copy(dataOut.pbData, output, 0, dataOut.cbData);
                return Encoding.UTF8.GetString(output);
            }
            finally
            {
                NativeMethods.LocalFree(dataOut.pbData);
            }
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            inHandle.Free();
        }
    }
}
