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

                    var tokens = await tokenStorage.LoadTokensAsync();

                    // Refresh pokud jsou blízko expiraci
                    if (tokens?.IsNearExpiry == true)
                    {
                        Logger.LogInformation("Refreshing tokens for user: {Username}", tokens.Username);

                        // Zde implementovat refresh logic
                        var refreshed = await RefreshTokensAsync(magentaService, tokens);

                        if (refreshed != null)
                        {
                            await tokenStorage.SaveTokensAsync(refreshed);

                            // Publikovat event
                            await EventBus.PublishAsync(new TokensRefreshedEvent
                            {
                                Username = refreshed.Username,
                                NewExpiryTime = refreshed.ExpiresAt,
                                SessionId = "background-refresh"
                            });
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
            // Implementace refresh logiky
            // TODO: Přidat refresh endpoint do Magenta service
            return null;
        }
    }
}
