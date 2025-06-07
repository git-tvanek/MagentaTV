using System;
using MagentaTV.Services.TokenStorage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class TokenDataTests
{
    [TestMethod]
    public void IsExpired_ReturnsTrue_WhenPastDate()
    {
        var token = new TokenData
        {
            AccessToken = "abc",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };

        Assert.IsTrue(token.IsExpired);
    }

    [TestMethod]
    public void IsValid_ReturnsTrue_WhenTokenNotExpired()
    {
        var token = new TokenData
        {
            AccessToken = "abc",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        Assert.IsTrue(token.IsValid);
    }

    [TestMethod]
    public void IsNearExpiry_ReturnsTrue_WhenLessThanFiveMinutes()
    {
        var token = new TokenData
        {
            AccessToken = "abc",
            ExpiresAt = DateTime.UtcNow.AddMinutes(4)
        };

        Assert.IsTrue(token.IsNearExpiry);
    }
}
