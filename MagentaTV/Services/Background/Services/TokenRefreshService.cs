using MagentaTV.Application.Events;
using MagentaTV.Services.Background.Core;
using MagentaTV.Services.Background.Events;
using MagentaTV.Services.Session;
using MagentaTV.Services.TokenStorage;

namespace MagentaTV.Services.Background.Services
{
    public class TokenRefreshService : BaseBackgroundService
    {
        private readonly IServiceScope _scope;

        public TokenRefreshService(
            ILogger<TokenRefreshService> logger,
            IServiceProvider serviceProvider,
            IEventBus eventBus)
            : base(logger, serviceProvider, eventBus, "TokenRefreshService")
        {
        }

        protected override async Task ExecuteServiceAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ExecuteWithEventsAsync("RefreshCheck", async () =>
                {
                    using var scope = CreateScope();
                    var tokenStorage = scope.ServiceProvider.GetRequiredService<ITokenStorage>();
                    var sessionManager = scope.ServiceProvider.GetRequiredService<ISessionManager>();
                    var magentaService = scope.ServiceProvider.GetRequiredService<IMagenta>();

                    var stats = await sessionManager.GetStatisticsAsync();
                    foreach (var username in stats.SessionsByUser.Keys)
                    {
                        var sessions = await sessionManager.GetUserSessionsAsync(username);
                        foreach (var session in sessions.Where(s => s.IsActive))
                        {
                            var tokens = await tokenStorage.LoadTokensAsync(session.SessionId);
                            if (tokens?.IsNearExpiry == true)
                            {
                                Logger.LogInformation("Refreshing tokens for user: {Username}, session {SessionId}", tokens.Username, session.SessionId);

                                var refreshed = await RefreshTokensAsync(magentaService, tokens);
                                if (refreshed != null)
                                {
                                    await tokenStorage.SaveTokensAsync(session.SessionId, refreshed);
                                    await sessionManager.RefreshSessionTokensAsync(session.SessionId, refreshed);

                                    await EventBus.PublishAsync(new TokensRefreshedEvent
                                    {
                                        Username = refreshed.Username,
                                        NewExpiryTime = refreshed.ExpiresAt,
                                        SessionId = session.SessionId
                                    });
                                }
                            }
                        }
                    }

                    return true;
                });

                // Čekáme 15 minut
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                UpdateHeartbeat();
            }
        }

        private async Task<TokenData?> RefreshTokensAsync(IMagenta service, TokenData currentTokens)
        {
            try
            {
                var refreshed = await service.RefreshTokensAsync(currentTokens);
                return refreshed;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Token refresh request failed");
                return null;
            }
        }
    }
}
