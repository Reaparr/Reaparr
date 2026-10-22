namespace Reaparr.Application;

public sealed record SearchPhotoAssetsQuery(
    string Query,
    int Page = 1,
    int PageSize = MediaQueryFilter.MaximumPageSize,
    string Sort = "title:asc"
) : ICommand<Result<PhotoAssetPageDTO>>;

public sealed class SearchPhotoAssetsQueryValidator : AbstractValidator<SearchPhotoAssetsQuery>
{
    public SearchPhotoAssetsQueryValidator()
    {
        RuleFor(x => x.Query).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x));
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MediaQueryFilter.MaximumPageSize);
        RuleFor(x => x.Sort).Must(PhotoBrowseQuerySupport.IsSupportedSort);
        RuleFor(x => x)
            .Must(x => (long)(x.Page - 1) * x.PageSize <= int.MaxValue)
            .WithMessage("The requested page offset is too large.");
    }
}

public sealed class SearchPhotoAssetsQueryHandler
    : ICommandHandler<SearchPhotoAssetsQuery, Result<PhotoAssetPageDTO>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;

    public SearchPhotoAssetsQueryHandler(IReaparrDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Result<PhotoAssetPageDTO>> ExecuteAsync(
        SearchPhotoAssetsQuery query,
        CancellationToken cancellationToken
    )
    {
        if (PhotoBrowseQuerySupport.GetSearchTerms(query.Query).Length == 0)
            return ResultExtensions.Create400BadRequestResult("Search query must contain an effective title term.");

        using var context = await _dbContextFactory.CreateAsync();
        var scopeResult = await context.ResolvePlexPhotoBrowseScopeAsync(null, cancellationToken);
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
        SearchPhotoAssetsQuery query,
        CancellationToken cancellationToken
    )
    {
        var terms = PhotoBrowseQuerySupport.GetSearchTerms(query.Query);
        var assets = context.PlexPhotoMediaData.Where(x => allowedLibraryIds.Contains(x.PlexLibraryId));
        foreach (var term in terms)
            assets = assets.Where(x => x.SearchTitle.Contains(term));

        var aggregate = await assets
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalCount = group.Count(),
                TotalBytes = group.Sum(x => (long?)x.Size),
            })
            .FirstOrDefaultAsync(cancellationToken);
        var totalCount = aggregate?.TotalCount ?? 0;
        var totalBytes = aggregate?.TotalBytes ?? 0L;
        var ordered = PhotoBrowseQuerySupport.IsDescending(query.Sort)
            ? assets.OrderByDescending(x => x.Title).ThenByDescending(x => x.Id)
            : assets.OrderBy(x => x.Title).ThenBy(x => x.Id);
        var assetIds = await ordered
            .Skip(PhotoBrowseQuerySupport.GetSkip(query.Page, query.PageSize))
            .Take(query.PageSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        return (totalCount, totalBytes, assetIds);
    }

}
