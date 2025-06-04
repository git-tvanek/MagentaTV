using MagentaTV.Configuration;
using MagentaTV.Models;
using MagentaTV.Services;
using Microsoft.AspNetCore.Mvc;

namespace MagentaTV.Controllers;

/// <summary>
/// Controller pro FFmpeg streaming operace
/// </summary>
[ApiController]
[Route("streaming")]
public class StreamingController : ControllerBase
{
    private readonly IStreaming _streaming;
    private readonly IMagenta _magenta;
    private readonly ILogger<StreamingController> _logger;

    public StreamingController(
        IStreaming streamingService,
        IMagenta magentaService,
        ILogger<StreamingController> logger)
    {
        _streaming = streamingService;
        _magenta = magentaService;
        _logger = logger;
    }

    /// <summary>
    /// Vytvoří proxy stream s quality adaptation
    /// </summary>
    [HttpPost("proxy/{channelId}")]
    [ProducesResponseType(typeof(ApiResponse<ProxyStreamDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 400)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    [ProducesResponseType(typeof(ApiResponse<string>), 404)]
    public async Task<IActionResult> CreateProxyStream(
        int channelId,
        [FromBody] StreamProxyRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<string>.ErrorResult("Validation failed", errors));
        }

        try
        {
            // Get original stream URL from MagentaTV
            var originalUrl = await _magenta.GetStreamUrlAsync(channelId);
            if (string.IsNullOrEmpty(originalUrl))
            {
                return NotFound(ApiResponse<string>.ErrorResult("Stream not found",
                    new List<string> { $"Stream pro kanál {channelId} nebyl nalezen" }));
            }

            // Create FFmpeg proxy stream
            var proxyStream = await _streaming.CreateProxyStreamAsync(
                originalUrl, request.Quality, channelId, request.Options);

            return Ok(ApiResponse<ProxyStreamDto>.SuccessResult(proxyStream,
                $"Proxy stream vytvořen pro kanál {channelId}"));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required",
                new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Operation failed",
                new List<string> { ex.Message }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating proxy stream for channel {ChannelId}", channelId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při vytváření proxy streamu" }));
        }
    }

    /// <summary>
    /// Nahrává program podle EPG
    /// </summary>
    [HttpPost("record/{scheduleId}")]
    [ProducesResponseType(typeof(ApiResponse<RecordingDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 400)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> RecordProgram(
        long scheduleId,
        [FromBody] RecordingRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<string>.ErrorResult("Validation failed", errors));
        }

        try
        {
            var recording = await _streaming.ScheduleRecordingAsync(scheduleId, request);
            return Ok(ApiResponse<RecordingDto>.SuccessResult(recording,
                $"Nahrávání naplánováno pro pořad {scheduleId}"));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required",
                new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Operation failed",
                new List<string> { ex.Message }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling recording for schedule {ScheduleId}", scheduleId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při plánování nahrávky" }));
        }
    }

    /// <summary>
    /// Generuje thumbnail pro program
    /// </summary>
    [HttpPost("thumbnail/{scheduleId}")]
    [ProducesResponseType(typeof(ApiResponse<ThumbnailDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 400)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> GenerateThumbnail(
        long scheduleId,
        [FromQuery] int timestampMinutes = 5)
    {
        if (timestampMinutes < 0 || timestampMinutes > 480) // max 8 hours
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Invalid timestamp",
                new List<string> { "Timestamp musí být mezi 0 a 480 minutami" }));
        }

        try
        {
            var thumbnail = await _streaming.GenerateThumbnailAsync(
                scheduleId, TimeSpan.FromMinutes(timestampMinutes));

            return Ok(ApiResponse<ThumbnailDto>.SuccessResult(thumbnail,
                $"Thumbnail vygenerován pro pořad {scheduleId}"));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required",
                new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Operation failed",
                new List<string> { ex.Message }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating thumbnail for schedule {ScheduleId}", scheduleId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při generování thumbnails" }));
        }
    }

    /// <summary>
    /// Monitoring aktivních streamů
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<List<ActiveStreamDto>>), 200)]
    public async Task<IActionResult> GetActiveStreams()
    {
        try
        {
            var activeStreams = await _streaming.GetActiveStreamsAsync();
            return Ok(ApiResponse<List<ActiveStreamDto>>.SuccessResult(activeStreams,
                $"Nalezeno {activeStreams.Count} aktivních streamů"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active streams");
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při načítání aktivních streamů" }));
        }
    }

    /// <summary>
    /// Ukončí aktivní stream
    /// </summary>
    [HttpDelete("proxy/{streamId}")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 404)]
    public async Task<IActionResult> StopProxyStream(string streamId)
    {
        try
        {
            await _streaming.StopProxyStreamAsync(streamId);
            return Ok(ApiResponse<string>.SuccessResult("Stream stopped",
                $"Stream {streamId} byl ukončen"));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiResponse<string>.ErrorResult("Stream not found",
                new List<string> { $"Stream {streamId} nebyl nalezen" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping stream {StreamId}", streamId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při ukončování streamu" }));
        }
    }

    /// <summary>
    /// Health check pro FFmpeg službu
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(ApiResponse<StreamingHealthDto>), 200)]
    public async Task<IActionResult> GetStreamingHealth()
    {
        try
        {
            var health = await _streaming.GetHealthAsync();
            return Ok(ApiResponse<StreamingHealthDto>.SuccessResult(health,
                health.IsHealthy ? "FFmpeg služba je v pořádku" : "FFmpeg služba má problémy"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting streaming service health");
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při kontrole stavu služby" }));
        }
    }

    /// <summary>
    /// Stažení nahrávky
    /// </summary>
    [HttpGet("recordings/{fileName}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 404)]
    public IActionResult DownloadRecording(string fileName)
    {
        try
        {
            var filePath = Path.Combine("data/recordings", fileName);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(ApiResponse<string>.ErrorResult("Recording not found",
                    new List<string> { $"Nahrávka {fileName} nebyla nalezena" }));
            }

            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var contentType = fileName.EndsWith(".mp4") ? "video/mp4" : "application/octet-stream";

            return File(fileStream, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading recording {FileName}", fileName);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při stahování nahrávky" }));
        }
    }

    /// <summary>
    /// Stažení thumbnail
    /// </summary>
    [HttpGet("thumbnails/{fileName}")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 404)]
    public IActionResult GetThumbnail(string fileName)
    {
        try
        {
            var filePath = Path.Combine("data/thumbnails", fileName);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(ApiResponse<string>.ErrorResult("Thumbnail not found",
                    new List<string> { $"Thumbnail {fileName} nebyl nalezen" }));
            }

            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return File(fileStream, "image/jpeg", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting thumbnail {FileName}", fileName);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při načítání thumbnail" }));
        }
    }
}