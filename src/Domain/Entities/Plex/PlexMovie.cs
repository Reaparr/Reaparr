namespace Reaparr.Domain;

[Table("PlexMovie")]
public class PlexMovie : BasePlexMedia
{
    [Column(Order = 3)]
    public required int Year { get; set; }

    [Column(Order = 16)]
    public required DateTime? OriginallyAvailableAt { get; init; }

    [Column(Order = 10)]
    public required string Summary { get; init; } = string.Empty;

    [Column(Order = 9)]
    public required string Studio { get; init; } = string.Empty;

    [Column(Order = 11)]
    public required string ContentRating { get; init; } = string.Empty;

    [Column(Order = 12)]
    public required double Rating { get; init; }

    [Column(Order = 21)]
    public required bool HasTheme { get; init; }

    [Column(Order = 22)]
    public required string FullTitle { get; set; } = string.Empty;

    [Column(Order = 23)]
    public required string Guid { get; init; } = string.Empty;

    /// <summary>Gets or sets the IMDB identifier without the provider prefix.</summary>
    [Column(Order = 24)]
    // ReSharper disable once InconsistentNaming
    public required string? Guid_IMDB { get; init; }

    /// <summary>Gets or sets the TMDB identifier without the provider prefix.</summary>
    [Column(Order = 25)]
    // ReSharper disable once InconsistentNaming
    public required int? Guid_TMDB { get; init; }

    /// <summary>Gets or sets the TVDB identifier without the provider prefix.</summary>
    [Column(Order = 26)]
    // ReSharper disable once InconsistentNaming
    public required int? Guid_TVDB { get; init; }

    [NotMapped]
    public string ThemeUrl => HasTheme ? $"{MetaDataUrl}/theme/{PlexApiMetaDataKey}" : string.Empty;

    public required ICollection<PlexActor> Actors { get; init; } = [];

    public required ICollection<PlexGenreVideo> Genres { get; init; } = [];

    public required ICollection<PlexCountry> Countries { get; init; } = [];

    public ICollection<PlexMovieMediaData> MediaDataList { get; init; } = [];

    [NotMapped]
    public override PlexMediaType Type => PlexMediaType.Movie;
}
