using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Models;

public class ChannelDto
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "ChannelId must be greater than 0")]
    public int ChannelId { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string Name { get; set; } = string.Empty;

    [Url(ErrorMessage = "LogoUrl must be a valid URL")]
    public string LogoUrl { get; set; } = string.Empty;

    public bool HasArchive { get; set; }
}
