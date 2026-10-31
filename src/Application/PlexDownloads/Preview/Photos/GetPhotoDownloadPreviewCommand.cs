namespace Reaparr.Application;

public record GetPhotoDownloadPreviewCommand(DownloadPreviewRequest Request)
    : ICommand<Result<IEnumerable<DownloadPreview>>>;

public class GetPhotoDownloadPreviewCommandValidator : AbstractValidator<GetPhotoDownloadPreviewCommand>
{
    public GetPhotoDownloadPreviewCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.Request).NotNull();
        RuleForEach(x => x.Request.DownloadMedias).NotNull().SetValidator(new DownloadMediaDTOValidator());
        RuleForEach(x => x.Request.DownloadMedias)
            .Must(x => x is null || x.Type is PlexMediaType.PhotoAlbum or PlexMediaType.PhotoImage)
            .WithMessage("Photo preview selections must contain only photo media.");
        RuleForEach(x => x.Request.DownloadMedias)
            .Must(x => x is null || x.Qualities is { Count: 0 })
            .WithMessage("Photo selections must not include quality or part selectors.");
    }
}

public class GetPhotoDownloadPreviewCommandHandler
    : ICommandHandler<GetPhotoDownloadPreviewCommand, Result<IEnumerable<DownloadPreview>>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public GetPhotoDownloadPreviewCommandHandler(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<GetPhotoDownloadPreviewCommandHandler>();
        _dbContext = dbContext;
    }

    public async Task<Result<IEnumerable<DownloadPreview>>> ExecuteAsync(
        GetPhotoDownloadPreviewCommand command,
        CancellationToken cancellationToken
    )
    {
        if (command.Request.DownloadMedias.Count == 0)
            return Result.Ok(Enumerable.Empty<DownloadPreview>());
        var selector = new PhotoDownloadSelector(_dbContext, _log);
        var selectionResult = await selector.ResolveSelectedFilesAsync(
            command.Request.DownloadMedias,
            command.Request.DestinationFolderPathId,
            command.Request.CustomDestinationFolderPath,
            cancellationToken
        );
        if (selectionResult.IsCancelledOrFailed)
            return selectionResult.ToResult<IEnumerable<DownloadPreview>>().LogIfFailed();
        if (selectionResult.Value.Count == 0)
            return Result.Ok(Enumerable.Empty<DownloadPreview>());

        var roots = await _dbContext
            .DownloadTaskPhotoFolders.Where(x => x.ParentId == null)
            .ToListAsync(cancellationToken);
        var files = await _dbContext.DownloadTaskPhotoImageFiles.ToListAsync(cancellationToken);
        var eligibleSelections = selector.FilterQueueEligibleFiles(selectionResult.Value, roots, files);
        return Result.Ok(CreatePreviews(eligibleSelections));
    }

    private static IEnumerable<DownloadPreview> CreatePreviews(IEnumerable<PhotoDownloadCandidate> candidates)
    {
        var roots = new Dictionary<(int ServerId, int LibraryId), DownloadPreview>();
        var folders = new Dictionary<(int ServerId, int LibraryId, string Path), DownloadPreview>();
        foreach (var candidate in candidates)
        {
            var media = candidate.MediaData;
            var scope = (media.PlexServerId, media.PlexLibraryId);
            if (!roots.TryGetValue(scope, out var root))
            {
                root = CreatePreview(media.PlexLibraryId, candidate.RootName, PlexMediaType.PhotoAlbum, 0);
                roots[scope] = root;
            }
            var parent = root;
            var path = string.Empty;
            foreach (var segment in candidate.RelativeDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                path = string.IsNullOrEmpty(path) ? segment : $"{path}/{segment}";
                var key = (scope.PlexServerId, scope.PlexLibraryId, path);
                if (!folders.TryGetValue(key, out var child))
                {
                    child = CreatePreview(media.PlexPhotoFolderId!.Value, segment, PlexMediaType.PhotoAlbum, 0);
                    folders[key] = child;
                    parent.Children.Add(child);
                }
                parent = child;
            }
            parent.Children.Add(CreatePreview(media.Id, candidate.FileName, PlexMediaType.PhotoImage, media.Size));
        }
        foreach (var root in roots.Values)
            AggregateAndSort(root);
        return roots.Values.OrderByNatural(x => x.Title);
    }

    private static DownloadPreview CreatePreview(int id, string title, PlexMediaType type, long size) =>
        new()
        {
            Id = id,
            Title = title,
            MediaType = type,
            Size = size,
            ChildCount = 0,
            TvShowId = default,
            SeasonId = default,
            ArtistId = default,
            AlbumId = default,
            Qualities = [],
            Children = [],
        };

    private static void AggregateAndSort(DownloadPreview folder)
    {
        foreach (var child in folder.Children)
        {
            if (child.MediaType == PlexMediaType.PhotoAlbum)
                AggregateAndSort(child);
            folder.Size += child.Size;
            folder.ChildCount += child.MediaType == PlexMediaType.PhotoImage ? 1 : child.ChildCount;
        }
        var sorted = folder.Children.OrderByNatural(x => x.Title).ToList();
        folder.Children.Clear();
        folder.Children.AddRange(sorted);
    }
}
