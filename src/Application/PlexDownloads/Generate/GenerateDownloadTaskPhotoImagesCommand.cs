namespace Reaparr.Application;

public record GenerateDownloadTaskPhotoImagesCommand : ICommand<Result<DownloadTaskCreationReport>>
{
    public GenerateDownloadTaskPhotoImagesCommand(CreateDownloadTasksRequest request) => Request = request;

    public GenerateDownloadTaskPhotoImagesCommand(List<DownloadMediaDTO> downloadMediaDtos)
        : this(new CreateDownloadTasksRequest(downloadMediaDtos)) { }

    public CreateDownloadTasksRequest Request { get; }
}

public class GenerateDownloadTaskPhotoImagesCommandValidator : AbstractValidator<GenerateDownloadTaskPhotoImagesCommand>
{
    public GenerateDownloadTaskPhotoImagesCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(x => x.Request.DownloadMedias).NotNull().NotEmpty();
                RuleForEach(x => x.Request.DownloadMedias).NotNull().SetValidator(new DownloadMediaDTOValidator());
                RuleForEach(x => x.Request.DownloadMedias)
                    .Must(x =>
                        x is null
                        || x.Type is not (PlexMediaType.PhotoAlbum or PlexMediaType.PhotoImage)
                        || x.Qualities is { Count: 0 }
                    )
                    .WithMessage("Photo selections must not include quality or part selectors.");
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
        if (!request.DownloadMedias.Any(x => x.Type is PlexMediaType.PhotoAlbum or PlexMediaType.PhotoImage))
            return ResultExtensions.IsEmpty("photoSelections").LogWarning();
        if (request.Integration is not null)
            return ResultExtensions
                .Create400BadRequestResult("Photo downloads do not support Sonarr or Radarr integrations.")
                .LogError();

        var selector = new PhotoDownloadSelector(_dbContext, _log);
        var selectionResult = await selector.ResolveSelectedFilesAsync(request, cancellationToken);
        if (selectionResult.IsCancelledOrFailed)
            return selectionResult.ToResult<DownloadTaskCreationReport>().LogIfFailed();
        var selectedPhotos = selectionResult.Value;
        if (selectedPhotos.Count == 0)
            return Result.Ok(new DownloadTaskCreationReport());
        var libraryIds = selectedPhotos.Select(x => x.MediaData.PlexLibraryId).Distinct().ToList();
        var transactionResult = await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                var existingFolders = await ctx
                    .DownloadTaskPhotoFolders.AsTracking()
                    .Where(x => libraryIds.Contains(x.PlexLibraryId) || x.ParentId == null)
                    .OrderBy(x => x.CreatedAt)
                    .ToListAsync(txCt);
                var existingFiles = await ctx.DownloadTaskPhotoImageFiles.ToListAsync(txCt);
                // Queue eligibility is calculated inside the transaction, not reused from an earlier preview.
                var accepted = selector.FilterQueueEligibleFiles(selectedPhotos, existingFolders, existingFiles);
                if (accepted.Count == 0)
                    return new DownloadTaskCreationReport();

                var existingFolderByKey = existingFolders
                    .GroupBy(
                        x => new FolderKey(
                            x.PlexServerId,
                            x.PlexLibraryId,
                            x.ParentId,
                            x.RelativePath,
                            PhotoDownloadSelector.NormalizeRootPath(x.DownloadRootPath),
                            PhotoDownloadSelector.NormalizeRootPath(x.DestinationRootPath)
                        ),
                        FolderKeyComparer.Instance
                    )
                    .ToDictionary(x => x.Key, x => x.OrderBy(y => y.CreatedAt).First(), FolderKeyComparer.Instance);
                var createdFolders = 0;
                var touchedRootIds = new HashSet<Guid>();
                var filesToCreate = new List<DownloadTaskPhotoImageFile>();
                DownloadTaskPhotoFolder GetOrCreateFolder(
                    PhotoDownloadCandidate photo,
                    DownloadTaskPhotoFolder? parent,
                    string title,
                    string relativePath
                )
                {
                    var key = new FolderKey(
                        photo.MediaData.PlexServerId,
                        photo.MediaData.PlexLibraryId,
                        parent?.Id,
                        relativePath,
                        photo.DirectoryMeta.DownloadRootPath,
                        photo.DirectoryMeta.DestinationRootPath
                    );
                    if (existingFolderByKey.TryGetValue(key, out var existing))
                        return existing;
                    var folder = new DownloadTaskPhotoFolder
                    {
                        Id = Guid.NewGuid(),
                        PlexApiRatingKey = 0,
                        PlexServerId = key.ServerId,
                        PlexLibraryId = key.LibraryId,
                        Title = title,
                        FullTitle = parent is null ? title : $"{parent.FullTitle}/{title}",
                        Year = 0,
                        DownloadStatus = DownloadStatus.Queued,
                        CreatedAt = DateTime.UtcNow,
                        Parent = parent,
                        ParentId = parent?.Id,
                        RelativePath = relativePath,
                        DownloadRootPath = photo.DirectoryMeta.DownloadRootPath,
                        DestinationRootPath = photo.DirectoryMeta.DestinationRootPath,
                        DataReceived = 0,
                        FileDataTransferred = 0,
                        DataTotal = 0,
                        DownloadSpeed = 0,
                        FileTransferSpeed = 0,
                        Children = [],
                        Files = [],
                    };
                    parent?.Children.Add(folder);
                    ctx.DownloadTaskPhotoFolders.Add(folder);
                    existingFolders.Add(folder);
                    existingFolderByKey.Add(key, folder);
                    createdFolders++;
                    if (folder.ParentId is null)
                        touchedRootIds.Add(folder.Id);
                    return folder;
                }

