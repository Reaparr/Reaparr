namespace Reaparr.Application;

public record SyncPlexPhotosCommand(
    InsertMediaMetaDataCommandResponse LibraryMetadata,
    bool ForceMediaRefresh = false
) : ICommand<Result<CrudPhotosReport>>;

public class SyncPlexPhotosCommandValidator : AbstractValidator<SyncPlexPhotosCommand>
{
    public SyncPlexPhotosCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.LibraryMetadata).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary.Id).GreaterThan(0);
        RuleFor(x => x.LibraryMetadata.PlexLibrary.PlexServerId).GreaterThan(0);
        RuleFor(x => x.LibraryMetadata.PlexLibrary.Type).Equal(PlexMediaType.PhotoAlbum);
    }
}

public class SyncPlexPhotosCommandHandler : ICommandHandler<SyncPlexPhotosCommand, Result<CrudPhotosReport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public SyncPlexPhotosCommandHandler(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<SyncPlexPhotosCommandHandler>();
        _dbContext = dbContext;
    }

    public async Task<Result<CrudPhotosReport>> ExecuteAsync(SyncPlexPhotosCommand command, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.TaskIsCancelled(nameof(SyncPlexPhotosCommand)).LogWarning();

        var library = command.LibraryMetadata.PlexLibrary;
        var result = await _dbContext.ExecuteTransactionAsync(
            (context, transactionToken) => ReconcileAsync(context, library, transactionToken),
            ct
        );
        if (result.IsCancelled)
            return result.LogWarning();
        if (result.IsFailed)
            return result.LogError();

        _log.Here().Information("Synchronized Photos library {PlexLibraryId}: {@Report}", library.Id, result.Value);
        return result;
    }

    private async Task<CrudPhotosReport> ReconcileAsync(IReaparrDbContext context, PlexLibrary library, CancellationToken ct)
    {
        var incomingAlbums = library.PhotoAlbums.ToList();
        var incomingFiles = library.PhotoMediaData.ToList();
        var incomingAlbumByKey = incomingAlbums.ToDictionary(x => x.PlexApiRatingKey);
        var incomingFileByKey = incomingFiles.ToDictionary(x => x.PlexApiRatingKey);

        var currentAlbums = await context.PlexPhotoAlbums.Where(x => x.PlexLibraryId == library.Id).AsTracking().ToListAsync(ct);
        var currentFiles = await context.PlexPhotoMediaData.Where(x => x.PlexLibraryId == library.Id).AsTracking().ToListAsync(ct);
        var currentFolders = await context.PlexPhotoFolders.Where(x => x.PlexLibraryId == library.Id).AsTracking().ToListAsync(ct);
        var currentMemberships = await context
            .PlexPhotoAlbumMemberships.Where(x =>
                x.PlexPhotoAlbum!.PlexLibraryId == library.Id
                && x.PlexPhotoMediaData!.PlexLibraryId == library.Id
            )
            .AsTracking()
            .ToListAsync(ct);

        var currentAlbumByKey = currentAlbums.ToDictionary(x => x.PlexApiRatingKey);
        var currentFileByKey = currentFiles.ToDictionary(x => x.PlexApiRatingKey);
        var currentFolderByKey = BuildFolderMap(currentFolders);
        var workingFolders = new Dictionary<FolderKey, PlexPhotoFolder>(currentFolderByKey);
        var incomingFolderByKey = BuildIncomingFolderMap(library.PhotoFolders);
        var desiredFolderKeys = incomingFolderByKey.Keys.ToHashSet();

        var createdAlbumCount = 0;
        var updatedAlbumCount = 0;
        var unchangedAlbumCount = 0;
        var workingAlbums = new Dictionary<int, PlexPhotoAlbum>();
        foreach (var sourceAlbum in incomingAlbums)
        {
            if (currentAlbumByKey.TryGetValue(sourceAlbum.PlexApiRatingKey, out var existing))
            {
                var entry = context.Entry(existing);
                sourceAlbum.Id = existing.Id;
                entry.CurrentValues.SetValues(sourceAlbum);
                workingAlbums[sourceAlbum.PlexApiRatingKey] = existing;
                if (entry.Properties.Any(x => x.IsModified))
                    updatedAlbumCount++;
                else
                    unchangedAlbumCount++;
            }
            else
            {
                var created = CreateAlbum(sourceAlbum, library);
                context.PlexPhotoAlbums.Add(created);
                workingAlbums[sourceAlbum.PlexApiRatingKey] = created;
                createdAlbumCount++;
            }
        }

        var incomingRoots = incomingFolderByKey
            .Where(x => x.Key.RelativePath.Length == 0)
            .OrderBy(x => x.Key.RootIdentity, StringComparer.Ordinal)
            .ToList();
        var incomingRootKeys = incomingRoots.Select(x => x.Key).ToHashSet();
        foreach (var currentRoot in currentFolders.Where(x => x.ParentId is null && x.RootIdentityKey is not null))
        {
            if (!incomingRootKeys.Contains(new FolderKey(currentRoot.RootIdentityKey!, string.Empty)))
                currentRoot.IsActive = false;
        }
        foreach (var (key, sourceRoot) in incomingRoots)
        {
            if (workingFolders.TryGetValue(key, out var existing))
            {
                ApplyFolder(existing, sourceRoot, library, null);
            }
            else
            {
                existing = CreateFolder(sourceRoot, library, null);
                context.PlexPhotoFolders.Add(existing);
                workingFolders.Add(key, existing);
            }
        }

        var incomingDescendants = incomingFolderByKey
            .Where(x => x.Key.RelativePath.Length > 0)
            .OrderBy(x => x.Key.RelativePath.Count(c => c == '/'))
            .ThenBy(x => x.Key.RootIdentity, StringComparer.Ordinal)
            .ThenBy(x => x.Key.RelativePath, StringComparer.Ordinal)
            .ToList();
        foreach (var (key, sourceFolder) in incomingDescendants)
        {
            var parent = workingFolders[GetIncomingFolderKey(sourceFolder.Parent!)];

            if (workingFolders.TryGetValue(key, out var existing))
            {
                ApplyFolder(existing, sourceFolder, library, parent);
            }
            else
            {
                existing = CreateFolder(sourceFolder, library, parent);
                context.PlexPhotoFolders.Add(existing);
                workingFolders.Add(key, existing);
            }
        }

        var previousFolderIdByFileKey = currentFiles.ToDictionary(x => x.PlexApiRatingKey, x => x.PlexPhotoFolderId);
        var scalarChangesByFileKey = new Dictionary<int, bool>();
        var workingFiles = new Dictionary<int, PlexPhotoMediaData>();
        var createdFileCount = 0;
        var updatedFileCount = 0;
        var unchangedFileCount = 0;
        foreach (var sourceFile in incomingFiles)
        {
            var targetFolder = sourceFile.PlexPhotoFolder is { } folder
                ? workingFolders[GetIncomingFolderKey(folder)]
                : null;
            sourceFile.PlexPhotoFolderId = targetFolder?.Id > 0 ? targetFolder.Id : null;
            if (currentFileByKey.TryGetValue(sourceFile.PlexApiRatingKey, out var existing))
            {
                var entry = context.Entry(existing);
                sourceFile.Id = existing.Id;
                entry.CurrentValues.SetValues(sourceFile);
                existing.PlexPhotoFolder = targetFolder;
                scalarChangesByFileKey[sourceFile.PlexApiRatingKey] = entry.Properties.Any(x => x.IsModified);
                workingFiles[sourceFile.PlexApiRatingKey] = existing;
            }
            else
            {
                var created = CreateFile(sourceFile, library, targetFolder);
                context.PlexPhotoMediaData.Add(created);
                workingFiles[sourceFile.PlexApiRatingKey] = created;
                createdFileCount++;
            }
        }

        var obsoleteFolders = currentFolderByKey
            .Where(x => x.Key.RelativePath.Length > 0 && !desiredFolderKeys.Contains(x.Key))
            .Select(x => x.Value)
            .ToList();
        var obsoleteFolderIds = obsoleteFolders.Select(x => x.Id).ToHashSet();
        foreach (var file in currentFiles)
        {
            if (file.PlexPhotoFolderId is int folderId && obsoleteFolderIds.Contains(folderId))
                file.PlexPhotoFolderId = null;
        }
        if (obsoleteFolders.Count > 0)
            context.PlexPhotoFolders.RemoveRange(obsoleteFolders);

        await context.SaveChangesAsync(ct);

        foreach (var sourceFile in incomingFiles)
        {
            var persistedFile = workingFiles[sourceFile.PlexApiRatingKey];
            var changedFolder = previousFolderIdByFileKey.TryGetValue(sourceFile.PlexApiRatingKey, out var oldFolderId)
                && oldFolderId != persistedFile.PlexPhotoFolderId;
            if (scalarChangesByFileKey.TryGetValue(sourceFile.PlexApiRatingKey, out var scalarChanged))
            {
                if (scalarChanged || changedFolder)
                    updatedFileCount++;
                else
                    unchangedFileCount++;
            }
        }

        var desiredMemberships = new HashSet<(int AlbumId, int FileId)>();
        foreach (var sourceAlbum in incomingAlbums)
        {
            var album = workingAlbums[sourceAlbum.PlexApiRatingKey];
            foreach (var membership in sourceAlbum.Memberships)
            {
                var file = workingFiles[membership.PlexPhotoMediaData!.PlexApiRatingKey];
                desiredMemberships.Add((album.Id, file.Id));
            }
        }

        var retainedMemberships = new HashSet<(int AlbumId, int FileId)>();
        foreach (var membership in currentMemberships)
        {
            var key = (membership.PlexPhotoAlbumId, membership.PlexPhotoMediaDataId);
            if (desiredMemberships.Contains(key))
                retainedMemberships.Add(key);
            else
                context.PlexPhotoAlbumMemberships.Remove(membership);
        }
        foreach (var key in desiredMemberships.Where(x => !retainedMemberships.Contains(x)))
        {
            context.PlexPhotoAlbumMemberships.Add(
                new PlexPhotoAlbumMembership
                {
                    PlexPhotoAlbumId = key.AlbumId,
                    PlexPhotoMediaDataId = key.FileId,
                }
            );
        }

        var deletedAlbums = currentAlbums.Where(x => !incomingAlbumByKey.ContainsKey(x.PlexApiRatingKey)).ToList();
        var deletedFiles = currentFiles.Where(x => !incomingFileByKey.ContainsKey(x.PlexApiRatingKey)).ToList();
        if (deletedAlbums.Count > 0)
            context.PlexPhotoAlbums.RemoveRange(deletedAlbums);
        if (deletedFiles.Count > 0)
            context.PlexPhotoMediaData.RemoveRange(deletedFiles);
        await context.SaveChangesAsync(ct);

        var photoCount = await context.PlexPhotoMediaData.CountAsync(x => x.PlexLibraryId == library.Id, ct);
        var clipCount = await context.PlexPhotoMediaData.CountAsync(
            x => x.PlexLibraryId == library.Id && x.AssetKind == PhotoAssetKind.VideoClip,
            ct
        );
        var albumCount = await context.PlexPhotoAlbums.CountAsync(x => x.PlexLibraryId == library.Id, ct);
        var totalMediaSize =
            await context.PlexPhotoMediaData.Where(x => x.PlexLibraryId == library.Id).Select(x => (long?)x.Size).SumAsync(ct) ?? 0;
        // Catalog metrics and shared sync completion must commit with photo reconciliation.
        var syncedAt = DateTime.UtcNow;
        await context
            .PlexLibraries
            .Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(x => x.Title, library.Title)
                        .SetProperty(x => x.ContentChangedAt, library.ContentChangedAt)
                        .SetProperty(x => x.UpdatedAt, library.UpdatedAt)
                        .SetProperty(x => x.ScannedAt, library.ScannedAt)
                        .SetProperty(x => x.PhotoAlbumCount, albumCount)
                        .SetProperty(x => x.PhotoImageCount, photoCount - clipCount)
                        .SetProperty(x => x.PhotoClipCount, clipCount)
                        .SetProperty(x => x.MediaSize, totalMediaSize)
                        .SetProperty(x => x.SyncedAt, syncedAt)
                        .SetProperty(x => x.SyncedContentChangedAt, library.ContentChangedAt)
                        .SetProperty(x => x.Outdated, false),
                ct
            );

        return new CrudPhotosReport
        {
            CreatedPhotoAlbums = createdAlbumCount,
            UpdatedPhotoAlbums = updatedAlbumCount,
            DeletedPhotoAlbums = deletedAlbums.Count,
            UnchangedPhotoAlbums = unchangedAlbumCount,
            CreatedPhotos = createdFileCount,
            UpdatedPhotos = updatedFileCount,
            DeletedPhotos = deletedFiles.Count,
            UnchangedPhotos = unchangedFileCount,
        };
    }

    private static PlexPhotoAlbum CreateAlbum(PlexPhotoAlbum source, PlexLibrary library) =>
        new()
        {
            Id = 0,
            PlexLibraryId = library.Id,
            PlexServerId = library.PlexServerId,
            PlexApiRatingKey = source.PlexApiRatingKey,
            Title = source.Title,
            SearchTitle = source.SearchTitle,
            Summary = source.Summary,
            HasThumb = source.HasThumb,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
        };

    private static PlexPhotoMediaData CreateFile(
        PlexPhotoMediaData source,
        PlexLibrary library,
        PlexPhotoFolder? folder
    ) =>
        new()
        {
            Id = 0,
            PlexLibraryId = library.Id,
            PlexServerId = library.PlexServerId,
            PlexApiRatingKey = source.PlexApiRatingKey,
            PlexApiMediaId = source.PlexApiMediaId,
            PlexApiPartId = source.PlexApiPartId,
            AssetKind = source.AssetKind,
            Title = source.Title,
            SearchTitle = source.SearchTitle,
            Summary = source.Summary,
            HasThumb = source.HasThumb,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            OriginallyAvailableAt = source.OriginallyAvailableAt,
            SourcePath = source.SourcePath,
            OriginalFilename = source.OriginalFilename,
            Key = source.Key,
            Size = source.Size,
            Container = source.Container,
            Width = source.Width,
            Height = source.Height,
            Duration = source.Duration,
            VideoCodec = source.VideoCodec,
            AudioCodec = source.AudioCodec,
            PlexPhotoFolder = folder,
            PlexPhotoFolderId = folder?.Id > 0 ? folder.Id : null,
        };

    private static PlexPhotoFolder CreateFolder(PlexPhotoFolder source, PlexLibrary library, PlexPhotoFolder? parent) =>
        new()
        {
            Id = 0,
            PlexLibraryId = library.Id,
            PlexServerId = library.PlexServerId,
            Parent = parent,
            RelativePath = source.RelativePath,
            Title = source.Title,
            RootIdentityKey = parent is null ? source.RootIdentityKey : null,
            SourceRootPath = parent is null ? source.SourceRootPath : null,
            PlexApiLocationId = parent is null ? source.PlexApiLocationId : null,
            IsActive = parent is null ? true : null,
        };

    private static void ApplyFolder(PlexPhotoFolder target, PlexPhotoFolder source, PlexLibrary library, PlexPhotoFolder? parent)
    {
        target.PlexLibraryId = library.Id;
        target.PlexServerId = library.PlexServerId;
        target.Parent = parent;
        target.ParentId = parent?.Id > 0 ? parent.Id : null;
        target.RelativePath = source.RelativePath;
        target.Title = source.Title;
        target.RootIdentityKey = parent is null ? source.RootIdentityKey : null;
        target.SourceRootPath = parent is null ? source.SourceRootPath : null;
        target.PlexApiLocationId = parent is null ? source.PlexApiLocationId : null;
        target.IsActive = parent is null ? true : null;
    }

    private static Dictionary<FolderKey, PlexPhotoFolder> BuildFolderMap(IReadOnlyCollection<PlexPhotoFolder> folders)
    {
        var byId = folders.ToDictionary(x => x.Id);
        var byKey = new Dictionary<FolderKey, PlexPhotoFolder>(folders.Count);
        foreach (var folder in folders)
        {
            var root = folder;
            while (root.ParentId is { } parentId)
                root = byId[parentId];
            byKey.Add(new FolderKey(root.RootIdentityKey!, folder.RelativePath), folder);
        }
        return byKey;
    }

    private static Dictionary<FolderKey, PlexPhotoFolder> BuildIncomingFolderMap(ICollection<PlexPhotoFolder> folders)
    {
        var byKey = new Dictionary<FolderKey, PlexPhotoFolder>(folders.Count);
        foreach (var folder in folders)
            byKey.Add(GetIncomingFolderKey(folder), folder);
        return byKey;
    }

    private static FolderKey GetIncomingFolderKey(PlexPhotoFolder folder)
    {
        var root = folder;
        while (root.Parent is { } parent)
            root = parent;
        return new FolderKey(root.RootIdentityKey!, folder.RelativePath);
    }

    private readonly record struct FolderKey(string RootIdentity, string RelativePath);
}

public record CrudPhotosReport
{
    public int CreatedPhotoAlbums { get; set; }
    public int UpdatedPhotoAlbums { get; set; }
    public int DeletedPhotoAlbums { get; set; }
    public int UnchangedPhotoAlbums { get; set; }
    public int CreatedPhotos { get; set; }
    public int UpdatedPhotos { get; set; }
    public int DeletedPhotos { get; set; }
    public int UnchangedPhotos { get; set; }
    public int ChangedItemCount =>
        CreatedPhotoAlbums + UpdatedPhotoAlbums + DeletedPhotoAlbums + CreatedPhotos + UpdatedPhotos + DeletedPhotos;
}
