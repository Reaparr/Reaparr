namespace Reaparr.Data.Contracts;

/// <summary>
/// A database-side music artist state projection. It intentionally contains only the
/// artist key and the canonical browse state so callers can filter/order before loading
/// the artist DTO graph.
/// </summary>
public sealed class MusicArtistComparisonStateRow
{
    public int ArtistId { get; init; }

    public PlexMediaComparisonState ComparisonState { get; init; }
}

public sealed class MusicArtistComparisonCandidateRow
{
    public int Id { get; init; }

    public string? SearchTitle { get; init; }

    public int SortIndex { get; init; }

    public int Duration { get; init; }

    public long MediaSize { get; init; }

    public DateTime AddedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public int AudioQuality { get; init; }

    public int ComparisonId { get; init; }
}

public static partial class DbSetExtensions
{
    /// <summary>
    /// Returns completed, current music comparison scopes. A scope is current only when
    /// both libraries still have the same media type, ownership role, and non-stale data.
    /// </summary>
    public static IQueryable<PlexComparisonState> QueryCurrentMusicComparisonScopes(this IReaparrDbContext dbContext)
    {
        var ownedLibraries = dbContext
            .PlexLibraries.WhereIsOwned()
            .Where(x => x.Type == PlexMediaType.MusicArtist && !x.Outdated);
        var remoteLibraries = dbContext
            .PlexLibraries.WhereIsNotOwned()
            .Where(x => x.Type == PlexMediaType.MusicArtist && !x.Outdated);

        return dbContext
            .PlexComparisonScopes.Where(x => x.MediaType == PlexMediaType.MusicArtist)
            .Join(ownedLibraries, scope => scope.OwnedPlexLibraryId, library => library.Id, (scope, _) => scope)
            .Join(remoteLibraries, scope => scope.RemotePlexLibraryId, library => library.Id, (scope, _) => scope);
    }

    /// <summary>
    /// Projects the state of artists in owned libraries using the same scope, album, track,
    /// partial-child and higher-quality precedence used by <c>ApplyComparisonState</c>.
    /// </summary>
    public static IQueryable<MusicArtistComparisonStateRow> ProjectOwnedMusicArtistComparisonStates(
        this IReaparrDbContext dbContext,
        IQueryable<PlexMusicArtist> source
    )
    {
        var currentScopes = dbContext.QueryCurrentMusicComparisonScopes();
        var partialArtistIds = QueryOwnedArtistsWithPartialChildren(dbContext, currentScopes);
        var higherQualityArtistIds = QueryOwnedArtistsWithHigherQuality(dbContext, currentScopes);

        return source.Select(artist => new MusicArtistComparisonStateRow
        {
            ArtistId = artist.Id,
            ComparisonState = partialArtistIds.Contains(artist.Id)
                ? higherQualityArtistIds.Contains(artist.Id)
                    ? PlexMediaComparisonState.PartialAndHigherQuality
                    : PlexMediaComparisonState.Partial
                : higherQualityArtistIds.Contains(artist.Id)
                    ? PlexMediaComparisonState.HigherQuality
                    : PlexMediaComparisonState.Owned,
        });
    }

    /// <summary>
    /// Projects the state of artists in remote libraries using the same current-scope,
    /// missing-child, partial-child and higher-quality precedence used by
    /// <c>ApplyComparisonState</c>.
    /// </summary>
    public static IQueryable<MusicArtistComparisonStateRow> ProjectRemoteMusicArtistComparisonStates(
        this IReaparrDbContext dbContext,
        IQueryable<PlexMusicArtist> source
    )
    {
        var currentScopes = dbContext.QueryCurrentMusicComparisonScopes();
        var matchedArtistIds = QueryCurrentMusicArtistComparisons(dbContext, currentScopes)
            .Select(comparison => comparison.RemotePlexMediaId)
            .Distinct();
        var partialArtistIds = QueryRemoteArtistsWithPartialChildren(dbContext, currentScopes);
        var higherQualityArtistIds = QueryRemoteArtistsWithHigherQuality(dbContext, currentScopes);

        return source.Select(artist => new MusicArtistComparisonStateRow
        {
            ArtistId = artist.Id,
            ComparisonState =
                !matchedArtistIds.Contains(artist.Id) ? PlexMediaComparisonState.Missing
                : partialArtistIds.Contains(artist.Id)
                    ? higherQualityArtistIds.Contains(artist.Id) ? PlexMediaComparisonState.PartialAndHigherQuality
                        : PlexMediaComparisonState.Partial
                : higherQualityArtistIds.Contains(artist.Id) ? PlexMediaComparisonState.HigherQuality
                : PlexMediaComparisonState.Owned,
        });
    }

