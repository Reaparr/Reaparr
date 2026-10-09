namespace Reaparr.Application;

public record ApplyRemoteMusicComparisonStateCommand(List<PlexMediaSlimDTO> Items, int RemoteLibraryId)
    : ICommand<Result>;

public class ApplyRemoteMusicComparisonStateCommandValidator : AbstractValidator<ApplyRemoteMusicComparisonStateCommand>
{
    public ApplyRemoteMusicComparisonStateCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Items).NotNull();
        RuleFor(x => x.RemoteLibraryId).GreaterThan(0);
    }
}

public class ApplyRemoteMusicComparisonStateCommandHandler
    : ICommandHandler<ApplyRemoteMusicComparisonStateCommand, Result>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly IScheduler _scheduler;

    public ApplyRemoteMusicComparisonStateCommandHandler(IReaparrDbContext dbContext, IScheduler scheduler)
    {
        _dbContext = dbContext;
        _scheduler = scheduler;
    }

    public async Task<Result> ExecuteAsync(ApplyRemoteMusicComparisonStateCommand command, CancellationToken ct)
    {
        var items = command.Items;
        foreach (var item in items)
        {
            item.SetComparisonState(PlexMediaComparisonState.NotCompared);
        }

        if (items.Count == 0)
            return Result.Ok();

        var remoteLibraryExists = await _dbContext
            .PlexLibraries.WhereIsNotOwned()
            .AnyAsync(
                x =>
                    x.Id == command.RemoteLibraryId
                    && x.Type == PlexMediaType.MusicArtist
                    && !x.Outdated,
                ct
            );

        if (!remoteLibraryExists)
            return Result.Ok();

        var currentOwnedLibraryIds = await _dbContext.GetCurrentOwnedLibraryIds(
            command.RemoteLibraryId,
            PlexMediaType.MusicArtist,
            ct
        );

        if (currentOwnedLibraryIds.Count == 0)
        {
            var ownedLibraryIds = await _dbContext
                .PlexLibraries.WhereIsOwned()
                .Where(x => x.Type == PlexMediaType.MusicArtist)
                .Select(x => x.Id)
                .ToHashSetAsync(ct);

            var hasActiveJobs = await _scheduler.HasActiveJobs(
                ownedLibraryIds.Select(x => PlexLibraryComparisonJob.GetJobKey(x, command.RemoteLibraryId)),
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
        var trackIds = items.Where(x => x.Type == PlexMediaType.MusicTrack).Select(x => x.Id).ToHashSet();

        var artistHits = await _dbContext
            .PlexMusicArtistComparisons.Where(x =>
                x.RemotePlexLibraryId == command.RemoteLibraryId
                && currentOwnedLibraryIds.Contains(x.OwnedPlexLibraryId)
                && artistIds.Contains(x.RemotePlexMediaId)
            )
            .Select(x => x.RemotePlexMediaId)
            .ToHashSetAsync(ct);

        var albumHits = await (
            from comparison in _dbContext.PlexMusicAlbumComparisons
            join album in _dbContext.PlexAlbums on comparison.RemotePlexMediaId equals album.Id
            where
                comparison.RemotePlexLibraryId == command.RemoteLibraryId
                && currentOwnedLibraryIds.Contains(comparison.OwnedPlexLibraryId)
                && (artistIds.Contains(album.PlexArtistId) || albumIds.Contains(album.Id))
            select new
            {
                album.PlexArtistId,
                comparison.RemotePlexMediaId,
            }
        ).ToListAsync(ct);

        var albumHitIdSet = albumHits.Select(x => x.RemotePlexMediaId).ToHashSet();
        var albumHitCountByArtist = albumHits
            .GroupBy(x => x.PlexArtistId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RemotePlexMediaId).Distinct().Count());

        var trackHits = await (
            from comparison in _dbContext.PlexMusicTrackComparisons
            join track in _dbContext.PlexTracks on comparison.RemotePlexMediaId equals track.Id
            join album in _dbContext.PlexAlbums on track.PlexAlbumId equals album.Id
            where
                comparison.RemotePlexLibraryId == command.RemoteLibraryId
                && currentOwnedLibraryIds.Contains(comparison.OwnedPlexLibraryId)
                && (
                    artistIds.Contains(album.PlexArtistId)
                    || albumIds.Contains(track.PlexAlbumId)
                    || trackIds.Contains(track.Id)
                )
            select new
            {
                album.PlexArtistId,
                track.PlexAlbumId,
                comparison.RemotePlexMediaId,
            }
        ).ToListAsync(ct);

        var trackHitIdSet = trackHits.Select(x => x.RemotePlexMediaId).ToHashSet();
        var trackHitCountByArtist = trackHits
            .GroupBy(x => x.PlexArtistId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RemotePlexMediaId).Distinct().Count());
        var trackHitCountByAlbum = trackHits
            .GroupBy(x => x.PlexAlbumId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RemotePlexMediaId).Distinct().Count());

        var artistChildCountLookup = await _dbContext
            .PlexArtists.Where(x =>
                x.PlexLibraryId == command.RemoteLibraryId
                && artistIds.Contains(x.Id)
            )
            .Select(x => new
            {
                x.Id,
                AlbumCount = x.Albums.Count,
                TrackCount = x.Albums.SelectMany(album => album.Tracks).Count(),
            })
            .ToDictionaryAsync(x => x.Id, ct);

        var albumTrackCountLookup = await _dbContext
            .PlexAlbums.Where(x =>
                x.PlexLibraryId == command.RemoteLibraryId
                && albumIds.Contains(x.Id)
            )
            .Select(x => new
            {
                x.Id,
                TrackCount = x.Tracks.Count,
            })
            .ToDictionaryAsync(x => x.Id, x => x.TrackCount, ct);

        foreach (var item in items)
        {
            switch (item.Type)
            {
                case PlexMediaType.MusicArtist:
                    if (!artistHits.Contains(item.Id) || !artistChildCountLookup.TryGetValue(item.Id, out var artist))
                    {
                        item.SetComparisonState(PlexMediaComparisonState.Missing);
                        break;
                    }

                    albumHitCountByArtist.TryGetValue(item.Id, out var matchedAlbumCount);
                    trackHitCountByArtist.TryGetValue(item.Id, out var matchedArtistTrackCount);

                    item.SetComparisonState(
                        matchedAlbumCount < artist.AlbumCount || matchedArtistTrackCount < artist.TrackCount
                            ? PlexMediaComparisonState.Partial
                            : PlexMediaComparisonState.Owned
                    );
                    break;

                case PlexMediaType.MusicAlbum:
                    if (!albumHitIdSet.Contains(item.Id) || !albumTrackCountLookup.TryGetValue(item.Id, out var trackCount))
                    {
                        item.SetComparisonState(PlexMediaComparisonState.Missing);
                        break;
                    }

                    trackHitCountByAlbum.TryGetValue(item.Id, out var matchedAlbumTrackCount);

                    item.SetComparisonState(
                        matchedAlbumTrackCount < trackCount
                            ? PlexMediaComparisonState.Partial
                            : PlexMediaComparisonState.Owned
                    );
                    break;

                case PlexMediaType.MusicTrack:
                    item.SetComparisonState(
                        trackHitIdSet.Contains(item.Id)
                            ? PlexMediaComparisonState.Owned
                            : PlexMediaComparisonState.Missing
                    );
                    break;

                default:
                    item.SetComparisonState(PlexMediaComparisonState.Missing);
                    break;
            }
        }

        return Result.Ok();
    }
}
