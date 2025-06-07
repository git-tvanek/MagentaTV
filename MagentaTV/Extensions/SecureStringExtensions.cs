using System.Runtime.InteropServices;
using System.Security;

namespace MagentaTV.Extensions;

/// <summary>
/// Helper extensions for working with <see cref="SecureString"/> instances.
/// </summary>
public static class SecureStringExtensions
{
    /// <summary>
    /// Converts the specified plain text to a new <see cref="SecureString"/>.
    /// </summary>
    /// <param name="text">The plain text password.</param>
    /// <returns>Instance of <see cref="SecureString"/> containing the password.</returns>
    public static SecureString ToSecureString(this string text)
    {
        var secure = new SecureString();
        if (!string.IsNullOrEmpty(text))
        {
            foreach (var c in text)
            {
                secure.AppendChar(c);
            }
        }
        secure.MakeReadOnly();
        return secure;
    }

    /// <summary>
    /// Converts the secure string back into plain text. Caller should dispose of
    /// the sensitive data as soon as possible.
    /// </summary>
    /// <param name="secure">Password stored in a secure string.</param>
    /// <returns>Plain text representation.</returns>
    public static string ToUnsecureString(this SecureString secure)
    {
        if (secure == null || secure.Length == 0)
        {
            return string.Empty;
        }

        var ptr = IntPtr.Zero;
        try
        {
            ptr = Marshal.SecureStringToGlobalAllocUnicode(secure);
            return Marshal.PtrToStringUni(ptr) ?? string.Empty;
        }
        finally
        {
            if (ptr != IntPtr.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
        }
    }
}
