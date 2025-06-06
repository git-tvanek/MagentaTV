using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Configuration;

public class DiscoveryOptions
{
    public const string SectionName = "Discovery";

    [Range(1, 65535)]
    public int Port { get; set; } = 15998;

    public string RequestMessage { get; set; } = "MAGENTATV_DISCOVERY_REQUEST";

    public string ResponseMessage { get; set; } = "MAGENTATV_DISCOVERY_RESPONSE";

    public string BaseUrl { get; set; } = "http://localhost:5000";
}
