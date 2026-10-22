namespace Reaparr.Application;

public sealed record GetPhotoSourceRootsQueryCommand(
    int PlexLibraryId,
    int Page = 1,
    int PageSize = MediaQueryFilter.MaximumPageSize,
    SortType Sort = SortType.Title,
    bool Descending = false
) : ICommand<Result<PhotoFolderPageDTO>>;

public sealed class GetPhotoSourceRootsQueryCommandValidator : AbstractValidator<GetPhotoSourceRootsQueryCommand>
{
    public GetPhotoSourceRootsQueryCommandValidator()
    {
        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MediaQueryFilter.MaximumPageSize);
        RuleFor(x => x.Sort).Equal(SortType.Title);
        RuleFor(x => x)
            .Must(x => (long)(x.Page - 1) * x.PageSize <= int.MaxValue)
            .WithMessage("The requested page offset is too large.");
    }
}

public sealed class GetPhotoSourceRootsQueryCommandHandler
    : ICommandHandler<GetPhotoSourceRootsQueryCommand, Result<PhotoFolderPageDTO>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;

    public GetPhotoSourceRootsQueryCommandHandler(IReaparrDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Result<PhotoFolderPageDTO>> ExecuteAsync(
        GetPhotoSourceRootsQueryCommand queryCommand,
        CancellationToken cancellationToken
    )
    {
        using var context = await _dbContextFactory.CreateAsync();
        var scopeResult = await context.ResolvePlexPhotoBrowseScopeAsync(queryCommand.PlexLibraryId, cancellationToken);
        if (scopeResult.IsCancelledOrFailed)
            return scopeResult.ToResult<PhotoFolderPageDTO>();

        var roots = context.PlexPhotoFolders.Where(x =>
            x.PlexLibraryId == queryCommand.PlexLibraryId && x.ParentId == null && x.IsActive == true
        );
        var totalCount = await roots.CountAsync(cancellationToken);
        var ordered = queryCommand.Descending
            ? roots.OrderByDescending(x => x.Title).ThenByDescending(x => x.Id)
            : roots.OrderBy(x => x.Title).ThenBy(x => x.Id);
        var rootRows = await ordered
            .Skip(PhotoBrowseQuerySupport.GetSkip(queryCommand.Page, queryCommand.PageSize))
            .Take(queryCommand.PageSize)
            .Select(x => new { x.Id, x.PlexServerId })
            .ToListAsync(cancellationToken);
        var rootIds = rootRows.Select(x => x.Id).ToList();
        var rootEntities = await context
            .PlexPhotoFolders.Where(x => rootIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var totals = new Dictionary<int, PhotoFolderTotals>();
        foreach (var group in rootRows.GroupBy(x => x.PlexServerId))
        {
            var values = await context.GetPlexPhotoFolderRecursiveTotalsAsync(
                (ServerId: group.Key, LibraryId: queryCommand.PlexLibraryId),
                group.Select(x => x.Id).ToArray(),
                cancellationToken
            );
            foreach (var value in values)
                totals[value.Key] = value.Value;
        }

        var items = rootIds
            .Where(rootEntities.ContainsKey)
            .Select(id =>
                PhotoBrowseQuerySupport.ToFolderDTO(
                    rootEntities[id],
                    totals.GetValueOrDefault(id) ?? new PhotoFolderTotals(0, 0L)
                )
            )
            .ToList();
        return Result.Ok(
            new PhotoFolderPageDTO
            {
                Items = items,
                Page = queryCommand.Page,
                PageSize = queryCommand.PageSize,
                TotalCount = totalCount,
            }
        );
    }
}
