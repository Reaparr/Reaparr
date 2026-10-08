namespace Reaparr.Application;

public record GenerateDownloadTaskPhotoImagesCommand(CreateDownloadTasksRequest Request)
    : ICommand<Result<DownloadTaskCreationReport>>;

public class GenerateDownloadTaskPhotoImagesCommandValidator : AbstractValidator<GenerateDownloadTaskPhotoImagesCommand>
{
    public GenerateDownloadTaskPhotoImagesCommandValidator()
    {
        RuleFor(x => x.Request).NotNull().DependentRules(() =>
        {
            RuleFor(x => x.Request.DownloadMedias).NotEmpty();
            RuleForEach(x => x.Request.DownloadMedias).NotNull().SetValidator(new DownloadMediaDTOValidator());
        });
    }
}

public class GenerateDownloadTaskPhotoImagesCommandHandler
    : ICommandHandler<GenerateDownloadTaskPhotoImagesCommand, Result<DownloadTaskCreationReport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public GenerateDownloadTaskPhotoImagesCommandHandler(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<GenerateDownloadTaskPhotoImagesCommandHandler>();
        _dbContext = dbContext;
    }

    public async Task<Result<DownloadTaskCreationReport>> ExecuteAsync(
        GenerateDownloadTaskPhotoImagesCommand command,
        CancellationToken cancellationToken
    )
    {
        var request = command.Request;
        var selections = request.DownloadMedias.Where(x => x.Type == PlexMediaType.PhotoImage).ToList().MergeAndGroupList();
        if (selections.Count == 0)
            return ResultExtensions.IsEmpty(nameof(selections)).LogWarning();
        if (request.Integration is not null)
            return ResultExtensions.Create400BadRequestResult("Photo downloads do not support Sonarr or Radarr integrations.").LogError();

        _log.Here().Debug("Processing {PhotoImageCount} photo image download tasks", selections.Sum(x => x.MediaIds.Count));

        var images = new List<PlexPhotoImage>();
        foreach (var selection in selections)
        {
            var library = await _dbContext.PlexLibraries.GetAsync(selection.PlexLibraryId, cancellationToken);
            if (library is null || library.PlexServerId != selection.PlexServerId || library.Type != PlexMediaType.PhotoAlbum)
                return ResultExtensions.Create400BadRequestResult("The selected photo library does not match the requested server or media type.").LogError();
            var ids = selection.MediaIds.Distinct().ToList();
            var selectedImages = await _dbContext.PlexPhotoImages.Include(x => x.PlexPhotoAlbum).Include(x => x.MediaDataList)
                .Where(x => ids.Contains(x.Id) && x.PlexLibraryId == selection.PlexLibraryId && x.PlexServerId == selection.PlexServerId)
                .ToListAsync(cancellationToken);
            if (selectedImages.Count != ids.Count)
                return ResultExtensions.Create400BadRequestResult("Every selected photo must belong to the requested library and server.").LogError();
            images.AddRange(selectedImages);
        }

        var downloadRootPath = (await _dbContext.GetDownloadFolder(null)).DirectoryPath;
        var albums = new List<DownloadTaskPhotoAlbum>();
        var files = new List<DownloadTaskPhotoImageFile>();
        var createdImages = 0;
        foreach (var image in images.DistinctBy(x => x.Id))
        {
            var selection = selections.First(x => x.PlexServerId == image.PlexServerId && x.PlexLibraryId == image.PlexLibraryId && x.MediaIds.Contains(image.Id));
            var selector = selection.Qualities.FirstOrDefault(x => x.MediaId == image.Id);
            var quality = image.MediaDataList.FirstOrDefault(x => x.Id == selector?.DataId) ?? image.MediaDataList.PickMediaQuality();
            if (quality is null)
            {
                _log.Here().Warning("Photo {Title} (ID: {Id}) has no suitable media data", image.Title, image.Id);
                continue;
            }
            var album = image.PlexPhotoAlbum!;
            var albumTask = albums.FirstOrDefault(x => x.PlexServerId == album.PlexServerId && x.PlexLibraryId == album.PlexLibraryId && x.PlexApiRatingKey == album.PlexApiRatingKey);
            if (albumTask is null)
            {
                albumTask = await _dbContext.DownloadTaskPhotoAlbums.AsTracking().WhereIntegrationOwnershipMatches(null)
                    .Include(x => x.Children).ThenInclude(x => x.Children)
                    .SingleOrDefaultAsync(x => x.PlexServerId == album.PlexServerId && x.PlexLibraryId == album.PlexLibraryId && x.PlexApiRatingKey == album.PlexApiRatingKey, cancellationToken);
                if (albumTask is null)
                {
                    albumTask = album.MapToDownloadTask(null);
                    _dbContext.DownloadTaskPhotoAlbums.Add(albumTask);
                }
                albums.Add(albumTask);
            }
            var imageTask = albumTask.Children.FirstOrDefault(x => x.PlexApiRatingKey == image.PlexApiRatingKey);
            if (imageTask is null)
            {
                imageTask = image.MapToDownloadTask(albumTask, null);
                albumTask.Children.Add(imageTask);
                _dbContext.DownloadTaskPhotoImages.Add(imageTask);
            }
            var imageFiles = image.MediaDataList.Where(x => x.PlexApiMediaId == quality.PlexApiMediaId)
                .Where(x => !imageTask.Children.Any(y => y.PlexApiMediaId == x.PlexApiMediaId && y.PlexApiPartId == x.PlexApiPartId))
                .Select(x => x.MapToDownloadTask(imageTask, image, album, request, downloadRootPath, selection.KeepCompletedInDownloadFolder))
                .ToList();
            if (imageFiles.Count == 0)
                continue;
            foreach (var file in imageFiles)
                imageTask.Children.Add(file);
            _dbContext.DownloadTaskPhotoImageFiles.AddRange(imageFiles);
            files.AddRange(imageFiles);
            createdImages++;
            imageTask.DownloadStatus = DownloadTaskActions.Aggregate(imageTask.Children.Select(x => x.DownloadStatus).ToList());
            albumTask.DownloadStatus = DownloadTaskActions.Aggregate(albumTask.Children.Select(x => x.DownloadStatus).ToList());
        }
        var saveResult = await Result.Try(() => _dbContext.SaveChangesAsync(cancellationToken));
        if (saveResult.IsFailed)
            return saveResult.ToResult().LogIfFailed();
        var logs = files.Select(file => new DownloadTaskPhotoImageFileLog
        {
            Status = DownloadStatus.Queued,
            LogLevel = NotificationLevel.Information,
            Message = $"DownloadTask {file.FileName} was queued for downloading",
            DownloadTaskFileId = file.Id,
            DownloadTaskPhotoId = file.ParentId,
            DownloadTaskPhotoAlbumId = file.Parent!.ParentId,
            CreatedAt = DateTime.UtcNow,
        }).ToList();
        var logsResult = await Result.Try(() => _dbContext.BulkInsertAsync(logs, cancellationToken: cancellationToken));
        if (logsResult.IsFailed)
            return logsResult.LogIfFailed();
        return Result.Ok(new DownloadTaskCreationReport { PhotoImages = createdImages });
    }
}
