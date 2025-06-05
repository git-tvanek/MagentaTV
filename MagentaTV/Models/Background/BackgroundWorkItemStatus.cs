namespace MagentaTV.Models.Background
{
    public enum BackgroundWorkItemStatus
    {
        Queued,
        Running,
        Completed,
        Failed,
        Cancelled,
        Retrying
    }
}
