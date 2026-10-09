namespace Reaparr.Application;

public record GenerateDownloadTaskMusicArtistsCommand : ICommand<Result<DownloadTaskCreationReport>>
{
    public GenerateDownloadTaskMusicArtistsCommand(CreateDownloadTasksRequest request) => Request = request;

    public GenerateDownloadTaskMusicArtistsCommand(List<DownloadMediaDTO> downloadMediaDtos) =>
        Request = new CreateDownloadTasksRequest(downloadMediaDtos);

    public CreateDownloadTasksRequest Request { get; }
}

public class GenerateDownloadTaskMusicArtistsCommandValidator
    : AbstractValidator<GenerateDownloadTaskMusicArtistsCommand>
{
    public GenerateDownloadTaskMusicArtistsCommandValidator()
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

public class GenerateDownloadTaskMusicArtistsCommandHandler
    : ICommandHandler<GenerateDownloadTaskMusicArtistsCommand, Result<DownloadTaskCreationReport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public GenerateDownloadTaskMusicArtistsCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor
    )
    {
        _log = log.ForContext<GenerateDownloadTaskMusicArtistsCommandHandler>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<DownloadTaskCreationReport>> ExecuteAsync(
        GenerateDownloadTaskMusicArtistsCommand command,
        CancellationToken cancellationToken
    )
    {
        var request = command.Request;
        var downloadMedias = request.DownloadMedias.MergeAndGroupList();
        var artistSelections = downloadMedias.FindAll(x => x.Type == PlexMediaType.MusicArtist);
        if (artistSelections.Count == 0)
            return ResultExtensions.IsEmpty(nameof(artistSelections)).LogWarning();
        if (request.Integration is not null)
            return ResultExtensions
                .Create400BadRequestResult("Music downloads do not support Sonarr or Radarr integrations.")
                .LogError();

        _log.Here().Debug("Creating {ArtistCount} Music artist download tasks", artistSelections.Sum(x => x.MediaIds.Count));
        var createdArtists = 0;
        var albumSelections = new List<DownloadMediaDTO>();
        foreach (var selection in artistSelections)
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

            var artistIds = selection.MediaIds.Distinct().ToList();
            var artists = await _dbContext
                .PlexArtists.Include(x => x.Albums)
                    .ThenInclude(x => x.Tracks)
                .Where(x =>
                    artistIds.Contains(x.Id)
                    && x.PlexLibraryId == selection.PlexLibraryId
                    && x.PlexServerId == selection.PlexServerId
                )
                .ToListAsync(cancellationToken);
            if (artists.Count != artistIds.Count)
                return ResultExtensions
                    .Create400BadRequestResult(
                        "Every selected Music artist must belong to the requested library and server."
                    )
                    .LogError();

            foreach (var artist in artists)
            {
                var existing = await _dbContext
                    .DownloadTaskMusicArtists.AsTracking()
                    .WhereIntegrationOwnershipMatches(null)
                    .AnyAsync(
                        x =>
                            x.PlexServerId == artist.PlexServerId
                            && x.PlexLibraryId == artist.PlexLibraryId
                            && x.PlexApiRatingKey == artist.PlexApiRatingKey,
                        cancellationToken
                    );
                if (!existing)
                {
                    _dbContext.DownloadTaskMusicArtists.Add(artist.MapToDownloadTask(null));
                    createdArtists++;
                }

                foreach (var album in artist.Albums)
                {
                    var directAlbum = downloadMedias.FirstOrDefault(x =>
                        x.Type == PlexMediaType.MusicAlbum
                        && x.PlexServerId == album.PlexServerId
                        && x.PlexLibraryId == album.PlexLibraryId
                        && x.MediaIds.Contains(album.Id)
                    );
                    albumSelections.Add(
                        new DownloadMediaDTO
                        {
                            Type = PlexMediaType.MusicAlbum,
                            PlexServerId = album.PlexServerId,
                            PlexLibraryId = album.PlexLibraryId,
                            MediaIds = [album.Id],
                            Qualities = directAlbum?.Qualities.Count > 0 ? directAlbum.Qualities : selection.Qualities,
                            KeepCompletedInDownloadFolder =
                                directAlbum?.KeepCompletedInDownloadFolder
                                ?? selection.KeepCompletedInDownloadFolder,
                        }
                    );
                }
                var trackIds = artist.Albums.SelectMany(x => x.Tracks).Select(x => x.Id).ToHashSet();
                albumSelections.AddRange(
                    downloadMedias
                        .Where(x =>
                            x.Type == PlexMediaType.MusicTrack
                            && x.PlexServerId == artist.PlexServerId
                            && x.PlexLibraryId == artist.PlexLibraryId
                            && x.MediaIds.Any(trackIds.Contains)
                        )
                        .Select(x => x with
                        {
                            MediaIds = x.MediaIds.Where(trackIds.Contains).ToList(),
                        })
                );
            }
        }

        if (createdArtists > 0)
            await _dbContext.SaveChangesAsync(cancellationToken);
        if (albumSelections.Count == 0)
            return Result.Ok(new DownloadTaskCreationReport { MusicArtists = createdArtists });

        var albumsResult = await _commandExecutor.Send(
            new GenerateDownloadTaskMusicAlbumsCommand(
                new CreateDownloadTasksRequest(
                    albumSelections,
                    request.DestinationFolderPathId,
                    request.CustomDestinationFolderPath,
                    request.Integration
                )
            ),
            cancellationToken
        );
        if (albumsResult.IsFailed)
            return albumsResult.LogIfFailed();

        return Result.Ok(new DownloadTaskCreationReport { MusicArtists = createdArtists } + albumsResult.Value);
    }
}
