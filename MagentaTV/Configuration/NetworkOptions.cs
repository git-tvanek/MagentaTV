using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Configuration;

public class NetworkOptions
{
    public const string SectionName = "Network";

    /// <summary>
    /// Name of the network interface to configure (e.g. "eth0" or "Ethernet").
    /// </summary>
    public string InterfaceName { get; set; } = "eth0";

    [RegularExpression(@"^\d{1,3}(\.\d{1,3}){3}$")]
    public string IpAddress { get; set; } = "127.0.0.1";

    [RegularExpression(@"^\d{1,3}(\.\d{1,3}){3}$")]
    public string SubnetMask { get; set; } = "255.255.255.0";

    [RegularExpression(@"^\d{1,3}(\.\d{1,3}){3}$")]
    public string Gateway { get; set; } = "127.0.0.1";

    public string[] DnsServers { get; set; } = new[] { "8.8.8.8", "8.8.4.4" };

    public string? ProxyAddress { get; set; }

    [Range(0, 65535)]
    public int ProxyPort { get; set; }

    [Range(1, 1000)]
    public int MaxConnectionsPerServer { get; set; } = 20;

    [Range(1, 600)]
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    public bool EnableSsl { get; set; } = true;

    public bool ValidateServerCertificate { get; set; } = true;

    [Range(0, 86400)]
    public int PooledConnectionLifetimeSeconds { get; set; } = 0;

    [Range(0, 86400)]
    public int PooledConnectionIdleTimeoutSeconds { get; set; } = 0;
}
