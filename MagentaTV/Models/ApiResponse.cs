namespace MagentaTV.Models;

/// <summary>
/// Generic wrapper used for all API responses.  The <typeparamref name="T"/>
/// parameter represents the actual payload returned to the client.  The object
/// also contains common metadata such as success flag, message, validation
/// errors and a timestamp.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a successful response with an optional descriptive message.
    /// </summary>
    /// <param name="data">Payload to send to the caller.</param>
    /// <param name="message">Optional message describing the outcome.</param>
    public static ApiResponse<T> SuccessResult(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    /// <summary>
    /// Creates an error response.  A list of validation errors can be provided
    /// for additional context.
    /// </summary>
    /// <param name="message">Human readable error message.</param>
    /// <param name="errors">Optional collection of validation errors.</param>
    public static ApiResponse<T> ErrorResult(string message, List<string>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = errors ?? new List<string>()
        };
    }
}