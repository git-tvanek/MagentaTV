using System.ComponentModel.DataAnnotations;
using System.Security;
using MagentaTV.Extensions;

namespace MagentaTV.Models;

public class LoginDto : IValidatableObject
{
    [Required(ErrorMessage = "Username is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Converts the plain text password to a <see cref="SecureString"/>.
    /// Caller is responsible for disposing the returned instance.
    /// </summary>
    public SecureString GetSecurePassword() => Password.ToSecureString();

    /// <summary>
    /// Zapamatovat si přihlášení (dlouhodobá session)
    /// </summary>
    public bool RememberMe { get; set; } = false;

    /// <summary>
    /// Vlastní doba trvání session v hodinách (1-168 hodin = 1 týden)
    /// </summary>
    [Range(1, 168, ErrorMessage = "Session duration must be between 1 and 168 hours")]
    public int? SessionDurationHours { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // Stávající validace pro Username
        if (!string.IsNullOrEmpty(Username))
        {
            // Username nesmí obsahovat mezery
            if (Username.Contains(' '))
            {
                results.Add(new ValidationResult(
                    "Username cannot contain spaces",
                    new[] { nameof(Username) }));
            }

            // Username nesmí obsahovat speciální znaky (kromě . _ -)
            if (Username.Any(c => !char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-'))
            {
                results.Add(new ValidationResult(
                    "Username can only contain letters, numbers, dots, underscores and hyphens",
                    new[] { nameof(Username) }));
            }
        }

        // Stávající validace pro Password
        if (!string.IsNullOrEmpty(Password))
        {
            // Password musí obsahovat alespoň jedno číslo
            if (!Password.Any(char.IsDigit))
            {
                results.Add(new ValidationResult(
                    "Password must contain at least one number",
                    new[] { nameof(Password) }));
            }

            // Password musí obsahovat alespoň jedno písmeno
            if (!Password.Any(char.IsLetter))
            {
                results.Add(new ValidationResult(
                    "Password must contain at least one letter",
                    new[] { nameof(Password) }));
            }

            // Password nesmí být stejný jako username
            if (string.Equals(Password, Username, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new ValidationResult(
                    "Password cannot be the same as username",
                    new[] { nameof(Password) }));
            }
        }

        // ✅ NOVÁ VALIDACE pro session parametry
        if (SessionDurationHours.HasValue)
        {
            if (SessionDurationHours.Value < 1)
            {
                results.Add(new ValidationResult(
                    "Session duration must be at least 1 hour",
                    new[] { nameof(SessionDurationHours) }));
            }
            else if (SessionDurationHours.Value > 168)
            {
                results.Add(new ValidationResult(
                    "Session duration cannot exceed 168 hours (1 week)",
                    new[] { nameof(SessionDurationHours) }));
            }
        }

        // Logická validace: pokud je RememberMe = true, SessionDurationHours by mělo být delší
        if (RememberMe && SessionDurationHours.HasValue && SessionDurationHours.Value < 24)
        {
            results.Add(new ValidationResult(
                "When 'Remember Me' is enabled, session duration should be at least 24 hours",
                new[] { nameof(SessionDurationHours), nameof(RememberMe) }));
        }

        return results;
    }
}