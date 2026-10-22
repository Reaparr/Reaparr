namespace Reaparr.Application;

public sealed record GetPhotoAlbumMembersQuery(
    int PlexLibraryId,
    int AlbumId,
    int Page = 1,
    int PageSize = MediaQueryFilter.MaximumPageSize,
    SortType Sort = SortType.Title,
    bool Descending = false
) : ICommand<Result<PhotoAssetPageDTO>>;

public sealed class GetPhotoAlbumMembersQueryValidator : AbstractValidator<GetPhotoAlbumMembersQuery>
{
    public GetPhotoAlbumMembersQueryValidator()
    {
        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.AlbumId).GreaterThan(0);
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MediaQueryFilter.MaximumPageSize);
        RuleFor(x => x.Sort).Equal(SortType.Title);
        RuleFor(x => x)
            .Must(x => (long)(x.Page - 1) * x.PageSize <= int.MaxValue)
            .WithMessage("The requested page offset is too large.");
    }
}

public sealed class GetPhotoAlbumMembersQueryHandler
    : ICommandHandler<GetPhotoAlbumMembersQuery, Result<PhotoAssetPageDTO>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;

    public GetPhotoAlbumMembersQueryHandler(IReaparrDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Result<PhotoAssetPageDTO>> ExecuteAsync(
        GetPhotoAlbumMembersQuery query,
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

        var libraryServerId = await context.PlexLibraries
            .Where(library => library.Id == query.PlexLibraryId)
            .Select(library => library.PlexServerId)
            .FirstAsync(cancellationToken);
        var albumExists = await context.PlexPhotoAlbums.AnyAsync(
            x =>
                x.Id == query.AlbumId
                && x.PlexLibraryId == query.PlexLibraryId
                && x.PlexServerId == libraryServerId,
            cancellationToken
        );
        if (!albumExists)
            return ResultExtensions.EntityNotFound(nameof(PlexPhotoAlbum), query.AlbumId);

        var (totalCount, totalBytes, assetIds) = await LoadMemberPageAsync(
            context,
            query,
            libraryServerId,
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

    private static async Task<(int TotalCount, long TotalBytes, List<int> AssetIds)> LoadMemberPageAsync(
        IReaparrDbContext context,
        GetPhotoAlbumMembersQuery query,
        int libraryServerId,
        CancellationToken cancellationToken
    )
    {
        var memberships = context.PlexPhotoAlbumMemberships.Where(x =>
            x.PlexPhotoAlbumId == query.AlbumId
            && x.PlexPhotoMediaData!.PlexLibraryId == query.PlexLibraryId
            && x.PlexPhotoMediaData.PlexServerId == libraryServerId
        );
        var totalCount = await memberships.Select(x => x.PlexPhotoMediaDataId).Distinct().CountAsync(cancellationToken);
        var totalBytes = await memberships
            .Select(x => (long?)x.PlexPhotoMediaData!.Size)
            .SumAsync(cancellationToken) ?? 0L;
        var ordered = query.Descending
            ? memberships.OrderByDescending(x => x.PlexPhotoMediaData!.Title).ThenByDescending(x => x.PlexPhotoMediaDataId)
            : memberships.OrderBy(x => x.PlexPhotoMediaData!.Title).ThenBy(x => x.PlexPhotoMediaDataId);
        var assetIds = await ordered
            .Skip(PhotoBrowseQuerySupport.GetSkip(query.Page, query.PageSize))
            .Take(query.PageSize)
            .Select(x => x.PlexPhotoMediaDataId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return (totalCount, totalBytes, assetIds);
    }

}
