using System;
using System.Threading;
using System.Threading.Tasks;
using MagentaTV.Extensions;
using MagentaTV.Services.Background.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class BackgroundServiceExtensionsTests
{
    [TestMethod]
    public void CreateWorkItem_SetsProperties()
    {
        var item = BackgroundServiceExtensions.CreateWorkItem(
            "name",
            "type",
            (_,_) => Task.CompletedTask,
            5);

        Assert.AreEqual("name", item.Name);
        Assert.AreEqual("type", item.Type);
        Assert.AreEqual(5, item.Priority);
        Assert.IsNotNull(item.WorkItem);
    }

    [TestMethod]
    public void CreateScheduledWorkItem_SetsDate()
    {
        var future = DateTime.UtcNow.AddDays(1);
        var item = BackgroundServiceExtensions.CreateScheduledWorkItem(
            "name",
            "type",
            (_,_) => Task.CompletedTask,
            future,
            1);

        Assert.AreEqual(future, item.ScheduledFor);
        Assert.AreEqual(1, item.Priority);
    }
}
