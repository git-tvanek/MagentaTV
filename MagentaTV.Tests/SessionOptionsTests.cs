using System;
using MagentaTV.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class SessionOptionsTests
{
    [TestMethod]
    public void Validate_Throws_WhenDefaultGreaterThanMax()
    {
        var opts = new SessionOptions
        {
            DefaultDurationHours = 10,
            MaxDurationHours = 5,
            EncryptionKey = new string('x',32)
        };

        Assert.ThrowsException<ArgumentException>(() => opts.Validate());
    }

    [TestMethod]
    public void GetEncryptionKey_ReturnsEnvVar_WhenSet()
    {
        Environment.SetEnvironmentVariable("SESSION_ENCRYPTION_KEY", "abcdef" + new string('x',26));
        var opts = new SessionOptions { EncryptionKey = "ignore" };
        var key = opts.GetEncryptionKey();
        Assert.AreEqual("abcdef" + new string('x',26), key);
        Environment.SetEnvironmentVariable("SESSION_ENCRYPTION_KEY", null);
    }
}
