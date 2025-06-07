using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MagentaTV.Application.Queries;
using MagentaTV.Models;
using MagentaTV.Services.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class GetChannelsBulkQueryHandlerTests
{
    private sealed class FakeChannelService : IChannelService
    {
        public List<ChannelDto> Channels { get; set; } = new();
        public Task<List<ChannelDto>> GetChannelsAsync() => Task.FromResult(Channels);
        public Task<string> GenerateM3UPlaylistAsync() => throw new System.NotImplementedException();
    }

    [TestMethod]
    public async Task Handle_FiltersChannelsById()
    {
        var service = new FakeChannelService
        {
            Channels = new List<ChannelDto>
            {
                new() { ChannelId = 1, Name = "One" },
                new() { ChannelId = 2, Name = "Two" },
                new() { ChannelId = 3, Name = "Three" }
            }
        };

        var handler = new GetChannelsBulkQueryHandler(service, NullLogger<GetChannelsBulkQueryHandler>.Instance);
        var query = new GetChannelsBulkQuery { ChannelIds = new[] { 1, 3 } };

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Data);
        CollectionAssert.AreEqual(new[] { 1, 3 }, result.Data.Select(c => c.ChannelId).ToList());
    }
}
