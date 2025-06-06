using System.Net;
using System.Net.Sockets;
using System.Text;
using MagentaTV.Configuration;
using MagentaTV.Services.Background.Core;
using MagentaTV.Services.Background.Events;
using Microsoft.Extensions.Options;

namespace MagentaTV.Services.Background.Services;

public class DiscoveryResponderService : BaseBackgroundService
{
    private readonly DiscoveryOptions _options;

    public DiscoveryResponderService(
        ILogger<DiscoveryResponderService> logger,
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        IOptions<DiscoveryOptions> options)
        : base(logger, serviceProvider, eventBus, "DiscoveryResponderService")
    {
        _options = options.Value;
    }

    protected override async Task ExecuteServiceAsync(CancellationToken stoppingToken)
    {
        using var udp = new UdpClient(_options.Port);
        udp.EnableBroadcast = true;
        Logger.LogInformation("Discovery responder listening on UDP {Port}", _options.Port);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(stoppingToken);
                var message = Encoding.UTF8.GetString(result.Buffer);
                if (message == _options.RequestMessage)
                {
                    var response = $"{_options.ResponseMessage}|{_options.BaseUrl}";
                    var bytes = Encoding.UTF8.GetBytes(response);
                    await udp.SendAsync(bytes, bytes.Length, result.RemoteEndPoint);
                }
                UpdateHeartbeat();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Discovery responder error");
            }
        }
    }
}
