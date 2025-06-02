using MagentaTV.Models;
using MagentaTV.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Xml.Linq;
using Polly;
using Polly.Extensions.Http;

namespace MagentaTV.Services;

public class Magenta : IMagenta
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<Magenta> _logger;
    private readonly MagentaTVOptions _options;
    private readonly CacheOptions _cacheOptions;
    private readonly string _devId;

    private string? _accessToken;
    private string? _refreshToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public Magenta(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<Magenta> logger,
        IOptions<MagentaTVOptions> options,
        IOptions<CacheOptions> cacheOptions)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
        _cacheOptions = cacheOptions.Value;

        _devId = GetOrCreateDeviceId();
        ConfigureHttpClient();
    }

    private string GetOrCreateDeviceId()
    {
        const string deviceIdFile = "dev_id.txt";

        if (File.Exists(deviceIdFile))
        {
            var deviceId = File.ReadAllText(deviceIdFile).Trim();
            if (!string.IsNullOrEmpty(deviceId))
            {
                _logger.LogInformation("Using existing device ID: {DeviceId}", deviceId);
                return deviceId;
            }
        }

        var newDeviceId = Guid.NewGuid().ToString();
        File.WriteAllText(deviceIdFile, newDeviceId);
        _logger.LogInformation("Created new device ID: {DeviceId}", newDeviceId);
        return newDeviceId;
    }

    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            _logger.LogInformation("Attempting login for user: {Username}", username);

            // Step 1: Initialize session
            var accessToken = await InitializeSessionAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Failed to initialize session");
                return false;
            }

            // Step 2: Login with credentials
            var loginSuccess = await PerformLoginAsync(accessToken, username, password);
            if (loginSuccess)
            {
                _logger.LogInformation("Login successful for user: {Username}", username);
                _tokenExpiry = DateTime.UtcNow.AddHours(1); // Token typically expires in 1 hour
            }
            else
            {
                _logger.LogWarning("Login failed for user: {Username}", username);
            }

            return loginSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for user: {Username}", username);
            return false;
        }
    }

    private async Task<string?> InitializeSessionAsync()
    {
        var initParams = new Dictionary<string, string>
        {
            {"dsid", _devId},
            {"deviceName", _options.DeviceName},
            {"deviceType", _options.DeviceType},
            {"osVersion", "0.0.0"},
            {"appVersion", "4.0.25.0"},
            {"language", _options.Language.ToUpper()},
            {"devicePlatform", "GO"}
        };

        var initUri = $"{_options.BaseUrl}/{_options.ApiVersion}/auth/init?" +
                     string.Join("&", initParams.Select(x => $"{x.Key}={x.Value}"));

        var response = await _httpClient.PostAsync(initUri, null);
        response.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        if (json.RootElement.TryGetProperty("token", out var token) &&
            token.TryGetProperty("accessToken", out var accessTokenProp))
        {
            return accessTokenProp.GetString();
        }

        return null;
    }

    private async Task<bool> PerformLoginAsync(string sessionToken, string username, string password)
    {
        var loginBody = new { loginOrNickname = username, password = password };
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/{_options.ApiVersion}/auth/login")
        {
            Content = new StringContent(JsonSerializer.Serialize(loginBody), System.Text.Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        if (json.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
        {
            _accessToken = json.RootElement.GetProperty("token").GetProperty("accessToken").GetString();
            _refreshToken = json.RootElement.GetProperty("token").GetProperty("refreshToken").GetString();
            return true;
        }

        return false;
    }

    public async Task<List<ChannelDto>> GetChannelsAsync()
    {
        const string cacheKey = "channels";

        if (_cache.TryGetValue(cacheKey, out List<ChannelDto>? cached))
        {
            _logger.LogDebug("Returning cached channels");
            return cached!;
        }

        await EnsureAuthenticatedAsync();

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_options.BaseUrl}/{_options.ApiVersion}/television/channels?list=LIVE&queryScope=LIVE");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var channels = ParseChannels(json);

            var cacheExpiry = TimeSpan.FromMinutes(_cacheOptions.ChannelsExpirationMinutes);
            _cache.Set(cacheKey, channels, cacheExpiry);

            _logger.LogInformation("Retrieved {ChannelCount} channels", channels.Count);
            return channels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get channels");
            throw;
        }
    }

    private List<ChannelDto> ParseChannels(JsonDocument json)
    {
        var result = new List<ChannelDto>();

        if (json.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                try
                {
                    var ch = item.GetProperty("channel");
                    result.Add(new ChannelDto
                    {
                        ChannelId = ch.GetProperty("channelId").GetInt32(),
                        Name = ch.GetProperty("name").GetString() ?? "",
                        LogoUrl = ch.TryGetProperty("logoUrl", out var logo) ? logo.GetString() ?? "" : "",
                        HasArchive = ch.TryGetProperty("hasArchive", out var archive) && archive.GetBoolean()
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse channel item");
                }
            }
        }

        return result;
    }

    public async Task<List<EpgItemDto>> GetEpgAsync(int channelId, DateTime? from = null, DateTime? to = null)
    {
        var cacheKey = $"epg_{channelId}_{from?.Date}_{to?.Date}";

        if (_cache.TryGetValue(cacheKey, out List<EpgItemDto>? cached))
        {
            _logger.LogDebug("Returning cached EPG for channel {ChannelId}", channelId);
            return cached!;
        }

        await EnsureAuthenticatedAsync();

        try
        {
            var now = DateTime.UtcNow;
            from ??= now.AddDays(-2);
            to ??= now.AddDays(1);

            var startTime = from.Value.ToString("yyyy-MM-ddT00:00:00.000Z");
            var endTime = to.Value.ToString("yyyy-MM-ddT23:59:59.000Z");
            var filter = $"channel.id=={channelId} and startTime=ge={startTime} and endTime=le={endTime}";
            var uri = $"{_options.BaseUrl}/{_options.ApiVersion}/television/epg?filter={Uri.EscapeDataString(filter)}&limit=1000&offset=0&lang=CZ";

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var epg = ParseEpg(json);

            var cacheExpiry = TimeSpan.FromMinutes(_cacheOptions.EpgExpirationMinutes);
            _cache.Set(cacheKey, epg, cacheExpiry);

            _logger.LogInformation("Retrieved {EpgCount} EPG items for channel {ChannelId}", epg.Count, channelId);
            return epg;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get EPG for channel {ChannelId}", channelId);
            throw;
        }
    }

    private List<EpgItemDto> ParseEpg(JsonDocument json)
    {
        var result = new List<EpgItemDto>();

        if (json.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("programs", out var programs))
                {
                    foreach (var prog in programs.EnumerateArray())
                    {
                        try
                        {
                            var pr = prog.GetProperty("program");
                            result.Add(new EpgItemDto
                            {
                                Title = pr.GetProperty("title").GetString() ?? "",
                                Description = pr.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                                StartTime = DateTimeOffset.FromUnixTimeMilliseconds(prog.GetProperty("startTimeUTC").GetInt64()).UtcDateTime,
                                EndTime = DateTimeOffset.FromUnixTimeMilliseconds(prog.GetProperty("endTimeUTC").GetInt64()).UtcDateTime,
                                Category = pr.TryGetProperty("programCategory", out var cat) && cat.TryGetProperty("desc", out var cdesc) ? cdesc.GetString() ?? "" : "",
                                ScheduleId = prog.GetProperty("scheduleId").GetInt64()
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse EPG program item");
                        }
                    }
                }
            }
        }

        return result;
    }

    public async Task<string?> GetStreamUrlAsync(int channelId)
    {
        var cacheKey = $"stream_{channelId}";

        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            _logger.LogDebug("Returning cached stream URL for channel {ChannelId}", channelId);
            return cached;
        }

        await EnsureAuthenticatedAsync();

        try
        {
            var parameters = new Dictionary<string, string>
            {
                {"service", "LIVE"},
                {"name", _options.DeviceName},
                {"devtype", _options.DeviceType},
                {"id", channelId.ToString()},
                {"prof", _options.Quality},
                {"ecid", ""},
                {"drm", "widevine"},
                {"start", "LIVE"},
                {"end", "END"},
                {"device", "OTT_PC_HD_1080p_v2"}
            };

            var url = $"{_options.BaseUrl}/{_options.ApiVersion}/television/stream-url?" +
                     string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            request.Headers.Referrer = new Uri($"{_options.BaseUrl}/");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            if (json.RootElement.TryGetProperty("url", out var streamUrl))
            {
                var streamUrlString = streamUrl.GetString();

                var cacheExpiry = TimeSpan.FromMinutes(_cacheOptions.StreamUrlExpirationMinutes);
                _cache.Set(cacheKey, streamUrlString, cacheExpiry);

                _logger.LogInformation("Retrieved stream URL for channel {ChannelId}", channelId);
                return streamUrlString;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stream URL for channel {ChannelId}", channelId);
            throw;
        }
    }

    public async Task<string?> GetCatchupStreamUrlAsync(long scheduleId)
    {
        await EnsureAuthenticatedAsync();

        try
        {
            var parameters = new Dictionary<string, string>
            {
                {"service", "ARCHIVE"},
                {"name", _options.DeviceName},
                {"devtype", _options.DeviceType},
                {"id", scheduleId.ToString()},
                {"prof", _options.Quality},
                {"ecid", ""},
                {"drm", "widevine"}
            };

            var url = $"{_options.BaseUrl}/{_options.ApiVersion}/television/stream-url?" +
                     string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            if (json.RootElement.TryGetProperty("url", out var streamUrl))
            {
                _logger.LogInformation("Retrieved catchup stream URL for schedule {ScheduleId}", scheduleId);
                return streamUrl.GetString();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get catchup stream URL for schedule {ScheduleId}", scheduleId);
            throw;
        }
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

        _logger.LogInformation("Generated M3U playlist with {ChannelCount} channels", channels.Count);
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

        _logger.LogInformation("Generated XMLTV for channel {ChannelId} with {EpgCount} programs", channelId, epg.Count);
        return doc.Declaration + doc.ToString();
    }

    private async Task EnsureAuthenticatedAsync()
    {
        if (string.IsNullOrEmpty(_accessToken) || DateTime.UtcNow >= _tokenExpiry)
        {
            throw new UnauthorizedAccessException("Authentication required. Please login first.");
        }
    }

    private async Task RefreshTokenAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken))
            throw new UnauthorizedAccessException("Refresh token not available. Please login again.");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/{_options.ApiVersion}/auth/tokens")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { refreshToken = _refreshToken }),
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
            // NEposílej Authorization, není potřeba!
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (json.RootElement.TryGetProperty("token", out var token))
            {
                _accessToken = token.GetProperty("accessToken").GetString();
                _refreshToken = token.GetProperty("refreshToken").GetString();
                _tokenExpiry = DateTime.UtcNow.AddHours(1);
                _logger.LogInformation("Token refreshed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh token");
            throw new UnauthorizedAccessException("Failed to refresh authentication token. Please login again.");
        }
    }
}