    private static IQueryable<PlexMusicArtistComparison> QueryCurrentMusicArtistComparisons(
        IReaparrDbContext dbContext,
        IQueryable<PlexComparisonState> currentScopes
    ) =>
        dbContext.PlexMusicArtistComparisons.Where(comparison =>
            currentScopes.Any(scope =>
                scope.RemotePlexLibraryId == comparison.RemotePlexLibraryId
                && scope.OwnedPlexLibraryId == comparison.OwnedPlexLibraryId
            )
        );

    private static IQueryable<PlexMusicAlbumComparison> QueryCurrentMusicAlbumComparisons(
        IReaparrDbContext dbContext,
        IQueryable<PlexComparisonState> currentScopes
    ) =>
        dbContext.PlexMusicAlbumComparisons.Where(comparison =>
            currentScopes.Any(scope =>
                scope.RemotePlexLibraryId == comparison.RemotePlexLibraryId
                && scope.OwnedPlexLibraryId == comparison.OwnedPlexLibraryId
            )
        );

    private static IQueryable<PlexMusicTrackComparison> QueryCurrentMusicTrackComparisons(
        IReaparrDbContext dbContext,
        IQueryable<PlexComparisonState> currentScopes
    ) =>
        dbContext.PlexMusicTrackComparisons.Where(comparison =>
            currentScopes.Any(scope =>
                scope.RemotePlexLibraryId == comparison.RemotePlexLibraryId
                && scope.OwnedPlexLibraryId == comparison.OwnedPlexLibraryId
            )
        );

    private static IQueryable<int> QueryOwnedArtistsWithPartialChildren(
        IReaparrDbContext dbContext,
        IQueryable<PlexComparisonState> currentScopes
    )
    {
        var artistComparisons = QueryCurrentMusicArtistComparisons(dbContext, currentScopes);
        var albumComparisons = QueryCurrentMusicAlbumComparisons(dbContext, currentScopes);
        var trackComparisons = QueryCurrentMusicTrackComparisons(dbContext, currentScopes);

        return artistComparisons
            .Where(comparison =>
                dbContext.PlexMusicArtists.Any(ownedArtist =>
                    ownedArtist.Id == comparison.OwnedPlexMediaId
                    && ownedArtist.PlexLibraryId == comparison.OwnedPlexLibraryId
                )
                && dbContext.PlexMusicAlbums.Any(remoteAlbum =>
                    remoteAlbum.PlexArtistId == comparison.RemotePlexMediaId
                    && (
                        !albumComparisons.Any(albumComparison =>
                            albumComparison.OwnedPlexLibraryId == comparison.OwnedPlexLibraryId
                            && albumComparison.RemotePlexLibraryId == comparison.RemotePlexLibraryId
                            && albumComparison.RemotePlexMediaId == remoteAlbum.Id
                            && dbContext.PlexMusicAlbums.Any(ownedAlbum =>
                                ownedAlbum.Id == albumComparison.OwnedPlexMediaId
                                && ownedAlbum.PlexLibraryId == comparison.OwnedPlexLibraryId
                                && ownedAlbum.PlexArtistId == comparison.OwnedPlexMediaId
                            )
                        )
                        || remoteAlbum.Tracks.Any(remoteTrack =>
                            !trackComparisons.Any(trackComparison =>
                                trackComparison.OwnedPlexLibraryId == comparison.OwnedPlexLibraryId
                                && trackComparison.RemotePlexLibraryId == comparison.RemotePlexLibraryId
                                && trackComparison.RemotePlexMediaId == remoteTrack.Id
                                && dbContext.PlexMusicTracks.Any(ownedTrack =>
                                    ownedTrack.Id == trackComparison.OwnedPlexMediaId
                                    && ownedTrack.PlexLibraryId == comparison.OwnedPlexLibraryId
                                    && ownedTrack.PlexAlbum != null
                                    && ownedTrack.PlexAlbum.PlexArtistId == comparison.OwnedPlexMediaId
                                    && albumComparisons.Any(albumComparison =>
                                        albumComparison.OwnedPlexLibraryId == comparison.OwnedPlexLibraryId
                                        && albumComparison.RemotePlexLibraryId == comparison.RemotePlexLibraryId
                                        && albumComparison.OwnedPlexMediaId == ownedTrack.PlexAlbumId
                                        && albumComparison.RemotePlexMediaId == remoteTrack.PlexAlbumId
                                    )
                                )
                            )
                        )
                    )
                )
            )
            .Select(comparison => comparison.OwnedPlexMediaId)
            .Distinct();
    }

