using MagentaTV.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Xml.Linq;
using static System.Net.WebRequestMethods;

namespace MagentaTV.Services;

public class Magenta
{
    private readonly string _lng = "cz";
    private readonly string _ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36 MagioGO/4.0.21";
    private readonly string _devName = "Android-STB";
    private readonly string _devType = "OTT_STB";
    private readonly string _quality = "p5";
    private readonly string _devId;
    private readonly IHttpClientFactory _factory;
    private string? _accessToken;
    private string? _refreshToken;

    public Magenta(IHttpClientFactory factory)
    {
        _factory = factory;
        _devId = System.IO.File.Exists("dev_id.txt") ? System.IO.File.ReadAllText("dev_id.txt") : Guid.NewGuid().ToString();
        if (!System.IO.File.Exists("dev_id.txt")) System.IO.File.WriteAllText("dev_id.txt", _devId);
    }

    public async Task<bool> LoginAsync(string user, string pass)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(_ua);

        var initParams = new Dictionary<string, string>
        {
            {"dsid", _devId},
            {"deviceName", _devName},
            {"deviceType", _devType},
            {"osVersion", "0.0.0"},
            {"appVersion", "4.0.25.0"},
            {"language", _lng.ToUpper()},
            {"devicePlatform", "GO"}
        };
        var initUri = $"https://czgo.magio.tv/v2/auth/init?" + string.Join("&", initParams.Select(x => $"{x.Key}={x.Value}"));
        var initResp = await client.PostAsync(initUri, null);
        var initJson = JsonDocument.Parse(await initResp.Content.ReadAsStringAsync());

        if (!initJson.RootElement.TryGetProperty("token", out var token) || !token.TryGetProperty("accessToken", out var accessTokenProp))
            return false;

        var accessToken = accessTokenProp.GetString();
        var loginBody = new { loginOrNickname = user, password = pass };
        var loginReq = new HttpRequestMessage(HttpMethod.Post, "https://czgo.magio.tv/v2/auth/login")
        {
            Content = new StringContent(JsonSerializer.Serialize(loginBody), System.Text.Encoding.UTF8, "application/json")
        };
        loginReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        loginReq.Headers.UserAgent.ParseAdd(_ua);

        var loginResp = await client.SendAsync(loginReq);
        var loginJson = JsonDocument.Parse(await loginResp.Content.ReadAsStringAsync());
        if (!loginJson.RootElement.TryGetProperty("success", out var succ) || !succ.GetBoolean())
            return false;

        _accessToken = loginJson.RootElement.GetProperty("token").GetProperty("accessToken").GetString();
        _refreshToken = loginJson.RootElement.GetProperty("token").GetProperty("refreshToken").GetString();

