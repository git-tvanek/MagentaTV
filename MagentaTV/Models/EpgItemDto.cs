using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Models;

public class EpgItemDto : IValidatableObject
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(500, ErrorMessage = "Title cannot exceed 500 characters")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    [StringLength(100, ErrorMessage = "Category cannot exceed 100 characters")]
    public string Category { get; set; } = string.Empty;

    [Required]
    [Range(1, long.MaxValue, ErrorMessage = "ScheduleId must be greater than 0")]
    public long ScheduleId { get; set; }

    // Implementace IValidatableObject pro validaci celého objektu
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // Validace časového rozsahu
        if (EndTime <= StartTime)
        {
            results.Add(new ValidationResult(
                "EndTime must be after StartTime",
                new[] { nameof(EndTime), nameof(StartTime) }));
        }

        // Validace že čas není v budoucnosti více než 30 dní
        if (StartTime > DateTime.UtcNow.AddDays(30))
        {
            results.Add(new ValidationResult(
                "StartTime cannot be more than 30 days in the future",
                new[] { nameof(StartTime) }));
        }

        // Validace maximální délky pořadu (např. 8 hodin)
        if (EndTime - StartTime > TimeSpan.FromHours(8))
        {
            results.Add(new ValidationResult(
                "Program duration cannot exceed 8 hours",
                new[] { nameof(EndTime), nameof(StartTime) }));
        }

        return results;
    }
}