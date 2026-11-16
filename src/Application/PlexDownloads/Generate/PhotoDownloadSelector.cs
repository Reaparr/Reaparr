namespace Reaparr.Application;

internal sealed class PhotoDownloadSelector
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ILogger _log;

    internal static StringComparer DestinationPathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static StringComparison DestinationPathComparison { get; } =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public PhotoDownloadSelector(IReaparrDbContext dbContext, ILogger log)
    {
        _dbContext = dbContext;
        _log = log;
    }

    public Task<Result<List<PhotoDownloadSelection>>> ResolveSelectedFilesAsync(
        CreateDownloadTasksRequest request,
        CancellationToken cancellationToken
    ) =>
        ResolveSelectedFilesAsync(
            request.DownloadMedias,
            request.DestinationFolderPathId,
            request.CustomDestinationFolderPath,
            cancellationToken
        );

    public async Task<Result<List<PhotoDownloadSelection>>> ResolveSelectedFilesAsync(
        IReadOnlyCollection<DownloadMediaDTO> downloadMedias,
        int? destinationFolderPathId,
        string customDestinationFolderPath,
        CancellationToken cancellationToken
    )
    {
        var selections = downloadMedias
            .Where(x => x.Type is PlexMediaType.PhotoAlbum or PlexMediaType.PhotoImage)
            .ToList();
        var libraryIds = selections.Select(x => x.PlexLibraryId).Distinct().ToList();
        var libraryRootResult = await ResolveLibraryRootNamesAsync(selections, libraryIds, cancellationToken);
        if (libraryRootResult.IsCancelledOrFailed)
            return libraryRootResult.ToResult<List<PhotoDownloadSelection>>().LogIfFailed();

        var selectedResult = await ResolveSelectedFilesForScopesAsync(selections, cancellationToken);
        if (selectedResult.IsCancelledOrFailed)
            return selectedResult.ToResult<List<PhotoDownloadSelection>>().LogIfFailed();
        if (selectedResult.Value.Count == 0)
            return Result.Ok(new List<PhotoDownloadSelection>());

        var destinationResult = await ResolveDestinationRootsAsync(
            destinationFolderPathId,
            customDestinationFolderPath,
            selectedResult.Value.Select(x => x.MediaData.PlexLibraryId).Distinct().ToList()
        );
        if (destinationResult.IsCancelledOrFailed)
            return destinationResult.ToResult<List<PhotoDownloadSelection>>().LogIfFailed();

        var context = await CreateSelectionContextAsync(
            libraryIds,
            libraryRootResult.Value,
            destinationResult.Value,
            cancellationToken
        );
        return Result.Ok(CreatePhotoSelections(selectedResult.Value, context));
    }

    private async Task<PhotoSelectionContext> CreateSelectionContextAsync(
        IReadOnlyCollection<int> libraryIds,
        IReadOnlyDictionary<(int ServerId, int LibraryId), string> libraryRootNames,
        IReadOnlyDictionary<int, string> destinations,
        CancellationToken cancellationToken
    )
    {
        var downloadRoot = NormalizeRootPath((await _dbContext.GetDownloadFolder()).DirectoryPath);
        var folders = await _dbContext
            .PlexPhotoFolders.Where(x => libraryIds.Contains(x.PlexLibraryId))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var activeRoots = folders
            .Values.Where(x => x.ParentId is null && x.IsActive == true)
            .ToLookup(x => (x.PlexServerId, x.PlexLibraryId));
        return new PhotoSelectionContext
        {
            LibraryRootNames = libraryRootNames,
            Destinations = destinations,
            DownloadRoot = downloadRoot,
            Folders = folders,
            ActiveRoots = activeRoots,
        };
    }

    private List<PhotoDownloadSelection> CreatePhotoSelections(
        IEnumerable<SelectedPhoto> selected,
        PhotoSelectionContext context
    )
    {
        var rootsByFolder = new Dictionary<int, PlexPhotoFolder?>();
        var photos = new List<PhotoDownloadSelection>();
        foreach (var item in selected.OrderBy(x => x.MediaData.Id))
            photos.Add(CreatePhotoSelection(item, context, rootsByFolder));
        return photos;
    }

    private PhotoDownloadSelection CreatePhotoSelection(
        SelectedPhoto item,
        PhotoSelectionContext context,
        IDictionary<int, PlexPhotoFolder?> rootsByFolder
    )
    {
        var media = item.MediaData;
        var scope = (media.PlexServerId, media.PlexLibraryId);
        var folder = media.PlexPhotoFolderId is { } folderId ? context.Folders.GetValueOrDefault(folderId) : null;
        PlexPhotoFolder? root = null;
        if (folder is not null && !rootsByFolder.TryGetValue(folder.Id, out root))
            rootsByFolder[folder.Id] = root = FindRoot(folder, context.Folders);
        var relativeDirectory = string.Empty;
        if (folder is not null && root is not null)
        {
            var activeRootCount = 0;
            var isActiveRoot = false;
            foreach (var activeRoot in context.ActiveRoots[scope])
            {
                activeRootCount++;
                isActiveRoot |= activeRoot.Id == root.Id;
            }
            if (isActiveRoot)
                relativeDirectory = BuildRelativePhotoDirectory(folder, root, activeRootCount);
        }
        return new PhotoDownloadSelection(
            media,
            relativeDirectory,
            media.OriginalFilename,
            item.KeepCompleted,
            context.DownloadRoot,
            context.Destinations[media.PlexLibraryId],
            context.LibraryRootNames[scope]
        );
    }

    private static string BuildRelativePhotoDirectory(PlexPhotoFolder folder, PlexPhotoFolder root, int activeRootCount)
    {
        var relativeDirectory = folder.RelativePath.Trim('/');
        if (activeRootCount <= 1)
            return relativeDirectory;
        return string.IsNullOrEmpty(relativeDirectory) ? $"root-{root.Id}" : $"root-{root.Id}/{relativeDirectory}";
    }

    private async Task<Result<Dictionary<(int ServerId, int LibraryId), string>>> ResolveLibraryRootNamesAsync(
        IReadOnlyCollection<DownloadMediaDTO> selections,
        IReadOnlyCollection<int> libraryIds,
        CancellationToken cancellationToken
    )
    {
        var libraries = await _dbContext
            .PlexLibraries.Where(x => libraryIds.Contains(x.Id) && x.Type == PlexMediaType.PhotoAlbum)
            .Select(x => new
            {
                x.Id,
                x.PlexServerId,
                x.Title,
                ServerName = x.PlexServer!.Name,
            })
            .ToListAsync(cancellationToken);
        var libraryRootNames = libraries.ToDictionary(
            x => (x.PlexServerId, x.Id),
            x =>
            {
                var name = $"{x.ServerName.SanitizeFolderName()} - {x.Title.SanitizeFolderName()}".SanitizeFolderName();
                return string.IsNullOrWhiteSpace(name) ? $"server-{x.PlexServerId}-library-{x.Id}" : name;
            }
        );
        return selections.Any(x => !libraryRootNames.ContainsKey((x.PlexServerId, x.PlexLibraryId)))
            ? ResultExtensions.Create400BadRequestResult(
                "The selected photo library does not match the requested server or media type."
            )
            : Result.Ok(libraryRootNames);
    }

    private async Task<Result<List<SelectedPhoto>>> ResolveSelectedFilesForScopesAsync(
        IReadOnlyCollection<DownloadMediaDTO> selections,
        CancellationToken cancellationToken
    )
    {
        var selected = new List<SelectedPhoto>();
        foreach (var scope in selections.GroupBy(x => (x.PlexServerId, x.PlexLibraryId)))
        {
            var result = await ResolveSelectedFilesForScopeAsync(scope, cancellationToken);
            if (result.IsCancelledOrFailed)
                return result;
            selected.AddRange(result.Value);
        }
        return Result.Ok(selected);
    }

    public List<PhotoDownloadCandidate> FilterQueueEligibleFiles(
        IReadOnlyCollection<PhotoDownloadSelection> selections,
        IReadOnlyCollection<DownloadTaskPhotoFolder> existingFolders,
        IReadOnlyCollection<DownloadTaskPhotoImageFile> existingFiles
    )
    {
        var rootNames = ResolveQueueRootNames(selections, existingFolders);
        var existing = existingFiles
            .Select(x => new QueuedPhotoContext(
                GetIdentity(x),
                NormalizeRootPath(x.DirectoryMeta.DownloadRootPath),
                NormalizeRootPath(x.DirectoryMeta.DestinationRootPath)
            ))
            .ToHashSet(QueuedPhotoContextComparer.Instance);
        var pathOwners = new Dictionary<string, HashSet<PhotoIdentity>>(DestinationPathComparer);

        foreach (var file in existingFiles.Where(x => x.DownloadStatus != DownloadStatus.Deleted))
        {
            if (!TryGetDestinationPath(file.DirectoryMeta, file.FileName, out var path))
                continue;
            if (!pathOwners.TryGetValue(path, out var owners))
                pathOwners[path] = owners = [];
            owners.Add(GetIdentity(file));
        }

        var candidates = new List<(PhotoDownloadCandidate Candidate, PhotoIdentity Identity, string Path)>();
        foreach (var selection in selections)
        {
            var rootName = rootNames[(selection.MediaData.PlexServerId, selection.MediaData.PlexLibraryId)];
            var namedSelection = selection.RootName == rootName ? selection : selection with { RootName = rootName };
            var identity = GetIdentity(namedSelection.MediaData);
            if (
                existing.Contains(
                    new QueuedPhotoContext(
                        identity,
                        namedSelection.DownloadRootPath,
                        namedSelection.DestinationRootPath
                    )
                )
            )
                continue;

            var directoryMeta = namedSelection.CreateDirectoryMetadata();
            if (TryGetDestinationPath(directoryMeta, namedSelection.FileName, out var path))
                candidates.Add((new(namedSelection, directoryMeta), identity, path));
            else
                LogInvalidDestination(namedSelection);
        }

        var accepted = new List<PhotoDownloadCandidate>();
        foreach (var group in candidates.GroupBy(x => x.Path, DestinationPathComparer))
        {
            // All new participants are rejected when different assets target the same path.
            var identity = group.First().Identity;
            var collides =
                group.Any(x => x.Identity != identity)
                || (pathOwners.TryGetValue(group.Key, out var owners) && owners.Any(x => x != identity));
            if (collides)
            {
                foreach (var candidate in group)
                    LogInvalidDestination(candidate.Candidate.Selection);
                continue;
            }

            accepted.AddRange(group.Select(x => x.Candidate));
        }
        return accepted;
    }

    internal static string NormalizeRootPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    private async Task<Result<List<SelectedPhoto>>> ResolveSelectedFilesForScopeAsync(
        IGrouping<(int ServerId, int LibraryId), DownloadMediaDTO> selections,
        CancellationToken cancellationToken
    )
    {
        var choices = new List<PhotoSelectionChoice>();
        var albumResult = await ResolveAlbumChoicesAsync(selections, cancellationToken);
        if (albumResult.IsCancelledOrFailed)
            return albumResult.ToResult<List<SelectedPhoto>>().LogIfFailed();
        choices.AddRange(albumResult.Value);

        var folderResult = await ResolveFolderChoicesAsync(selections, cancellationToken);
        if (folderResult.IsCancelledOrFailed)
            return folderResult.ToResult<List<SelectedPhoto>>().LogIfFailed();
        choices.AddRange(folderResult.Value);

        var directResult = await ResolveDirectChoicesAsync(selections, cancellationToken);
        if (directResult.IsCancelledOrFailed)
            return directResult.ToResult<List<SelectedPhoto>>().LogIfFailed();
        choices.AddRange(directResult.Value);

        return MergeSelectionChoices(choices);
    }

    private async Task<Result<List<PhotoSelectionChoice>>> ResolveAlbumChoicesAsync(
        IGrouping<(int ServerId, int LibraryId), DownloadMediaDTO> selections,
        CancellationToken cancellationToken
    )
    {
        var albumIds = selections
            .Where(x => x.Type == PlexMediaType.PhotoAlbum && x.SelectionKind == PhotoItemKind.Album)
            .SelectMany(x => x.MediaIds)
            .Distinct()
            .ToList();
        var filesResult = await LoadAlbumFilesAsync(selections, albumIds, cancellationToken);
        if (filesResult.IsCancelledOrFailed)
            return filesResult.ToResult<List<PhotoSelectionChoice>>().LogIfFailed();
        return Result.Ok(CreateAlbumChoices(selections, filesResult.Value));
    }

    private async Task<Result<ILookup<int, PlexPhotoMediaData>>> LoadAlbumFilesAsync(
        IGrouping<(int ServerId, int LibraryId), DownloadMediaDTO> selections,
        IReadOnlyCollection<int> albumIds,
        CancellationToken cancellationToken
    )
    {
        var (serverId, libraryId) = selections.Key;
        var albums = await _dbContext
            .PlexPhotoAlbums.Where(x =>
                albumIds.Contains(x.Id) && x.PlexServerId == serverId && x.PlexLibraryId == libraryId
            )
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (albums.Count != albumIds.Count)
            return ResultExtensions.Create400BadRequestResult(
                "Every selected photo album must belong to the requested library and server."
            );

        var memberships = await _dbContext
            .PlexPhotoAlbumMemberships.Include(x => x.PlexPhotoMediaData)
            .Where(x =>
                albumIds.Contains(x.PlexPhotoAlbumId)
                && x.PlexPhotoMediaData!.PlexServerId == serverId
                && x.PlexPhotoMediaData.PlexLibraryId == libraryId
            )
            .ToListAsync(cancellationToken);
        return Result.Ok(memberships.ToLookup(x => x.PlexPhotoAlbumId, x => x.PlexPhotoMediaData!));
    }

    private static List<PhotoSelectionChoice> CreateAlbumChoices(
        IEnumerable<DownloadMediaDTO> selections,
        ILookup<int, PlexPhotoMediaData> filesByAlbum
    )
    {
        var choices = new List<PhotoSelectionChoice>();
        foreach (
            var selection in selections.Where(x =>
                x.Type == PlexMediaType.PhotoAlbum && x.SelectionKind == PhotoItemKind.Album
            )
        )
        {
            foreach (var id in selection.MediaIds.Distinct())
                choices.AddRange(
                    filesByAlbum[id]
                        .Select(file => new PhotoSelectionChoice(
                            file,
                            selection.KeepCompletedInDownloadFolder,
                            Direct: false
                        ))
                );
        }
        return choices;
    }

    private async Task<Result<List<PhotoSelectionChoice>>> ResolveFolderChoicesAsync(
        IGrouping<(int ServerId, int LibraryId), DownloadMediaDTO> selections,
        CancellationToken cancellationToken
    )
    {
        var (serverId, libraryId) = selections.Key;
        var choices = new List<PhotoSelectionChoice>();
        foreach (
            var selection in selections.Where(x =>
                x.Type == PlexMediaType.PhotoImage && x.SelectionKind == PhotoItemKind.Folder
            )
        )
        {
            foreach (var folderId in selection.MediaIds.Distinct())
            {
                var subtree = await _dbContext.ResolvePlexPhotoFolderSubtreeAsync(
                    (ServerId: serverId, LibraryId: libraryId),
                    folderId,
                    cancellationToken
                );
                if (subtree is null)
                    return ResultExtensions.Create400BadRequestResult(
                        "Every selected photo folder must belong to the requested library and active source root."
                    );
                choices.AddRange(
                    subtree.Assets.Select(file => new PhotoSelectionChoice(
                        file,
                        selection.KeepCompletedInDownloadFolder,
                        Direct: false
                    ))
                );
            }
        }
        return Result.Ok(choices);
    }

    private async Task<Result<List<PhotoSelectionChoice>>> ResolveDirectChoicesAsync(
        IGrouping<(int ServerId, int LibraryId), DownloadMediaDTO> selections,
        CancellationToken cancellationToken
    )
    {
        var (serverId, libraryId) = selections.Key;
        var directIds = selections
            .Where(x => x.Type == PlexMediaType.PhotoImage && x.SelectionKind == PhotoItemKind.Asset)
            .SelectMany(x => x.MediaIds)
            .Distinct()
            .ToList();
        var directFiles = await _dbContext
            .PlexPhotoMediaData.Where(x =>
                directIds.Contains(x.Id) && x.PlexServerId == serverId && x.PlexLibraryId == libraryId
            )
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (directFiles.Count != directIds.Count)
            return ResultExtensions.Create400BadRequestResult(
                "Every selected photo file must belong to the requested library and server."
            );

        var choices = new List<PhotoSelectionChoice>();
        foreach (
            var selection in selections.Where(x =>
                x.Type == PlexMediaType.PhotoImage && x.SelectionKind == PhotoItemKind.Asset
            )
        )
        {
            foreach (var id in selection.MediaIds.Distinct())
                choices.Add(
                    new PhotoSelectionChoice(directFiles[id], selection.KeepCompletedInDownloadFolder, Direct: true)
                );
        }
        return Result.Ok(choices);
    }

    private static Result<List<SelectedPhoto>> MergeSelectionChoices(IEnumerable<PhotoSelectionChoice> choices)
    {
        var selected = new List<SelectedPhoto>();
        foreach (var file in choices.GroupBy(x => x.File.Id))
        {
            // Direct selection overrides album/folder options; peers must agree, regardless of request order.
            var effective = file.Any(x => x.Direct) ? file.Where(x => x.Direct) : file;
            var options = effective.Select(x => x.KeepCompleted).Distinct().ToList();
            if (options.Count > 1)
                return ResultExtensions.Create400BadRequestResult(
                    "Conflicting photo KeepCompletedInDownloadFolder values were supplied."
                );
            selected.Add(new(file.First().File, options[0]));
        }
        return Result.Ok(selected);
    }

    private async Task<Result<Dictionary<int, string>>> ResolveDestinationRootsAsync(
        int? destinationFolderPathId,
        string customDestinationFolderPath,
        IEnumerable<int> libraryIds
    )
    {
        var destinationOverride = customDestinationFolderPath;
        if (string.IsNullOrWhiteSpace(destinationOverride) && destinationFolderPathId is { } destinationId)
        {
            destinationOverride = await _dbContext
                .FolderPaths.Where(x => x.Id == destinationId)
                .Select(x => x.DirectoryPath)
                .FirstOrDefaultAsync();
            if (destinationOverride is null)
                return ResultExtensions.EntityNotFound(nameof(FolderPath), destinationId);
        }

        var destinations = new Dictionary<int, string>();
        foreach (var libraryId in libraryIds)
        {
            var destination = string.IsNullOrWhiteSpace(destinationOverride)
                ? (await _dbContext.GetDestinationFolder(libraryId))?.DirectoryPath
                : destinationOverride;
            if (string.IsNullOrWhiteSpace(destination))
                return ResultExtensions.EntityNotFound(nameof(FolderPath), libraryId);
            destinations[libraryId] = NormalizeRootPath(destination);
        }
        return Result.Ok(destinations);
    }

    private static Dictionary<(int ServerId, int LibraryId), string> ResolveQueueRootNames(
        IEnumerable<PhotoDownloadSelection> selections,
        IEnumerable<DownloadTaskPhotoFolder> folders
    )
    {
        var existing = folders
            .Where(x => x.ParentId is null)
            .Select(x => new
            {
                x.PlexServerId,
                x.PlexLibraryId,
                DownloadRoot = NormalizeRootPath(x.DownloadRootPath),
                DestinationRoot = NormalizeRootPath(x.DestinationRootPath),
                x.Title,
                x.CreatedAt,
            })
            .OrderBy(x => x.CreatedAt)
            .ToList();
        var libraries = selections.DistinctBy(x => (x.MediaData.PlexServerId, x.MediaData.PlexLibraryId)).ToList();
        var names = new Dictionary<(int ServerId, int LibraryId), string>();
        foreach (var selection in libraries)
        {
            var scope = (selection.MediaData.PlexServerId, selection.MediaData.PlexLibraryId);
            var captured = existing.FirstOrDefault(x =>
                x.PlexServerId == scope.PlexServerId
                && x.PlexLibraryId == scope.PlexLibraryId
                && DestinationPathComparer.Equals(x.DownloadRoot, selection.DownloadRootPath)
                && DestinationPathComparer.Equals(x.DestinationRoot, selection.DestinationRootPath)
            );
            if (captured is not null)
            {
                names[scope] = captured.Title;
                continue;
            }

            var selectedCollision = libraries.Any(x =>
                (x.MediaData.PlexServerId, x.MediaData.PlexLibraryId) != scope
                && DestinationPathComparer.Equals(x.DestinationRootPath, selection.DestinationRootPath)
                && DestinationPathComparer.Equals(x.RootName, selection.RootName)
            );
            var existingCollision = existing.Any(x =>
                DestinationPathComparer.Equals(x.DestinationRoot, selection.DestinationRootPath)
                && DestinationPathComparer.Equals(x.Title, selection.RootName)
                && (x.PlexServerId, x.PlexLibraryId) != scope
            );
            names[scope] =
                selectedCollision || existingCollision
                    ? $"{selection.RootName} [{scope.PlexServerId}-{scope.PlexLibraryId}]"
                    : selection.RootName;
        }
        return names;
    }

    private static PlexPhotoFolder? FindRoot(PlexPhotoFolder folder, IReadOnlyDictionary<int, PlexPhotoFolder> folders)
    {
        var current = folder;
        while (current.ParentId is { } parentId)
        {
            if (!folders.TryGetValue(parentId, out var parent))
                return null;
            current = parent;
        }
        return current;
    }

    private static bool TryGetDestinationPath(DownloadTaskDirectory directory, string fileName, out string path)
    {
        path = string.Empty;
        if (
            !Path.IsPathFullyQualified(directory.DestinationRootPath)
            || !IsValidDestinationComponent(fileName)
            || !directory.PhotoFolder.Split('/').All(IsValidDestinationComponent)
        )
            return false;
        try
        {
            var root = Path.GetFullPath(directory.DestinationRootPath);
            var full = Path.GetFullPath(directory.GetDestinationFilePath(DownloadTaskType.PhotoImage, fileName));
            var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, DestinationPathComparison))
                return false;
            path = full.Replace(Path.DirectorySeparatorChar, '/');
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsValidDestinationComponent(string component)
    {
        if (
            string.IsNullOrWhiteSpace(component)
            || component is "." or ".."
            || component.Length > 255
            || component.EndsWith('.')
            || component.EndsWith(' ')
        )
            return false;
        foreach (var character in component)
            if (
                character is '/' or '\\' or '\0' or '<' or '>' or ':' or '"' or '|' or '?' or '*'
                || char.IsControl(character)
            )
                return false;
        var extension = component.IndexOf('.');
        var name = (extension >= 0 ? component[..extension] : component).ToUpperInvariant();
        return name is not ("CON" or "PRN" or "AUX" or "NUL")
            && !(
                name.Length == 4 && (name.StartsWith("COM") || name.StartsWith("LPT")) && name[3] is >= '1' and <= '9'
            );
    }

    private void LogInvalidDestination(PhotoDownloadSelection selection) =>
        _log.Here()
            .Warning(
                "Skipping photo {Title} (ID: {Id}) because its destination path is invalid or collides",
                selection.MediaData.Title,
                selection.MediaData.Id
            );

    private static PhotoIdentity GetIdentity(PlexPhotoMediaData file) =>
        new(file.PlexServerId, file.PlexLibraryId, file.PlexApiRatingKey, file.PlexApiMediaId, file.PlexApiPartId);

    private static PhotoIdentity GetIdentity(DownloadTaskPhotoImageFile file) =>
        new(file.PlexServerId, file.PlexLibraryId, file.PlexApiRatingKey, file.PlexApiMediaId, file.PlexApiPartId);

    private readonly record struct PhotoSelectionContext
    {
        public required IReadOnlyDictionary<(int ServerId, int LibraryId), string> LibraryRootNames { get; init; }
        public required IReadOnlyDictionary<int, string> Destinations { get; init; }
        public required string DownloadRoot { get; init; }
        public required IReadOnlyDictionary<int, PlexPhotoFolder> Folders { get; init; }
        public required ILookup<(int ServerId, int LibraryId), PlexPhotoFolder> ActiveRoots { get; init; }
    }

    private readonly record struct PhotoSelectionChoice(PlexPhotoMediaData File, bool KeepCompleted, bool Direct);

    private readonly record struct SelectedPhoto(PlexPhotoMediaData MediaData, bool KeepCompleted);

    private readonly record struct QueuedPhotoContext(
        PhotoIdentity Identity,
        string DownloadRootPath,
        string DestinationRootPath
    );

    private sealed class QueuedPhotoContextComparer : IEqualityComparer<QueuedPhotoContext>
    {
        public static QueuedPhotoContextComparer Instance { get; } = new();

        public bool Equals(QueuedPhotoContext x, QueuedPhotoContext y) =>
            x.Identity == y.Identity
            && DestinationPathComparer.Equals(x.DownloadRootPath, y.DownloadRootPath)
            && DestinationPathComparer.Equals(x.DestinationRootPath, y.DestinationRootPath);

        public int GetHashCode(QueuedPhotoContext obj) =>
            HashCode.Combine(
                obj.Identity,
                DestinationPathComparer.GetHashCode(obj.DownloadRootPath),
                DestinationPathComparer.GetHashCode(obj.DestinationRootPath)
            );
    }

    private readonly record struct PhotoIdentity(int ServerId, int LibraryId, int RatingKey, int MediaId, int PartId);
}

