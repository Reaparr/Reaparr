namespace Reaparr.Application;

public record GenerateDownloadTaskMusicAlbumsCommand(CreateDownloadTasksRequest Request)
    : ICommand<Result<DownloadTaskCreationReport>>;

public class GenerateDownloadTaskMusicAlbumsCommandValidator
    : AbstractValidator<GenerateDownloadTaskMusicAlbumsCommand>
{
    public GenerateDownloadTaskMusicAlbumsCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(x => x.Request.DownloadMedias).NotNull().NotEmpty();
                RuleForEach(x => x.Request.DownloadMedias).SetValidator(new DownloadMediaDTOValidator());
            });
    }
}

public class GenerateDownloadTaskMusicAlbumsCommandHandler
    : ICommandHandler<GenerateDownloadTaskMusicAlbumsCommand, Result<DownloadTaskCreationReport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public GenerateDownloadTaskMusicAlbumsCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor
    )
    {
        _log = log.ForContext<GenerateDownloadTaskMusicAlbumsCommandHandler>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<DownloadTaskCreationReport>> ExecuteAsync(
        GenerateDownloadTaskMusicAlbumsCommand command,
        CancellationToken cancellationToken
    )
    {
        var request = command.Request;
        var albumSelections = request.DownloadMedias.MergeAndGroupList().FindAll(x => x.Type == PlexMediaType.MusicAlbum);
        if (albumSelections.Count == 0)
            return ResultExtensions.IsEmpty(nameof(albumSelections)).LogWarning();
        if (request.Integration is not null)
            return ResultExtensions
                .Create400BadRequestResult("Music downloads do not support Sonarr or Radarr integrations.")
                .LogError();
        _log.Here().Debug("Creating {AlbumCount} Music album download tasks", albumSelections.Sum(x => x.MediaIds.Count));
        var artistTasks = new Dictionary<(int ServerId, int LibraryId, int RatingKey), DownloadTaskMusicArtist>();

        var createdAlbums = 0;
        var trackSelections = new List<DownloadMediaDTO>();
        foreach (var selection in albumSelections)
        {
            var library = await _dbContext.PlexLibraries.GetAsync(selection.PlexLibraryId, cancellationToken);
            if (
                library is null
                || library.PlexServerId != selection.PlexServerId
                || library.Type != PlexMediaType.MusicArtist
            )
                return ResultExtensions
                    .Create400BadRequestResult(
                        "The selected Music library does not match the requested server or media type."
                    )
                    .LogError();

            var albumIds = selection.MediaIds.Distinct().ToList();
            var albums = await _dbContext
                .PlexAlbums.Include(x => x.PlexArtist)
                .Include(x => x.Tracks)
                .Where(x =>
                    albumIds.Contains(x.Id)
                    && x.PlexLibraryId == selection.PlexLibraryId
                    && x.PlexServerId == selection.PlexServerId
                )
                .ToListAsync(cancellationToken);
            if (albums.Count != albumIds.Count || albums.Any(x => x.PlexArtist is null))
                return ResultExtensions
                    .Create400BadRequestResult(
                        "Every selected Music album must belong to the requested library and server."
                    )
                    .LogError();

            foreach (var album in albums)
            {
                var artist = album.PlexArtist!;
                var artistKey = (artist.PlexServerId, artist.PlexLibraryId, artist.PlexApiRatingKey);
                if (!artistTasks.TryGetValue(artistKey, out var artistTask))
                {
                    artistTask = await _dbContext
                        .DownloadTaskMusicArtists.AsTracking()
                        .WhereIntegrationOwnershipMatches(null)
                        .Include(x => x.Children)
                        .SingleOrDefaultAsync(
                            x =>
                                x.PlexServerId == artist.PlexServerId
                                && x.PlexLibraryId == artist.PlexLibraryId
                                && x.PlexApiRatingKey == artist.PlexApiRatingKey,
                            cancellationToken
                        );
                    if (artistTask is null)
                    {
                        artistTask = artist.MapToDownloadTask(null);
                        _dbContext.DownloadTaskMusicArtists.Add(artistTask);
                    }
                    artistTasks.Add(artistKey, artistTask);
                }
                var albumTask = artistTask.Children.SingleOrDefault(x =>
                    x.PlexApiRatingKey == album.PlexApiRatingKey
                );
                if (albumTask is null)
                {
                    albumTask = album.MapToDownloadTask(artistTask, null);
                    artistTask.Children.Add(albumTask);
                    _dbContext.DownloadTaskMusicAlbums.Add(albumTask);
                    createdAlbums++;
                }

                foreach (var track in album.Tracks)
                {
                    var directTrack = request.DownloadMedias.FirstOrDefault(x =>
                        x.Type == PlexMediaType.MusicTrack
                        && x.PlexServerId == track.PlexServerId
                        && x.PlexLibraryId == track.PlexLibraryId
                        && x.MediaIds.Contains(track.Id)
                    );
                    trackSelections.Add(
                        new DownloadMediaDTO
                        {
                            Type = PlexMediaType.MusicTrack,
                            PlexServerId = track.PlexServerId,
                            PlexLibraryId = track.PlexLibraryId,
                            MediaIds = [track.Id],
                            Qualities = directTrack?.Qualities.Count > 0 ? directTrack.Qualities : selection.Qualities,
                            KeepCompletedInDownloadFolder =
                                directTrack?.KeepCompletedInDownloadFolder
                                ?? selection.KeepCompletedInDownloadFolder,
                        }
                    );
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (trackSelections.Count == 0)
            return Result.Ok(new DownloadTaskCreationReport { MusicAlbums = createdAlbums });

        var tracksResult = await _commandExecutor.Send(
            new GenerateDownloadTaskMusicTracksCommand(
                new CreateDownloadTasksRequest(
                    trackSelections,
                    request.DestinationFolderPathId,
                    request.CustomDestinationFolderPath,
                    request.Integration
                )
            ),
            cancellationToken
        );
        if (tracksResult.IsFailed)
            return tracksResult.LogIfFailed();

        return Result.Ok(new DownloadTaskCreationReport { MusicAlbums = createdAlbums } + tracksResult.Value);
    }
}
