namespace Reaparr.Data.Contracts;

/// <summary>Photo catalog operations shared by photo browsing and download selection.</summary>
// SQLite otherwise favors a server-wide child scan on sparse catalogs. The recursive parent must
// remain the outer loop, with existing parent/folder foreign-key indexes providing bounded seeks.
public static partial class DbContextExtensions
{
    /// <summary>
    /// Returns the asset count and total file size in bytes for the supplied photo asset query
    /// using a single database aggregate command. An empty query returns zero for both totals.
    /// </summary>
    /// <param name="assets">
    ///     The unpaged asset query, with the caller's visibility, library, folder, album, or search
    ///     filters already applied. Rows are counted as supplied; this method does not deduplicate
    ///     assets or traverse descendant folders.
    /// </param>
    /// <returns>The count and byte total across all matching assets, before paging.</returns>
    public static async Task<(int TotalCount, long TotalBytes)> GetPlexPhotoAssetTotalsAsync(
        this IQueryable<PlexPhotoMediaData> assets
    )
    {
        var aggregate = await assets
            .GroupBy(_ => 1)
            .Select(group => new { TotalCount = group.Count(), TotalBytes = group.Sum(x => (long?)x.Size) })
            .FirstOrDefaultAsync(CancellationToken.None);
        return (aggregate?.TotalCount ?? 0, aggregate?.TotalBytes ?? 0L);
    }

    /// <summary>
    /// Resolves a folder and all mapped descendants in one scoped recursive traversal. The selected
    /// folder is included in <see cref="PhotoFolderSubtree.Folders"/> and directly assigned files are
    /// included in <see cref="PhotoFolderSubtree.Assets"/>.
    /// </summary>
    public static async Task<PhotoFolderSubtree?> ResolvePlexPhotoFolderSubtreeAsync(
        this IReaparrDbContext dbContext,
        (int ServerId, int LibraryId) scope,
        int folderId,
        CancellationToken cancellationToken
    )
    {
        var folders = await LoadSubtreeFoldersAsync(dbContext, scope, folderId, cancellationToken);
        if (folders.Count == 0)
            return null;

        var folder = folders.FirstOrDefault(x => x.Id == folderId);
        if (folder is null)
            return null;

        var root = await ResolveSingleRootAsync(dbContext, scope, folderId, cancellationToken);
        if (root is null || root.IsActive != true)
            return null;

        var folderIds = folders.Select(x => x.Id).ToList();
        var assets = await dbContext
            .PlexPhotoMediaData.Where(x =>
                x.PlexServerId == scope.ServerId
                && x.PlexLibraryId == scope.LibraryId
                && x.PlexPhotoFolderId.HasValue
                && folderIds.Contains(x.PlexPhotoFolderId.Value)
            )
            .ToListAsync(cancellationToken);

        return new PhotoFolderSubtree(folder, root, folders, assets);
    }

    /// <summary>Returns the complete root-to-folder ancestry for one scoped folder.</summary>
    public static async Task<IReadOnlyList<PlexPhotoFolder>> GetPlexPhotoFolderAncestryAsync(
        this IReaparrDbContext dbContext,
        (int ServerId, int LibraryId) scope,
        int folderId,
        CancellationToken cancellationToken
    )
    {
        var format = $$"""
            WITH RECURSIVE ancestors AS (
                SELECT f.*
                FROM "{{nameof(IReaparrDbContext.PlexPhotoFolders)}}" AS f
                WHERE f."{{nameof(PlexPhotoFolder.Id)}}" = {0}
                  AND f."{{nameof(PlexPhotoFolder.PlexServerId)}}" = {1}
                  AND f."{{nameof(PlexPhotoFolder.PlexLibraryId)}}" = {2}
                UNION ALL
                SELECT parent.*
                FROM "{{nameof(IReaparrDbContext.PlexPhotoFolders)}}" AS parent
                INNER JOIN ancestors AS child ON parent."{{nameof(PlexPhotoFolder.Id)}}" = child."{{nameof(
                PlexPhotoFolder.ParentId
            )}}"
                WHERE parent."{{nameof(PlexPhotoFolder.PlexServerId)}}" = {1}
                  AND parent."{{nameof(PlexPhotoFolder.PlexLibraryId)}}" = {2}
            )
            SELECT * FROM ancestors
            """;
        var sql = System.Runtime.CompilerServices.FormattableStringFactory.Create(
            format,
            folderId,
            scope.ServerId,
            scope.LibraryId
        );
        var folders = await dbContext.PlexPhotoFolders.FromSqlInterpolated(sql).ToListAsync(cancellationToken);

        return folders.OrderBy(x => x.RelativePath.Length).ThenBy(x => x.Id).ToList();
    }

