namespace Reaparr.Application;

internal static class PhotoBrowseQuerySupport
{
    public static int GetSkip(int page, int pageSize) => checked((page - 1) * pageSize);

    public static string[] GetSearchTerms(string query)
    {
        var normalized = query.ToSearchTitle();
        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length >= 2)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Loads the requested assets with scoped server/library labels and folder-root mapping.
    /// The returned DTOs preserve the supplied asset ID order.
    /// </summary>
    internal static async Task<List<PhotoAssetBrowseDTO>> LoadAssetItemsAsync(
        IReaparrDbContext context,
        IReadOnlyList<int> assetIds,
        CancellationToken cancellationToken
    )
    {
        var rows = await context
            .PlexPhotoMediaData.Where(x => assetIds.Contains(x.Id))
            .Select(x => new
            {
                Asset = x,
                PlexServerName = x.PlexServer!.Name,
                PlexLibraryTitle = x.PlexLibrary!.Title,
                Folder = x.PlexPhotoFolder,
            })
            .ToListAsync(cancellationToken);
        var rowsById = rows.ToDictionary(x => x.Asset.Id);
        var rootByFolderId = new Dictionary<int, PlexPhotoFolder>();
        foreach (
            var scopeRows in rows.Where(x => x.Asset.PlexPhotoFolderId.HasValue)
                .GroupBy(x => (x.Asset.PlexServerId, x.Asset.PlexLibraryId))
        )
        {
            var roots = await context.ResolvePlexPhotoFolderRootsAsync(
                (ServerId: scopeRows.Key.PlexServerId, LibraryId: scopeRows.Key.PlexLibraryId),
                scopeRows.Select(x => x.Asset.PlexPhotoFolderId!.Value).Distinct().ToArray(),
                cancellationToken
            );
            foreach (var root in roots)
                rootByFolderId[root.Key] = root.Value;
        }

        return assetIds
            .Where(rowsById.ContainsKey)
            .Select(id =>
            {
                var row = rowsById[id];
                var root =
                    row.Asset.PlexPhotoFolderId is { } folderId
                    && rootByFolderId.TryGetValue(folderId, out var resolvedRoot)
                        ? resolvedRoot
                        : null;
                return ToAssetDTO(row.Asset, (row.PlexServerName, row.PlexLibraryTitle), row.Folder, root);
            })
            .ToList();
    }

    public static PhotoAssetBrowseDTO ToAssetDTO(
        PlexPhotoMediaData asset,
        (string ServerName, string LibraryTitle) labels,
        PlexPhotoFolder? folder,
        PlexPhotoFolder? root
    ) =>
        new()
        {
            Id = asset.Id,
            PlexServerId = asset.PlexServerId,
            PlexLibraryId = asset.PlexLibraryId,
            PlexServerName = labels.ServerName,
            PlexLibraryTitle = labels.LibraryTitle,
            Title = asset.Title,
            AssetKind = asset.AssetKind,
            HasThumbnail = asset.HasThumb,
            PlexApiRatingKey = asset.PlexApiRatingKey,
            PlexApiMetaDataKey = asset.PlexApiRatingKey,
            OriginalFilename = asset.OriginalFilename,
            Size = asset.Size,
            Container = asset.Container,
            Width = asset.Width,
            Height = asset.Height,
            Duration = asset.Duration,
            RelativeFolderPath = folder?.RelativePath,
            SourceRootTitle = root?.Title,
        };

    public static PhotoAlbumBrowseDTO ToAlbumDTO(
        PlexPhotoAlbum album,
        (string ServerName, string LibraryTitle) labels,
        PhotoAlbumMetrics? metrics
    ) =>
        new()
        {
            Id = album.Id,
            PlexServerId = album.PlexServerId,
            PlexLibraryId = album.PlexLibraryId,
            PlexServerName = labels.ServerName,
            PlexLibraryTitle = labels.LibraryTitle,
            Title = album.Title,
            HasThumbnail = album.HasThumb,
            PlexApiRatingKey = album.PlexApiRatingKey,
            PlexApiMetaDataKey = album.PlexApiRatingKey,
            AssetCount = metrics?.AssetCount ?? 0,
            AssetBytes = metrics?.AssetBytes ?? 0L,
        };

    public static PhotoFolderBrowseDTO ToFolderDTO(PlexPhotoFolder folder, PhotoFolderTotals totals) =>
        new()
        {
            Id = folder.Id,
            PlexServerId = folder.PlexServerId,
            PlexLibraryId = folder.PlexLibraryId,
            Title = folder.Title,
            RelativePath = folder.RelativePath,
            ParentId = folder.ParentId,
            IsRoot = folder.ParentId is null,
            IsActive = folder.IsActive,
            RecursiveAssetCount = totals.AssetCount,
            RecursiveAssetBytes = totals.AssetBytes,
        };

    public static PhotoFolderBreadcrumbDTO ToBreadcrumbDTO(PlexPhotoFolder folder) =>
        new()
        {
            Id = folder.Id,
            Title = folder.Title,
            RelativePath = folder.RelativePath,
        };
}

internal sealed record PhotoAlbumMetrics(int AlbumId, int AssetCount, long AssetBytes);
