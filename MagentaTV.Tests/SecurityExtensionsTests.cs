using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MagentaTV.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class SecurityExtensionsTests
{
    [TestMethod]
    public void AddSecurityValidation_Throws_WhenMissingEnvVars_InProduction()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        var inMemory = new Dictionary<string, string?>
        {
            ["Session:EncryptionKey"] = "development-key"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var services = new ServiceCollection();

        Assert.ThrowsException<InvalidOperationException>(() => services.AddSecurityValidation(config));
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
    }
}
