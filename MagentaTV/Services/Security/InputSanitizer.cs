using System.Text.Encodings.Web;

namespace MagentaTV.Services.Security;

/// <summary>
/// Provides basic input sanitization using <see cref="HtmlEncoder"/>.
/// </summary>
public interface IInputSanitizer
{
    /// <summary>
    /// Sanitizes the specified input string to prevent XSS attacks.
    /// </summary>
    /// <param name="input">Raw user input.</param>
    /// <returns>Sanitized string safe for further processing.</returns>
    string Sanitize(string input);
}

/// <summary>
/// Default implementation of <see cref="IInputSanitizer"/>.
/// </summary>
public sealed class InputSanitizer : IInputSanitizer
{
    private readonly HtmlEncoder _encoder = HtmlEncoder.Default;

    /// <inheritdoc />
    public string Sanitize(string input)
    {
        return string.IsNullOrEmpty(input) ? string.Empty : _encoder.Encode(input);
    }
}
