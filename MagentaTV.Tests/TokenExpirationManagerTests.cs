using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MagentaTV.Configuration;
using MagentaTV.Services.TokenStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class TokenExpirationManagerTests
{
    [TestMethod]
    public void CleanupExpiredTokens_RemovesEntries()
    {
        var metrics = new TokenStorageMetrics();
        var cache = new TokenCache(10, metrics, NullLogger<TokenCache>.Instance);
        using var manager = new TokenExpirationManager(cache, metrics, NullLogger<TokenExpirationManager>.Instance);

        cache.Save("expired", new TokenData { AccessToken = "a", ExpiresAt = DateTime.UtcNow.AddMilliseconds(-1) });
        cache.Save("valid", new TokenData { AccessToken = "b", ExpiresAt = DateTime.UtcNow.AddHours(1) });

        // invoke private cleanup method to simulate timer tick
        var method = typeof(TokenExpirationManager).GetMethod("CleanupExpiredTokens", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(manager, null);

        Assert.IsFalse(cache.TryGet("expired", out _));
        Assert.IsTrue(cache.TryGet("valid", out _));
        Assert.AreEqual(1, metrics.Expirations);
    }

    [TestMethod]
    public async Task MetricsCounters_UpdateCorrectly()
    {
        var options = Options.Create(new TokenStorageOptions { MaxTokenCount = 3 });
        var storage = new InMemoryTokenStorage(NullLogger<InMemoryTokenStorage>.Instance, options);

        await storage.SaveTokensAsync("1", new TokenData { AccessToken = "a", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        await storage.SaveTokensAsync("2", new TokenData { AccessToken = "b", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        await storage.SaveTokensAsync("3", new TokenData { AccessToken = "c", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        await storage.SaveTokensAsync("4", new TokenData { AccessToken = "d", ExpiresAt = DateTime.UtcNow.AddHours(1) });

        _ = await storage.LoadTokensAsync("4");
        _ = await storage.LoadTokensAsync("missing");

        await storage.SaveTokensAsync("expired", new TokenData { AccessToken = "x", ExpiresAt = DateTime.UtcNow.AddMilliseconds(-1) });
        _ = await storage.LoadTokensAsync("expired");

        Assert.AreEqual(1, storage.Metrics.Hits);
        Assert.AreEqual(2, storage.Metrics.Misses);
        Assert.AreEqual(2, storage.Metrics.Evictions);
        Assert.AreEqual(1, storage.Metrics.Expirations);
    }
}
