namespace Reaparr.Application;

public sealed record GetPhotoAssetInspectionQuery(
    int PlexLibraryId,
    int AssetId,
    int MembershipPage = 1,
    int MembershipPageSize = MediaQueryFilter.MaximumPageSize
) : ICommand<Result<PhotoAssetInspectionDTO>>;

public sealed class GetPhotoAssetInspectionQueryValidator
    : AbstractValidator<GetPhotoAssetInspectionQuery>
{
    public GetPhotoAssetInspectionQueryValidator()
    {
        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.AssetId).GreaterThan(0);
        RuleFor(x => x.MembershipPage).GreaterThan(0);
        RuleFor(x => x.MembershipPageSize).InclusiveBetween(1, MediaQueryFilter.MaximumPageSize);
        RuleFor(x => x)
            .Must(x => (long)(x.MembershipPage - 1) * x.MembershipPageSize <= int.MaxValue)
            .WithMessage("The requested membership page offset is too large.");
    }
}

public sealed class GetPhotoAssetInspectionQueryHandler
    : ICommandHandler<GetPhotoAssetInspectionQuery, Result<PhotoAssetInspectionDTO>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;

    public GetPhotoAssetInspectionQueryHandler(IReaparrDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Result<PhotoAssetInspectionDTO>> ExecuteAsync(
        GetPhotoAssetInspectionQuery query,
        CancellationToken cancellationToken
    )
    {
        using var context = await _dbContextFactory.CreateAsync();
        var scopeResult = await context.ResolvePlexPhotoBrowseScopeAsync(query.PlexLibraryId, cancellationToken);
        if (scopeResult.IsCancelledOrFailed)
            return scopeResult.ToResult<PhotoAssetInspectionDTO>();

        var asset = await context.PlexPhotoMediaData
            .Where(x => x.Id == query.AssetId && x.PlexLibraryId == query.PlexLibraryId)
            .Select(x => new
            {
                Asset = x,
                PlexServerName = x.PlexServer!.Name,
                PlexLibraryTitle = x.PlexLibrary!.Title,
                Folder = x.PlexPhotoFolder,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (asset is null)
            return ResultExtensions.EntityNotFound(nameof(PlexPhotoMediaData), query.AssetId);

        var breadcrumbs = asset.Folder is null
            ? []
            : await context.GetPlexPhotoFolderAncestryAsync(
                (ServerId: asset.Asset.PlexServerId, LibraryId: query.PlexLibraryId),
                asset.Folder.Id,
                cancellationToken
            );
        var root = breadcrumbs.FirstOrDefault(x => x.ParentId is null);
        var membershipPage = await LoadMembershipPageAsync(context, asset.Asset, query, cancellationToken);
        var albumItems = await LoadAlbumItemsAsync(context, membershipPage.AlbumIds, cancellationToken);

        return Result.Ok(
            BuildInspectionDTO(
                asset.Asset,
                (asset.PlexServerName, asset.PlexLibraryTitle),
                asset.Folder,
                new InspectionContext(
                    breadcrumbs,
                    root,
                    membershipPage,
                    albumItems,
                    query.MembershipPage,
                    query.MembershipPageSize
                )
            )
        );
    }

    private static async Task<MembershipPage> LoadMembershipPageAsync(
        IReaparrDbContext context,
        PlexPhotoMediaData asset,
        GetPhotoAssetInspectionQuery query,
        CancellationToken cancellationToken
    )
    {
        var memberships = context.PlexPhotoAlbumMemberships.Where(x =>
            x.PlexPhotoMediaDataId == asset.Id
            && x.PlexPhotoAlbum!.PlexLibraryId == query.PlexLibraryId
            && x.PlexPhotoAlbum.PlexServerId == asset.PlexServerId
        );
        var totalCount = await memberships.CountAsync(cancellationToken);
        var albumIds = await memberships
            .OrderBy(x => x.PlexPhotoAlbum!.Title)
            .ThenBy(x => x.PlexPhotoAlbumId)
            .Skip(PhotoBrowseQuerySupport.GetSkip(query.MembershipPage, query.MembershipPageSize))
            .Take(query.MembershipPageSize)
            .Select(x => x.PlexPhotoAlbumId)
            .ToListAsync(cancellationToken);
        return new MembershipPage(totalCount, albumIds);
    }

    private static async Task<List<PhotoAlbumSummaryDTO>> LoadAlbumItemsAsync(
        IReaparrDbContext context,
        IReadOnlyList<int> albumIds,
        CancellationToken cancellationToken
    )
    {
        var albums = await context.PlexPhotoAlbums
            .Where(x => albumIds.Contains(x.Id))
            .Select(x => new
            {
                Album = x,
                PlexServerName = x.PlexServer!.Name,
                PlexLibraryTitle = x.PlexLibrary!.Title,
            })
            .ToListAsync(cancellationToken);
        var albumsById = albums.ToDictionary(x => x.Album.Id);
        return albumIds
            .Where(albumsById.ContainsKey)
            .Select(id =>
            {
                var row = albumsById[id];
                return new PhotoAlbumSummaryDTO
                {
                    Id = row.Album.Id,
                    PlexServerId = row.Album.PlexServerId,
                    PlexLibraryId = row.Album.PlexLibraryId,
                    PlexServerName = row.PlexServerName,
                    PlexLibraryTitle = row.PlexLibraryTitle,
                    Title = row.Album.Title,
                    HasThumbnail = row.Album.HasThumb,
                    PlexApiRatingKey = row.Album.PlexApiRatingKey,
                    PlexApiMetaDataKey = row.Album.PlexApiRatingKey,
                };
            })
            .ToList();
    }

    private static PhotoAssetInspectionDTO BuildInspectionDTO(
        PlexPhotoMediaData asset,
        (string ServerName, string LibraryTitle) labels,
        PlexPhotoFolder? folder,
        InspectionContext details
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
            Container = asset.Container,
            Size = asset.Size,
            Width = asset.Width,
            Height = asset.Height,
            Duration = asset.Duration,
            VideoCodec = asset.VideoCodec,
            AudioCodec = asset.AudioCodec,
            Summary = asset.Summary,
            AddedAt = asset.AddedAt,
            UpdatedAt = asset.UpdatedAt,
            OriginallyAvailableAt = asset.OriginallyAvailableAt,
            RelativeFolderPath = folder?.RelativePath,
            SourceRootTitle = details.Root?.Title,
            Breadcrumbs = details.Breadcrumbs.Select(PhotoBrowseQuerySupport.ToBreadcrumbDTO).ToList(),
            Albums = new PhotoAlbumSummaryPageDTO
            {
                Items = details.AlbumItems,
                Page = details.Page,
                PageSize = details.PageSize,
                TotalCount = details.MembershipPage.TotalCount,
            },
        };

    private sealed record InspectionContext(
        IReadOnlyList<PlexPhotoFolder> Breadcrumbs,
        PlexPhotoFolder? Root,
        MembershipPage MembershipPage,
        List<PhotoAlbumSummaryDTO> AlbumItems,
        int Page,
        int PageSize
    );

    private sealed record MembershipPage(int TotalCount, IReadOnlyList<int> AlbumIds);
}
