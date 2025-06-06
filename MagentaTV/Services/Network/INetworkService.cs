using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MagentaTV.Configuration;

namespace MagentaTV.Services.Network;

public interface INetworkService
{
    HttpMessageHandler CreateHttpMessageHandler();
    Task ConfigureNetworkAsync(CancellationToken cancellationToken = default);
    NetworkOptions Options { get; }
}
