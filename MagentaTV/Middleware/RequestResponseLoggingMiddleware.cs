using System.Diagnostics;
using System.Text;

namespace MagentaTV.Middleware;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        // Log request
        await LogRequest(context.Request);

        // Capture response
        var originalResponseBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        await _next(context);

        stopwatch.Stop();

        // Log response
        await LogResponse(context.Response, stopwatch.ElapsedMilliseconds);

        // Copy response back to original stream
        responseBodyStream.Seek(0, SeekOrigin.Begin);
        await responseBodyStream.CopyToAsync(originalResponseBodyStream);
    }

    private async Task LogRequest(HttpRequest request)
    {
        request.EnableBuffering();

        var body = await new StreamReader(request.Body, Encoding.UTF8).ReadToEndAsync();
        request.Body.Position = 0;

        _logger.LogInformation("HTTP Request: {Method} {Path} {QueryString} Body: {Body}",
            request.Method,
            request.Path,
            request.QueryString,
            body.Length > 1000 ? body.Substring(0, 1000) + "..." : body);
    }

    private async Task LogResponse(HttpResponse response, long elapsedMs)
    {
        response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(response.Body, Encoding.UTF8).ReadToEndAsync();
        response.Body.Seek(0, SeekOrigin.Begin);

        _logger.LogInformation("HTTP Response: {StatusCode} in {ElapsedMs}ms Body: {Body}",
            response.StatusCode,
            elapsedMs,
            body.Length > 1000 ? body.Substring(0, 1000) + "..." : body);
    }
}