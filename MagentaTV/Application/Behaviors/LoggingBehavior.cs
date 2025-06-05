using MediatR;

namespace MagentaTV.Application.Behaviors
{
    namespace MagentaTV.Application.Behaviors
    {
        public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
            where TRequest : notnull
        {
            private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

            public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
            {
                _logger = logger;
            }

            public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
            {
                var requestName = typeof(TRequest).Name;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                _logger.LogInformation("Handling {RequestName}", requestName);

                try
                {
                    var response = await next();
                    stopwatch.Stop();

                    _logger.LogInformation("Completed {RequestName} in {ElapsedMs}ms",
                        requestName, stopwatch.ElapsedMilliseconds);

                    return response;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex, "Error handling {RequestName} after {ElapsedMs}ms",
                        requestName, stopwatch.ElapsedMilliseconds);
                    throw;
                }
            }
        }
    }
}
