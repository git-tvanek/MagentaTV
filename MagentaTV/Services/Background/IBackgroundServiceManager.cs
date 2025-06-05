using MagentaTV.Models.Background;
using MagentaTV.Services.Background.Core;

namespace MagentaTV.Services.Background
{
    /// <summary>
    /// Manager pro správu background services
    /// </summary>
    public interface IBackgroundServiceManager
    {
        Task StartServiceAsync<T>() where T : BaseBackgroundService;
        Task StopServiceAsync<T>() where T : BaseBackgroundService;
        Task<BackgroundServiceInfo?> GetServiceInfoAsync<T>() where T : BaseBackgroundService;
        Task<List<BackgroundServiceInfo>> GetAllServicesInfoAsync();
        Task QueueWorkItemAsync(BackgroundWorkItem workItem);
        Task<BackgroundServiceStats> GetStatsAsync();
    }
}
