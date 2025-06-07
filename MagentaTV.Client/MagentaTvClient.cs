using System.Net;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using MagentaTV.Client.Models;
using MagentaTV.Client.Models.Session;

namespace MagentaTV.Client;

public class MagentaTvClient
{
    private readonly HttpClient _httpClient;

    public CookieContainer Cookies { get; } = new();

    public MagentaTvClient(string baseUrl)
    {
        var handler = new HttpClientHandler { CookieContainer = Cookies };
        _httpClient = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<ApiResponse<SessionCreatedDto>> LoginAsync(LoginDto dto)
    {
        var response = await _httpClient.PostAsJsonAsync("auth/login", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse<SessionCreatedDto>>()
               ?? new ApiResponse<SessionCreatedDto> { Success = false, Message = "Invalid response" };
    }

    public async Task<ApiResponse<string>> LogoutAsync()
    {
        var response = await _httpClient.PostAsync("auth/logout", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse<string>>()
               ?? new ApiResponse<string> { Success = false, Message = "Invalid response" };
    }

    public async Task<ApiResponse<AuthStatusDto>> GetAuthStatusAsync()
    {
        var response = await _httpClient.GetAsync("magenta/auth/status");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse<AuthStatusDto>>()
               ?? new ApiResponse<AuthStatusDto> { Success = false, Message = "Invalid response" };
    }

    public async Task<ApiResponse<List<ChannelDto>>> GetChannelsAsync()
    {
        var response = await _httpClient.GetAsync("magenta/channels");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse<List<ChannelDto>>>()
               ?? new ApiResponse<List<ChannelDto>> { Success = false, Message = "Invalid response" };
    }

    public async Task<ApiResponse<List<EpgItemDto>>> GetEpgAsync(int channelId, DateTime? from = null, DateTime? to = null)
    {
        var url = $"magenta/epg/{channelId}";
        var query = new List<string>();
        if (from.HasValue) query.Add($"from={from:O}");
        if (to.HasValue) query.Add($"to={to:O}");
        if (query.Count > 0) url += "?" + string.Join("&", query);

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse<List<EpgItemDto>>>()
               ?? new ApiResponse<List<EpgItemDto>> { Success = false, Message = "Invalid response" };
    }

    public async Task<ApiResponse<Dictionary<int, List<EpgItemDto>>>> GetEpgBulkAsync(IEnumerable<int> channelIds, DateTime? from = null, DateTime? to = null)
    {
        var idList = string.Join(",", channelIds);
        var query = new List<string> { $"ids={idList}" };
        if (from.HasValue) query.Add($"from={from:O}");
        if (to.HasValue) query.Add($"to={to:O}");
        var url = "magenta/epg/bulk?" + string.Join("&", query);

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse<Dictionary<int, List<EpgItemDto>>>>()
               ?? new ApiResponse<Dictionary<int, List<EpgItemDto>>> { Success = false, Message = "Invalid response" };
    }

    public async Task<ApiResponse<StreamUrlDto>> GetStreamUrlAsync(int channelId)
    {
        var response = await _httpClient.GetAsync($"magenta/stream/{channelId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse<StreamUrlDto>>()
               ?? new ApiResponse<StreamUrlDto> { Success = false, Message = "Invalid response" };
    }

    public async Task<ApiResponse<StreamUrlDto>> GetCatchupStreamAsync(long scheduleId)
    {
        var response = await _httpClient.GetAsync($"magenta/catchup/{scheduleId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse<StreamUrlDto>>()
               ?? new ApiResponse<StreamUrlDto> { Success = false, Message = "Invalid response" };
    }

    public async Task<string> GetPlaylistAsync()
    {
        var response = await _httpClient.GetAsync("magenta/playlist");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetEpgXmlAsync(int channelId)
    {
        var response = await _httpClient.GetAsync($"magenta/epgxml/{channelId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<ApiResponse<PingResultDto>> PingAsync()
    {
        var response = await _httpClient.GetAsync("magenta/ping");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse<PingResultDto>>()
               ?? new ApiResponse<PingResultDto> { Success = false, Message = "Invalid response" };
    }

    public static async Task<string?> DiscoverServerAsync(int port = 15998, int timeoutMs = 3000)
    {
        using var udp = new UdpClient();
        udp.EnableBroadcast = true;
        var request = Encoding.UTF8.GetBytes("MAGENTATV_DISCOVERY_REQUEST");
        await udp.SendAsync(request, request.Length, new IPEndPoint(IPAddress.Broadcast, port));

        var receiveTask = udp.ReceiveAsync();
        var completed = await Task.WhenAny(receiveTask, Task.Delay(timeoutMs));
        if (completed == receiveTask)
        {
            var result = await receiveTask;
            var message = Encoding.UTF8.GetString(result.Buffer);
            const string prefix = "MAGENTATV_DISCOVERY_RESPONSE|";
            if (message.StartsWith(prefix))
            {
                return message.Substring(prefix.Length);
            }
        }
        return null;
    }
}
