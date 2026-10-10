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
        {
            item.SetComparisonState(PlexMediaComparisonState.NotCompared);
        }

        if (items.Count == 0)
            return Result.Ok();

        var ownedLibraryExists = await _dbContext
            .PlexLibraries.WhereIsOwned()
            .AnyAsync(x =>
                x.Id == command.OwnedLibraryId
                && x.Type == PlexMediaType.MusicArtist
                && !x.Outdated,
                ct
            );

        if (!ownedLibraryExists)
            return Result.Ok();

        var currentRemoteLibraryIds = await _dbContext.GetCurrentRemoteLibraryIds(
            command.OwnedLibraryId,
            PlexMediaType.MusicArtist,
            ct
        );

        if (currentRemoteLibraryIds.Count == 0)
        {
            var remoteLibraryIds = await _dbContext
                .PlexLibraries.WhereIsNotOwned()
                .Where(x => x.Type == PlexMediaType.MusicArtist)
                .Select(x => x.Id)
                .ToListAsync(ct);

            var hasActiveJobs = await _scheduler.HasActiveJobs(
                remoteLibraryIds
                    .Select(x => PlexLibraryComparisonJob.GetJobKey(command.OwnedLibraryId, x)),
                ct
            );
            if (hasActiveJobs)
            {
                foreach (var item in items)
                {
                    item.SetComparisonState(PlexMediaComparisonState.Pending);
                }
            }

            return Result.Ok();
        }

        var artistIds = items.Where(x => x.Type == PlexMediaType.MusicArtist).Select(x => x.Id).ToHashSet();
        var albumIds = items.Where(x => x.Type == PlexMediaType.MusicAlbum).Select(x => x.Id).ToHashSet();

        var artistHits = await (
            from comparison in _dbContext.PlexMusicArtistComparisons
            join ownedArtist in _dbContext.PlexArtists on comparison.OwnedPlexMediaId equals ownedArtist.Id
            join remoteAlbum in _dbContext.PlexAlbums on comparison.RemotePlexMediaId equals remoteAlbum.PlexArtistId
            where
                comparison.OwnedPlexLibraryId == command.OwnedLibraryId
                && currentRemoteLibraryIds.Contains(comparison.RemotePlexLibraryId)
                && ownedArtist.PlexLibraryId == command.OwnedLibraryId
                && artistIds.Contains(ownedArtist.Id)
            select new
            {
                OwnedArtistId = ownedArtist.Id,
                comparison.RemotePlexLibraryId,
                RemoteAlbumId = remoteAlbum.Id,
                TrackCount = remoteAlbum.Tracks.Count,
            }
        ).ToListAsync(ct);

        var albumHits = await (
            from comparison in _dbContext.PlexMusicAlbumComparisons
            join ownedAlbum in _dbContext.PlexAlbums on comparison.OwnedPlexMediaId equals ownedAlbum.Id
            join ownedArtist in _dbContext.PlexArtists on ownedAlbum.PlexArtistId equals ownedArtist.Id
            join remoteAlbum in _dbContext.PlexAlbums on comparison.RemotePlexMediaId equals remoteAlbum.Id
            where
                comparison.OwnedPlexLibraryId == command.OwnedLibraryId
                && currentRemoteLibraryIds.Contains(comparison.RemotePlexLibraryId)
                && (
                    (artistIds.Contains(ownedArtist.Id) && ownedArtist.PlexLibraryId == command.OwnedLibraryId)
                    || (albumIds.Contains(ownedAlbum.Id) && ownedAlbum.PlexLibraryId == command.OwnedLibraryId)
                )
            select new
            {
                OwnedArtistId = ownedArtist.Id,
                OwnedAlbumId = ownedAlbum.Id,
                OwnedLibraryId = ownedAlbum.PlexLibraryId,
                comparison.RemotePlexLibraryId,
                RemoteAlbumId = remoteAlbum.Id,
                TrackCount = remoteAlbum.Tracks.Count,
            }
        ).ToListAsync(ct);

        var trackHits = await (
            from comparison in _dbContext.PlexMusicTrackComparisons
            join ownedTrack in _dbContext.PlexTracks on comparison.OwnedPlexMediaId equals ownedTrack.Id
            join ownedAlbum in _dbContext.PlexAlbums on ownedTrack.PlexAlbumId equals ownedAlbum.Id
            join ownedArtist in _dbContext.PlexArtists on ownedAlbum.PlexArtistId equals ownedArtist.Id
            join remoteTrack in _dbContext.PlexTracks on comparison.RemotePlexMediaId equals remoteTrack.Id
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
                )
            select new
            {
                OwnedArtistId = ownedArtist.Id,
                OwnedAlbumId = ownedAlbum.Id,
                comparison.RemotePlexLibraryId,
                RemoteAlbumId = remoteTrack.PlexAlbumId,
                RemoteTrackId = remoteTrack.Id,
            }
        ).ToListAsync(ct);

        var albumHitSet = albumHits
            .Select(x => (x.OwnedArtistId, x.RemotePlexLibraryId, x.RemoteAlbumId))
            .ToHashSet();

        var artistTrackHitLookup = trackHits
            .GroupBy(x => (x.OwnedArtistId, x.RemotePlexLibraryId, x.RemoteAlbumId))
            .ToDictionary(g => g.Key, g => g.Select(x => x.RemoteTrackId).Distinct().Count());

        var albumTrackHitLookup = trackHits
            .GroupBy(x => (x.OwnedAlbumId, x.RemotePlexLibraryId, x.RemoteAlbumId))
            .ToDictionary(g => g.Key, g => g.Select(x => x.RemoteTrackId).Distinct().Count());

        var partialArtistIds = artistHits
            .Where(x =>
                !albumHitSet.Contains((x.OwnedArtistId, x.RemotePlexLibraryId, x.RemoteAlbumId))
                || artistTrackHitLookup.GetValueOrDefault(
                    (x.OwnedArtistId, x.RemotePlexLibraryId, x.RemoteAlbumId)
                ) < x.TrackCount
            )
            .Select(x => x.OwnedArtistId)
            .ToHashSet();

        var partialAlbumIds = albumHits
            .Where(x =>
                albumIds.Contains(x.OwnedAlbumId)
                && x.OwnedLibraryId == command.OwnedLibraryId
                && albumTrackHitLookup.GetValueOrDefault(
                    (x.OwnedAlbumId, x.RemotePlexLibraryId, x.RemoteAlbumId)
                ) < x.TrackCount
            )
            .Select(x => x.OwnedAlbumId)
            .ToHashSet();

        foreach (var item in items)
        {
            var hasPartialMissingChildren = item.Type switch
            {
                PlexMediaType.MusicArtist => partialArtistIds.Contains(item.Id),
                PlexMediaType.MusicAlbum => partialAlbumIds.Contains(item.Id),
                _ => false,
            };

            item.SetComparisonState(
                hasPartialMissingChildren ? PlexMediaComparisonState.Partial : PlexMediaComparisonState.Owned
            );
        }

        return Result.Ok();
    }
}
