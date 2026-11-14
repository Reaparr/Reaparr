using FlexQuery.NET;

namespace Reaparr.Application;

public sealed record SearchPhotoAssetsQuery(
    string Query,
    int Page = 1,
    int PageSize = MediaQueryFilter.MaximumPageSize,
    SortType Sort = SortType.Title,
    SortDirection Direction = SortDirection.Asc
) : ICommand<Result<PhotoAssetPageDTO>>;

public sealed class SearchPhotoAssetsQueryValidator : AbstractValidator<SearchPhotoAssetsQuery>
{
    public SearchPhotoAssetsQueryValidator()
    {
        RuleFor(x => x.Query).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x));
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MediaQueryFilter.MaximumPageSize);
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.Sort)
            .Must(sort =>
                sort
                    is SortType.Title
                        or SortType.AddedAt
                        or SortType.UpdatedAt
                        or SortType.Duration
                        or SortType.MediaSize
            )
            .WithMessage("Unsupported photo asset sort.");
        RuleFor(x => x)
            .Must(x => (long)(x.Page - 1) * x.PageSize <= int.MaxValue)
            .WithMessage("The requested page offset is too large.");
    }
}

public sealed class SearchPhotoAssetsQueryHandler : ICommandHandler<SearchPhotoAssetsQuery, Result<PhotoAssetPageDTO>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly ITelemetry _telemetry;

    public SearchPhotoAssetsQueryHandler(IReaparrDbContextFactory dbContextFactory, ITelemetry telemetry)
    {
        _dbContextFactory = dbContextFactory;
        _telemetry = telemetry;
    }

    public async Task<Result<PhotoAssetPageDTO>> ExecuteAsync(
        SearchPhotoAssetsQuery query,
        CancellationToken cancellationToken
    )
    {
        var searchTerms = PhotoBrowseQuerySupport.GetSearchTerms(query.Query);
        if (searchTerms.Length == 0)
        {
            var invalid = ResultExtensions
                .Create400BadRequestResult("Search query must contain an effective title term.")
                .ToResult<PhotoAssetPageDTO>();
            _telemetry.PhotoSearchFinished(ToTelemetryDTO(query), searchTerms, invalid);
            return invalid;
        }

        using var context = await _dbContextFactory.CreateAsync();
        var scopeResult = await context.ResolvePlexPhotoBrowseScopeAsync(null, cancellationToken);
        if (scopeResult.IsCancelledOrFailed)
        {
            var failed = scopeResult.ToResult<PhotoAssetPageDTO>();
            _telemetry.PhotoSearchFinished(ToTelemetryDTO(query), searchTerms, failed);
            return failed;
        }

        var (totalCount, totalBytes, assetIds) = await LoadAssetPageAsync(
            context,
            scopeResult.Value.LibraryIds,
            query,
            searchTerms,
            cancellationToken
        );
        var items = await PhotoBrowseQuerySupport.LoadAssetItemsAsync(context, assetIds, cancellationToken);
        var result = Result.Ok(
            new PhotoAssetPageDTO
            {
                Items = items,
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount,
                TotalBytes = totalBytes,
            }
        );
        _telemetry.PhotoSearchFinished(ToTelemetryDTO(query), searchTerms, result);
        return result;

    }

    private static PhotoSearchTelemetryDTO ToTelemetryDTO(SearchPhotoAssetsQuery query) =>
        new()
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Sort = query.Sort,
            Direction = query.Direction,
        };

    private static async Task<(int TotalCount, long TotalBytes, List<int> AssetIds)> LoadAssetPageAsync(
        IReaparrDbContext context,
        IReadOnlyList<int> allowedLibraryIds,
        SearchPhotoAssetsQuery query,
        IReadOnlyList<string> searchTerms,
        CancellationToken cancellationToken
    )
    {
        var assets = context.PlexPhotoMediaData.Where(x => allowedLibraryIds.Contains(x.PlexLibraryId));
        foreach (var term in searchTerms)
            assets = assets.Where(x => x.SearchTitle.Contains(term));

        var (totalCount, totalBytes) = await assets.GetPlexPhotoAssetTotalsAsync();
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