    private static IQueryable<int> QueryOwnedArtistsWithHigherQuality(
        IReaparrDbContext dbContext,
        IQueryable<PlexComparisonState> currentScopes
    )
    {
        var trackComparisons = QueryCurrentMusicTrackComparisons(dbContext, currentScopes);
        var albumComparisons = QueryCurrentMusicAlbumComparisons(dbContext, currentScopes);

        return (
            from comparison in trackComparisons
            join ownedTrack in dbContext.PlexMusicTracks on comparison.OwnedPlexMediaId equals ownedTrack.Id
            join ownedAlbum in dbContext.PlexMusicAlbums on ownedTrack.PlexAlbumId equals ownedAlbum.Id
            where
                comparison.HitState == PlexMediaComparisonHitState.HigherQuality
                && ownedTrack.PlexLibraryId == comparison.OwnedPlexLibraryId
                && ownedAlbum.PlexLibraryId == comparison.OwnedPlexLibraryId
                && dbContext.PlexMusicTracks.Any(remoteTrack =>
                    remoteTrack.Id == comparison.RemotePlexMediaId
                    && albumComparisons.Any(albumComparison =>
                        albumComparison.OwnedPlexLibraryId == comparison.OwnedPlexLibraryId
                        && albumComparison.RemotePlexLibraryId == comparison.RemotePlexLibraryId
                        && albumComparison.OwnedPlexMediaId == ownedTrack.PlexAlbumId
                        && albumComparison.RemotePlexMediaId == remoteTrack.PlexAlbumId
                    )
                )
            select ownedAlbum.PlexArtistId
        ).Distinct();
    }

    private static IQueryable<int> QueryRemoteArtistsWithPartialChildren(
        IReaparrDbContext dbContext,
        IQueryable<PlexComparisonState> currentScopes
    )
    {
        var albumComparisons = QueryCurrentMusicAlbumComparisons(dbContext, currentScopes);
        var trackComparisons = QueryCurrentMusicTrackComparisons(dbContext, currentScopes);

        return dbContext
            .PlexMusicArtists.Where(artist =>
                artist.Albums.Count()
                    > albumComparisons
                        .Where(comparison =>
                            comparison.RemotePlexLibraryId == artist.PlexLibraryId
                            && dbContext.PlexMusicAlbums.Any(remoteAlbum =>
                                remoteAlbum.Id == comparison.RemotePlexMediaId && remoteAlbum.PlexArtistId == artist.Id
                            )
                        )
                        .Select(comparison => comparison.RemotePlexMediaId)
                        .Distinct()
                        .Count()
                || artist.Albums.SelectMany(album => album.Tracks).Count()
                    > trackComparisons
                        .Where(comparison =>
                            comparison.RemotePlexLibraryId == artist.PlexLibraryId
                            && dbContext.PlexMusicTracks.Any(remoteTrack =>
                                remoteTrack.Id == comparison.RemotePlexMediaId
                                && remoteTrack.PlexAlbum != null
                                && remoteTrack.PlexAlbum.PlexArtistId == artist.Id
                            )
                        )
                        .Select(comparison => comparison.RemotePlexMediaId)
                        .Distinct()
                        .Count()
            )
            .Select(artist => artist.Id);
    }

    private static IQueryable<int> QueryRemoteArtistsWithHigherQuality(
        IReaparrDbContext dbContext,
        IQueryable<PlexComparisonState> currentScopes
    )
    {
        var trackComparisons = QueryCurrentMusicTrackComparisons(dbContext, currentScopes);

        return (
            from comparison in trackComparisons
            join remoteTrack in dbContext.PlexMusicTracks on comparison.RemotePlexMediaId equals remoteTrack.Id
            where comparison.HitState == PlexMediaComparisonHitState.HigherQuality && remoteTrack.PlexAlbum != null
            select remoteTrack.PlexAlbum!.PlexArtistId
        ).Distinct();
    }

    public static IQueryable<MusicArtistComparisonStateRow> ProjectMusicArtistComparisonState(
        this IQueryable<PlexMusicArtist> source,
        PlexMediaComparisonState state
    ) => source.Select(artist => new MusicArtistComparisonStateRow { ArtistId = artist.Id, ComparisonState = state });

    public static IQueryable<MusicArtistComparisonCandidateRow> ProjectMusicArtistComparisonCandidates(
        this IQueryable<PlexMusicArtist> artists,
        IQueryable<MusicArtistComparisonStateRow> states
    ) =>
        artists.Join(
            states,
            artist => artist.Id,
            state => state.ArtistId,
            (artist, state) =>
                new MusicArtistComparisonCandidateRow
                {
                    Id = artist.Id,
                    SearchTitle = artist.SearchTitle,
                    SortIndex = artist.SortIndex,
                    Duration = artist.Duration,
                    MediaSize = artist.MediaSize,
                    AddedAt = artist.AddedAt,
                    UpdatedAt = artist.UpdatedAt,
                    AudioQuality = artist.Qualities.Select(quality => (int?)quality.AudioQuality).Max() ?? 0,
                    ComparisonId = (int)state.ComparisonState,
                }
        );

    public static IQueryable<MusicArtistComparisonCandidateRow> ProjectMusicArtistComparisonCandidates(
        this IQueryable<PlexMusicArtist> artists,
        PlexMediaComparisonState state
    ) => artists.ProjectMusicArtistComparisonCandidates(artists.ProjectMusicArtistComparisonState(state));
}
