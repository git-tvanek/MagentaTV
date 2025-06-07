using System;
using System.Reflection;
using System.Threading.Tasks;
using MagentaTV.Services.TokenStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class TokenExpirationManagerTests
{
    [TestMethod]
    public void CleanupExpiredTokens_RemovesEntriesAndUpdatesMetrics()
    {
        var metrics = new TokenStorageMetrics();
        var cache = new TokenCache(10, metrics, NullLogger<TokenCache>.Instance);
        using var manager = new TokenExpirationManager(cache, metrics, NullLogger<TokenExpirationManager>.Instance);

        cache.Save("expired", new TokenData { AccessToken = "a", ExpiresAt = DateTime.UtcNow.AddMilliseconds(-1) });
        cache.Save("valid", new TokenData { AccessToken = "b", ExpiresAt = DateTime.UtcNow.AddMinutes(5) });

        var method = typeof(TokenExpirationManager).GetMethod("CleanupExpiredTokens", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(manager, null);

        Assert.IsFalse(cache.TryGet("expired", out _));
        Assert.IsTrue(cache.TryGet("valid", out _));
        Assert.AreEqual(1, metrics.Expirations);
    }
}
