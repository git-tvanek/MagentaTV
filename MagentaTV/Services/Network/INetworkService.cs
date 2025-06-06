using System.Net.Http;
using MagentaTV.Configuration;

namespace MagentaTV.Services.Network;

public interface INetworkService
{
    HttpMessageHandler CreateHttpMessageHandler();
    NetworkOptions Options { get; }
}
