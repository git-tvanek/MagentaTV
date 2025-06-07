using System;
using MagentaTV.Extensions;
using MagentaTV.Services.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class SessionExtensionsTests
{
    [TestMethod]
    public void GetCurrentSession_ReturnsSession_WhenPresent()
    {
        var context = new DefaultHttpContext();
        var session = new SessionData { SessionId = "1" };
        context.Items["CurrentSession"] = session;

        var result = context.GetCurrentSession();
        Assert.AreSame(session, result);
    }

    [TestMethod]
    public void GetCurrentUsername_ReturnsUsername_WhenPresent()
    {
        var context = new DefaultHttpContext();
        context.Items["CurrentUsername"] = "user";

        var result = context.GetCurrentUsername();
        Assert.AreEqual("user", result);
    }

    [TestMethod]
    public void IsAuthenticated_ReturnsTrue_ForActiveSession()
    {
        var context = new DefaultHttpContext();
        var session = new SessionData
        {
            SessionId = "1",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        context.Items["CurrentSession"] = session;

        Assert.IsTrue(context.IsAuthenticated());
    }

    [TestMethod]
    public void RequireSession_Throws_WhenNoSession()
    {
        var context = new DefaultHttpContext();
        Assert.ThrowsException<UnauthorizedAccessException>(() => context.RequireSession());
    }
}
