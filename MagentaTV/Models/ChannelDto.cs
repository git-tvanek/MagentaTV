namespace MagentaTV.Models
{
    public class ChannelDto
    {
        public int ChannelId { get; set; }
        public string Name { get; set; }
        public string LogoUrl { get; set; }
        public bool HasArchive { get; set; }
    }
}
