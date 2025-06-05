using MagentaTV.Models.Background;
using MagentaTV.Services.Background.Core;

namespace MagentaTV.Services.Background
{
    /// <summary>
    /// Enhanced manager pro správu background services s intelligent startup
    /// </summary>
    public interface IBackgroundServiceManager
    {
        #region Original Methods
        Task StartServiceAsync<T>() where T : BaseBackgroundService;
        Task StopServiceAsync<T>() where T : BaseBackgroundService;
        Task<BackgroundServiceInfo?> GetServiceInfoAsync<T>() where T : BaseBackgroundService;
        Task<List<BackgroundServiceInfo>> GetAllServicesInfoAsync();
        Task QueueWorkItemAsync(BackgroundWorkItem workItem);
        Task<BackgroundServiceStats> GetStatsAsync();
        #endregion

        #region ✨ New Intelligent Startup Methods
        /// <summary>
        /// Inteligentní startup všech background services na základě dostupnosti tokenů
        /// </summary>
        Task StartAllServicesIntelligentlyAsync();

        /// <summary>
        /// Spustí pouze core services které nejsou závislé na tokenech
        /// </summary>
        Task StartCoreServicesAsync();

        /// <summary>
        /// Analyzuje stav tokenů a sessions
        /// </summary>
        Task<TokenAnalysisResult> AnalyzeTokenStatusAsync();

        /// <summary>
        /// Triggerne cache warming pokud je to možné (má tokeny)
        /// </summary>
        Task TriggerCacheWarmingIfPossibleAsync();
        #endregion
    }

    /// <summary>
    /// Výsledek analýzy tokenů a sessions
    /// </summary>
    public class TokenAnalysisResult
    {
        public bool HasTokens { get; set; }
        public bool HasValidTokens { get; set; }
        public string? TokenUsername { get; set; }
        public DateTime? TokenExpiresAt { get; set; }
        public bool IsNearExpiry { get; set; }
        public TimeSpan? TimeToExpiry { get; set; }
        public bool HasActiveSessions { get; set; }
        public int ActiveSessionCount { get; set; }
        public string? AnalysisError { get; set; }

        /// <summary>
        /// Comprehensive status summary
        /// </summary>
        public string StatusSummary
        {
            get
            {
                if (!HasTokens) return "No tokens";
                if (!HasValidTokens) return "Expired tokens";
                if (IsNearExpiry) return "Tokens near expiry";
                if (HasActiveSessions) return $"Active with {ActiveSessionCount} sessions";
                return "Valid tokens available";
            }
        }

        /// <summary>
        /// Určuje jestli by cache warming měl být možný
        /// </summary>
        public bool ShouldAllowCacheWarming => HasValidTokens && !IsNearExpiry;
    }
}