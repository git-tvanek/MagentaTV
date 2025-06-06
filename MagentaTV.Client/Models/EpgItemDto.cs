namespace MagentaTV.Client.Models;

public class EpgItemDto
{
    public long Id { get; set; }
    public int ChannelId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}
