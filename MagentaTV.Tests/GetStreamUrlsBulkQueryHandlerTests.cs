using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MagentaTV.Application.Queries;
using MagentaTV.Models;
using MagentaTV.Services;
using MagentaTV.Services.TokenStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class GetStreamUrlsBulkQueryHandlerTests
{
    private sealed class FakeMagenta : IMagenta
    {
        public Task<bool> LoginAsync(string username, string password) => throw new System.NotImplementedException();
        public Task LogoutAsync(string sessionId) => throw new System.NotImplementedException();
        public Task<List<ChannelDto>> GetChannelsAsync() => throw new System.NotImplementedException();
        public Task<List<EpgItemDto>> GetEpgAsync(int channelId, System.DateTime? from = null, System.DateTime? to = null) => throw new System.NotImplementedException();
        public Task<string?> GetStreamUrlAsync(int channelId) => Task.FromResult<string?>("url" + channelId);
        public Task<string?> GetCatchupStreamUrlAsync(long scheduleId) => throw new System.NotImplementedException();
        public Task<string> GenerateM3UPlaylistAsync() => throw new System.NotImplementedException();
        public string GenerateXmlTv(List<EpgItemDto> epg, int channelId) => throw new System.NotImplementedException();
        public Task<TokenData?> RefreshTokensAsync(TokenData currentTokens) => Task.FromResult<TokenData?>(null);
    }

    [TestMethod]
    public async Task Handle_ReturnsDictionaryWithUrls()
    {
        var handler = new GetStreamUrlsBulkQueryHandler(new FakeMagenta(), NullLogger<GetStreamUrlsBulkQueryHandler>.Instance);
        var query = new GetStreamUrlsBulkQuery { ChannelIds = new[] { 1, 2 } };

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Data);
        Assert.AreEqual("url1", result.Data[1]);
        Assert.AreEqual("url2", result.Data[2]);
    }
}
