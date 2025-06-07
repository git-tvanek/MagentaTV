using System;
using System.Threading.Tasks;
using System.Reflection;
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

        Assert.AreEqual(2, storage.Metrics.Evictions);
    }

    [TestMethod]
    public async Task LoadTokensAsync_IncrementsHitAndMissCounters()
    {
        var options = Options.Create(new TokenStorageOptions { MaxTokenCount = 10 });
        var storage = new InMemoryTokenStorage(new NullLogger<InMemoryTokenStorage>(), options);

        await storage.SaveTokensAsync("1", new TokenData { AccessToken = "a", ExpiresAt = DateTime.UtcNow.AddHours(1) });

        var hit = await storage.LoadTokensAsync("1");
        var miss = await storage.LoadTokensAsync("missing");

        Assert.IsNotNull(hit);
        Assert.IsNull(miss);
        Assert.AreEqual(1, storage.Metrics.Hits);
        Assert.AreEqual(1, storage.Metrics.Misses);
    }

    [TestMethod]
    public async Task LoadTokensAsync_RemovesExpiredTokensAndUpdatesMetrics()
    {
        var options = Options.Create(new TokenStorageOptions { MaxTokenCount = 10 });
        var storage = new InMemoryTokenStorage(new NullLogger<InMemoryTokenStorage>(), options);

        await storage.SaveTokensAsync("1", new TokenData { AccessToken = "a", ExpiresAt = DateTime.UtcNow.AddMilliseconds(-1) });

        var result = await storage.LoadTokensAsync("1");

        var cacheField = typeof(InMemoryTokenStorage).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var cache = (TokenCache)cacheField.GetValue(storage)!;

        Assert.IsNull(result);
        Assert.IsFalse(cache.TryGet("1", out _));
        Assert.AreEqual(1, storage.Metrics.Expirations);
        Assert.AreEqual(1, storage.Metrics.Misses);
    }

    [TestMethod]
    public async Task LoadTokensAsync_UpdatesLastAccessAndAccessCount()
    {
        var options = Options.Create(new TokenStorageOptions { MaxTokenCount = 10 });
        var storage = new InMemoryTokenStorage(new NullLogger<InMemoryTokenStorage>(), options);

        await storage.SaveTokensAsync("1", new TokenData { AccessToken = "a", ExpiresAt = DateTime.UtcNow.AddHours(1) });

        var cacheField = typeof(InMemoryTokenStorage).GetField("_cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var cache = (TokenCache)cacheField.GetValue(storage)!;
        var tokensField = typeof(TokenCache).GetField("_tokens", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<string, TokenEntry>)tokensField.GetValue(cache)!;

        dict.TryGetValue("1", out var beforeEntry);
        var beforeAccess = beforeEntry.LastAccess;
        var beforeCount = beforeEntry.AccessCount;

        await Task.Delay(10);
        _ = await storage.LoadTokensAsync("1");

        dict.TryGetValue("1", out var afterEntry);
        Assert.IsTrue(afterEntry.LastAccess > beforeAccess);
        Assert.AreEqual(beforeCount + 1, afterEntry.AccessCount);
    }

    [TestMethod]
    public async Task SaveTokensAsync_PreservesAccessCountWhenUpdating()
    {
        var options = Options.Create(new TokenStorageOptions { MaxTokenCount = 10 });
        var storage = new InMemoryTokenStorage(new NullLogger<InMemoryTokenStorage>(), options);

        await storage.SaveTokensAsync("1", new TokenData { AccessToken = "a", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        _ = await storage.LoadTokensAsync("1");

        var cacheField = typeof(InMemoryTokenStorage).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var cache = (TokenCache)cacheField.GetValue(storage)!;
        var tokensField = typeof(TokenCache).GetField("_tokens", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<string, TokenEntry>)tokensField.GetValue(cache)!;

        dict.TryGetValue("1", out var beforeEntry);
        var beforeCount = beforeEntry.AccessCount;

        await storage.SaveTokensAsync("1", new TokenData { AccessToken = "b", ExpiresAt = DateTime.UtcNow.AddHours(2) });

        dict.TryGetValue("1", out var afterEntry);
        Assert.AreEqual(beforeCount + 1, afterEntry.AccessCount);
        Assert.IsTrue(afterEntry.LastAccess >= beforeEntry.LastAccess);
    }

    [TestMethod]
    public async Task MetricsCounters_AreUpdatedCorrectly()
    {
        var options = Options.Create(new TokenStorageOptions { MaxTokenCount = 5 });
        var storage = new InMemoryTokenStorage(new NullLogger<InMemoryTokenStorage>(), options);

        await storage.SaveTokensAsync("expired", new TokenData { AccessToken = "e", ExpiresAt = DateTime.UtcNow.AddMilliseconds(-1) });
        await storage.SaveTokensAsync("1", new TokenData { AccessToken = "a", ExpiresAt = DateTime.UtcNow.AddMinutes(1) });
        await storage.SaveTokensAsync("2", new TokenData { AccessToken = "b", ExpiresAt = DateTime.UtcNow.AddMinutes(1) });

        _ = await storage.LoadTokensAsync("expired");

        await storage.SaveTokensAsync("3", new TokenData { AccessToken = "c", ExpiresAt = DateTime.UtcNow.AddMinutes(1) });
        await storage.SaveTokensAsync("4", new TokenData { AccessToken = "d", ExpiresAt = DateTime.UtcNow.AddMinutes(1) });
        await storage.SaveTokensAsync("5", new TokenData { AccessToken = "f", ExpiresAt = DateTime.UtcNow.AddMinutes(1) });
        await storage.SaveTokensAsync("6", new TokenData { AccessToken = "g", ExpiresAt = DateTime.UtcNow.AddMinutes(1) });

        var hit = await storage.LoadTokensAsync("5");
        var miss = await storage.LoadTokensAsync("missing");

        Assert.IsNotNull(hit);
        Assert.IsNull(miss);
        Assert.AreEqual(1, storage.Metrics.Hits);
        Assert.AreEqual(2, storage.Metrics.Misses);
        Assert.AreEqual(2, storage.Metrics.Evictions);
        Assert.AreEqual(1, storage.Metrics.Expirations);
    }
}
