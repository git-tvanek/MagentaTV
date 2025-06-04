namespace MagentaTV.Services.Session
{
    public class SessionStatistics
    {
        public int TotalActiveSessions { get; set; }
        public int TotalExpiredSessions { get; set; }
        public int TotalInactiveSessions { get; set; }
        public int TotalRevokedSessions { get; set; }
        public int UniqueUsers { get; set; }
        public DateTime LastCleanup { get; set; }
        public TimeSpan AverageSessionDuration { get; set; }
        public Dictionary<string, int> SessionsByStatus { get; set; } = new();
        public Dictionary<string, int> SessionsByUser { get; set; } = new();
    }
}
