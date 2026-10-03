namespace Reaparr.Application;

public record RefreshPlexMusicLibraryCommand(
    InsertMediaMetaDataCommandResponse LibraryMetadata,
    bool ForceMediaRefresh = false
) : ICommand<Result<PlexLibrary>>;

public class RefreshPlexMusicLibraryCommandValidator : AbstractValidator<RefreshPlexMusicLibraryCommand>
{
    public RefreshPlexMusicLibraryCommandValidator()
    {
        RuleFor(x => x.LibraryMetadata).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.LibraryMetadata.PlexLibrary.Type).Equal(PlexMediaType.Music);
    }
}

public class RefreshPlexMusicLibraryCommandHandler
    : ICommandHandler<RefreshPlexMusicLibraryCommand, Result<PlexLibrary>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IReaparrDbContext _dbContext;
    private readonly ILibrarySyncProgressStore _librarySyncProgressStore;

    public RefreshPlexMusicLibraryCommandHandler(
        ILogger log,
        ICommandExecutor commandExecutor,
        IReaparrDbContext dbContext,
        ILibrarySyncProgressStore librarySyncProgressStore
    )
    {
        _log = log.ForContext<RefreshPlexMusicLibraryCommandHandler>();
        _commandExecutor = commandExecutor;
        _dbContext = dbContext;
        _librarySyncProgressStore = librarySyncProgressStore;
    }

    public async Task<Result<PlexLibrary>> ExecuteAsync(
        RefreshPlexMusicLibraryCommand command,
        CancellationToken cancellationToken
    )
    {
        var plexLibrary = command.LibraryMetadata.PlexLibrary;
        var plexLibraryId = command.LibraryMetadata.PlexLibraryId;

        var albumsResult = await Result.Try(() =>
            _commandExecutor.Send(new GetAllMediaByTypeFromPlexApiCommand(plexLibrary, PlexMediaType.Album), cancellationToken)
        );

        if (albumsResult.IsCancelled)
            return albumsResult.ToResult().LogWarning();

        if (albumsResult.IsFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(
                plexLibraryId,
                albumsResult.ToResult(),
                cancellationToken
            );
            return albumsResult.ToResult().LogError();
        }

        var artistsByKey = plexLibrary.Artists.ToDictionary(x => x.PlexApiRatingKey);
        var albums = new List<PlexMusicAlbum>(albumsResult.Value.Count);
        var albumSortIndices = new Dictionary<int, int>();
        foreach (var source in albumsResult.Value.OrderByNatural(x => x.SortTitle))
        {
            if (source.Type != PlexMediaType.Album
                || !int.TryParse(source.ParentRatingKey, out var parentKey)
                || !artistsByKey.TryGetValue(parentKey, out var artist))
            {
                var error = Result.Fail("Plex album does not belong to a retrieved artist");
                await _librarySyncProgressStore.UpdateErrorAsync(plexLibraryId, error, cancellationToken);
                return error.LogError();
            }

            albumSortIndices.TryGetValue(parentKey, out var sortIndex);
            source.SortIndex = albumSortIndices[parentKey] = sortIndex + 1;
            albums.Add(source.ToPlexMusicAlbum(artist, plexLibrary));
        }

        plexLibrary.Albums.Clear();
        foreach (var album in albums)
            plexLibrary.Albums.Add(album);

        var tracksResult = await Result.Try(() =>
            _commandExecutor.Send(new GetAllMediaByTypeFromPlexApiCommand(plexLibrary, PlexMediaType.Song), cancellationToken)
        );

        if (tracksResult.IsCancelled)
            return tracksResult.ToResult().LogWarning();

        if (tracksResult.IsFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(
                plexLibraryId,
                tracksResult.ToResult(),
                cancellationToken
            );
            return tracksResult.ToResult().LogError();
        }

        var albumsByKey = albums.ToDictionary(x => x.PlexApiRatingKey);
        var tracks = new List<PlexMusicTrack>(tracksResult.Value.Count);
        var trackSortIndices = new Dictionary<int, int>();
        foreach (var source in tracksResult.Value.OrderByNatural(x => x.SortTitle))
        {
            if (source.Type != PlexMediaType.Song
                || !int.TryParse(source.ParentRatingKey, out var parentKey)
                || !albumsByKey.TryGetValue(parentKey, out var album))
            {
                var error = Result.Fail("Plex track does not belong to a retrieved album");
                await _librarySyncProgressStore.UpdateErrorAsync(plexLibraryId, error, cancellationToken);
                return error.LogError();
            }

            trackSortIndices.TryGetValue(parentKey, out var sortIndex);
            source.SortIndex = trackSortIndices[parentKey] = sortIndex + 1;
            tracks.Add(source.ToPlexMusicTrack(album, plexLibrary));
        }

        BuildMusicTree(plexLibrary, albums, tracks);

        var syncResult = await Result.Try(() =>
            _commandExecutor.Send(
                new SyncPlexMusicCommand(command.LibraryMetadata, command.ForceMediaRefresh),
                cancellationToken
            )
        );

        if (syncResult.IsCancelled)
            return syncResult.ToResult().LogWarning();

        if (syncResult.IsFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(plexLibraryId, syncResult.ToResult(), cancellationToken);
            return syncResult.ToResult().LogError();
        }
        var optimizationResult = await _commandExecutor.Send(
            new ScheduleOptimizeDatabaseJobCommand { ChangedItemCount = syncResult.Value.ChangedItemCount },
            cancellationToken
        );
        optimizationResult.LogIfFailed();

        await _librarySyncProgressStore.UpdateItemAsync(
            plexLibraryId,
            new LibraryProgressItem
            {
                MediaType = PlexMediaType.Artist,
                Received = plexLibrary.Artists.Count,
                Total = plexLibrary.Artists.Count,
                TimeRemaining = TimeSpan.Zero,
            },
            cancellationToken
        );
        await _librarySyncProgressStore.UpdateItemAsync(
            plexLibraryId,
            new LibraryProgressItem
            {
                MediaType = PlexMediaType.Album,
                Received = plexLibrary.Albums.Count,
                Total = plexLibrary.Albums.Count,
                TimeRemaining = TimeSpan.Zero,
            },
            cancellationToken
        );
        await _librarySyncProgressStore.UpdateItemAsync(
            plexLibraryId,
            new LibraryProgressItem
            {
                MediaType = PlexMediaType.Song,
                Received = plexLibrary.Tracks.Count,
                Total = plexLibrary.Tracks.Count,
                TimeRemaining = TimeSpan.Zero,
            },
            cancellationToken
        );

        _log.Here()
            .Information(
                "Successfully refreshed library {PlexLibraryName} with id: {PlexLibraryId}",
                plexLibrary.Title,
                plexLibraryId
            );

        var plexLibraryDb = await _dbContext.PlexLibraries.GetAsync(plexLibraryId, cancellationToken);
        return plexLibraryDb is null
            ? ResultExtensions.EntityNotFound(nameof(PlexLibrary), plexLibraryId)
            : Result.Ok(plexLibraryDb);
    }

    private static void BuildMusicTree(
        PlexLibrary library,
        ICollection<PlexMusicAlbum> albums,
        ICollection<PlexMusicTrack> tracks
    )
    {
        var albumsByArtist = albums.GroupBy(x => x.PlexArtist!.PlexApiRatingKey)
            .ToDictionary(x => x.Key, x => x.ToList());
        var tracksByAlbum = tracks.GroupBy(x => x.PlexAlbum!.PlexApiRatingKey)
            .ToDictionary(x => x.Key, x => x.ToList());

        foreach (var artist in library.Artists)
        {
            artist.Albums.Clear();
            if (albumsByArtist.TryGetValue(artist.PlexApiRatingKey, out var artistAlbums))
            {
                foreach (var album in artistAlbums)
                {
                    album.PlexArtist = artist;
                    album.Tracks.Clear();

                    if (tracksByAlbum.TryGetValue(album.PlexApiRatingKey, out var albumTracks))
                    {
                        foreach (var track in albumTracks)
                        {
                            track.PlexAlbum = album;
                            album.Tracks.Add(track);
                        }
                    }

                    album.ChildCount = album.Tracks.Count;
                    album.TrackCount = album.Tracks.Count;
                    album.Duration = album.Tracks.Sum(x => x.Duration);
                    album.MediaSize = album.Tracks.Sum(x => x.MediaSize);
                    artist.Albums.Add(album);
                }
            }

            artist.ChildCount = artist.Albums.Count;
            artist.Duration = artist.Albums.Sum(x => x.Duration);
            artist.MediaSize = artist.Albums.Sum(x => x.MediaSize);
        }

        library.Tracks.Clear();
        foreach (var track in tracks)
            library.Tracks.Add(track);
    }

}
