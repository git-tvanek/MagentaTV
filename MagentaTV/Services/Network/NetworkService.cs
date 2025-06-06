using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Options;
using MagentaTV.Configuration;

namespace MagentaTV.Services.Network;

public class NetworkService : INetworkService
{
    private readonly NetworkOptions _options;
    private readonly ILogger<NetworkService> _logger;

    public NetworkService(IOptions<NetworkOptions> options, ILogger<NetworkService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public NetworkOptions Options => _options;

    public HttpMessageHandler CreateHttpMessageHandler()
    {
        var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = _options.MaxConnectionsPerServer,
            ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds)
        };

        if (!string.IsNullOrEmpty(_options.ProxyAddress))
        {
            handler.Proxy = new WebProxy(_options.ProxyAddress, _options.ProxyPort);
            handler.UseProxy = true;
            _logger.LogInformation("Using proxy {Proxy}:{Port}", _options.ProxyAddress, _options.ProxyPort);
        }

        if (!_options.EnableSsl)
        {
            handler.SslOptions.EnabledSslProtocols = System.Security.Authentication.SslProtocols.None;
        }
        else if (!_options.ValidateServerCertificate)
        {
            handler.SslOptions.RemoteCertificateValidationCallback = (_,_,_,_) => true;
        }

        return handler;
    }
}
