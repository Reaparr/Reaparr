namespace Reaparr.Domain;

public class PlexTvShowSeason : BasePlexMedia
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

    public required int SeasonNumber { get; set; }

    /// <summary>
    /// The Plex key of the <see cref="PlexTvShow"/> this belongs too.
    /// </summary>
    public required int ParentKey { get; set; }

    /// <summary>
    /// The Guid of the <see cref="PlexTvShow"/> this belongs too.
    /// </summary>
    public required string? ParentGuid { get; set; }

    #region Relationships

    public PlexTvShow? TvShow { get; set; }

    public int TvShowId { get; set; }

    public ICollection<PlexTvShowEpisode> Episodes { get; set; } = [];

    public ICollection<PlexTvShowSeasonMediaQuality> Qualities { get; set; } = [];

    #endregion

    #region Helpers

    [NotMapped]
    public override PlexMediaType Type => PlexMediaType.Season;

    #endregion
}
