using MagentaTV.Models.Background;
using MagentaTV.Services.Background.Core;
using MagentaTV.Services.Background.Services;
using MagentaTV.Services.TokenStorage;
using MagentaTV.Services.Session;
using System.Collections.Concurrent;

namespace MagentaTV.Services.Background
{
    public class BackgroundServiceManager : IBackgroundServiceManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly ITokenStorage _tokenStorage;
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<BackgroundServiceManager> _logger;
        private readonly ConcurrentDictionary<Type, BackgroundServiceInfo> _services = new();

        public BackgroundServiceManager(
            IServiceProvider serviceProvider,
            IBackgroundTaskQueue taskQueue,
            ITokenStorage tokenStorage,
            ISessionManager sessionManager,
            ILogger<BackgroundServiceManager> logger)
        {
            _serviceProvider = serviceProvider;
            _taskQueue = taskQueue;
            _tokenStorage = tokenStorage;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        #region Original BackgroundServiceManager Methods

        public async Task StartServiceAsync<T>() where T : BaseBackgroundService
        {
            var serviceType = typeof(T);
            var service = _serviceProvider.GetService<T>();

            if (service == null)
            {
                throw new InvalidOperationException($"Service {serviceType.Name} is not registered");
            }

            var info = new BackgroundServiceInfo
            {
                Type = serviceType,
                Name = serviceType.Name,
                Status = BackgroundServiceStatus.Starting,
                StartedAt = DateTime.UtcNow
            };

            _services.AddOrUpdate(serviceType, info, (key, existing) => info);

            try
            {
                await service.StartAsync(CancellationToken.None);
                info.Status = BackgroundServiceStatus.Running;

                _logger.LogInformation("Started background service {ServiceName}", serviceType.Name);
            }
            catch (Exception ex)
            {
                info.Status = BackgroundServiceStatus.Failed;
                info.ErrorMessage = ex.Message;

                _logger.LogError(ex, "Failed to start background service {ServiceName}", serviceType.Name);
                throw;
            }
        }

        public async Task StopServiceAsync<T>() where T : BaseBackgroundService
        {
            var serviceType = typeof(T);

            if (_services.TryGetValue(serviceType, out var info))
            {
                info.Status = BackgroundServiceStatus.Stopping;

                var service = _serviceProvider.GetService<T>();
                if (service != null)
                {
                    await service.StopAsync(CancellationToken.None);
                    info.Status = BackgroundServiceStatus.Stopped;
                    info.StoppedAt = DateTime.UtcNow;

                    _logger.LogInformation("Stopped background service {ServiceName}", serviceType.Name);
                }
            }
        }

        public Task<BackgroundServiceInfo?> GetServiceInfoAsync<T>() where T : BaseBackgroundService
        {
            var serviceType = typeof(T);
            _services.TryGetValue(serviceType, out var info);
            return Task.FromResult(info);
        }

        public Task<List<BackgroundServiceInfo>> GetAllServicesInfoAsync()
        {
            return Task.FromResult(_services.Values.ToList());
        }

        public async Task QueueWorkItemAsync(BackgroundWorkItem workItem)
        {
            await _taskQueue.QueueBackgroundWorkItemAsync(workItem);
            _logger.LogDebug("Queued work item {Id} ({Name})", workItem.Id, workItem.Name);
        }

        public Task<BackgroundServiceStats> GetStatsAsync()
        {
            var stats = new BackgroundServiceStats
            {
                TotalServices = _services.Count,
                RunningServices = _services.Values.Count(s => s.Status == BackgroundServiceStatus.Running),
                QueuedItems = _taskQueue.Count,
                QueueCapacity = _taskQueue.Capacity,
                LastUpdated = DateTime.UtcNow
            };

            return Task.FromResult(stats);
        }

        #endregion

        #region ✨ New Intelligent Startup Methods

        /// <summary>
        /// Inteligentní startup všech background services
        /// </summary>
        public async Task StartAllServicesIntelligentlyAsync()
        {
            _logger.LogInformation("Starting intelligent background service startup process");

            try
            {
                // 1. Vždy spusť core services (nejsou závislé na tokenech)
                await StartCoreServicesAsync();

                // 2. Zkontroluj dostupnost tokenů
                var tokenStatus = await AnalyzeTokenStatusAsync();

                // 3. Spusť token-dependent services na základě dostupnosti tokenů
                await StartTokenDependentServicesAsync(tokenStatus);

                // 4. Naplánuj kontroly pro pozdější spuštění services
                await ScheduleDelayedServiceStartupAsync(tokenStatus);

                _logger.LogInformation("Intelligent background service startup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed during intelligent background service startup");
                throw;
            }
        }

        /// <summary>
        /// Spustí pouze core services které nejsou závislé na tokenech
        /// </summary>
        public async Task StartCoreServicesAsync()
        {
            var coreServices = new[]
            {
                typeof(SessionCleanupService), // Vždy potřebný
                typeof(TokenRefreshService)    // Potřebný pro refresh existujících tokenů
            };

            foreach (var serviceType in coreServices)
            {
                try
                {
                    await StartServiceByTypeAsync(serviceType);
                    _logger.LogInformation("Started core service: {ServiceName}", serviceType.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start core service: {ServiceName}", serviceType.Name);
                    // Nebudeme hazet exception - jiné services mohou být v pořádku
                }
            }
        }

        /// <summary>
        /// Analyzuje stav tokenů a sessions
        /// </summary>
        public async Task<TokenAnalysisResult> AnalyzeTokenStatusAsync()
        {
            var result = new TokenAnalysisResult();

            try
            {
                var tokens = await _tokenStorage.LoadTokensAsync();
                result.HasTokens = tokens != null;
                result.HasValidTokens = tokens?.IsValid == true;
                result.TokenUsername = tokens?.Username;
                result.TokenExpiresAt = tokens?.ExpiresAt;

                if (tokens != null)
                {
                    result.IsNearExpiry = tokens.IsNearExpiry;
                    result.TimeToExpiry = tokens.TimeToExpiry;
                }

                // Zkontroluj aktivní sessions
                if (!string.IsNullOrEmpty(result.TokenUsername))
                {
                    var userSessions = await _sessionManager.GetUserSessionsAsync(result.TokenUsername);
                    result.HasActiveSessions = userSessions.Any(s => s.IsActive);
                    result.ActiveSessionCount = userSessions.Count(s => s.IsActive);
                }

                _logger.LogInformation("Token analysis: HasValid={HasValid}, Username={Username}, HasSessions={HasSessions}",
                    result.HasValidTokens, result.TokenUsername, result.HasActiveSessions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze token status");
                result.AnalysisError = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Triggerne cache warming pokud je možný
        /// </summary>
        public async Task TriggerCacheWarmingIfPossibleAsync()
        {
            try
            {
                var tokenStatus = await AnalyzeTokenStatusAsync();

                if (!tokenStatus.HasValidTokens)
                {
                    _logger.LogDebug("Skipping cache warming trigger - no valid tokens");
                    return;
                }

                var cacheService = _serviceProvider.GetService<CacheWarmingService>();
                if (cacheService != null)
                {
                    await cacheService.TriggerWarmingAsync();
                    _logger.LogInformation("Cache warming triggered successfully");
                }
                else
                {
                    _logger.LogWarning("CacheWarmingService not found for manual trigger");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to trigger cache warming");
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Spustí services závislé na tokenech na základě jejich dostupnosti
        /// </summary>
        private async Task StartTokenDependentServicesAsync(TokenAnalysisResult tokenStatus)
        {
            // CacheWarmingService - spustíme vždy, ale s intelligent behavior
            try
            {
                await StartServiceAsync<CacheWarmingService>();

                if (tokenStatus.HasValidTokens)
                {
                    _logger.LogInformation("CacheWarmingService started with valid tokens available");

                    // Trigger okamžité warming pokud jsou tokeny dostupné
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000); // Dáme službě čas na startup
                        await TriggerCacheWarmingIfPossibleAsync();
                    });
                }
                else
                {
                    _logger.LogInformation("CacheWarmingService started in standby mode - will activate after login");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start CacheWarmingService");
            }
        }

        /// <summary>
        /// Naplánuje kontroly pro pozdější spuštění services
        /// </summary>
        private async Task ScheduleDelayedServiceStartupAsync(TokenAnalysisResult tokenStatus)
        {
            if (!tokenStatus.HasValidTokens)
            {
                // Naplánuj periodické kontroly jestli se někdo nepřihlásil
                _ = Task.Run(async () =>
                {
                    await MonitorForTokenAvailabilityAsync();
                });

                _logger.LogInformation("Scheduled monitoring for token availability");
            }

            // Naplánuj health check monitoring
            _ = Task.Run(async () =>
            {
                await MonitorServiceHealthAsync();
            });
        }

        /// <summary>
        /// Monitoruje dostupnost tokenů a triggerne cache warming když se stanou dostupnými
        /// </summary>
        private async Task MonitorForTokenAvailabilityAsync()
        {
            var checkInterval = TimeSpan.FromMinutes(1);
            var maxChecks = 60; // Sleduj max 1 hodinu
            var checkCount = 0;

            while (checkCount < maxChecks)
            {
                try
                {
                    await Task.Delay(checkInterval);
                    checkCount++;

                    var hasValidTokens = await _tokenStorage.HasValidTokensAsync();
                    if (hasValidTokens)
                    {
                        _logger.LogInformation("Tokens became available - triggering cache warming");
                        await TriggerCacheWarmingIfPossibleAsync();
                        break; // Tokeny jsou dostupné, ukončíme monitoring
                    }

                    if (checkCount % 10 == 0) // Log každých 10 minut
                    {
                        _logger.LogDebug("Still waiting for tokens to become available (check {Count}/{Max})",
                            checkCount, maxChecks);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during token availability monitoring");
                }
            }

            if (checkCount >= maxChecks)
            {
                _logger.LogInformation("Token availability monitoring ended after {Minutes} minutes", maxChecks);
            }
        }

        /// <summary>
        /// Monitoruje zdraví services
        /// </summary>
        private async Task MonitorServiceHealthAsync()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5));

                    var stats = await GetStatsAsync();
                    var serviceInfos = await GetAllServicesInfoAsync();

                    var failedServices = serviceInfos
                        .Where(s => s.Status == BackgroundServiceStatus.Failed)
                        .ToList();

                    if (failedServices.Any())
                    {
                        _logger.LogWarning("Found {Count} failed background services: {ServiceNames}",
                            failedServices.Count,
                            string.Join(", ", failedServices.Select(s => s.Name)));

                        // Můžete zde implementovat restart logic nebo alerting
                    }

                    // Log health stats periodically
                    _logger.LogDebug("Background service health check: Total={Total}, Running={Running}, Queue={Queue}",
                        stats.TotalServices, stats.RunningServices, stats.QueuedItems);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during service health monitoring");
                }
            }
        }

        /// <summary>
        /// Helper pro spuštění service podle typu
        /// </summary>
        private async Task StartServiceByTypeAsync(Type serviceType)
        {
            var method = typeof(IBackgroundServiceManager)
                .GetMethod(nameof(IBackgroundServiceManager.StartServiceAsync))
                ?.MakeGenericMethod(serviceType);

            if (method != null)
            {
                var task = (Task)method.Invoke(this, null)!;
                await task;
            }
        }

        #endregion
    }
}