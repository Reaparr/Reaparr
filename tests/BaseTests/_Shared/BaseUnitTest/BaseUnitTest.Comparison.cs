namespace Reaparr.BaseTests;

public partial class BaseUnitTest
{
    protected async Task SetOwnedOverrideAsync(int plexServerId, bool ownedOverride)
    {
        await IDbContext
            .PlexServers.Where(x => x.Id == plexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, ownedOverride), CancellationToken);
    }

    protected async Task SetLibraryUpdatedAtAsync(int plexLibraryId, DateTime updatedAt)
    {
        await IDbContext
            .PlexLibraries.Where(x => x.Id == plexLibraryId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.UpdatedAt, updatedAt), CancellationToken);
    }

    protected async Task SetLibraryContentChangedAtAsync(int plexLibraryId, long contentChangedAt)
    {
        await IDbContext
            .PlexLibraries.Where(x => x.Id == plexLibraryId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.ContentChangedAt, contentChangedAt), CancellationToken);
    }

    protected async Task<PlexLibrary> GetLibraryAsync(int plexLibraryId) =>
        await IDbContext.PlexLibraries.Where(x => x.Id == plexLibraryId).SingleAsync(CancellationToken);

    protected async Task<PlexMovie> GetLibraryMovieAsync(int plexLibraryId) =>
        await IDbContext.PlexMovies.Where(x => x.PlexLibraryId == plexLibraryId).SingleAsync(CancellationToken);

    protected async Task<PlexTvShow> GetLibraryTvShowAsync(int plexLibraryId) =>
        await IDbContext.PlexTvShows.Where(x => x.PlexLibraryId == plexLibraryId).SingleAsync(CancellationToken);

    protected async Task<List<PlexTvShowEpisode>> GetLibraryEpisodesAsync(int plexLibraryId) =>
        await IDbContext
            .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == plexLibraryId)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

    protected Task AddCurrentScopeAsync(PlexLibrary remoteLibrary, PlexLibrary ownedLibrary) =>
        AddCurrentScopeAsync(remoteLibrary, ownedLibrary, remoteLibrary.Type);

    protected async Task AddCurrentScopeAsync(
        PlexLibrary remoteLibrary,
        PlexLibrary ownedLibrary,
        PlexMediaType mediaType
    )
    {
        var dbContext = IDbContext;
        dbContext.PlexComparisonScopes.Add(
            new PlexComparisonState
            {
                Id = 0,
                RemotePlexLibraryId = remoteLibrary.Id,
                OwnedPlexLibraryId = ownedLibrary.Id,
                MediaType = mediaType,
                CompletedAt = DateTime.UtcNow,
            }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
    }

    protected static PlexMovieComparison CreateMovieComparison(
        int remotePlexLibraryId,
        int ownedPlexLibraryId,
        int remotePlexMediaId,
        int ownedPlexMediaId,
        PlexMediaComparisonHitState hitState,
        VideoQuality? ownedQuality = null
    ) =>
        new()
        {
            Id = 0,
            RemotePlexLibraryId = remotePlexLibraryId,
            OwnedPlexLibraryId = ownedPlexLibraryId,
            RemotePlexMediaId = remotePlexMediaId,
            OwnedPlexMediaId = ownedPlexMediaId,
            HitState = hitState,
            RemoteQuality = VideoQuality.FullHD,
            OwnedQuality = ownedQuality
                ?? (hitState == PlexMediaComparisonHitState.HigherQuality ? VideoQuality.HD : VideoQuality.FullHD),
            MatchType = PlexMediaComparisonMatchType.TmdbGuid,
            ComparedAt = DateTime.UtcNow,
        };

    protected static PlexTvShowComparison CreateTvShowComparison(
        int remotePlexLibraryId,
        int ownedPlexLibraryId,
        int remotePlexMediaId,
        int ownedPlexMediaId
    ) =>
        CreateTvShowComparison(
            remotePlexLibraryId,
            ownedPlexLibraryId,
            remotePlexMediaId,
            ownedPlexMediaId,
            PlexMediaComparisonHitState.Matched
        );

    protected static PlexTvShowComparison CreateTvShowComparison(
        int remotePlexLibraryId,
        int ownedPlexLibraryId,
        int remotePlexMediaId,
        int ownedPlexMediaId,
        PlexMediaComparisonHitState hitState,
        VideoQuality? ownedQuality = null
    ) =>
        new()
        {
            Id = 0,
            RemotePlexLibraryId = remotePlexLibraryId,
            OwnedPlexLibraryId = ownedPlexLibraryId,
            RemotePlexMediaId = remotePlexMediaId,
            OwnedPlexMediaId = ownedPlexMediaId,
            HitState = hitState,
            RemoteQuality = VideoQuality.FullHD,
            OwnedQuality = ownedQuality
                ?? (hitState == PlexMediaComparisonHitState.HigherQuality ? VideoQuality.HD : VideoQuality.FullHD),
            MatchType = PlexMediaComparisonMatchType.TmdbGuid,
            ComparedAt = DateTime.UtcNow,
        };

    protected static PlexSeasonComparison CreateSeasonComparison(
        int remotePlexLibraryId,
        int ownedPlexLibraryId,
        int remotePlexMediaId,
        int ownedPlexMediaId
    ) =>
        CreateSeasonComparison(
            remotePlexLibraryId,
            ownedPlexLibraryId,
            remotePlexMediaId,
            ownedPlexMediaId,
            PlexMediaComparisonHitState.Matched
        );

    protected static PlexSeasonComparison CreateSeasonComparison(
        int remotePlexLibraryId,
        int ownedPlexLibraryId,
        int remotePlexMediaId,
        int ownedPlexMediaId,
        PlexMediaComparisonHitState hitState,
        VideoQuality? ownedQuality = null
    ) =>
        new()
        {
            Id = 0,
            RemotePlexLibraryId = remotePlexLibraryId,
            OwnedPlexLibraryId = ownedPlexLibraryId,
            RemotePlexMediaId = remotePlexMediaId,
            OwnedPlexMediaId = ownedPlexMediaId,
            HitState = hitState,
            RemoteQuality = VideoQuality.FullHD,
            OwnedQuality = ownedQuality
                ?? (hitState == PlexMediaComparisonHitState.HigherQuality ? VideoQuality.HD : VideoQuality.FullHD),
            MatchType = PlexMediaComparisonMatchType.ParentAndChildNumbers,
            ComparedAt = DateTime.UtcNow,
        };

    protected static PlexEpisodeComparison CreateEpisodeComparison(
        int remotePlexLibraryId,
        int ownedPlexLibraryId,
        int remotePlexMediaId,
        int ownedPlexMediaId
    ) =>
        CreateEpisodeComparison(
            remotePlexLibraryId,
            ownedPlexLibraryId,
            remotePlexMediaId,
            ownedPlexMediaId,
            PlexMediaComparisonHitState.Matched
        );

    protected static PlexEpisodeComparison CreateEpisodeComparison(
        int remotePlexLibraryId,
        int ownedPlexLibraryId,
        int remotePlexMediaId,
        int ownedPlexMediaId,
        PlexMediaComparisonHitState hitState,
        VideoQuality? ownedQuality = null,
        PlexMediaComparisonMatchType matchType = PlexMediaComparisonMatchType.ParentAndChildNumbers
    ) =>
        new()
        {
            Id = 0,
            RemotePlexLibraryId = remotePlexLibraryId,
            OwnedPlexLibraryId = ownedPlexLibraryId,
            RemotePlexMediaId = remotePlexMediaId,
            OwnedPlexMediaId = ownedPlexMediaId,
            HitState = hitState,
            RemoteQuality = VideoQuality.FullHD,
            OwnedQuality = ownedQuality
                ?? (hitState == PlexMediaComparisonHitState.HigherQuality ? VideoQuality.HD : VideoQuality.FullHD),
            MatchType = matchType,
            ComparedAt = DateTime.UtcNow,
        };

    protected static PlexMediaSlimDTO CreateMovieItem(PlexMovie movie) =>
        new()
        {
            Id = movie.Id,
            PlexApiRatingKey = movie.PlexApiRatingKey,
            PlexApiMetaDataKey = movie.PlexApiMetaDataKey,
            Title = movie.Title,
            SearchTitle = movie.SearchTitle,
            SortIndex = movie.SortIndex,
            Year = movie.Year,
            Duration = movie.Duration,
            MediaSize = movie.MediaSize,
            ChildCount = movie.ChildCount,
            GrandChildCount = 0,
            AddedAt = movie.AddedAt,
            UpdatedAt = movie.UpdatedAt,
            PlexLibraryId = movie.PlexLibraryId,
            PlexServerId = movie.PlexServerId,
            Type = PlexMediaType.Movie,
            HasThumb = movie.HasThumb,
            Qualities = [],
        };

    protected static PlexMediaSlimDTO CreateTvShowItem(PlexTvShow tvShow) =>
        new()
        {
            Id = tvShow.Id,
            PlexApiRatingKey = tvShow.PlexApiRatingKey,
            PlexApiMetaDataKey = tvShow.PlexApiMetaDataKey,
            Title = tvShow.Title,
            SearchTitle = tvShow.SearchTitle,
            SortIndex = tvShow.SortIndex,
            Year = tvShow.Year,
            Duration = tvShow.Duration,
            MediaSize = tvShow.MediaSize,
            ChildCount = tvShow.ChildCount,
            GrandChildCount = tvShow.GrandChildCount,
            AddedAt = tvShow.AddedAt,
            UpdatedAt = tvShow.UpdatedAt,
            PlexLibraryId = tvShow.PlexLibraryId,
            PlexServerId = tvShow.PlexServerId,
            Type = PlexMediaType.TvShow,
            HasThumb = tvShow.HasThumb,
            Qualities = [],
        };
}
