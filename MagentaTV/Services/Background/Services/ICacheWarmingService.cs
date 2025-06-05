namespace MagentaTV.Services.Background.Services
{
    /// <summary>
    /// Interface pro cache warming functionality
    /// </summary>
    public interface ICacheWarmingService
    {
        /// <summary>
        /// Manuálně triggerne cache warming
        /// </summary>
        Task TriggerWarmingAsync();

        /// <summary>
        /// Zkontroluje jestli byl cache warming úspěšný
        /// </summary>
        bool HasWarmedSuccessfully { get; }

        /// <summary>
        /// Čas posledního úspěšného warming
        /// </summary>
        DateTime? LastSuccessfulWarm { get; }
    }
}