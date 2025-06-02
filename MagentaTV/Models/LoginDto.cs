using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Models;

public class LoginDto : IValidatableObject
{
    [Required(ErrorMessage = "Username is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
    public string Password { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // Dodatečné validace nad rámec základních atributů
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

        return results;
    }
}