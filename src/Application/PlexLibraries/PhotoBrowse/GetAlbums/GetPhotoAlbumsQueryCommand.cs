using FlexQuery.NET;

namespace Reaparr.Application;

public sealed record GetPhotoAlbumsQueryCommand(
    int? PlexLibraryId,
    int Page = 1,
    int PageSize = MediaQueryFilter.MaximumPageSize,
    SortType Sort = SortType.Title,
    SortDirection Direction = SortDirection.Asc
) : ICommand<Result<PhotoAlbumPageDTO>>;

public sealed class GetPhotoAlbumsQueryCommandValidator : AbstractValidator<GetPhotoAlbumsQueryCommand>
{
    public GetPhotoAlbumsQueryCommandValidator()
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
            .WithMessage("Unsupported photo album sort.");
        RuleFor(x => x)
            .Must(x => (long)(x.Page - 1) * x.PageSize <= int.MaxValue)
            .WithMessage("The requested page offset is too large.");
    }
}

public sealed class GetPhotoAlbumsQueryCommandHandler
    : ICommandHandler<GetPhotoAlbumsQueryCommand, Result<PhotoAlbumPageDTO>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;

    public GetPhotoAlbumsQueryCommandHandler(IReaparrDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Result<PhotoAlbumPageDTO>> ExecuteAsync(
        GetPhotoAlbumsQueryCommand queryCommand,
        CancellationToken cancellationToken
    )
    {
        using var context = await _dbContextFactory.CreateAsync();
        var scopeResult = await context.ResolvePlexPhotoBrowseScopeAsync(queryCommand.PlexLibraryId, cancellationToken);
        if (scopeResult.IsCancelledOrFailed)
            return scopeResult.ToResult<PhotoAlbumPageDTO>();

        var allowed = scopeResult.Value.LibraryIds;
        var albums = context.PlexPhotoAlbums.Where(x => allowed.Contains(x.PlexLibraryId));
        var albumRowsQuery = albums.ProjectToMediaSlimDTO();
        var totalCount = await albums.CountAsync(cancellationToken);
        var albumIds = await LoadAlbumIdsAsync(albumRowsQuery, queryCommand, cancellationToken);
        var totalAssetBytes = await LoadTotalAssetBytesAsync(context, albums, allowed, cancellationToken);
        var metrics = await LoadAlbumMetricsAsync(context, albumIds, cancellationToken);
        var albumRows = await context
            .PlexPhotoAlbums.Where(x => albumIds.Contains(x.Id))
            .Select(x => new
            {
                Album = x,
                PlexServerName = x.PlexServer!.Name,
                PlexLibraryTitle = x.PlexLibrary!.Title,
            })
            .ToListAsync(cancellationToken);
        var albumById = albumRows.ToDictionary(x => x.Album.Id);
        var items = albumIds
            .Where(albumById.ContainsKey)
            .Select(id =>
            {
                var row = albumById[id];
                return PhotoBrowseQuerySupport.ToAlbumDTO(
                    row.Album,
                    (row.PlexServerName, row.PlexLibraryTitle),
                    metrics.GetValueOrDefault(id)
                );
            })
            .ToList();

        return Result.Ok(
            new PhotoAlbumPageDTO
            {
                Items = items,
                Page = queryCommand.Page,
                PageSize = queryCommand.PageSize,
                TotalCount = totalCount,
                TotalAssetBytes = totalAssetBytes,
            }
        );
    }

    private static async Task<List<int>> LoadAlbumIdsAsync(
        IQueryable<PlexMediaSlimDTO> albums,
        GetPhotoAlbumsQueryCommand queryCommand,
        CancellationToken cancellationToken
    )
    {
        var sortOptions = MediaOverviewExtensions.BuildSortOptions(
            new Sort(queryCommand.Sort, queryCommand.Direction),
            nameof(PlexMediaSlimDTO.Title),
            nameof(PlexMediaSlimDTO.Id),
            nameof(PlexMediaSlimDTO.MediaSize)
        );
        var ordered = albums.ApplySort(sortOptions);
        return await ordered
            .Skip(PhotoBrowseQuerySupport.GetSkip(queryCommand.Page, queryCommand.PageSize))
            .Take(queryCommand.PageSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    private static async Task<long> LoadTotalAssetBytesAsync(
        IReaparrDbContext context,
        IQueryable<PlexPhotoAlbum> albums,
        IReadOnlyList<int> allowedLibraryIds,
        CancellationToken cancellationToken
    )
    {
        return await context
                .PlexPhotoMediaData.Where(x =>
                    allowedLibraryIds.Contains(x.PlexLibraryId)
                    && x.AlbumMemberships.Any(membership =>
                        albums.Any(album =>
                            album.Id == membership.PlexPhotoAlbumId
                            && album.PlexLibraryId == x.PlexLibraryId
                            && album.PlexServerId == x.PlexServerId
                        )
                    )
                )
                .Select(x => (long?)x.Size)
                .SumAsync(cancellationToken)
            ?? 0L;
    }

    private static async Task<Dictionary<int, PhotoAlbumMetrics>> LoadAlbumMetricsAsync(
        IReaparrDbContext context,
        IReadOnlyList<int> albumIds,
        CancellationToken cancellationToken
    )
    {
        return await context
            .PlexPhotoAlbumMemberships.Where(x =>
                albumIds.Contains(x.PlexPhotoAlbumId)
                && x.PlexPhotoMediaData!.PlexLibraryId == x.PlexPhotoAlbum!.PlexLibraryId
                && x.PlexPhotoMediaData.PlexServerId == x.PlexPhotoAlbum.PlexServerId
            )
            .GroupBy(x => x.PlexPhotoAlbumId)
            .Select(x => new PhotoAlbumMetrics(
                x.Key,
                x.Select(membership => membership.PlexPhotoMediaDataId).Distinct().Count(),
                x.Select(membership => (long?)membership.PlexPhotoMediaData!.Size).Sum() ?? 0L
            ))
            .ToDictionaryAsync(x => x.AlbumId, cancellationToken);
    }
}
