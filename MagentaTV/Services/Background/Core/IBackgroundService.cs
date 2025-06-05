namespace MagentaTV.Services.Background.Core
{
    namespace MagentaTV.Services.Background.Core
    {
        public interface IBackgroundService
        {
            string ServiceName { get; }
            Task StartAsync(CancellationToken cancellationToken);
            Task StopAsync(CancellationToken cancellationToken);
            Task<ServiceHealth> GetHealthAsync();
        }
    }
}