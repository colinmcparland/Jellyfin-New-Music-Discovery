namespace Jellyfin.Plugin.MusicDiscovery.Api;

public class SaveRecommendationRequest
{
    public string Name { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public double MatchScore { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Type { get; set; } = string.Empty;
    public string? LastFmUrl { get; set; }
}

public class DeleteRecommendationRequest
{
    public string Name { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class CheckRecommendationsRequest
{
    public List<string> Names { get; set; } = new();
    public List<string> Artists { get; set; } = new();
    public List<string> Types { get; set; } = new();
}

public class SavedCheckResult
{
    public string Name { get; set; } = string.Empty;
    public string ArtistName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
