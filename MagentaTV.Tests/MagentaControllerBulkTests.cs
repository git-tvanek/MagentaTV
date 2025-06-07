using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MagentaTV.Application.Queries;
using MagentaTV.Controllers;
using MagentaTV.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MagentaTV.Tests;

[TestClass]
public sealed class MagentaControllerBulkTests
{
    private sealed class TestMediator : IMediator
    {
        private readonly ApiResponse<Dictionary<int, List<EpgItemDto>>> _response;
        public TestMediator(ApiResponse<Dictionary<int, List<EpgItemDto>>> response) => _response = response;
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((TResponse)(object)_response);
        }
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => Task.FromResult<object?>(_response);
    }

    [TestMethod]
    public async Task GetEpgBulk_ReturnsOk()
    {
        var data = new Dictionary<int, List<EpgItemDto>> { { 1, new List<EpgItemDto>() } };
        var response = ApiResponse<Dictionary<int, List<EpgItemDto>>>.SuccessResult(data);
        var mediator = new TestMediator(response);
        var controller = new MagentaController(mediator, NullLogger<MagentaController>.Instance);

        var result = await controller.GetEpgBulk("1");

        Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        var ok = (OkObjectResult)result;
        Assert.AreSame(response, ok.Value);
    }
}
