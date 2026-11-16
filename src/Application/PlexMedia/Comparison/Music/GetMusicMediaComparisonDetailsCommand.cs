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
        var artist = await _dbContext.PlexMusicArtists.SingleOrDefaultAsync(x => x.Id == command.PlexMusicArtistId, ct);
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
            .PlexMusicAlbums.Where(x => x.PlexLibraryId == artist.PlexLibraryId && x.PlexArtistId == artist.Id)
            .OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.SortIndex)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var albumIds = albums.Select(x => x.Id).ToHashSet();
        var tracks = await _dbContext
            .PlexMusicTracks.Include(x => x.MediaDataList)
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
            .PlexMusicTracks.Include(x => x.MediaDataList)
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
            .PlexMusicAlbums.Where(x =>
                currentRemoteLibraryIds.Contains(x.PlexLibraryId) && remoteArtistIds.Contains(x.PlexArtistId)
            )
            .OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.SortIndex)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var albumIds = albums.Select(x => x.Id).ToHashSet();
        var tracks = await _dbContext
            .PlexMusicTracks.Include(x => x.MediaDataList)
            .Where(x => currentRemoteLibraryIds.Contains(x.PlexLibraryId) && albumIds.Contains(x.PlexAlbumId))
            .OrderBy(x => x.DiscNumber)
            .ThenBy(x => x.TrackNumber)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var albumHitIds = await (
            from comparison in _dbContext.PlexMusicAlbumComparisons
            join ownedAlbum in _dbContext.PlexMusicAlbums on comparison.OwnedPlexMediaId equals ownedAlbum.Id
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
            join ownedTrack in _dbContext.PlexMusicTracks on comparison.OwnedPlexMediaId equals ownedTrack.Id
            join ownedAlbum in _dbContext.PlexMusicAlbums on ownedTrack.PlexAlbumId equals ownedAlbum.Id
            where
                comparison.OwnedPlexLibraryId == artist.PlexLibraryId
                && currentRemoteLibraryIds.Contains(comparison.RemotePlexLibraryId)
                && trackIds.Contains(comparison.RemotePlexMediaId)
                && ownedAlbum.PlexArtistId == artist.Id
            select comparison
        ).ToListAsync(ct);

        var ownedTrackIds = hits.Select(x => x.OwnedPlexMediaId).ToHashSet();
        var ownedTracks = await _dbContext
            .PlexMusicTracks.Include(x => x.MediaDataList)
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
            var children = tracksByAlbum[album.Id].ToList();
            var albumRowId = ++rowId;
            var childRows = new List<ComparisonDetailsRow>(children.Count);
            foreach (var track in children)
            {
                var trackHits = trackHitLookup[track.Id].ToList();
                var hit = trackHits
                    .OrderByDescending(x => x.HitState == PlexMediaComparisonHitState.HigherQuality)
                    .ThenBy(x => x.OwnedPlexLibraryId)
                    .ThenBy(x => x.OwnedPlexMediaId)
                    .ThenBy(x => x.Id)
                    .FirstOrDefault();
                var ownedTrack = hit is null ? null : ownedTracks.GetValueOrDefault(hit.OwnedPlexMediaId);
                var remoteRepresentative = GetBestAudioFile(track);
                var ownedRepresentative = ownedTrack is null ? null : GetBestAudioFile(ownedTrack);
                var remoteFile = hit?.RemotePlexMediaDataId is { } remoteDataId
                    ? track.MediaDataList.SingleOrDefault(x => x.Id == remoteDataId) ?? remoteRepresentative
                    : remoteRepresentative;
                var ownedFile = hit?.OwnedPlexMediaDataId is { } ownedDataId
                    ? ownedTrack?.MediaDataList.SingleOrDefault(x => x.Id == ownedDataId) ?? ownedRepresentative
                    : ownedRepresentative;
                var trackState =
                    hit is null ? PlexMediaComparisonState.Missing
                    : hit.HitState == PlexMediaComparisonHitState.HigherQuality ? PlexMediaComparisonState.HigherQuality
                    : PlexMediaComparisonState.Owned;
                childRows.Add(
                    new ComparisonDetailsRow
                    {
                        RowId = ++rowId,
                        ParentRowId = albumRowId,
                        Level = 1,
                        PlexMediaId = track.Id,
                        Type = PlexMediaType.MusicTrack,
                        Title = track.Title,
                        ComparisonId = trackState.ToComparisonId(),
                        IsActionable =
                            trackState is PlexMediaComparisonState.Missing or PlexMediaComparisonState.HigherQuality,
                        RemoteQuality = VideoQuality.None,
                        OwnedQuality = VideoQuality.None,
                        RemoteAudioQuality = ResolveAudioQuality(
                            hit?.RemoteAudioQuality ?? AudioQuality.Unknown,
                            remoteFile
                        ),
                        OwnedAudioQuality = hit is null
                            ? AudioQuality.None
                            : ResolveAudioQuality(hit.OwnedAudioQuality, ownedFile),
                        RemotePlexMediaDataId = remoteFile?.Id ?? hit?.RemotePlexMediaDataId,
                        OwnedPlexMediaDataId = ownedFile?.Id ?? hit?.OwnedPlexMediaDataId,
                        RemoteLocation = remoteFile?.GetFileName ?? GetFirstFileName(track),
                        OwnedLocation =
                            ownedFile?.GetFileName
                            ?? (ownedTrack is null ? string.Empty : GetFirstFileName(ownedTrack)),
                        RemotePlexLibraryId = track.PlexLibraryId,
                        RemotePlexServerId = track.PlexServerId,
                    }
                );
            }

            var albumState =
                !albumHitIds.Contains(album.Id) ? PlexMediaComparisonState.Missing
                : childRows.Any(x => x.ComparisonId == PlexMediaComparisonState.Missing.ToComparisonId())
                    ? childRows.Any(x => x.ComparisonId == PlexMediaComparisonState.HigherQuality.ToComparisonId())
                            ? PlexMediaComparisonState.PartialAndHigherQuality
                        : PlexMediaComparisonState.Partial
                : childRows.Any(x => x.ComparisonId == PlexMediaComparisonState.HigherQuality.ToComparisonId())
                    ? PlexMediaComparisonState.HigherQuality
                : PlexMediaComparisonState.Owned;
            rows.Add(
                new ComparisonDetailsRow
                {
                    RowId = albumRowId,
                    Level = 0,
                    PlexMediaId = album.Id,
                    Type = PlexMediaType.MusicAlbum,
                    Title = album.Title,
                    ComparisonId = albumState.ToComparisonId(),
                    IsActionable =
                        albumState
                            is PlexMediaComparisonState.Missing
                                or PlexMediaComparisonState.Partial
                                or PlexMediaComparisonState.HigherQuality
                                or PlexMediaComparisonState.PartialAndHigherQuality,
                    RemoteQuality = VideoQuality.None,
                    OwnedQuality = VideoQuality.None,
                    RemoteAudioQuality = AudioQuality.None,
                    OwnedAudioQuality = AudioQuality.None,
                    RemotePlexLibraryId = album.PlexLibraryId,
                    RemotePlexServerId = album.PlexServerId,
                }
            );
            rows.AddRange(childRows);
        }

        return rows;
    }

    private static PlexMusicTrackMediaData? GetBestAudioFile(PlexMusicTrack track) =>
        track.MediaDataList.Count == 0 ? null : track.MediaDataList.ToList().GetBestAudioFile();

    private static AudioQuality ResolveAudioQuality(AudioQuality persistedQuality, PlexMusicTrackMediaData? file) =>
        persistedQuality is AudioQuality.None or AudioQuality.Unknown
            ? file?.ClassifyAudioQuality() ?? persistedQuality
            : persistedQuality;

    private static string GetFirstFileName(PlexMusicTrack track) =>
        track.MediaDataList.Select(x => x.GetFileName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
        ?? string.Empty;
}