    /// <summary>
    /// Resolves the configured source root for every supplied folder in one parameterized
    /// recursive traversal. Missing or orphaned folders are omitted.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, PlexPhotoFolder>> ResolvePlexPhotoFolderRootsAsync(
        this IReaparrDbContext dbContext,
        (int ServerId, int LibraryId) scope,
        IReadOnlyCollection<int> folderIds,
        CancellationToken cancellationToken
    )
    {
        var requestedIds = folderIds.Distinct().ToArray();
        if (requestedIds.Length == 0)
            return new Dictionary<int, PlexPhotoFolder>();

        var sql = CreateRootsQuery(scope, requestedIds);
        var rootRows = await dbContext.SqlQuery<PhotoFolderRootRow>(sql).ToListAsync(cancellationToken);
        return rootRows.ToDictionary(row => row.RequestedFolderId, MapRoot);
    }

    private static async Task<PlexPhotoFolder?> ResolveSingleRootAsync(
        IReaparrDbContext dbContext,
        (int ServerId, int LibraryId) scope,
        int folderId,
        CancellationToken cancellationToken
    )
    {
        var roots = await dbContext.ResolvePlexPhotoFolderRootsAsync(scope, [folderId], cancellationToken);
        return roots.GetValueOrDefault(folderId);
    }

    private static PlexPhotoFolder MapRoot(PhotoFolderRootRow row) =>
        new()
        {
            Id = row.RootId,
            PlexServerId = row.PlexServerId,
            PlexLibraryId = row.PlexLibraryId,
            ParentId = row.ParentId,
            RelativePath = row.RelativePath,
            Title = row.Title,
            RootIdentityKey = row.RootIdentityKey,
            SourceRootPath = row.SourceRootPath,
            PlexApiLocationId = row.PlexApiLocationId,
            IsActive = row.IsActive,
        };

    private static FormattableString CreateRootsQuery(
        (int ServerId, int LibraryId) scope,
        IReadOnlyCollection<int> requestedIds
    )
    {
        var idPlaceholders = string.Join(",", requestedIds.Select((_, index) => $"{{{index + 2}}}"));
        var format = $$"""
            WITH RECURSIVE ancestors AS (
                SELECT f."{{nameof(PlexPhotoFolder.Id)}}" AS "{{nameof(PlexPhotoFolder.Id)}}",
                       f."{{nameof(PlexPhotoFolder.ParentId)}}" AS "{{nameof(PlexPhotoFolder.ParentId)}}",
                       f."{{nameof(PlexPhotoFolder.PlexServerId)}}" AS "{{nameof(PlexPhotoFolder.PlexServerId)}}",
                       f."{{nameof(PlexPhotoFolder.PlexLibraryId)}}" AS "{{nameof(PlexPhotoFolder.PlexLibraryId)}}",
                       f."{{nameof(PlexPhotoFolder.RelativePath)}}" AS "{{nameof(PlexPhotoFolder.RelativePath)}}",
                       f."{{nameof(PlexPhotoFolder.Title)}}" AS "{{nameof(PlexPhotoFolder.Title)}}",
                       f."{{nameof(PlexPhotoFolder.RootIdentityKey)}}" AS "{{nameof(PlexPhotoFolder.RootIdentityKey)}}",
                       f."{{nameof(PlexPhotoFolder.SourceRootPath)}}" AS "{{nameof(PlexPhotoFolder.SourceRootPath)}}",
                       f."{{nameof(PlexPhotoFolder.PlexApiLocationId)}}" AS "{{nameof(PlexPhotoFolder.PlexApiLocationId)}}",
                       f."{{nameof(PlexPhotoFolder.IsActive)}}" AS "{{nameof(PlexPhotoFolder.IsActive)}}",
                       f."{{nameof(PlexPhotoFolder.Id)}}" AS "{{nameof(PhotoFolderRootRow.RequestedFolderId)}}"
                FROM "{{nameof(IReaparrDbContext.PlexPhotoFolders)}}" AS f
                WHERE f."{{nameof(PlexPhotoFolder.PlexServerId)}}" = {0}
                  AND f."{{nameof(PlexPhotoFolder.PlexLibraryId)}}" = {1}
                  AND f."{{nameof(PlexPhotoFolder.Id)}}" IN ({{idPlaceholders}})
                UNION ALL
                SELECT parent."{{nameof(PlexPhotoFolder.Id)}}",
                       parent."{{nameof(PlexPhotoFolder.ParentId)}}",
                       parent."{{nameof(PlexPhotoFolder.PlexServerId)}}",
                       parent."{{nameof(PlexPhotoFolder.PlexLibraryId)}}",
                       parent."{{nameof(PlexPhotoFolder.RelativePath)}}",
                       parent."{{nameof(PlexPhotoFolder.Title)}}",
                       parent."{{nameof(PlexPhotoFolder.RootIdentityKey)}}",
                       parent."{{nameof(PlexPhotoFolder.SourceRootPath)}}",
                       parent."{{nameof(PlexPhotoFolder.PlexApiLocationId)}}",
                       parent."{{nameof(PlexPhotoFolder.IsActive)}}",
                       ancestors."{{nameof(PhotoFolderRootRow.RequestedFolderId)}}"
                FROM "{{nameof(IReaparrDbContext.PlexPhotoFolders)}}" AS parent
                INNER JOIN ancestors ON parent."{{nameof(PlexPhotoFolder.Id)}}" = ancestors."{{nameof(PlexPhotoFolder.ParentId)}}"
                WHERE parent."{{nameof(PlexPhotoFolder.PlexServerId)}}" = {0}
                  AND parent."{{nameof(PlexPhotoFolder.PlexLibraryId)}}" = {1}
            )
            SELECT CAST(root."{{nameof(PhotoFolderRootRow.RequestedFolderId)}}" AS INTEGER) AS "{{nameof(PhotoFolderRootRow.RequestedFolderId)}}",
                   root."{{nameof(PlexPhotoFolder.Id)}}" AS "{{nameof(PhotoFolderRootRow.RootId)}}",
                   root."{{nameof(PlexPhotoFolder.PlexServerId)}}" AS "{{nameof(PhotoFolderRootRow.PlexServerId)}}",
                   root."{{nameof(PlexPhotoFolder.PlexLibraryId)}}" AS "{{nameof(PhotoFolderRootRow.PlexLibraryId)}}",
                   root."{{nameof(PlexPhotoFolder.ParentId)}}" AS "{{nameof(PhotoFolderRootRow.ParentId)}}",
                   root."{{nameof(PlexPhotoFolder.RelativePath)}}" AS "{{nameof(PhotoFolderRootRow.RelativePath)}}",
                   root."{{nameof(PlexPhotoFolder.Title)}}" AS "{{nameof(PhotoFolderRootRow.Title)}}",
                   root."{{nameof(PlexPhotoFolder.RootIdentityKey)}}" AS "{{nameof(PhotoFolderRootRow.RootIdentityKey)}}",
                   root."{{nameof(PlexPhotoFolder.SourceRootPath)}}" AS "{{nameof(PhotoFolderRootRow.SourceRootPath)}}",
                   root."{{nameof(PlexPhotoFolder.PlexApiLocationId)}}" AS "{{nameof(PhotoFolderRootRow.PlexApiLocationId)}}",
                   root."{{nameof(PlexPhotoFolder.IsActive)}}" AS "{{nameof(PhotoFolderRootRow.IsActive)}}"
            FROM ancestors AS root
            WHERE root."{{nameof(PlexPhotoFolder.ParentId)}}" IS NULL
            """;
        var arguments = new object[requestedIds.Count + 2];
        arguments[0] = scope.ServerId;
        arguments[1] = scope.LibraryId;
        var index = 0;
        foreach (var requestedId in requestedIds)
            arguments[index++ + 2] = requestedId;

        return System.Runtime.CompilerServices.FormattableStringFactory.Create(format, arguments);
    }

    /// <summary>
    /// Computes recursive mapped asset totals for every requested folder with one parameterized
    /// recursive SQL statement and an unmapped result row. The result is reduced to totals before
    /// the helper returns.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, PhotoFolderTotals>> GetPlexPhotoFolderRecursiveTotalsAsync(
        this IReaparrDbContext dbContext,
        (int ServerId, int LibraryId) scope,
        IReadOnlyCollection<int> folderIds,
        CancellationToken cancellationToken
    )
    {
        var requestedIds = folderIds.Distinct().ToArray();
        if (requestedIds.Length == 0)
            return new Dictionary<int, PhotoFolderTotals>();

        var idPlaceholders = string.Join(",", requestedIds.Select((_, index) => $"{{{index + 2}}}"));
        var format = $$"""
            WITH RECURSIVE descendants AS (
                SELECT f."{{nameof(PlexPhotoFolder.Id)}}", f."{{nameof(PlexPhotoFolder.ParentId)}}", f."{{nameof(PlexPhotoFolder.Id)}}" AS "{{nameof(PhotoFolderRootRow.RequestedFolderId)}}"
                FROM "{{nameof(IReaparrDbContext.PlexPhotoFolders)}}" AS f
                WHERE f."{{nameof(PlexPhotoFolder.PlexServerId)}}" = {0}
                  AND f."{{nameof(PlexPhotoFolder.PlexLibraryId)}}" = {1}
                  AND f."{{nameof(PlexPhotoFolder.Id)}}" IN ({{idPlaceholders}})
                UNION ALL
                SELECT child."{{nameof(PlexPhotoFolder.Id)}}", child."{{nameof(PlexPhotoFolder.ParentId)}}", descendants."{{nameof(PhotoFolderRootRow.RequestedFolderId)}}"
                FROM descendants
                CROSS JOIN "{{nameof(IReaparrDbContext.PlexPhotoFolders)}}" AS child INDEXED BY "IX_{{nameof(IReaparrDbContext.PlexPhotoFolders)}}_{{nameof(PlexPhotoFolder.ParentId)}}_{{nameof(PlexPhotoFolder.RelativePath)}}"
                  ON child."{{nameof(PlexPhotoFolder.ParentId)}}" = descendants."{{nameof(PlexPhotoFolder.Id)}}"
                WHERE child."{{nameof(PlexPhotoFolder.ParentId)}}" IS NOT NULL
                  AND child."{{nameof(PlexPhotoFolder.PlexServerId)}}" = {0}
                  AND child."{{nameof(PlexPhotoFolder.PlexLibraryId)}}" = {1}
            ),
            aggregates AS (
                SELECT descendants."{{nameof(PhotoFolderRootRow.RequestedFolderId)}}" AS "{{nameof(PhotoFolderTotalsRow.FolderId)}}",
                       COUNT(media."{{nameof(PlexPhotoMediaData.Id)}}") AS "{{nameof(PhotoFolderTotalsRow.AssetCount)}}",
                       COALESCE(SUM(media."{{nameof(PlexPhotoMediaData.Size)}}"), 0) AS "{{nameof(PhotoFolderTotalsRow.AssetBytes)}}"
                FROM descendants
                LEFT JOIN "{{nameof(IReaparrDbContext.PlexPhotoMediaData)}}" AS media INDEXED BY "IX_{{nameof(IReaparrDbContext.PlexPhotoMediaData)}}_{{nameof(PlexPhotoMediaData.PlexPhotoFolderId)}}"
                  ON media."{{nameof(PlexPhotoMediaData.PlexPhotoFolderId)}}" = descendants."{{nameof(PlexPhotoFolder.Id)}}"
                 AND media."{{nameof(PlexPhotoMediaData.PlexServerId)}}" = {0}
                 AND media."{{nameof(PlexPhotoMediaData.PlexLibraryId)}}" = {1}
                GROUP BY descendants."{{nameof(PhotoFolderRootRow.RequestedFolderId)}}"
            )
            SELECT CAST(aggregates."{{nameof(PhotoFolderTotalsRow.FolderId)}}" AS INTEGER) AS "{{nameof(PhotoFolderTotalsRow.FolderId)}}",
                   CAST(aggregates."{{nameof(PhotoFolderTotalsRow.AssetCount)}}" AS INTEGER) AS "{{nameof(PhotoFolderTotalsRow.AssetCount)}}",
                   CAST(aggregates."{{nameof(PhotoFolderTotalsRow.AssetBytes)}}" AS INTEGER) AS "{{nameof(PhotoFolderTotalsRow.AssetBytes)}}"
            FROM aggregates
            """;
        var arguments = new object[requestedIds.Length + 2];
        arguments[0] = scope.ServerId;
        arguments[1] = scope.LibraryId;
        for (var index = 0; index < requestedIds.Length; index++)
            arguments[index + 2] = requestedIds[index];

        var sql = System.Runtime.CompilerServices.FormattableStringFactory.Create(format, arguments);
        var rows = await dbContext.SqlQuery<PhotoFolderTotalsRow>(sql).ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.FolderId, row => new PhotoFolderTotals(row.AssetCount, row.AssetBytes));
    }

    private static async Task<List<PlexPhotoFolder>> LoadSubtreeFoldersAsync(
        IReaparrDbContext dbContext,
        (int ServerId, int LibraryId) scope,
        int folderId,
        CancellationToken cancellationToken
    )
    {
        var format = $$"""
            WITH RECURSIVE descendants AS (
                SELECT f.*
                FROM "{{nameof(IReaparrDbContext.PlexPhotoFolders)}}" AS f
                WHERE f."{{nameof(PlexPhotoFolder.Id)}}" = {0}
                  AND f."{{nameof(PlexPhotoFolder.PlexServerId)}}" = {1}
                  AND f."{{nameof(PlexPhotoFolder.PlexLibraryId)}}" = {2}
                UNION ALL
                SELECT child.*
                FROM descendants AS parent
                CROSS JOIN "{{nameof(IReaparrDbContext.PlexPhotoFolders)}}" AS child INDEXED BY "IX_{{nameof(
                IReaparrDbContext.PlexPhotoFolders
            )}}_{{nameof(PlexPhotoFolder.ParentId)}}_{{nameof(PlexPhotoFolder.RelativePath)}}"
                  ON child."{{nameof(PlexPhotoFolder.ParentId)}}" = parent."{{nameof(PlexPhotoFolder.Id)}}"
                WHERE child."{{nameof(PlexPhotoFolder.ParentId)}}" IS NOT NULL
                  AND child."{{nameof(PlexPhotoFolder.PlexServerId)}}" = {1}
                  AND child."{{nameof(PlexPhotoFolder.PlexLibraryId)}}" = {2}
            )
            SELECT * FROM descendants
            """;
        var sql = System.Runtime.CompilerServices.FormattableStringFactory.Create(
            format,
            folderId,
            scope.ServerId,
            scope.LibraryId
        );
        return await dbContext.PlexPhotoFolders.FromSqlInterpolated(sql).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Applies the same enabled-server, enabled-photo-library, and persisted account-link visibility
    /// policy to every photo browse operation.
    /// </summary>
    public static async Task<Result<PhotoBrowseScope>> ResolvePlexPhotoBrowseScopeAsync(
        this IReaparrDbContext dbContext,
        int? plexLibraryId,
        CancellationToken cancellationToken
    )
    {
        if (plexLibraryId is <= 0)
            return ResultExtensions.Create400BadRequestResult("The photo library ID must be positive.");

        var hasLinkedAccounts = await dbContext.PlexAccounts.AnyAsync(cancellationToken);
        var allowedLibraryIds = await ResolveEligibleLibraryIdsAsync(dbContext, hasLinkedAccounts, cancellationToken);

        if (plexLibraryId is not int requestedLibraryId)
            return Result.Ok(new PhotoBrowseScope(allowedLibraryIds));

        return await ResolveExplicitLibraryAsync(dbContext, requestedLibraryId, allowedLibraryIds, cancellationToken);
    }

    private static async Task<Result<PhotoBrowseScope>> ResolveExplicitLibraryAsync(
        IReaparrDbContext dbContext,
        int requestedLibraryId,
        IReadOnlyList<int> allowedLibraryIds,
        CancellationToken cancellationToken
    )
    {
        var requestedLibrary = await dbContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .Where(x => x.Id == requestedLibraryId && x.Type == PlexMediaType.PhotoAlbum)
            .Select(x => new
            {
                x.Id,
                x.PlexServerId,
                x.IsEnabled,
                HasServer = x.PlexServer != null,
                HasLibraryAccountLink = x.PlexAccountLibraries.Any(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (requestedLibrary is null)
            return ResultExtensions.EntityNotFound(nameof(PlexLibrary), requestedLibraryId);

        var serverEnabled = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == requestedLibrary.PlexServerId)
            .Select(x => (bool?)x.IsEnabled)
            .FirstOrDefaultAsync(cancellationToken);

        if (requestedLibrary.IsEnabled == false || serverEnabled == false)
            return ResultExtensions.Create400BadRequestResult("The requested photo library or server is disabled.");

        if (serverEnabled is null || !requestedLibrary.HasServer)
            return ResultExtensions.EntityNotFound(nameof(PlexLibrary), requestedLibraryId);

        if (!allowedLibraryIds.Contains(requestedLibraryId))
            return ResultExtensions.EntityNotFound(nameof(PlexLibrary), requestedLibraryId);

        return Result.Ok(new PhotoBrowseScope([requestedLibraryId]));
    }

    private static async Task<IReadOnlyList<int>> ResolveEligibleLibraryIdsAsync(
        IReaparrDbContext dbContext,
        bool hasLinkedAccounts,
        CancellationToken cancellationToken
    )
    {
        var libraries = dbContext
            .PlexLibraries.Where(x =>
                x.Type == PlexMediaType.PhotoAlbum
                && x.IsEnabled
                && x.PlexServer!.IsEnabled
                && (!hasLinkedAccounts || (x.PlexServer.PlexAccountServers.Any() && x.PlexAccountLibraries.Any()))
            )
            .Select(x => x.Id);
        return await libraries.ToListAsync(cancellationToken);
    }
}

internal sealed class PhotoFolderRootRow
{
    public int RequestedFolderId { get; set; }
    public int RootId { get; set; }
    public int PlexServerId { get; set; }
    public int PlexLibraryId { get; set; }
    public int? ParentId { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? RootIdentityKey { get; set; }
    public string? SourceRootPath { get; set; }
    public long? PlexApiLocationId { get; set; }
    public bool? IsActive { get; set; }
}

internal sealed class PhotoFolderTotalsRow
{
    public int FolderId { get; set; }
    public int AssetCount { get; set; }
    public long AssetBytes { get; set; }
}

public sealed record PhotoFolderSubtree(
    PlexPhotoFolder Folder,
    PlexPhotoFolder Root,
    IReadOnlyList<PlexPhotoFolder> Folders,
    IReadOnlyList<PlexPhotoMediaData> Assets
);

public sealed record PhotoFolderTotals(int AssetCount, long AssetBytes);

public sealed record PhotoBrowseScope(IReadOnlyList<int> LibraryIds);
