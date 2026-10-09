namespace Reaparr.Domain;

/// <summary>
/// The waterfall layer that produced a match between remote and owned media.
/// Higher-confidence layers are tried first; stored on each comparison hit row
/// so confidence can be exposed or filtered later.
/// </summary>
public enum PlexMediaComparisonMatchType
{
    /// <summary>
    /// No match resolved yet (default for rows created before a match resolved).
    /// </summary>
    None = 0,

    /// <summary>
    /// Matched by shared TMDB GUID.
    /// </summary>
    TmdbGuid = 1,

    /// <summary>
    /// Matched by shared IMDB GUID.
    /// </summary>
    ImdbGuid = 2,

    /// <summary>
    /// Matched by shared TVDB GUID.
    /// </summary>
    TvdbGuid = 3,

    /// <summary>
    /// Matched by normalized title (<see cref="BasePlexMedia.SearchTitle"/>) + exact year.
    /// </summary>
    NormalizedTitleAndYear = 4,

    /// <summary>
    /// Matched by normalized title + exact year + duration (top-level video fallback).
    /// </summary>
    NormalizedTitleYearAndDuration = 5,

    /// <summary>
    /// Matched by parent structure (matched show + season/episode numbers).
    /// </summary>
    ParentAndChildNumbers = 6,

    MusicBrainzArtistId = 7,

    MusicBrainzReleaseId = 8,

    MusicBrainzReleaseTrackId = 9,

    MusicBrainzRecordingId = 10,
}
