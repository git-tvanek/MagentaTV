using System;
using System.Threading.Tasks;
using MagentaTV.Configuration;
using MagentaTV.Services.TokenStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class InMemoryTokenStorageTests
{
    [TestMethod]
    public async Task SaveTokensAsync_EvictsLeastRecentlyUsed_WhenLimitReached()
    {
        var options = Options.Create(new TokenStorageOptions { MaxTokenCount = 3 });
        var storage = new InMemoryTokenStorage(new NullLogger<InMemoryTokenStorage>(), options);

        await storage.SaveTokensAsync("1", new TokenData { AccessToken = "a", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        await storage.SaveTokensAsync("2", new TokenData { AccessToken = "b", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        await storage.SaveTokensAsync("3", new TokenData { AccessToken = "c", ExpiresAt = DateTime.UtcNow.AddHours(1) });

        // Access first entry to make it most recently used
        _ = await storage.LoadTokensAsync("1");

        await storage.SaveTokensAsync("4", new TokenData { AccessToken = "d", ExpiresAt = DateTime.UtcNow.AddHours(1) });

        var removed = await storage.LoadTokensAsync("2");
        Assert.IsNull(removed);
    }
}
