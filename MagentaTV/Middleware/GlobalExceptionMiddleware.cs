using System.Net;
using System.Text.Json;
using MagentaTV.Models;

namespace MagentaTV.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var traceId = context.TraceIdentifier;
            var errorId = Guid.NewGuid();

            if (exception is AggregateException agg)
            {
                exception = agg.Flatten().InnerExceptions.First();
            }

            _logger.LogError(exception, "Unhandled exception occurred. TraceId: {TraceId}, ErrorId: {ErrorId}", traceId, errorId);

            context.Response.ContentType = "application/json";

            var (statusCode, message, errors) = GetErrorDetails(exception);
            context.Response.StatusCode = (int)statusCode;

            var response = new ApiResponse<object>
            {
                Success = false,
                Message = message,
                Errors = errors,
                Data = _environment.IsDevelopment() ? new
                {
                    TraceId = traceId,
                    ErrorId = errorId,
                    StackTrace = exception.StackTrace,
                    InnerException = exception.InnerException?.Message
                } : new { TraceId = traceId, ErrorId = errorId }
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = _environment.IsDevelopment()
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
        }

        private static (HttpStatusCode statusCode, string message, List<string> errors) GetErrorDetails(Exception exception)
        {
            return exception switch
            {
                ArgumentException or ArgumentNullException =>
                    (HttpStatusCode.BadRequest, "Invalid request parameters", new List<string> { exception.Message }),

                UnauthorizedAccessException =>
                    (HttpStatusCode.Unauthorized, "Authentication required", new List<string> { "Please login to access this resource" }),

                KeyNotFoundException =>
                    (HttpStatusCode.NotFound, "Resource not found", new List<string> { exception.Message }),

                TimeoutException =>
                    (HttpStatusCode.RequestTimeout, "Request timeout", new List<string> { "The request took too long to process" }),

                HttpRequestException =>
                    (HttpStatusCode.BadGateway, "External service error", new List<string> { "Unable to connect to external service" }),

                InvalidOperationException when exception.Message.Contains("session") =>
                    (HttpStatusCode.Unauthorized, "Session error", new List<string> { exception.Message }),

                InvalidOperationException when exception.Message.Contains("token") =>
                    (HttpStatusCode.Unauthorized, "Token error", new List<string> { exception.Message }),

                TaskCanceledException =>
                    (HttpStatusCode.RequestTimeout, "Request cancelled", new List<string> { "Request was cancelled or timed out" }),

                NotImplementedException =>
                    (HttpStatusCode.NotImplemented, "Endpoint not implemented", new List<string> { "This functionality is not available" }),

                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred", new List<string> { "Please try again later" })
            };
        }
    }
}