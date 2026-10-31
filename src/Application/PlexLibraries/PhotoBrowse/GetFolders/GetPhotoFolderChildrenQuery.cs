using FlexQuery.NET;

namespace Reaparr.Application;

public sealed record GetPhotoFolderChildrenQuery(
    int PlexLibraryId,
    int FolderId,
    int FolderPage = 1,
    int FolderPageSize = MediaQueryFilter.MaximumPageSize,
    int AssetPage = 1,
    int AssetPageSize = MediaQueryFilter.MaximumPageSize,
    SortType Sort = SortType.Title,
    SortDirection Direction = SortDirection.Asc
) : ICommand<Result<PhotoFolderChildrenDTO>>;

public sealed class GetPhotoFolderChildrenQueryValidator : AbstractValidator<GetPhotoFolderChildrenQuery>
{
    public GetPhotoFolderChildrenQueryValidator()
    {
        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.FolderId).GreaterThan(0);
        RuleFor(x => x.FolderPage).GreaterThan(0);
        RuleFor(x => x.FolderPageSize).InclusiveBetween(1, MediaQueryFilter.MaximumPageSize);
        RuleFor(x => x.AssetPage).GreaterThan(0);
        RuleFor(x => x.AssetPageSize).InclusiveBetween(1, MediaQueryFilter.MaximumPageSize);
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.Sort).Equal(SortType.Title).WithMessage("Photo folders support title sorting only.");
        RuleFor(x => x)
            .Must(x =>
                (long)(x.FolderPage - 1) * x.FolderPageSize <= int.MaxValue
                && (long)(x.AssetPage - 1) * x.AssetPageSize <= int.MaxValue
            )
            .WithMessage("The requested page offset is too large.");
    }
}

