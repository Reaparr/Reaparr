namespace Reaparr.Application;

public record GenerateDownloadTaskPhotoAlbumsCommand(CreateDownloadTasksRequest Request)
    : ICommand<Result<DownloadTaskCreationReport>>;

public class GenerateDownloadTaskPhotoAlbumsCommandValidator : AbstractValidator<GenerateDownloadTaskPhotoAlbumsCommand>
{
    public GenerateDownloadTaskPhotoAlbumsCommandValidator()
    {
        RuleFor(x => x.Request).NotNull().DependentRules(() =>
        {
            RuleFor(x => x.Request.DownloadMedias).NotEmpty();
            RuleForEach(x => x.Request.DownloadMedias).NotNull().SetValidator(new DownloadMediaDTOValidator());
        });
    }
}

public class GenerateDownloadTaskPhotoAlbumsCommandHandler
    : ICommandHandler<GenerateDownloadTaskPhotoAlbumsCommand, Result<DownloadTaskCreationReport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _command;

    public GenerateDownloadTaskPhotoAlbumsCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor command
    )
    {
        _log = log.ForContext<GenerateDownloadTaskPhotoAlbumsCommandHandler>();
        _dbContext = dbContext;
        _command = command;
    }

    public async Task<Result<DownloadTaskCreationReport>> ExecuteAsync(
        GenerateDownloadTaskPhotoAlbumsCommand command,
        CancellationToken cancellationToken
    )
    {
        var request = command.Request;
        var downloadMedias = request.DownloadMedias.MergeAndGroupList();
        var selections = downloadMedias.FindAll(x => x.Type == PlexMediaType.PhotoAlbum);
        if (selections.Count == 0)
            return ResultExtensions.IsEmpty(nameof(selections)).LogWarning();
        if (request.Integration is not null)
            return ResultExtensions.Create400BadRequestResult("Photo downloads do not support Sonarr or Radarr integrations.").LogError();

        _log.Here().Debug("Creating {PhotoAlbumCount} photo album download tasks", selections.Sum(x => x.MediaIds.Count));

        var albums = new List<PlexPhotoAlbum>();
        foreach (var selection in selections)
        {
            var library = await _dbContext.PlexLibraries.GetAsync(selection.PlexLibraryId, cancellationToken);
            if (library is null || library.PlexServerId != selection.PlexServerId || library.Type != PlexMediaType.PhotoAlbum)
                return ResultExtensions.Create400BadRequestResult("The selected photo library does not match the requested server or media type.").LogError();
            var ids = selection.MediaIds.Distinct().ToList();
            var selectedAlbums = await _dbContext.PlexPhotoAlbums.Include(x => x.Photos)
                .Where(x => ids.Contains(x.Id) && x.PlexLibraryId == selection.PlexLibraryId && x.PlexServerId == selection.PlexServerId)
                .ToListAsync(cancellationToken);
            if (selectedAlbums.Count != ids.Count)
                return ResultExtensions.Create400BadRequestResult("Every selected photo album must belong to the requested library and server.").LogError();
            albums.AddRange(selectedAlbums);
        }

        var images = new List<DownloadMediaDTO>();
        var createdAlbums = 0;
        foreach (var album in albums.DistinctBy(x => x.Id))
        {
            var existing = await _dbContext.DownloadTaskPhotoAlbums.WhereIntegrationOwnershipMatches(null)
                .SingleOrDefaultAsync(x => x.PlexServerId == album.PlexServerId && x.PlexLibraryId == album.PlexLibraryId && x.PlexApiRatingKey == album.PlexApiRatingKey, cancellationToken);
            if (existing is null)
            {
                _dbContext.DownloadTaskPhotoAlbums.Add(album.MapToDownloadTask(null));
                createdAlbums++;
            }
            var inherited = selections.First(x => x.PlexServerId == album.PlexServerId && x.PlexLibraryId == album.PlexLibraryId && x.MediaIds.Contains(album.Id));
            foreach (var image in album.Photos)
            {
                var direct = downloadMedias.FirstOrDefault(x => x.Type == PlexMediaType.PhotoImage && x.PlexServerId == image.PlexServerId && x.PlexLibraryId == image.PlexLibraryId && x.MediaIds.Contains(image.Id));
                var qualities = direct?.Qualities.Where(x => x.MediaId == image.Id).ToList() ?? [];
                if (qualities.Count == 0)
                    qualities = inherited.Qualities.Where(x => x.MediaId == image.Id).ToList();
                images.Add(new DownloadMediaDTO
                {
                    Type = PlexMediaType.PhotoImage,
                    PlexServerId = image.PlexServerId,
                    PlexLibraryId = image.PlexLibraryId,
                    MediaIds = [image.Id],
                    Qualities = qualities,
                    KeepCompletedInDownloadFolder = (direct ?? inherited).KeepCompletedInDownloadFolder,
                });
            }
        }
        var saveResult = await Result.Try(() => _dbContext.SaveChangesAsync(cancellationToken));
        if (saveResult.IsFailed)
            return saveResult.ToResult().LogIfFailed();
        var report = new DownloadTaskCreationReport { PhotoAlbums = createdAlbums };
        if (images.Count == 0)
            return Result.Ok(report);
        var imagesResult = await _command.Send(new GenerateDownloadTaskPhotoImagesCommand(request with { DownloadMedias = images }), cancellationToken);
        if (imagesResult.IsFailed)
            return imagesResult.LogIfFailed();
        return Result.Ok(report + imagesResult.Value);
    }
}