internal sealed record PhotoDownloadSelection(
    PlexPhotoMediaData MediaData,
    string RelativeDirectory,
    string FileName,
    bool KeepCompletedInDownloadFolder,
    string DownloadRootPath,
    string DestinationRootPath,
    string RootName
)
{
    public string PhotoFolder => string.IsNullOrEmpty(RelativeDirectory) ? RootName : $"{RootName}/{RelativeDirectory}";

    public DownloadTaskDirectory CreateDirectoryMetadata() =>
        new()
        {
            DownloadRootPath = DownloadRootPath,
            DestinationRootPath = DestinationRootPath,
            MovieFolder = string.Empty,
            TvShowFolder = string.Empty,
            SeasonFolder = string.Empty,
            MusicArtistFolder = string.Empty,
            MusicAlbumFolder = string.Empty,
            PhotoFolder = PhotoFolder,
            OtherVideoFolder = string.Empty,
            KeepCompletedInDownloadFolder = KeepCompletedInDownloadFolder,
        };
}

internal readonly record struct PhotoDownloadCandidate(
    PhotoDownloadSelection Selection,
    DownloadTaskDirectory DirectoryMeta
)
{
    public PlexPhotoMediaData MediaData => Selection.MediaData;
    public string RelativeDirectory => Selection.RelativeDirectory;
    public string FileName => Selection.FileName;
    public string RootName => Selection.RootName;
    public string PhotoFolder => DirectoryMeta.PhotoFolder;
}
