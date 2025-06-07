using System;
using System.ComponentModel.DataAnnotations;
using MagentaTV.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace MagentaTV.Tests;

[TestClass]
public sealed class EpgItemDtoTests
{
    [TestMethod]
    public void Validate_ReturnsError_WhenDurationTooLong()
    {
        var dto = new EpgItemDto
        {
            Title = "Test",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(9),
            ScheduleId = 1
        };

        var results = dto.Validate(new ValidationContext(dto)).ToList();
        Assert.IsTrue(results.Any(r => r.ErrorMessage!.Contains("duration")));
    }
}
