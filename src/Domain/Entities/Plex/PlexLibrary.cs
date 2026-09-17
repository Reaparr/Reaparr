namespace Reaparr.Domain;

public class PlexLibrary : BaseEntity
{
    #region Properties

    /// <summary>
    /// Gets or sets plex Library type, see: https://github.com/Arcanemagus/plex-api/wiki/MediaTypes.
    /// </summary>
    [Column(Order = 1)]
    public required PlexMediaType Type { get; init; }

    /// <summary>
    /// Gets or sets the display title of this <see cref="PlexLibrary"/>.
    /// <remarks>Value is set by the PlexApi</remarks>
    /// </summary>
    [Column(Order = 2)]
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets the Library Section Identifier used by Plex.
    /// <remarks>Value is set by the PlexApi</remarks>
    /// </summary>
    [Column(Order = 3)]
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets the creation date of this <see cref="PlexLibrary"/> on the <see cref="PlexServer"/> by the owner.
    /// <remarks>Value is set by the PlexApi</remarks>
    /// </summary>
    [Column(Order = 5)]
    public required DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last time this <see cref="PlexLibrary"/> was updated by the <see cref="PlexServer"/> owner.
    /// <remarks>Value is set by the PlexApi</remarks>
    /// </summary>
    [Column(Order = 6)]
    public required DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last time this <see cref="PlexLibrary"/> was scanned for new media by the <see cref="PlexServer"/> owner.
    /// <remarks>Value is set by the PlexApi</remarks>
    /// </summary>
    [Column(Order = 7)]
    public required DateTime? ScannedAt { get; set; }

    /// <summary>
    /// Gets or sets Plex's raw <c>contentChangedAt</c> counter for this library.
    /// <remarks>Value is set by the PlexApi</remarks>
    /// </summary>
    [Column(Order = 8)]
    public long ContentChangedAt { get; set; }

    /// <summary>
    /// Gets or sets the Plex content changestamp consumed by the last successful media sync and comparison scheduling.
    /// </summary>
    [Column(Order = 9)]
    public long? SyncedContentChangedAt { get; set; }

    /// <summary>
    /// Gets or sets the DateTime this <see cref="PlexLibrary"/> had its media last synced with the PlexApi.
    /// </summary>
    [Column(Order = 10)]
    public DateTime? SyncedAt { get; set; }

    /// <summary>
    /// Gets or sets the unique id of the <see cref="PlexLibrary"/>.
    /// Can be a valid GUID or a Plex generated UUID.
    /// <remarks>Value is set by the PlexApi</remarks>
    /// </summary>
    [Column(Order = 11)]
    public required string Uuid { get; set; }

    /// <summary>
    ///
    /// <remarks>Value is set by the PlexApi</remarks>
    /// </summary>
    [Column(Order = 12)]
    public required string Language { get; set; }

    /// <summary>
    /// Gets the total file size of the nested media.
    /// </summary>
    [Column(Order = 13)]
    public long MediaSize { get; init; }

    /// <summary>
    /// Gets the total <see cref="PlexMovie"/> count.
    /// </summary>
    [Column(Order = 14)]
    public int MovieCount { get; init; }

    /// <summary>
    /// Gets the total downloadable movie media-data rows in this library.
    /// A logical movie can contribute multiple rows when Plex exposes multiple versions or file parts.
    /// </summary>
    [Column(Order = 15)]
    public int MovieMediaDataCount { get; init; }

    /// <summary>
    /// Gets the total <see cref="PlexTvShow"/> count.
    /// </summary>
    [Column(Order = 16)]
    public int TvShowCount { get; init; }

    /// <summary>
    /// Gets the total <see cref="PlexTvShowSeason"/> count of all <see cref="PlexTvShow">PlexTvShows</see> in this library.
    /// </summary>
    [Column(Order = 17)]
    public int SeasonCount { get; init; }

    /// <summary>
    /// Gets the total <see cref="PlexTvShowEpisode"/> count of all <see cref="PlexTvShow">PlexTvShows</see> in this library.
    /// </summary>
    [Column(Order = 18)]
    public int EpisodeCount { get; init; }

    /// <summary>
    /// Gets the total downloadable episode media-data rows in this library.
    /// A logical episode can contribute multiple rows when Plex exposes multiple versions or file parts.
    /// </summary>
    [Column(Order = 19)]
    public int EpisodeMediaDataCount { get; init; }

    [Column(Order = 20)]
    public int ActorsCount { get; init; }

    [Column(Order = 21)]
    public int GenresCount { get; init; }

    [Column(Order = 22)]
    public int CountriesCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether this <see cref="PlexLibrary"/> needs to be synced with Reaparr.
    /// This is dependent on <see cref="ContentChangedAt"/>, when a higher value is set from the Plex API, then its marked as outdated.
    /// </summary>
    [Column(Order = 23)]
    public bool Outdated { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="PlexLibrary"/> is enabled.
    /// When disabled, the library is excluded from normal workflows and its synced media data is purged.
    /// </summary>
    [Column(Order = 24)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// DB-computed column that holds the total count of media items associated with this <see cref="PlexLibrary"/>.
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    // ReSharper disable once UnusedAutoPropertyAccessor.Local // Used by EF Core
    public int MediaCount { get; private set; }

    #endregion

    #region Relationships

    /// <summary>
    /// Gets or sets the PlexServer this PlexLibrary belongs to.
    /// </summary>
    public PlexServer? PlexServer { get; set; }

    /// <summary>
    /// Gets or sets the PlexServerId of the PlexServer this PlexLibrary belongs to.
    /// </summary>
    public required int PlexServerId { get; set; }

    /// <summary>
    /// Gets or sets the default download destination <see cref="FolderPath"/>.
    /// </summary>
    public FolderPath? DefaultDestination { get; set; }

    /// <summary>
    /// Gets or sets the id of the Default Destination <see cref="FolderPath"/>.
    /// Is only set if the user has diverted from the default <see cref="FolderPath"/> by the <see cref="PlexMediaType">Library Type</see>
    /// </summary>
    public int? DefaultDestinationId { get; set; }

    public ICollection<PlexMovie> Movies { get; private set; } = [];

    public ICollection<PlexTvShow> TvShows { get; private set; } = [];

    public ICollection<PlexAccountLibrary> PlexAccountLibraries { get; private set; } = [];

    public ICollection<PlexActor> Actors { get; set; } = [];

    public ICollection<PlexGenre> Genres { get; set; } = [];

    public ICollection<PlexCountry> Countries { get; set; } = [];

    #endregion

    #region Helpers

    [NotMapped]
    public string Name => Title;

    #endregion
}