        return true;
    }

    public async Task<List<ChannelDto>> GetChannelsAsync()
    {
        if (_accessToken == null)
            throw new Exception("Not authenticated");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(_ua);
        var resp = await client.GetAsync("https://czgo.magio.tv/v2/television/channels?list=LIVE&queryScope=LIVE");
        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = new List<ChannelDto>();

        if (json.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var ch = item.GetProperty("channel");
                result.Add(new ChannelDto
                {
                    ChannelId = ch.GetProperty("channelId").GetInt32(),
                    Name = ch.GetProperty("name").GetString(),
                    LogoUrl = ch.TryGetProperty("logoUrl", out var l) ? l.GetString() : "",
                    HasArchive = ch.TryGetProperty("hasArchive", out var a) && a.GetBoolean()
                });
            }
        }
        return result;
    }

    public async Task<List<EpgItemDto>> GetEpgAsync(int channelId, DateTime? from = null, DateTime? to = null)
    {
        if (_accessToken == null)
            throw new Exception("Not authenticated");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(_ua);

        var now = DateTime.UtcNow;
        from ??= now.AddDays(-2);
        to ??= now.AddDays(1);

        var startTime = from.Value.ToString("yyyy-MM-ddT00:00:00.000Z");
        var endTime = to.Value.ToString("yyyy-MM-ddT23:59:59.000Z");
        var filter = $"channel.id=={channelId} and startTime=ge={startTime} and endTime=le={endTime}";
        var uri = $"https://czgo.magio.tv/v2/television/epg?filter={Uri.EscapeDataString(filter)}&limit=1000&offset=0&lang=CZ";

        var resp = await client.GetAsync(uri);
        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = new List<EpgItemDto>();

        if (json.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("programs", out var programs))
                {
                    foreach (var prog in programs.EnumerateArray())
                    {
                        var pr = prog.GetProperty("program");
                        result.Add(new EpgItemDto
                        {
                            Title = pr.GetProperty("title").GetString(),
                            Description = pr.TryGetProperty("description", out var d) ? d.GetString() : "",
                            StartTime = DateTimeOffset.FromUnixTimeMilliseconds(prog.GetProperty("startTimeUTC").GetInt64()).UtcDateTime,
                            EndTime = DateTimeOffset.FromUnixTimeMilliseconds(prog.GetProperty("endTimeUTC").GetInt64()).UtcDateTime,
                            Category = pr.TryGetProperty("programCategory", out var cat) && cat.TryGetProperty("desc", out var cdesc) ? cdesc.GetString() : "",
                            ScheduleId = prog.GetProperty("scheduleId").GetInt64()
                        });
                    }
                }
            }
        }
        return result;
    }

    public async Task<string?> GetStreamUrlAsync(int channelId)
    {
        if (_accessToken == null)
            throw new Exception("Not authenticated");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(_ua);
        client.DefaultRequestHeaders.Referrer = new Uri("https://czgo.magio.tv/");

        var parameters = new Dictionary<string, string>
        {
            {"service", "LIVE"},
            {"name", _devName},
            {"devtype", _devType},
            {"id", channelId.ToString()},
            {"prof", _quality},
            {"ecid", ""},
            {"drm", "widevine"},
            {"start", "LIVE"},
            {"end", "END"},
            {"device", "OTT_PC_HD_1080p_v2"}
        };
        var url = "https://czgo.magio.tv/v2/television/stream-url?" + string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));

        var resp = await client.GetAsync(url);
        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        if (json.RootElement.TryGetProperty("url", out var streamUrl))
            return streamUrl.GetString();

        return null;
    }

    public async Task<string?> GetCatchupStreamUrlAsync(long scheduleId)
    {
        if (_accessToken == null)
            throw new Exception("Not authenticated");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(_ua);

        var parameters = new Dictionary<string, string>
        {
            {"service", "ARCHIVE"},
            {"name", _devName},
            {"devtype", _devType},
            {"id", scheduleId.ToString()},
            {"prof", _quality},
            {"ecid", ""},
            {"drm", "widevine"}
        };
        var url = "https://czgo.magio.tv/v2/television/stream-url?" + string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));

        var resp = await client.GetAsync(url);
        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        if (json.RootElement.TryGetProperty("url", out var streamUrl))
            return streamUrl.GetString();

        return null;
    }

    public async Task<string> GenerateM3UPlaylistAsync()
    {
        var channels = await GetChannelsAsync();
        var sb = new System.Text.StringBuilder("#EXTM3U\n");
        foreach (var ch in channels)
        {
            sb.Append($"#EXTINF:-1 tvg-id=\"{ch.ChannelId}\" tvg-name=\"{ch.Name}\"");
            if (ch.HasArchive)
                sb.Append($" catchup=\"default\" catchup-source=\"/magenta/catchup/{ch.ChannelId}/" + "${start}-${end}\" catchup-days=\"7\"");
            if (!string.IsNullOrEmpty(ch.LogoUrl))
                sb.Append($" tvg-logo=\"{ch.LogoUrl}\"");
            sb.Append($",{ch.Name}\n");
            sb.Append($"https://localhost:3000/magenta/stream/{ch.ChannelId}\n");
        }
        return sb.ToString();
    }

    public string GenerateXmlTv(List<EpgItemDto> epg, int channelId)
    {
        var doc = new XDocument(
            new XElement("tv",
                new XElement("channel",
                    new XAttribute("id", channelId),
                    new XElement("display-name", $"Channel {channelId}")
                ),
                epg.Select(e => new XElement("programme",
                    new XAttribute("start", e.StartTime.ToString("yyyyMMddHHmmss") + " +0000"),
                    new XAttribute("stop", e.EndTime.ToString("yyyyMMddHHmmss") + " +0000"),
                    new XAttribute("channel", channelId),
                    new XElement("title", e.Title),
                    new XElement("desc", e.Description ?? ""),
                    new XElement("category", e.Category ?? ""),
                    new XElement("scheduleId", e.ScheduleId.ToString())
                ))
            )
        );
        return doc.Declaration + doc.ToString();
    }
}

