namespace Reaparr.Data.Contracts;

public record PagedMediaQueryResult
{
    public string QueryHash { get; set; } = string.Empty;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int MediaCount { get; set; }

    public int MovieCount { get; set; }

    public int TvShowCount { get; set; }

    public int SeasonCount { get; set; }

    public int EpisodeCount { get; set; }

    public int TotalMovieCount { get; set; }

    public int TotalTvShowCount { get; set; }

    public int TotalSeasonCount { get; set; }

    public int TotalEpisodeCount { get; set; }

    public long MediaSize { get; set; }

    public long TotalMediaSize { get; set; }

    public List<PlexMediaSlimDTO> Items { get; set; } = [];

    public List<MediaNavigationIndexDTO> NavigationIndexes { get; set; } = [];

    #region Metadata

    /// <summary>
    /// Gets or sets the list of distinct roles available in the media items of the <see cref="PlexLibrary"/>.
    /// </summary>
    public List<int> Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of distinct countries available in the media items of the <see cref="PlexLibrary"/>.
    /// </summary>
    public List<int> Countries { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of distinct genres available in the media items of the <see cref="PlexLibrary"/>.
    /// </summary>
    public List<int> Genres { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of distinct quality levels available in the media items of the <see cref="PlexLibrary"/>.
    /// </summary>
    public List<int> Qualities { get; set; } = [];

    #endregion
}
