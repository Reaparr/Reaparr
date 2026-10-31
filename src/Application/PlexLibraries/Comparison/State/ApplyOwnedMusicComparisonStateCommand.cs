namespace Reaparr.Application;

public record ApplyOwnedMusicComparisonStateCommand(List<PlexMediaSlimDTO> Items, int OwnedLibraryId)
    : ICommand<Result>;

public class ApplyOwnedMusicComparisonStateCommandValidator : AbstractValidator<ApplyOwnedMusicComparisonStateCommand>
{
    public ApplyOwnedMusicComparisonStateCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Items).NotNull();
        RuleFor(x => x.OwnedLibraryId).GreaterThan(0);
    }
}

public class ApplyOwnedMusicComparisonStateCommandHandler
    : ICommandHandler<ApplyOwnedMusicComparisonStateCommand, Result>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly IScheduler _scheduler;

    public ApplyOwnedMusicComparisonStateCommandHandler(IReaparrDbContext dbContext, IScheduler scheduler)
    {
        _dbContext = dbContext;
        _scheduler = scheduler;
    }

    public async Task<Result> ExecuteAsync(ApplyOwnedMusicComparisonStateCommand command, CancellationToken ct)
    {
        var items = command.Items;
        foreach (var item in items)
            item.SetComparisonState(PlexMediaComparisonState.NotCompared);

        if (items.Count == 0)
            return Result.Ok();

        var ownedLibraryExists = await _dbContext
            .PlexLibraries.WhereIsOwned()
            .AnyAsync(x => x.Id == command.OwnedLibraryId && x.Type == PlexMediaType.MusicArtist && !x.Outdated, ct);

        if (!ownedLibraryExists)
            return Result.Ok();

        var currentRemoteLibraryIds = await _dbContext.GetCurrentRemoteLibraryIds(
            command.OwnedLibraryId,
            PlexMediaType.MusicArtist,
            ct
        );

        if (currentRemoteLibraryIds.Count == 0)
        {
            var fallbackState = await _dbContext.GetUncomparedComparisonStateAsync(
                _scheduler,
                command.OwnedLibraryId,
                isOwned: true,
                PlexMediaType.MusicArtist,
                ct
            );
            if (fallbackState == PlexMediaComparisonState.Pending)
            {
                foreach (var item in items)
                    item.SetComparisonState(PlexMediaComparisonState.Pending);
            }
            return Result.Ok();
        }

        var artistIds = items.Where(x => x.Type == PlexMediaType.MusicArtist).Select(x => x.Id).ToHashSet();
        var albumIds = items.Where(x => x.Type == PlexMediaType.MusicAlbum).Select(x => x.Id).ToHashSet();
        var trackIds = items.Where(x => x.Type == PlexMediaType.MusicTrack).Select(x => x.Id).ToHashSet();
        var artistStateLookup = await _dbContext
            .ProjectOwnedMusicArtistComparisonStates(
                _dbContext.PlexMusicArtists.Where(x =>
                    x.PlexLibraryId == command.OwnedLibraryId && artistIds.Contains(x.Id)
                )
            )
            .ToDictionaryAsync(x => x.ArtistId, x => x.ComparisonState, ct);
        var albumHits = await (
            from comparison in _dbContext.PlexMusicAlbumComparisons
            join ownedAlbum in _dbContext.PlexMusicAlbums on comparison.OwnedPlexMediaId equals ownedAlbum.Id
            join ownedArtist in _dbContext.PlexMusicArtists on ownedAlbum.PlexArtistId equals ownedArtist.Id
            join remoteAlbum in _dbContext.PlexMusicAlbums on comparison.RemotePlexMediaId equals remoteAlbum.Id
            where
                comparison.OwnedPlexLibraryId == command.OwnedLibraryId
                && currentRemoteLibraryIds.Contains(comparison.RemotePlexLibraryId)
                && (
                    (artistIds.Contains(ownedArtist.Id) && ownedArtist.PlexLibraryId == command.OwnedLibraryId)
                    || (albumIds.Contains(ownedAlbum.Id) && ownedAlbum.PlexLibraryId == command.OwnedLibraryId)
                )
            select new
            {
                OwnedAlbumId = ownedAlbum.Id,
                OwnedLibraryId = ownedAlbum.PlexLibraryId,
                comparison.RemotePlexLibraryId,
                RemoteAlbumId = remoteAlbum.Id,
                TrackCount = remoteAlbum.Tracks.Count,
            }
        ).ToListAsync(ct);

        var trackHits = await (
            from comparison in _dbContext.PlexMusicTrackComparisons
            join ownedTrack in _dbContext.PlexMusicTracks on comparison.OwnedPlexMediaId equals ownedTrack.Id
            join ownedAlbum in _dbContext.PlexMusicAlbums on ownedTrack.PlexAlbumId equals ownedAlbum.Id
            join ownedArtist in _dbContext.PlexMusicArtists on ownedAlbum.PlexArtistId equals ownedArtist.Id
            join remoteTrack in _dbContext.PlexMusicTracks on comparison.RemotePlexMediaId equals remoteTrack.Id
            join albumComparison in _dbContext.PlexMusicAlbumComparisons
                on new
                {
                    comparison.OwnedPlexLibraryId,
                    comparison.RemotePlexLibraryId,
                    OwnedPlexMediaId = ownedAlbum.Id,
                    RemotePlexMediaId = remoteTrack.PlexAlbumId,
                } equals new
                {
                    albumComparison.OwnedPlexLibraryId,
                    albumComparison.RemotePlexLibraryId,
                    albumComparison.OwnedPlexMediaId,
                    albumComparison.RemotePlexMediaId,
                }
            where
                comparison.OwnedPlexLibraryId == command.OwnedLibraryId
                && currentRemoteLibraryIds.Contains(comparison.RemotePlexLibraryId)
                && (
                    (artistIds.Contains(ownedArtist.Id) && ownedArtist.PlexLibraryId == command.OwnedLibraryId)
                    || (albumIds.Contains(ownedAlbum.Id) && ownedAlbum.PlexLibraryId == command.OwnedLibraryId)
                    || (trackIds.Contains(ownedTrack.Id) && ownedTrack.PlexLibraryId == command.OwnedLibraryId)
                )
            select new
            {
                OwnedAlbumId = ownedAlbum.Id,
                OwnedTrackId = ownedTrack.Id,
                comparison.RemotePlexLibraryId,
                RemoteAlbumId = remoteTrack.PlexAlbumId,
                RemoteTrackId = remoteTrack.Id,
                comparison.HitState,
            }
        ).ToListAsync(ct);
        var albumTrackHitLookup = trackHits
            .GroupBy(x => (x.OwnedAlbumId, x.RemotePlexLibraryId, x.RemoteAlbumId))
            .ToDictionary(g => g.Key, g => g.Select(x => x.RemoteTrackId).Distinct().Count());
        var partialAlbumIds = albumHits
            .Where(x =>
                albumIds.Contains(x.OwnedAlbumId)
                && x.OwnedLibraryId == command.OwnedLibraryId
                && albumTrackHitLookup.GetValueOrDefault((x.OwnedAlbumId, x.RemotePlexLibraryId, x.RemoteAlbumId))
                    < x.TrackCount
            )
            .Select(x => x.OwnedAlbumId)
            .ToHashSet();

        var higherAlbumIds = trackHits
            .Where(x => x.HitState == PlexMediaComparisonHitState.HigherQuality)
            .Select(x => x.OwnedAlbumId)
            .ToHashSet();
        var higherTrackIds = trackHits
            .Where(x => x.HitState == PlexMediaComparisonHitState.HigherQuality)
            .Select(x => x.OwnedTrackId)
            .ToHashSet();

        foreach (var item in items)
        {
            var hasPartialMissingChildren = item.Type switch
            {
                PlexMediaType.MusicAlbum => partialAlbumIds.Contains(item.Id),
                _ => false,
            };
            var hasHigherQuality = item.Type switch
            {
                PlexMediaType.MusicArtist => artistStateLookup.GetValueOrDefault(
                    item.Id,
                    PlexMediaComparisonState.Owned
                )
                    is PlexMediaComparisonState.HigherQuality
                        or PlexMediaComparisonState.PartialAndHigherQuality,
                PlexMediaType.MusicAlbum => higherAlbumIds.Contains(item.Id),
                PlexMediaType.MusicTrack => higherTrackIds.Contains(item.Id),
                _ => false,
            };

            if (item.Type == PlexMediaType.MusicArtist)
            {
                item.SetComparisonState(
                    artistStateLookup.GetValueOrDefault(item.Id, PlexMediaComparisonState.Owned)
                );
                continue;
            }

            item.SetComparisonState(GetState(hasPartialMissingChildren, hasHigherQuality));
        }

        return Result.Ok();
    }

    private static PlexMediaComparisonState GetState(bool hasPartialChildren, bool hasHigherQuality) =>
        (hasPartialChildren, hasHigherQuality) switch
        {
            (true, true) => PlexMediaComparisonState.PartialAndHigherQuality,
            (true, false) => PlexMediaComparisonState.Partial,
            (false, true) => PlexMediaComparisonState.HigherQuality,
            _ => PlexMediaComparisonState.Owned,
        };
}
