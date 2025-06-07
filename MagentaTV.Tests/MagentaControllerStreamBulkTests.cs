using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
public sealed class MagentaControllerStreamBulkTests
{
    private sealed class TestMediator : IMediator
    {
        private readonly ApiResponse<Dictionary<int, string?>> _response;
        public TestMediator(ApiResponse<Dictionary<int, string?>> response) => _response = response;
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => Task.FromResult((TResponse)(object)_response);
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => Task.CompletedTask;
        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(_response);
        public async IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield break;
        }
        public async IAsyncEnumerable<object?> CreateStream(object request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield break;
        }
    }

    [TestMethod]
    public async Task GetStreamUrlsBulk_ReturnsOk()
    {
        var data = new Dictionary<int, string?> { { 1, "u" } };
        var response = ApiResponse<Dictionary<int, string?>>.SuccessResult(data);
        var mediator = new TestMediator(response);
        var controller = new MagentaController(mediator, NullLogger<MagentaController>.Instance);

        var result = await controller.GetStreamUrlsBulk("1");

        Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        var ok = (OkObjectResult)result;
        Assert.AreSame(response, ok.Value);
    }
}
