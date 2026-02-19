using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MusicDiscovery.LastFm.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MusicDiscovery.LastFm;

public class LastFmApiClient
{
    private const string BaseUrl = "https://ws.audioscrobbler.com/2.0/";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LastFmApiClient> _logger;
    private readonly SemaphoreSlim _rateLimiter = new(5, 5);
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    private record CacheEntry(object Data, DateTime ExpiresAt);

    public LastFmApiClient(
        IHttpClientFactory httpClientFactory,
        ILogger<LastFmApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private string ApiKey => Plugin.Instance?.Configuration.LastFmApiKey ?? string.Empty;
    private int CacheDuration => Plugin.Instance?.Configuration.CacheDurationMinutes ?? 30;

    public async Task<List<SimilarArtistEntry>> GetSimilarArtistsAsync(
        string artistName, int limit, CancellationToken ct = default)
    {
        var cacheKey = $"artist.getSimilar:{artistName}:{limit}";
        if (TryGetCached<ArtistGetSimilarResponse>(cacheKey, out var cached))
            return cached!.SimilarArtists.Artists;

        var url = BuildUrl("artist.getSimilar",
            ("artist", artistName), ("limit", limit.ToString()), ("autocorrect", "1"));

        var response = await GetAsync<ArtistGetSimilarResponse>(url, ct);
        if (response != null)
        {
            SetCache(cacheKey, response);
            return response.SimilarArtists.Artists;
        }
        return new List<SimilarArtistEntry>();
    }

    public async Task<List<SimilarTrackEntry>> GetSimilarTracksAsync(
        string artistName, string trackName, int limit, CancellationToken ct = default)
    {
        var cacheKey = $"track.getSimilar:{artistName}:{trackName}:{limit}";
        if (TryGetCached<TrackGetSimilarResponse>(cacheKey, out var cached))
            return cached!.SimilarTracks.Tracks;

        var url = BuildUrl("track.getSimilar",
            ("artist", artistName), ("track", trackName),
            ("limit", limit.ToString()), ("autocorrect", "1"));

        var response = await GetAsync<TrackGetSimilarResponse>(url, ct);
        if (response != null)
        {
            SetCache(cacheKey, response);
            return response.SimilarTracks.Tracks;
        }
        return new List<SimilarTrackEntry>();
    }

    public async Task<AlbumInfoData?> GetAlbumInfoAsync(
        string artistName, string albumName, CancellationToken ct = default)
    {
        var cacheKey = $"album.getInfo:{artistName}:{albumName}";
        if (TryGetCached<AlbumGetInfoResponse>(cacheKey, out var cached))
            return cached!.Album;

        var url = BuildUrl("album.getInfo",
            ("artist", artistName), ("album", albumName), ("autocorrect", "1"));

        var response = await GetAsync<AlbumGetInfoResponse>(url, ct);
        if (response != null)
        {
            SetCache(cacheKey, response);
            return response.Album;
        }
        return null;
    }

    public async Task<ArtistInfoData?> GetArtistInfoAsync(
        string artistName, CancellationToken ct = default)
    {
        var cacheKey = $"artist.getInfo:{artistName}";
        if (TryGetCached<ArtistGetInfoResponse>(cacheKey, out var cached))
            return cached!.Artist;

        var url = BuildUrl("artist.getInfo",
            ("artist", artistName), ("autocorrect", "1"));

        var response = await GetAsync<ArtistGetInfoResponse>(url, ct);
        if (response != null)
        {
            SetCache(cacheKey, response);
            return response.Artist;
        }
        return null;
    }

    public async Task<List<TopAlbumEntry>> GetArtistTopAlbumsAsync(
        string artistName, int limit, CancellationToken ct = default)
    {
        var cacheKey = $"artist.getTopAlbums:{artistName}:{limit}";
        if (TryGetCached<ArtistGetTopAlbumsResponse>(cacheKey, out var cached))
            return cached!.TopAlbums.Albums;

        var url = BuildUrl("artist.getTopAlbums",
            ("artist", artistName), ("limit", limit.ToString()), ("autocorrect", "1"));

        var response = await GetAsync<ArtistGetTopAlbumsResponse>(url, ct);
        if (response != null)
        {
            SetCache(cacheKey, response);
            return response.TopAlbums.Albums;
        }
        return new List<TopAlbumEntry>();
    }

    private string BuildUrl(string method, params (string key, string value)[] parameters)
    {
        var query = $"?method={method}&api_key={Uri.EscapeDataString(ApiKey)}&format=json";
        foreach (var (key, value) in parameters)
        {
            query += $"&{key}={Uri.EscapeDataString(value)}";
        }
        return BaseUrl + query;
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct) where T : class
    {
        await _rateLimiter.WaitAsync(ct);
        try
        {
            var client = _httpClientFactory.CreateClient("MusicDiscovery");
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Last.fm API returned {StatusCode} for {Url}",
                    response.StatusCode, url);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);

            // Check for Last.fm error response
            if (json.Contains("\"error\""))
            {
                var error = JsonSerializer.Deserialize<LastFmErrorResponse>(json);
                if (error?.Error > 0)
                {
                    _logger.LogWarning("Last.fm API error {Code}: {Message}",
                        error.Error, error.Message);
                    return null;
                }
            }

            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Last.fm API");
            return null;
        }
        finally
        {
            // Release after a short delay to enforce rate limiting
            _ = Task.Delay(200, CancellationToken.None)
                .ContinueWith(_ => _rateLimiter.Release(), CancellationToken.None);
        }
    }

    private bool TryGetCached<T>(string key, out T? value) where T : class
    {
        if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
        {
            value = entry.Data as T;
            return value != null;
        }
        value = null;
        return false;
    }

    private void SetCache(string key, object data)
    {
        var expiry = DateTime.UtcNow.AddMinutes(CacheDuration);
        _cache[key] = new CacheEntry(data, expiry);

        // Lazy cleanup: remove expired entries when cache gets large
        if (_cache.Count > 500)
        {
            foreach (var (k, v) in _cache)
            {
                if (v.ExpiresAt < DateTime.UtcNow)
                    _cache.TryRemove(k, out _);
            }
        }
    }
}
