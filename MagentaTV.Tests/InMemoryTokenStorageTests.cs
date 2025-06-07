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

        Assert.IsNull(result);
        Assert.AreEqual(1, storage.Metrics.Expirations);
        Assert.AreEqual(1, storage.Metrics.Misses);
    }

    [TestMethod]
    public async Task LoadTokensAsync_UpdatesLastAccessAndAccessCount()
    {
        var options = Options.Create(new TokenStorageOptions { MaxTokenCount = 10 });
        var storage = new InMemoryTokenStorage(new NullLogger<InMemoryTokenStorage>(), options);

        await storage.SaveTokensAsync("1", new TokenData { AccessToken = "a", ExpiresAt = DateTime.UtcNow.AddHours(1) });

        var field = typeof(InMemoryTokenStorage).GetField("_tokens", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<string, TokenEntry>)field.GetValue(storage)!;

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

        var field = typeof(InMemoryTokenStorage).GetField("_tokens", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var dict = (System.Collections.Concurrent.ConcurrentDictionary<string, TokenEntry>)field.GetValue(storage)!;

        dict.TryGetValue("1", out var beforeEntry);
        var beforeCount = beforeEntry.AccessCount;

        await storage.SaveTokensAsync("1", new TokenData { AccessToken = "b", ExpiresAt = DateTime.UtcNow.AddHours(2) });

        dict.TryGetValue("1", out var afterEntry);
        Assert.AreEqual(beforeCount + 1, afterEntry.AccessCount);
        Assert.IsTrue(afterEntry.LastAccess >= beforeEntry.LastAccess);
    }
}
