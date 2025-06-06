using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
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

    public async Task ConfigureNetworkAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_options.InterfaceName))
            {
                _logger.LogWarning("InterfaceName not specified, skipping network configuration");
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                await RunProcessAsync("netsh",
                    $"interface ip set address name=\"{_options.InterfaceName}\" static {_options.IpAddress} {_options.SubnetMask} {_options.Gateway}", cancellationToken);

                if (_options.DnsServers.Length > 0)
                {
                    await RunProcessAsync("netsh", $"interface ip set dns name=\"{_options.InterfaceName}\" static {_options.DnsServers[0]} primary", cancellationToken);

                    for (int i = 1; i < _options.DnsServers.Length; i++)
                    {
                        await RunProcessAsync("netsh", $"interface ip add dns name=\"{_options.InterfaceName}\" {_options.DnsServers[i]}", cancellationToken);
                    }
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                await RunProcessAsync("ip", $"addr flush dev {_options.InterfaceName}", cancellationToken);
                await RunProcessAsync("ip", $"addr add {_options.IpAddress}/{SubnetMaskToCidr(_options.SubnetMask)} dev {_options.InterfaceName}", cancellationToken);
                await RunProcessAsync("ip", $"route add default via {_options.Gateway}", cancellationToken);

                var resolv = new StringBuilder();
                foreach (var dns in _options.DnsServers)
                {
                    resolv.AppendLine($"nameserver {dns}");
                }
                await File.WriteAllTextAsync("/etc/resolv.conf", resolv.ToString(), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply network configuration");
        }
    }

    private static async Task RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);
    }

    private static int SubnetMaskToCidr(string mask)
    {
        var parts = mask.Split('.');
        int cidr = 0;
        foreach (var part in parts)
        {
            cidr += Convert.ToString(int.Parse(part), 2).Count(c => c == '1');
        }
        return cidr;
    }

    public HttpMessageHandler CreateHttpMessageHandler()
    {
        var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = _options.MaxConnectionsPerServer,
            ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds)
        };

        if (_options.PooledConnectionLifetimeSeconds > 0)
        {
            handler.PooledConnectionLifetime = TimeSpan.FromSeconds(_options.PooledConnectionLifetimeSeconds);
        }

        if (_options.PooledConnectionIdleTimeoutSeconds > 0)
        {
            handler.PooledConnectionIdleTimeout = TimeSpan.FromSeconds(_options.PooledConnectionIdleTimeoutSeconds);
        }

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
