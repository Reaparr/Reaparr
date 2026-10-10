namespace Reaparr.Application;

public record GetMusicMediaComparisonDetailsCommand(int PlexMusicArtistId)
    : ICommand<Result<PlexMediaComparisonDetailsDTO>>;

public class GetMusicMediaComparisonDetailsCommandValidator : AbstractValidator<GetMusicMediaComparisonDetailsCommand>
{
    public GetMusicMediaComparisonDetailsCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.PlexMusicArtistId).GreaterThan(0);
    }
}

public class GetMusicMediaComparisonDetailsCommandHandler
    : ICommandHandler<GetMusicMediaComparisonDetailsCommand, Result<PlexMediaComparisonDetailsDTO>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public GetMusicMediaComparisonDetailsCommandHandler(IReaparrDbContext dbContext, ICommandExecutor commandExecutor)
    {
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<PlexMediaComparisonDetailsDTO>> ExecuteAsync(
        GetMusicMediaComparisonDetailsCommand command,
        CancellationToken ct
    )
    {
        var artist = await _dbContext.PlexArtists.SingleOrDefaultAsync(x => x.Id == command.PlexMusicArtistId, ct);
        if (artist is null)
            return ResultExtensions.EntityNotFound(nameof(PlexMusicArtist), command.PlexMusicArtistId).LogError();

        var items = new List<PlexMediaSlimDTO> { artist.ToSlimDTOMapper() };
        var projection = await _commandExecutor.Send(
            new ApplyComparisonStateCommand(items, PlexMediaType.MusicArtist, artist.PlexLibraryId),
            ct
        );
        if (projection.IsCancelled)
            return projection.ToResult<PlexMediaComparisonDetailsDTO>().LogWarning();
        if (projection.IsFailed)
            return projection.ToResult<PlexMediaComparisonDetailsDTO>().LogError();

        var state = items[0].ComparisonId.ToComparisonState();
        var rows = new List<ComparisonDetailsRow>();
        if (state is not PlexMediaComparisonState.NotCompared and not PlexMediaComparisonState.Pending)
        {
            var isOwned = await _dbContext.PlexLibraries.WhereIsOwned().AnyAsync(x => x.Id == artist.PlexLibraryId, ct);
            rows = isOwned ? await GetOwnedMusicRowsAsync(artist, ct) : await GetRemoteMusicRowsAsync(artist, ct);
        }

        return Result.Ok(
            new PlexMediaComparisonDetailsDTO
            {
                PlexMediaId = artist.Id,
                Type = PlexMediaType.MusicArtist,
                State = state,
                Rows = PlexMediaComparisonDetailsMapper.ToDtoRows(rows),
            }
        );
    }

    private async Task<List<ComparisonDetailsRow>> GetRemoteMusicRowsAsync(PlexMusicArtist artist, CancellationToken ct)
    {
        var currentOwnedLibraryIds = await _dbContext.GetCurrentOwnedLibraryIds(
            artist.PlexLibraryId,
            PlexMediaType.MusicArtist,
            ct
        );
        if (currentOwnedLibraryIds.Count == 0)
            return [];

        var albums = await _dbContext
            .PlexAlbums.Where(x => x.PlexLibraryId == artist.PlexLibraryId && x.PlexArtistId == artist.Id)
            .OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.SortIndex)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var albumIds = albums.Select(x => x.Id).ToHashSet();
        var tracks = await _dbContext
            .PlexTracks.Include(x => x.MediaDataList)
            .Where(x => x.PlexLibraryId == artist.PlexLibraryId && albumIds.Contains(x.PlexAlbumId))
            .OrderBy(x => x.DiscNumber)
            .ThenBy(x => x.TrackNumber)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var albumHitIds = await _dbContext
            .PlexMusicAlbumComparisons.Where(x =>
                x.RemotePlexLibraryId == artist.PlexLibraryId
                && currentOwnedLibraryIds.Contains(x.OwnedPlexLibraryId)
                && albumIds.Contains(x.RemotePlexMediaId)
            )
            .Select(x => x.RemotePlexMediaId)
            .ToHashSetAsync(ct);

        var trackIds = tracks.Select(x => x.Id).ToHashSet();
        var hits = await _dbContext
            .PlexMusicTrackComparisons.Where(x =>
                x.RemotePlexLibraryId == artist.PlexLibraryId
                && currentOwnedLibraryIds.Contains(x.OwnedPlexLibraryId)
                && trackIds.Contains(x.RemotePlexMediaId)
            )
            .ToListAsync(ct);

        var ownedTrackIds = hits.Select(x => x.OwnedPlexMediaId).ToHashSet();
        var ownedTracks = await _dbContext
            .PlexTracks.Include(x => x.MediaDataList)
            .Where(x => ownedTrackIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        return BuildRemoteMusicRows(albums, tracks, albumHitIds, hits, ownedTracks);
    }

    private async Task<List<ComparisonDetailsRow>> GetOwnedMusicRowsAsync(PlexMusicArtist artist, CancellationToken ct)
    {
        var currentRemoteLibraryIds = await _dbContext.GetCurrentRemoteLibraryIds(
            artist.PlexLibraryId,
            PlexMediaType.MusicArtist,
            ct
        );
        if (currentRemoteLibraryIds.Count == 0)
            return [];

        var remoteArtistIds = _dbContext
            .PlexMusicArtistComparisons.Where(x =>
                x.OwnedPlexLibraryId == artist.PlexLibraryId
                && x.OwnedPlexMediaId == artist.Id
                && currentRemoteLibraryIds.Contains(x.RemotePlexLibraryId)
            )
            .Select(x => x.RemotePlexMediaId);
        var albums = await _dbContext
            .PlexAlbums.Where(x =>
                currentRemoteLibraryIds.Contains(x.PlexLibraryId) && remoteArtistIds.Contains(x.PlexArtistId)
            )
            .OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.SortIndex)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var albumIds = albums.Select(x => x.Id).ToHashSet();
        var tracks = await _dbContext
            .PlexTracks.Include(x => x.MediaDataList)
            .Where(x => currentRemoteLibraryIds.Contains(x.PlexLibraryId) && albumIds.Contains(x.PlexAlbumId))
            .OrderBy(x => x.DiscNumber)
            .ThenBy(x => x.TrackNumber)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var albumHitIds = await (
            from comparison in _dbContext.PlexMusicAlbumComparisons
            join ownedAlbum in _dbContext.PlexAlbums on comparison.OwnedPlexMediaId equals ownedAlbum.Id
            where
                comparison.OwnedPlexLibraryId == artist.PlexLibraryId
                && currentRemoteLibraryIds.Contains(comparison.RemotePlexLibraryId)
                && albumIds.Contains(comparison.RemotePlexMediaId)
                && ownedAlbum.PlexArtistId == artist.Id
            select comparison.RemotePlexMediaId
        ).ToHashSetAsync(ct);

        var trackIds = tracks.Select(x => x.Id).ToHashSet();
        var hits = await (
            from comparison in _dbContext.PlexMusicTrackComparisons
            join ownedTrack in _dbContext.PlexTracks on comparison.OwnedPlexMediaId equals ownedTrack.Id
            join ownedAlbum in _dbContext.PlexAlbums on ownedTrack.PlexAlbumId equals ownedAlbum.Id
            where
                comparison.OwnedPlexLibraryId == artist.PlexLibraryId
                && currentRemoteLibraryIds.Contains(comparison.RemotePlexLibraryId)
                && trackIds.Contains(comparison.RemotePlexMediaId)
                && ownedAlbum.PlexArtistId == artist.Id
            select comparison
        ).ToListAsync(ct);

        var ownedTrackIds = hits.Select(x => x.OwnedPlexMediaId).ToHashSet();
        var ownedTracks = await _dbContext
            .PlexTracks.Include(x => x.MediaDataList)
            .Where(x => ownedTrackIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        return BuildRemoteMusicRows(albums, tracks, albumHitIds, hits, ownedTracks);
    }

    private static List<ComparisonDetailsRow> BuildRemoteMusicRows(
        List<PlexMusicAlbum> albums,
        List<PlexMusicTrack> tracks,
        HashSet<int> albumHitIds,
        List<PlexMusicTrackComparison> hits,
        Dictionary<int, PlexMusicTrack> ownedTracks
    )
    {
        var trackHitLookup = hits.ToLookup(x => x.RemotePlexMediaId);
        var tracksByAlbum = tracks.ToLookup(x => x.PlexAlbumId);
        var rows = new List<ComparisonDetailsRow>();
        var rowId = 0;

        foreach (var album in albums)
        {
            var children = tracksByAlbum[album.Id];
            var albumState = !albumHitIds.Contains(album.Id)
                ? PlexMediaComparisonState.Missing
                : children.Any(x => !trackHitLookup[x.Id].Any())
                    ? PlexMediaComparisonState.Partial
                    : PlexMediaComparisonState.Owned;
            var albumRowId = ++rowId;
            rows.Add(
                new ComparisonDetailsRow
                {
                    RowId = albumRowId,
                    Level = 0,
                    PlexMediaId = album.Id,
                    Type = PlexMediaType.MusicAlbum,
                    Title = album.Title,
                    ComparisonId = albumState.ToComparisonId(),
                    IsActionable = albumState is PlexMediaComparisonState.Missing or PlexMediaComparisonState.Partial,
                    RemoteQuality = VideoQuality.None,
                    OwnedQuality = VideoQuality.None,
                    RemotePlexLibraryId = album.PlexLibraryId,
                    RemotePlexServerId = album.PlexServerId,
                }
            );

            foreach (var track in children)
            {
                var hit = trackHitLookup[track.Id].MinBy(x => (x.OwnedPlexLibraryId, x.OwnedPlexMediaId));
                var ownedTrack = hit is null ? null : ownedTracks.GetValueOrDefault(hit.OwnedPlexMediaId);
                rows.Add(
                    new ComparisonDetailsRow
                    {
                        RowId = ++rowId,
                        ParentRowId = albumRowId,
                        Level = 1,
                        PlexMediaId = track.Id,
                        Type = PlexMediaType.MusicTrack,
                        Title = track.Title,
                        ComparisonId = (hit is null ? PlexMediaComparisonState.Missing : PlexMediaComparisonState.Owned)
                            .ToComparisonId(),
                        IsActionable = hit is null,
                        RemoteQuality = VideoQuality.None,
                        OwnedQuality = VideoQuality.None,
                        RemoteLocation = track
                            .MediaDataList.Select(x => x.GetFileName)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                            ?? string.Empty,
                        OwnedLocation = ownedTrack
                            ?.MediaDataList.Select(x => x.GetFileName)
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                            ?? string.Empty,
                        RemotePlexLibraryId = track.PlexLibraryId,
                        RemotePlexServerId = track.PlexServerId,
                    }
                );
            }
        }

        return rows;
    }
}
