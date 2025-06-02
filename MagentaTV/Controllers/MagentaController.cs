using Microsoft.AspNetCore.Mvc;
using MagentaTV.Models;
using MagentaTV.Services;

namespace MagentaTV.Controllers;

[ApiController]
[Route("magenta")]
public class MagentaController : ControllerBase
{
    private readonly Magenta _service;

    public MagentaController(Magenta service)
    {
        _service = service;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto login)
    {
        var ok = await _service.LoginAsync(login.Username, login.Password);
        return ok ? Ok("Přihlášení OK") : Unauthorized("Chyba přihlášení");
    }

    [HttpGet("channels")]
    public async Task<ActionResult<List<ChannelDto>>> Channels()
        => await _service.GetChannelsAsync();

    [HttpGet("epg/{channelId}")]
    public async Task<ActionResult<List<EpgItemDto>>> Epg(int channelId)
        => await _service.GetEpgAsync(channelId);

    [HttpGet("stream/{channelId}")]
    public async Task<IActionResult> Stream(int channelId)
    {
        var url = await _service.GetStreamUrlAsync(channelId);
        if (string.IsNullOrEmpty(url))
            return NotFound();
        return Ok(url);
    }

    [HttpGet("catchup/{scheduleId}")]
    public async Task<IActionResult> Catchup(long scheduleId)
    {
        var url = await _service.GetCatchupStreamUrlAsync(scheduleId);
        if (string.IsNullOrEmpty(url))
            return NotFound();
        return Ok(url);
    }

    [HttpGet("playlist")]
    public async Task<IActionResult> Playlist()
    {
        var playlist = await _service.GenerateM3UPlaylistAsync();
        return File(System.Text.Encoding.UTF8.GetBytes(playlist), "audio/x-mpegurl", "playlist.m3u");
    }

    [HttpGet("epgxml/{channelId}")]
    public async Task<IActionResult> EpgXml(int channelId)
    {
        var epg = await _service.GetEpgAsync(channelId);
        var xml = _service.GenerateXmlTv(epg, channelId);
        return File(System.Text.Encoding.UTF8.GetBytes(xml), "application/xml", "epg.xml");
    }
}
