using System;
using MagentaTV.Services.Session;
using MagentaTV.Models.Session;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class SessionDataTests
{
    [TestMethod]
    public void UpdateActivity_SetsLastActivityAndActivates()
    {
        var session = new SessionData
        {
            SessionId = "1",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            Status = SessionStatus.Inactive
        };

        var before = session.LastActivity;
        session.UpdateActivity();

        Assert.IsTrue(session.LastActivity > before);
        Assert.AreEqual(SessionStatus.Active, session.Status);
    }

    [TestMethod]
    public void Expire_SetsStatusAndExpiresAt()
    {
        var session = new SessionData
        {
            SessionId = "1",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        session.Expire();

        Assert.AreEqual(SessionStatus.Expired, session.Status);
        Assert.IsTrue(session.ExpiresAt <= DateTime.UtcNow);
    }

    [TestMethod]
    public void Revoke_SetsStatusToRevoked()
    {
        var session = new SessionData { SessionId = "1" };

        session.Revoke();

        Assert.AreEqual(SessionStatus.Revoked, session.Status);
    }

    [TestMethod]
    public void IsInactive_ReturnsTrue_WhenTimeoutExceeded()
    {
        var session = new SessionData { LastActivity = DateTime.UtcNow.AddMinutes(-10) };

        Assert.IsTrue(session.IsInactive(TimeSpan.FromMinutes(5)));
    }
}
