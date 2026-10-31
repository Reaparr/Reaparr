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
        RuleFor(x => x.LibraryMetadata.PlexLibrary.Type).Equal(PlexMediaType.MusicArtist);
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
            _commandExecutor.Send(
                new GetLibraryMediaFromPlexApiCommand(plexLibrary, PlexMediaType.MusicAlbum),
                cancellationToken
            )
        );

        if (albumsResult.IsCancelledOrFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(plexLibraryId, albumsResult.ToResult());
            return albumsResult.ToResult().LogIfFailed();
        }

        var albumMetadataResult = await Result.Try(() =>
            _commandExecutor.Send(
                new InsertMediaMetaDataCommand(
                    new LibraryMetadata(plexLibrary) { Countries = albumsResult.Value.Countries }
                ),
                cancellationToken
            )
        );

        if (albumMetadataResult.IsCancelledOrFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(plexLibraryId, albumMetadataResult.ToResult());
            return albumMetadataResult.ToResult().LogIfFailed();
        }

        foreach (var (key, country) in albumMetadataResult.Value.PlexCountries)
            command.LibraryMetadata.PlexCountries[key] = country;

        var tracksResult = await Result.Try(() =>
            _commandExecutor.Send(
                new GetLibraryMediaFromPlexApiCommand(plexLibrary, PlexMediaType.MusicTrack),
                cancellationToken
            )
        );

        if (tracksResult.IsCancelledOrFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(plexLibraryId, tracksResult.ToResult());
            return tracksResult.ToResult().LogIfFailed();
        }

        var albums = albumsResult.Value.Library.Albums;
        var tracks = tracksResult.Value.Library.Tracks;
        var treeResult = BuildMusicTree(plexLibrary, albums, tracks);
        if (treeResult.IsFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(plexLibraryId, treeResult);
            return treeResult.LogError();
        }

        var syncResult = await Result.Try(() =>
            _commandExecutor.Send(
                new SyncPlexMusicCommand(command.LibraryMetadata, command.ForceMediaRefresh),
                cancellationToken
            )
        );

        if (syncResult.IsCancelledOrFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(plexLibraryId, syncResult.ToResult());
            return syncResult.ToResult().LogIfFailed();
        }

        if (command.ForceMediaRefresh || syncResult.Value.ChangedItemCount > 0)
        {
            var comparisonResult = await Result.Try(() =>
                _commandExecutor.Send(
                    new InvalidateLibraryComparisonJobsCommand([command.LibraryMetadata.PlexLibraryId]),
                    cancellationToken
                )
            );

            if (comparisonResult.IsCancelled || comparisonResult.IsFailed)
            {
                using var finalizationCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var finalizationToken = finalizationCancellation.Token;
                var optimizationAfterFailure = await Result.Try(() =>
                    _commandExecutor.Send(
                        new ScheduleOptimizeDatabaseJobCommand { ChangedItemCount = syncResult.Value.ChangedItemCount },
                        finalizationToken
                    )
                );
                optimizationAfterFailure.LogIfFailed();
                var rebuildAfterFailure = await Result.Try(() =>
                    _commandExecutor.Send(
                        new QueueMediaOverviewRebuildCommand(PlexMediaType.MusicArtist),
                        finalizationToken
                    )
                );
                rebuildAfterFailure.LogIfFailed();
                var progressAfterFailure = await Result.Try(async Task () =>
                    await _librarySyncProgressStore.UpdateErrorAsync(plexLibraryId, comparisonResult)
                );
                progressAfterFailure.LogIfFailed();
                return comparisonResult.LogIfFailed();
            }
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
                MediaType = PlexMediaType.MusicArtist,
                Received = plexLibrary.Music.Count,
                Total = plexLibrary.Music.Count,
                TimeRemaining = TimeSpan.Zero,
            }
        );
        await _librarySyncProgressStore.UpdateItemAsync(
            plexLibraryId,
            new LibraryProgressItem
            {
                MediaType = PlexMediaType.MusicAlbum,
                Received = plexLibrary.Albums.Count,
                Total = plexLibrary.Albums.Count,
                TimeRemaining = TimeSpan.Zero,
            }
        );
        await _librarySyncProgressStore.UpdateItemAsync(
            plexLibraryId,
            new LibraryProgressItem
            {
                MediaType = PlexMediaType.MusicTrack,
                Received = plexLibrary.Tracks.Count,
                Total = plexLibrary.Tracks.Count,
                TimeRemaining = TimeSpan.Zero,
            }
        );
        var rebuildResult = await _commandExecutor.Send(
            new QueueMediaOverviewRebuildCommand(PlexMediaType.MusicArtist),
            cancellationToken
        );
        rebuildResult.LogIfFailed();

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

    private static Result BuildMusicTree(
        PlexLibrary library,
        ICollection<PlexMusicAlbum> albums,
        ICollection<PlexMusicTrack> tracks
    )
    {
        var artistKeys = library.Music.Select(x => x.PlexApiRatingKey).ToHashSet();
        var missingAlbum = albums.FirstOrDefault(x => !artistKeys.Contains(x.ParentKey));
        if (missingAlbum is not null)
            return Result.Fail(
                "Album {PlexApiRatingKey} has an unknown artist {ParentKey}",
                missingAlbum.PlexApiRatingKey,
                missingAlbum.ParentKey
            );

        var albumKeys = albums.Select(x => x.PlexApiRatingKey).ToHashSet();
        var missingTrack = tracks.FirstOrDefault(x => !albumKeys.Contains(x.ParentKey));
        if (missingTrack is not null)
            return Result.Fail(
                "Track {PlexApiRatingKey} has an unknown album {ParentKey}",
                missingTrack.PlexApiRatingKey,
                missingTrack.ParentKey
            );

        var albumsByArtist = albums.GroupBy(x => x.ParentKey).ToDictionary(x => x.Key, x => x.ToList());
        var tracksByAlbum = tracks.GroupBy(x => x.ParentKey).ToDictionary(x => x.Key, x => x.ToList());

        foreach (var artist in library.Music)
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
                    album.Duration = album.Tracks.Sum(x => x.Duration);
                    album.MediaSize = album.Tracks.Sum(x => x.MediaSize);
                    artist.Albums.Add(album);
                }
            }

            artist.ChildCount = artist.Albums.Count;
            artist.Duration = artist.Albums.Sum(x => x.Duration);
            artist.MediaSize = artist.Albums.Sum(x => x.MediaSize);
        }

        return Result.Ok();
    }
}
