using MagentaTV.Configuration;
using MagentaTV.Models;
using MagentaTV.Services;
using MagentaTV.Services.TokenStorage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace MagentaTV.Services
{
    /// <summary>
    /// High level client responsible for communicating with the MagentaTV API
    /// and caching results. It also manages authentication tokens via
    /// <see cref="ITokenStorage"/>.
    /// </summary>
    public class Magenta : IMagenta
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<Magenta> _logger;
        private readonly MagentaTVOptions _options;
        private readonly CacheOptions _cacheOptions;
        private readonly TokenStorageOptions _tokenOptions;
        private readonly ITokenStorage _tokenStorage;
        private readonly NetworkOptions _networkOptions;
        private readonly string _devId;

        // Current session tokens
        private string? _accessToken;
        private string? _refreshToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        public Magenta(
            HttpClient httpClient,
            IMemoryCache cache,
            ILogger<Magenta> logger,
            IOptions<MagentaTVOptions> options,
            IOptions<CacheOptions> cacheOptions,
            IOptions<TokenStorageOptions> tokenOptions,
            ITokenStorage tokenStorage,
            IOptions<NetworkOptions> networkOptions)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _options = options.Value;
            _cacheOptions = cacheOptions.Value;
            _tokenOptions = tokenOptions.Value;
            _tokenStorage = tokenStorage;
            _networkOptions = networkOptions.Value;

            _devId = GetOrCreateDeviceIdAsync().GetAwaiter().GetResult();
            ConfigureHttpClient();

            // Auto-load tokens on startup if enabled
            if (_tokenOptions.AutoLoad)
            {
                _ = Task.Run(LoadStoredTokensAsync);
            }
        }

        private async Task LoadStoredTokensAsync()
        {
            try
            {
                var tokens = await _tokenStorage.LoadTokensAsync();
                if (tokens?.IsValid == true)
                {
                    _accessToken = tokens.AccessToken;
                    _refreshToken = tokens.RefreshToken;
                    _tokenExpiry = tokens.ExpiresAt;

                    _logger.LogInformation("Loaded valid tokens for user: {Username}, expires: {ExpiresAt}",
                        tokens.Username, tokens.ExpiresAt);
                }
                else if (tokens != null)
                {
                    _logger.LogInformation("Loaded expired tokens for user: {Username}, expired: {ExpiresAt}",
                        tokens.Username, tokens.ExpiresAt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load stored tokens on startup");
            }
        }

        /// <summary>
        /// Performs a full login flow using the provided user credentials.
        /// The method initializes a device session, authenticates the user and
        /// persists the received tokens if configured to do so.
        /// </summary>
        /// <param name="username">MagentaTV account name.</param>
        /// <param name="password">Password for the account.</param>
        /// <returns><c>true</c> when login succeeded.</returns>
        public async Task<bool> LoginAsync(string username, string password)
        {
            var stopwatch = Stopwatch.StartNew();

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
                    _logger.LogInformation("Login successful for user: {Username} in {ElapsedMs}ms",
                        username, stopwatch.ElapsedMilliseconds);

                    // Save tokens to storage if auto-save is enabled
                    if (_tokenOptions.AutoSave && !string.IsNullOrEmpty(_accessToken))
                    {
                        var tokenData = new TokenData
                        {
                            AccessToken = _accessToken,
                            RefreshToken = _refreshToken ?? "",
                            ExpiresAt = _tokenExpiry,
                            Username = username,
                            DeviceId = _devId,
                            CreatedAt = DateTime.UtcNow
                        };

                        await _tokenStorage.SaveTokensAsync(tokenData);
                        _logger.LogDebug("Tokens saved to storage for user: {Username}", username);
                    }
                }
                else
                {
                    _logger.LogWarning("Login failed for user: {Username} after {ElapsedMs}ms",
                        username, stopwatch.ElapsedMilliseconds);
                }

                return loginSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for user: {Username} after {ElapsedMs}ms",
                    username, stopwatch.ElapsedMilliseconds);
                return false;
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public async Task LogoutAsync(string sessionId)
        {
            try
            {
                _logger.LogInformation("Logging out...");

                // Clear tokens from memory
                _accessToken = null;
                _refreshToken = null;
                _tokenExpiry = DateTime.MinValue;

                // Clear tokens from storage
                await _tokenStorage.ClearTokensAsync(sessionId);

                _logger.LogInformation("Logout completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                throw;
            }
        }

        private async Task<string?> InitializeSessionAsync()
        {
            try
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
                    var accessToken = accessTokenProp.GetString();
                    _logger.LogDebug("Session initialized successfully");
                    return accessToken;
                }

                _logger.LogWarning("No access token in session initialization response");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize session");
                throw;
            }
        }

        private async Task<bool> PerformLoginAsync(string sessionToken, string username, string password)
        {
            try
            {
                var loginBody = new { loginOrNickname = username, password = password };
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/{_options.ApiVersion}/auth/login")
                {
                    Content = new StringContent(JsonSerializer.Serialize(loginBody), Encoding.UTF8, "application/json")
                };

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                if (json.RootElement.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    _accessToken = json.RootElement.GetProperty("token").GetProperty("accessToken").GetString();
                    _refreshToken = json.RootElement.GetProperty("token").GetProperty("refreshToken").GetString();
                    _tokenExpiry = DateTime.UtcNow.AddHours(_tokenOptions.TokenExpirationHours);

                    _logger.LogDebug("Login performed successfully for user: {Username}", username);
                    return true;
                }

                _logger.LogWarning("Login unsuccessful - invalid credentials for user: {Username}", username);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform login for user: {Username}", username);
                throw;
            }
        }

        private async Task EnsureAuthenticatedAsync()
        {
            // Check if current tokens are valid
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
                return;

            // Try to load from storage
            var tokens = await _tokenStorage.LoadTokensAsync();
            if (tokens?.IsValid == true)
            {
                _accessToken = tokens.AccessToken;
                _refreshToken = tokens.RefreshToken;
                _tokenExpiry = tokens.ExpiresAt;
                _logger.LogDebug("Loaded valid tokens from storage for user: {Username}", tokens.Username);
                return;
            }

            // If storage doesn't have valid tokens either
            throw new UnauthorizedAccessException("Authentication required. Please login first.");
        }

        public async Task<List<ChannelDto>> GetChannelsAsync()
        {
            const string cacheKey = "channels";

            if (_cache.TryGetValue(cacheKey, out List<ChannelDto>? cached))
            {
                _logger.LogDebug("Returning cached channels");
                return cached!;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await EnsureAuthenticatedAsync();

                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{_options.BaseUrl}/{_options.ApiVersion}/television/channels?list=LIVE&queryScope=LIVE");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var channels = ParseChannels(json);

                var cacheExpiry = TimeSpan.FromMinutes(_cacheOptions.ChannelsExpirationMinutes);
                _cache.Set(cacheKey, channels, cacheExpiry);

                stopwatch.Stop();
                _logger.LogInformation("Retrieved {ChannelCount} channels in {Duration}ms",
                    channels.Count, stopwatch.ElapsedMilliseconds);

                return channels;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to get channels after {Duration}ms", stopwatch.ElapsedMilliseconds);
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
            var cacheKey = $"epg_{channelId}_{from?.Date:yyyyMMdd}_{to?.Date:yyyyMMdd}";

            if (_cache.TryGetValue(cacheKey, out List<EpgItemDto>? cached))
            {
                _logger.LogDebug("Returning cached EPG for channel {ChannelId}", channelId);
                return cached!;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await EnsureAuthenticatedAsync();

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

                stopwatch.Stop();
                _logger.LogInformation("Retrieved {EpgCount} EPG items for channel {ChannelId} in {Duration}ms",
                    epg.Count, channelId, stopwatch.ElapsedMilliseconds);

                return epg;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Failed to get EPG for channel {ChannelId} after {Duration}ms",
                    channelId, stopwatch.ElapsedMilliseconds);
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

            try
            {
                await EnsureAuthenticatedAsync();

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

                _logger.LogWarning("No stream URL in response for channel {ChannelId}", channelId);
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
            try
            {
                await EnsureAuthenticatedAsync();

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
                    var result = streamUrl.GetString();
                    _logger.LogInformation("Retrieved catchup stream URL for schedule {ScheduleId}", scheduleId);
                    return result;
                }

                _logger.LogWarning("No catchup stream URL in response for schedule {ScheduleId}", scheduleId);
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
            try
            {
                var channels = await GetChannelsAsync();
                var sb = new StringBuilder("#EXTM3U\n");

                foreach (var ch in channels)
                {
                    sb.Append($"#EXTINF:-1 tvg-id=\"{ch.ChannelId}\" tvg-name=\"{ch.Name}\"");

                    if (ch.HasArchive)
                        sb.Append($" catchup=\"default\" catchup-source=\"/magenta/catchup/{ch.ChannelId}/" + "${start}-${end}\" catchup-days=\"7\"");

                    if (!string.IsNullOrEmpty(ch.LogoUrl))
                        sb.Append($" tvg-logo=\"{ch.LogoUrl}\"");

                    sb.Append($",{ch.Name}\n");
                    sb.Append($"https://{_networkOptions.IpAddress}:3000/magenta/stream/{ch.ChannelId}\n");
                }

                var result = sb.ToString();
                _logger.LogInformation("Generated M3U playlist with {ChannelCount} channels", channels.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate M3U playlist");
                throw;
            }
        }

        public string GenerateXmlTv(List<EpgItemDto> epg, int channelId)
        {
            try
            {
                var doc = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
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

                var result = doc.Declaration + doc.ToString();
                _logger.LogInformation("Generated XMLTV for channel {ChannelId} with {EpgCount} programs", channelId, epg.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate XMLTV for channel {ChannelId}", channelId);
                throw;
            }
        }

        public async Task<TokenData?> RefreshTokensAsync(TokenData currentTokens)
        {
            try
            {
                if (string.IsNullOrEmpty(currentTokens.RefreshToken))
                {
                    _logger.LogWarning("Cannot refresh tokens - no refresh token available for user: {Username}", currentTokens.Username);
                    return null;
                }

                var body = new { refreshToken = currentTokens.RefreshToken };
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/{_options.ApiVersion}/auth/refresh")
                {
                    Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentTokens.RefreshToken);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Token refresh failed with status {Status} for user: {Username}",
                        response.StatusCode, currentTokens.Username);
                    return null;
                }

                var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (json.RootElement.TryGetProperty("token", out var token))
                {
                    _accessToken = token.GetProperty("accessToken").GetString();
                    _refreshToken = token.GetProperty("refreshToken").GetString();
                    _tokenExpiry = DateTime.UtcNow.AddHours(_tokenOptions.TokenExpirationHours);

                    var newTokenData = new TokenData
                    {
                        AccessToken = _accessToken ?? string.Empty,
                        RefreshToken = _refreshToken ?? string.Empty,
                        ExpiresAt = _tokenExpiry,
                        Username = currentTokens.Username,
                        DeviceId = _devId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _logger.LogInformation("Successfully refreshed tokens for user {Username}", currentTokens.Username);
                    return newTokenData;
                }

                _logger.LogWarning("Token refresh response missing token data for user: {Username}", currentTokens.Username);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh tokens for user {Username}", currentTokens.Username);
                return null;
            }
        }

        private async Task<string> GetOrCreateDeviceIdAsync()
        {
            const string deviceIdFile = "dev_id.txt";

            try
            {
                if (File.Exists(deviceIdFile))
                {
                    var deviceId = (await File.ReadAllTextAsync(deviceIdFile)).Trim();
                    if (!string.IsNullOrEmpty(deviceId))
                    {
                        _logger.LogDebug("Using existing device ID: {DeviceId}", deviceId);
                        return deviceId;
                    }
                }

                var newDeviceId = Guid.NewGuid().ToString();
                await File.WriteAllTextAsync(deviceIdFile, newDeviceId);
                _logger.LogInformation("Created new device ID: {DeviceId}", newDeviceId);
                return newDeviceId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read/write device ID file, using temporary ID");
                return Guid.NewGuid().ToString();
            }
        }

        private void ConfigureHttpClient()
        {
            try
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
                _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

                _logger.LogDebug("HTTP client configured with timeout: {Timeout}s, UserAgent: {UserAgent}",
                    _options.TimeoutSeconds, _options.UserAgent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to configure HTTP client headers");
            }
        }
    }
}