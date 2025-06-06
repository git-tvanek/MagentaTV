namespace MagentaTV.Client.Models;

public class ChannelDto
{
    public int ChannelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public bool HasArchive { get; set; }
}