                foreach (var photo in accepted)
                {
                    var parent = GetOrCreateFolder(photo, null, photo.RootName, string.Empty);
                    touchedRootIds.Add(parent.Id);
                    var relativeSegments = photo.PhotoFolder.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1);
                    var cumulative = string.Empty;
                    foreach (var segment in relativeSegments)
                    {
                        cumulative = string.IsNullOrEmpty(cumulative) ? segment : $"{cumulative}/{segment}";
                        parent = GetOrCreateFolder(photo, parent, segment, cumulative);
                    }

                    var file = photo.MediaData.MapToDownloadTask(parent, request, photo.DirectoryMeta);
                    parent.Files.Add(file);
                    UpdatePhotoFolderAggregates(parent, file.DataTotal);
                    ctx.DownloadTaskPhotoImageFiles.Add(file);
                    filesToCreate.Add(file);
                }

                await ctx.SaveChangesAsync(txCt);
                foreach (var file in filesToCreate)
                {
                    ctx.DownloadTaskPhotoImageFileLogs.Add(
                        new DownloadTaskPhotoImageFileLog
                        {
                            Status = DownloadStatus.Queued,
                            LogLevel = NotificationLevel.Information,
                            Message = $"DownloadTask {file.FileName} was queued for downloading",
                            DownloadTaskFileId = file.Id,
                            CreatedAt = DateTime.UtcNow,
                        }
                    );
                }
                await ctx.SaveChangesAsync(txCt);

                return new DownloadTaskCreationReport
                {
                    PhotoFolders = createdFolders,
                    PhotoImages = filesToCreate.Count,
                    RootTaskIds = touchedRootIds.ToArray(),
                };
            },
            cancellationToken
        );
        return transactionResult.IsFailed ? transactionResult.LogIfFailed() : transactionResult;
    }

    private static void UpdatePhotoFolderAggregates(DownloadTaskPhotoFolder folder, long dataTotal)
    {
        folder.DownloadStatus = DownloadStatus.Queued;
        for (var current = folder; current is not null; current = current.Parent)
            current.DataTotal += dataTotal;
    }

    private readonly record struct FolderKey(
        int ServerId,
        int LibraryId,
        Guid? ParentId,
        string RelativePath,
        string DownloadRootPath,
        string DestinationRootPath
    );

    private sealed class FolderKeyComparer : IEqualityComparer<FolderKey>
    {
        public static FolderKeyComparer Instance { get; } = new();

        public bool Equals(FolderKey x, FolderKey y) =>
            x.ServerId == y.ServerId
            && x.LibraryId == y.LibraryId
            && x.ParentId == y.ParentId
            && PhotoDownloadSelector.DestinationPathComparer.Equals(x.RelativePath, y.RelativePath)
            && PhotoDownloadSelector.DestinationPathComparer.Equals(x.DownloadRootPath, y.DownloadRootPath)
            && PhotoDownloadSelector.DestinationPathComparer.Equals(x.DestinationRootPath, y.DestinationRootPath);

        public int GetHashCode(FolderKey obj) =>
            HashCode.Combine(
                obj.ServerId,
                obj.LibraryId,
                obj.ParentId,
                PhotoDownloadSelector.DestinationPathComparer.GetHashCode(obj.RelativePath),
                PhotoDownloadSelector.DestinationPathComparer.GetHashCode(obj.DownloadRootPath),
                PhotoDownloadSelector.DestinationPathComparer.GetHashCode(obj.DestinationRootPath)
            );
    }
}
