namespace MagentaTV.Models
{
    public class EpgItemDto
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Category { get; set; }
        public long ScheduleId { get; set; }
    }
}