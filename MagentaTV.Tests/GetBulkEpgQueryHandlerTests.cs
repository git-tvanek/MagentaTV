using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MagentaTV.Application.Queries;
using MagentaTV.Models;
using MagentaTV.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class GetBulkEpgQueryHandlerTests
{
    private sealed class FakeMagenta : IMagenta
    {
        public Task<bool> LoginAsync(string username, string password) => throw new NotImplementedException();
        public Task LogoutAsync(string sessionId) => throw new NotImplementedException();
        public Task<List<ChannelDto>> GetChannelsAsync() => throw new NotImplementedException();
        public Task<List<EpgItemDto>> GetEpgAsync(int channelId, DateTime? from = null, DateTime? to = null)
        {
            var epg = new List<EpgItemDto>
            {
                new() { Title = $"Ch{channelId}", StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddHours(1), ScheduleId = channelId }
            };
            return Task.FromResult(epg);
        }
        public Task<string?> GetStreamUrlAsync(int channelId) => throw new NotImplementedException();
        public Task<string?> GetCatchupStreamUrlAsync(long scheduleId) => throw new NotImplementedException();
        public Task<string> GenerateM3UPlaylistAsync() => throw new NotImplementedException();
        public string GenerateXmlTv(List<EpgItemDto> epg, int channelId) => throw new NotImplementedException();
        public Task<TokenData?> RefreshTokensAsync(TokenData currentTokens) => throw new NotImplementedException();
    }

    [TestMethod]
    public async Task Handle_ReturnsDictionaryWithResults()
    {
        var handler = new GetBulkEpgQueryHandler(new FakeMagenta(), NullLogger<GetBulkEpgQueryHandler>.Instance);
        var query = new GetBulkEpgQuery { ChannelIds = new[] { 1, 2 } };

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Data);
        Assert.AreEqual(2, result.Data.Count);
        Assert.IsTrue(result.Data.ContainsKey(1));
        Assert.AreEqual(1, result.Data[1][0].ScheduleId);
    }
}
