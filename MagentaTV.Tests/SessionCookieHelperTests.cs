using System;
using MagentaTV.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class SessionCookieHelperTests
{
    [TestMethod]
    public void GetSessionId_ReturnsId_FromCookie()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Append("Cookie", "SessionId=abc");

        var result = SessionCookieHelper.GetSessionId(context.Request);
        Assert.AreEqual("abc", result);
    }

    [TestMethod]
    public void GetSessionId_ReturnsId_FromAuthorizationHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Session xyz";

        var result = SessionCookieHelper.GetSessionId(context.Request);
        Assert.AreEqual("xyz", result);
    }

    [TestMethod]
    public void SetSessionCookie_AppendsCookie()
    {
        var context = new DefaultHttpContext();

        SessionCookieHelper.SetSessionCookie(context.Response, "token", true);

        Assert.IsTrue(context.Response.Headers.SetCookie.ToString().Contains("SessionId=token"));
    }

    [TestMethod]
    public void RemoveSessionCookie_DeletesCookie()
    {
        var context = new DefaultHttpContext();
        SessionCookieHelper.SetSessionCookie(context.Response, "abc", true);

        SessionCookieHelper.RemoveSessionCookie(context.Response);

        Assert.IsTrue(context.Response.Headers.SetCookie.ToString().Contains("SessionId=;"));
    }
}
