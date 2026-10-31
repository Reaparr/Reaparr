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
            item.SetComparisonState(PlexMediaComparisonState.NotCompared);

        if (items.Count == 0)
            return Result.Ok();

        var remoteLibraryExists = await _dbContext
            .PlexLibraries.WhereIsNotOwned()
            .AnyAsync(x => x.Id == command.RemoteLibraryId && x.Type == PlexMediaType.MusicArtist && !x.Outdated, ct);

        if (!remoteLibraryExists)
            return Result.Ok();

        var currentOwnedLibraryIds = await _dbContext.GetCurrentOwnedLibraryIds(
            command.RemoteLibraryId,
            PlexMediaType.MusicArtist,
            ct
        );

        if (currentOwnedLibraryIds.Count == 0)
        {
            var fallbackState = await _dbContext.GetUncomparedComparisonStateAsync(
                _scheduler,
                command.RemoteLibraryId,
                isOwned: false,
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
            .ProjectRemoteMusicArtistComparisonStates(
                _dbContext.PlexMusicArtists.Where(x =>
                    x.PlexLibraryId == command.RemoteLibraryId && artistIds.Contains(x.Id)
                )
            )
            .ToDictionaryAsync(x => x.ArtistId, x => x.ComparisonState, ct);

        var albumHits = await (
            from comparison in _dbContext.PlexMusicAlbumComparisons
            join album in _dbContext.PlexMusicAlbums on comparison.RemotePlexMediaId equals album.Id
            where
                comparison.RemotePlexLibraryId == command.RemoteLibraryId
                && currentOwnedLibraryIds.Contains(comparison.OwnedPlexLibraryId)
                && (artistIds.Contains(album.PlexArtistId) || albumIds.Contains(album.Id))
            select new { comparison.RemotePlexMediaId }
        ).ToListAsync(ct);

        var albumHitIdSet = albumHits.Select(x => x.RemotePlexMediaId).ToHashSet();

        var trackHits = await (
            from comparison in _dbContext.PlexMusicTrackComparisons
            join track in _dbContext.PlexMusicTracks on comparison.RemotePlexMediaId equals track.Id
            join album in _dbContext.PlexMusicAlbums on track.PlexAlbumId equals album.Id
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
                track.PlexAlbumId,
                comparison.RemotePlexMediaId,
                comparison.HitState,
            }
        ).ToListAsync(ct);

        var trackHitIdSet = trackHits.Select(x => x.RemotePlexMediaId).ToHashSet();
        var trackHitCountByAlbum = trackHits
            .GroupBy(x => x.PlexAlbumId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RemotePlexMediaId).Distinct().Count());
        var higherTrackIdSet = trackHits
            .Where(x => x.HitState == PlexMediaComparisonHitState.HigherQuality)
            .Select(x => x.RemotePlexMediaId)
            .ToHashSet();
        var higherTrackIdsByAlbum = trackHits
            .Where(x => x.HitState == PlexMediaComparisonHitState.HigherQuality)
            .GroupBy(x => x.PlexAlbumId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RemotePlexMediaId).Distinct().ToHashSet());

        var albumTrackCountLookup = await _dbContext
            .PlexMusicAlbums.Where(x => x.PlexLibraryId == command.RemoteLibraryId && albumIds.Contains(x.Id))
            .Select(x => new { x.Id, TrackCount = x.Tracks.Count })
            .ToDictionaryAsync(x => x.Id, x => x.TrackCount, ct);

        foreach (var item in items)
        {
            switch (item.Type)
            {
                case PlexMediaType.MusicArtist:
                    item.SetComparisonState(
                        artistStateLookup.GetValueOrDefault(item.Id, PlexMediaComparisonState.Missing)
                    );
                    break;

                case PlexMediaType.MusicAlbum:
                    if (!albumHitIdSet.Contains(item.Id) || !albumTrackCountLookup.TryGetValue(item.Id, out var trackCount))
                    {
                        item.SetComparisonState(PlexMediaComparisonState.Missing);
                        break;
                    }

                    trackHitCountByAlbum.TryGetValue(item.Id, out var matchedAlbumTrackCount);
                    var albumHasPartialChildren = matchedAlbumTrackCount < trackCount;
                    var albumHasHigherQuality = higherTrackIdsByAlbum.ContainsKey(item.Id);
                    item.SetComparisonState(GetState(albumHasPartialChildren, albumHasHigherQuality));
                    break;

                case PlexMediaType.MusicTrack:
                    item.SetComparisonState(
                        !trackHitIdSet.Contains(item.Id)
                            ? PlexMediaComparisonState.Missing
                            : higherTrackIdSet.Contains(item.Id)
                                ? PlexMediaComparisonState.HigherQuality
                                : PlexMediaComparisonState.Owned
                    );
                    break;

                default:
                    item.SetComparisonState(PlexMediaComparisonState.Missing);
                    break;
            }
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
