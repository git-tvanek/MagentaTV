namespace MagentaTV.Configuration
{
    public class BackgroundServiceOptions
    {
        public const string SectionName = "BackgroundServices";

        /// <summary>
        /// Maximální velikost fronty úloh
        /// </summary>
        public int MaxQueueSize { get; set; } = 1000;

        /// <summary>
        /// Zpoždění před startem service (sekundy)
        /// </summary>
        public int StartupDelaySeconds { get; set; } = 0;

        /// <summary>
        /// Pokračovat při chybě nebo zastavit service
        /// </summary>
        public bool ContinueOnError { get; set; } = true;

        /// <summary>
        /// Automaticky restartovat service při selhání
        /// </summary>
        public bool RestartOnFailure { get; set; } = false;

        /// <summary>
        /// Timeout pro heartbeat
        /// </summary>
        public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Enablement jednotlivých services
        /// </summary>
        public Dictionary<string, bool> ServiceEnabled { get; set; } = new();

        /// <summary>
        /// Konfigurace jednotlivých services
        /// </summary>
        public Dictionary<string, Dictionary<string, object>> ServiceSettings { get; set; } = new();
    }

}
