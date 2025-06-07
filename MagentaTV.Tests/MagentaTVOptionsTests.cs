using System;
using System.ComponentModel.DataAnnotations;
using MagentaTV.Configuration;
using MagentaTV.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class MagentaTVOptionsTests
{
    [TestMethod]
    public void ValidateTimeRange_ReturnsError_WhenEndBeforeStart()
    {
        var dto = new EpgItemDto
        {
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(-1)
        };

        var result = MagentaTVOptions.ValidateTimeRange(dto, new ValidationContext(dto));
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void ValidateTimeRange_Succeeds_WhenEndAfterStart()
    {
        var dto = new EpgItemDto
        {
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(1)
        };

        var result = MagentaTVOptions.ValidateTimeRange(dto, new ValidationContext(dto));
        Assert.IsNull(result);
    }
}
