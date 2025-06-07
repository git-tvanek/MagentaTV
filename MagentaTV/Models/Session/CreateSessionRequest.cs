using System.ComponentModel.DataAnnotations;
using System.Security;

namespace MagentaTV.Models.Session
{
    public class CreateSessionRequest : IValidatableObject
    {
        [Required(ErrorMessage = "Username is required")]
        [StringLength(100, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6)]
        public SecureString Password { get; set; } = new();

        [Range(1, 168)] // 1 hour to 1 week
        public int? SessionDurationHours { get; set; }

        public bool RememberMe { get; set; } = false;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            if (!string.IsNullOrEmpty(Username) && Username.Contains(' '))
            {
                results.Add(new ValidationResult(
                    "Username cannot contain spaces",
                    new[] { nameof(Username) }));
            }

            if (SessionDurationHours.HasValue && SessionDurationHours.Value < 1)
            {
                results.Add(new ValidationResult(
                    "Session duration must be at least 1 hour",
                    new[] { nameof(SessionDurationHours) }));
            }

            return results;
        }
    }
}
