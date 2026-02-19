namespace Jellyfin.Plugin.MusicDiscovery.Api;

public class RecommendationDto
{
    public string Name { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public double MatchScore { get; set; }
    public List<string> Tags { get; set; } = new();
    public ExternalLinksDto Links { get; set; } = new();
    public string Type { get; set; } = string.Empty; // "artist", "album", "track"
}

public class ExternalLinksDto
{
    public string? LastFmUrl { get; set; }
    public string? MusicBrainzUrl { get; set; }
    public string? DiscogsSearchUrl { get; set; }
    public string? BandcampSearchUrl { get; set; }
}

public class RecommendationsResponse
{
    public string SourceName { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public List<RecommendationDto> Recommendations { get; set; } = new();
}