public sealed class GetPhotoFolderChildrenQueryHandler
    : ICommandHandler<GetPhotoFolderChildrenQuery, Result<PhotoFolderChildrenDTO>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;

    public GetPhotoFolderChildrenQueryHandler(IReaparrDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Result<PhotoFolderChildrenDTO>> ExecuteAsync(
        GetPhotoFolderChildrenQuery query,
        CancellationToken cancellationToken
    )
    {
        using var context = await _dbContextFactory.CreateAsync();
        var scopeResult = await context.ResolvePlexPhotoBrowseScopeAsync(query.PlexLibraryId, cancellationToken);
        if (scopeResult.IsCancelledOrFailed)
            return scopeResult.ToResult<PhotoFolderChildrenDTO>();

        var folderContext = await LoadFolderContextAsync(context, query, cancellationToken);
        if (folderContext is null)
            return ResultExtensions.EntityNotFound(nameof(PlexPhotoFolder), query.FolderId);

        var folderTotals = await context.GetPlexPhotoFolderRecursiveTotalsAsync(
            (ServerId: folderContext.Folder.PlexServerId, LibraryId: query.PlexLibraryId),
            [folderContext.Folder.Id],
            cancellationToken
        );
        var childPage = await LoadChildFolderPageAsync(context, folderContext.Folder, query, cancellationToken);
        var assetPage = await LoadDirectAssetPageAsync(context, folderContext.Folder, query, cancellationToken);
        var assetItems = await LoadAssetItemsAsync(context, assetPage.AssetIds, folderContext.Root, cancellationToken);

        var folderDTO = PhotoBrowseQuerySupport.ToFolderDTO(
            folderContext.Folder,
            folderTotals.GetValueOrDefault(folderContext.Folder.Id) ?? new PhotoFolderTotals(0, 0L)
        );
        var folderItems = childPage
            .Rows.Where(row => childPage.Entities.ContainsKey(row.Id))
            .Select(row =>
                PhotoBrowseQuerySupport.ToFolderDTO(
                    childPage.Entities[row.Id],
                    childPage.Totals.GetValueOrDefault(row.Id) ?? new PhotoFolderTotals(0, 0L)
                )
            )
            .ToList();

        return Result.Ok(
            new PhotoFolderChildrenDTO
            {
                Folder = folderDTO,
                Breadcrumbs = folderContext.Breadcrumbs.Select(PhotoBrowseQuerySupport.ToBreadcrumbDTO).ToList(),
                Folders = new PhotoFolderPageDTO
                {
                    Items = folderItems,
                    Page = query.FolderPage,
                    PageSize = query.FolderPageSize,
                    TotalCount = childPage.TotalCount,
                },
                Assets = new PhotoAssetPageDTO
                {
                    Items = assetItems,
                    Page = query.AssetPage,
                    PageSize = query.AssetPageSize,
                    TotalCount = assetPage.TotalCount,
                    TotalBytes = assetPage.TotalBytes,
                },
            }
        );
    }

    private async Task<FolderContext?> LoadFolderContextAsync(
        IReaparrDbContext context,
        GetPhotoFolderChildrenQuery query,
        CancellationToken cancellationToken
    )
    {
        var serverId = await context
            .PlexLibraries.Where(x => x.Id == query.PlexLibraryId)
            .Select(x => x.PlexServerId)
            .FirstAsync(cancellationToken);
        var folder = await context.PlexPhotoFolders.FirstOrDefaultAsync(
            x => x.Id == query.FolderId && x.PlexServerId == serverId && x.PlexLibraryId == query.PlexLibraryId,
            cancellationToken
        );
        if (folder is null)
            return null;

        var breadcrumbs = await context.GetPlexPhotoFolderAncestryAsync(
            (ServerId: folder.PlexServerId, LibraryId: query.PlexLibraryId),
            query.FolderId,
            cancellationToken
        );
        var root = breadcrumbs.FirstOrDefault(x => x.ParentId is null);
        return root is null || root.IsActive != true ? null : new FolderContext(folder, root, breadcrumbs);
    }

    private async Task<ChildFolderPage> LoadChildFolderPageAsync(
        IReaparrDbContext context,
        PlexPhotoFolder folder,
        GetPhotoFolderChildrenQuery query,
        CancellationToken cancellationToken
    )
    {
        var childFolders = context.PlexPhotoFolders.Where(x =>
            x.PlexServerId == folder.PlexServerId && x.PlexLibraryId == query.PlexLibraryId && x.ParentId == folder.Id
        );
        var folderCount = await childFolders.CountAsync(cancellationToken);
        var folderOptions = MediaOverviewExtensions.BuildSortOptions(
            new Sort(query.Sort, query.Direction),
            nameof(PlexPhotoFolder.Title),
            nameof(PlexPhotoFolder.Id)
        );
        var orderedFolders = childFolders.ApplySort(folderOptions);
        var rows = await orderedFolders
            .Skip(PhotoBrowseQuerySupport.GetSkip(query.FolderPage, query.FolderPageSize))
            .Take(query.FolderPageSize)
            .Select(x => new { x.Id, x.PlexServerId })
            .ToListAsync(cancellationToken);
        var entities = await context
            .PlexPhotoFolders.Where(x => rows.Select(row => row.Id).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var totals = new Dictionary<int, PhotoFolderTotals>();
        foreach (var group in rows.GroupBy(x => x.PlexServerId))
        {
            var values = await context.GetPlexPhotoFolderRecursiveTotalsAsync(
                (ServerId: group.Key, LibraryId: query.PlexLibraryId),
                group.Select(x => x.Id).ToArray(),
                cancellationToken
            );
            foreach (var value in values)
                totals[value.Key] = value.Value;
        }

        return new ChildFolderPage(
            folderCount,
            rows.Select(row => new FolderPageRow(row.Id, row.PlexServerId)).ToList(),
            entities,
            totals
        );
    }

    private static async Task<DirectAssetPage> LoadDirectAssetPageAsync(
        IReaparrDbContext context,
        PlexPhotoFolder folder,
        GetPhotoFolderChildrenQuery query,
        CancellationToken cancellationToken
    )
    {
        var directAssets = context.PlexPhotoMediaData.Where(x =>
            x.PlexServerId == folder.PlexServerId
            && x.PlexLibraryId == query.PlexLibraryId
            && x.PlexPhotoFolderId == folder.Id
        );
        var (assetCount, assetBytes) = await directAssets.GetPlexPhotoAssetTotalsAsync();
        var assetOptions = MediaOverviewExtensions.BuildSortOptions(
            new Sort(query.Sort, query.Direction),
            nameof(PlexMediaSlimDTO.Title),
            nameof(PlexMediaSlimDTO.Id),
            nameof(PlexMediaSlimDTO.MediaSize)
        );
        var orderedAssets = directAssets.ProjectToMediaSlimDTO().ApplySort(assetOptions);
        var assetIds = await orderedAssets
            .Skip(PhotoBrowseQuerySupport.GetSkip(query.AssetPage, query.AssetPageSize))
            .Take(query.AssetPageSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        return new DirectAssetPage(assetCount, assetBytes, assetIds);
    }

    private static async Task<List<PhotoAssetBrowseDTO>> LoadAssetItemsAsync(
        IReaparrDbContext context,
        IReadOnlyList<int> assetIds,
        PlexPhotoFolder root,
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
        return assetIds
            .Where(rowsById.ContainsKey)
            .Select(id =>
            {
                var row = rowsById[id];
                return PhotoBrowseQuerySupport.ToAssetDTO(
                    row.Asset,
                    (row.PlexServerName, row.PlexLibraryTitle),
                    row.Folder,
                    root
                );
            })
            .ToList();
    }

    private sealed record FolderContext(
        PlexPhotoFolder Folder,
        PlexPhotoFolder Root,
        IReadOnlyList<PlexPhotoFolder> Breadcrumbs
    );

    private sealed record FolderPageRow(int Id, int PlexServerId);

    private sealed record ChildFolderPage(
        int TotalCount,
        IReadOnlyList<FolderPageRow> Rows,
        IReadOnlyDictionary<int, PlexPhotoFolder> Entities,
        IReadOnlyDictionary<int, PhotoFolderTotals> Totals
    );

    private sealed record DirectAssetPage(int TotalCount, long TotalBytes, IReadOnlyList<int> AssetIds);
}
