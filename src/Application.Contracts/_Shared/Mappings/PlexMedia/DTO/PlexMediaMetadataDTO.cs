namespace Reaparr.Application.Contracts;

public record PlexMediaMetadataDTO
{
    /// <summary>
    /// Gets or sets the total count of media items associated with the <see cref="PlexLibrary"/>.
    /// </summary>
    public required int MediaCount { get; init; }

    /// <summary>
    /// Gets or sets the total count of distinct roles available in the media items of the <see cref="PlexLibrary"/>.
    /// </summary>
    public required int RoleCount { get; init; }

    /// <summary>
    /// Gets or sets the total count of distinct countries available in the media items of the <see cref="PlexLibrary"/>.
    /// </summary>
    public required int CountryCount { get; init; }

    /// <summary>
    /// Gets or sets the total count of distinct genres available in the media items of the <see cref="PlexLibrary"/>.
    /// </summary>
    public required int GenreCount { get; init; }

    /// <summary>
    /// Gets or sets the total count of distinct quality levels available in the media items of the <see cref="PlexLibrary"/>.
    /// </summary>
    public required int QualityCount { get; init; }

    /// <summary>
    /// Gets or sets the list of distinct roles available in the media items of the <see cref="PlexLibrary"/>.
    /// </summary>
    public required List<PlexRoleDTO> Roles { get; init; }

    /// <summary>
    /// Gets or sets the list of distinct countries available in the media items of the <see cref="PlexLibrary"/>.
    /// </summary>
    public required List<PlexCountryDTO> Countries { get; init; }

    /// <summary>
    /// Gets or sets the list of distinct genres available in the media items of the <see cref="PlexLibrary"/>.
    /// </summary>
    public required List<PlexGenreDTO> Genres { get; init; }

    /// <summary>
    /// Gets or sets the list of distinct quality levels available in the media items of the <see cref="PlexLibrary"/>.
    /// </summary>
    public required List<PlexQualityDTO> Qualities { get; init; }

    /// <summary>
    /// Gets the authoritative sort fields supported by this response's root media type and browsing view.
    /// </summary>
    public required IReadOnlyList<SortType> SupportedSorts { get; init; }
}
