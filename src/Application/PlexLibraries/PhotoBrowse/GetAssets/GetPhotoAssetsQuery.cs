using FlexQuery.NET;

namespace Reaparr.Application;

public sealed record GetPhotoAssetsQuery(
    int? PlexLibraryId,
    int Page = 1,
    int PageSize = MediaQueryFilter.MaximumPageSize,
    SortType Sort = SortType.Title,
    SortDirection Direction = SortDirection.Asc,
    bool UnmappedOnly = false
) : ICommand<Result<PhotoAssetPageDTO>>;

public sealed class GetPhotoAssetsQueryValidator : AbstractValidator<GetPhotoAssetsQuery>
{
    public GetPhotoAssetsQueryValidator()
    {
        RuleFor(x => x.PlexLibraryId).GreaterThan(0).When(x => x.PlexLibraryId.HasValue);
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MediaQueryFilter.MaximumPageSize);
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.Sort)
            .Must(sort =>
                sort is SortType.Title
                or SortType.AddedAt
                or SortType.UpdatedAt
                or SortType.Duration
                or SortType.MediaSize
            )
            .WithMessage("Unsupported photo asset sort.");
        RuleFor(x => x)
            .Must(x => !x.UnmappedOnly || x.PlexLibraryId.HasValue)
            .WithMessage("Unmapped-only assets require an explicit photo library.");
        RuleFor(x => x)
            .Must(x => (long)(x.Page - 1) * x.PageSize <= int.MaxValue)
            .WithMessage("The requested page offset is too large.");
    }
}

public sealed class GetPhotoAssetsQueryHandler : ICommandHandler<GetPhotoAssetsQuery, Result<PhotoAssetPageDTO>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;

    public GetPhotoAssetsQueryHandler(IReaparrDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Result<PhotoAssetPageDTO>> ExecuteAsync(
        GetPhotoAssetsQuery query,
        CancellationToken cancellationToken
    )
    {
        using var context = await _dbContextFactory.CreateAsync();
        var scopeResult = await context.ResolvePlexPhotoBrowseScopeAsync(
            query.PlexLibraryId,
            cancellationToken
        );
        if (scopeResult.IsCancelledOrFailed)
            return scopeResult.ToResult<PhotoAssetPageDTO>();

        var (totalCount, totalBytes, assetIds) = await LoadAssetPageAsync(
            context,
            scopeResult.Value.LibraryIds,
            query,
            cancellationToken
        );
        var items = await PhotoBrowseQuerySupport.LoadAssetItemsAsync(
            context,
            assetIds,
            cancellationToken
        );

        return Result.Ok(new PhotoAssetPageDTO
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalBytes = totalBytes,
        });
    }

    private static async Task<(int TotalCount, long TotalBytes, List<int> AssetIds)> LoadAssetPageAsync(
        IReaparrDbContext context,
        IReadOnlyList<int> allowedLibraryIds,
        GetPhotoAssetsQuery query,
        CancellationToken cancellationToken
    )
    {
        var assets = context.PlexPhotoMediaData.Where(x => allowedLibraryIds.Contains(x.PlexLibraryId));
        if (query.UnmappedOnly)
        {
            assets = assets.Where(x =>
                x.PlexLibraryId == query.PlexLibraryId!.Value
                && x.PlexPhotoFolderId == null
            );
        }

        var totalCount = await assets.CountAsync(cancellationToken);
        var totalBytes = await assets.Select(x => (long?)x.Size).SumAsync(cancellationToken) ?? 0L;
        var sortOptions = MediaOverviewExtensions.BuildSortOptions(
            new Sort(query.Sort, query.Direction),
            nameof(PlexMediaSlimDTO.Title),
            nameof(PlexMediaSlimDTO.Id),
            nameof(PlexMediaSlimDTO.MediaSize)
        );
        var ordered = assets.ProjectToMediaSlimDTO().ApplySort(sortOptions);
        var assetIds = await ordered
            .Skip(PhotoBrowseQuerySupport.GetSkip(query.Page, query.PageSize))
            .Take(query.PageSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        return (totalCount, totalBytes, assetIds);
    }

}

