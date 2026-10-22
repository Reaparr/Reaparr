namespace Reaparr.Application;

public sealed record GetPhotoAlbumMembersRequest
{
    [RouteParam]
    public int PlexLibraryId { get; init; }

    [RouteParam]
    public int AlbumId { get; init; }

    [QueryParam, BindFrom("page")]
    public int? Page { get; init; }

    [QueryParam, BindFrom("size")]
    public int? PageSize { get; init; }

    [QueryParam, BindFrom("sort")]
    public string? Sort { get; init; }
}

public sealed class GetPhotoAlbumMembersRequestValidator : Validator<GetPhotoAlbumMembersRequest>
{
    public GetPhotoAlbumMembersRequestValidator()
    {
        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.AlbumId).GreaterThan(0);
        RuleFor(x => x.Page).GreaterThan(0).When(x => x.Page.HasValue);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MediaQueryFilter.MaximumPageSize).When(x => x.PageSize.HasValue);
        RuleFor(x => x.Sort).Must(PhotoBrowseQuerySupport.IsSupportedSort).When(x => x.Sort is not null);
        RuleFor(x => x)
            .Must(x => (long)((x.Page ?? 1) - 1) * (x.PageSize ?? MediaQueryFilter.MaximumPageSize) <= int.MaxValue)
            .WithMessage("The requested page offset is too large.");
    }
}

public sealed class GetPhotoAlbumMembersEndpoint : Endpoint<GetPhotoAlbumMembersRequest, PhotoAssetPageDTO>
{
    private readonly ICommandExecutor _commandExecutor;

    public GetPhotoAlbumMembersEndpoint(ICommandExecutor commandExecutor)
    {
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexPhotoController + "/libraries/{PlexLibraryId}/albums/{AlbumId}/assets");
        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PhotoAssetPageDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(GetPhotoAlbumMembersRequest req, CancellationToken ct)
    {
        var sort = PhotoBrowseQuerySupport.ParseSort(req.Sort);
        var result = await _commandExecutor.Send(
            new GetPhotoAlbumMembersQuery(
                req.PlexLibraryId,
                req.AlbumId,
                req.Page ?? 1,
                req.PageSize ?? MediaQueryFilter.MaximumPageSize,
                sort.Sort,
                sort.Descending
            ),
            ct
        );
        await Send.FluentResult(result, ct);
    }
}
