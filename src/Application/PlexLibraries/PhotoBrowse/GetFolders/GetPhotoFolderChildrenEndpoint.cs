namespace Reaparr.Application;

public sealed record GetPhotoFolderChildrenRequest
{
    [RouteParam]
    public int PlexLibraryId { get; init; }

    [RouteParam]
    public int FolderId { get; init; }

    [QueryParam, BindFrom("folderPage")]
    public int? FolderPage { get; init; }

    [QueryParam, BindFrom("folderSize")]
    public int? FolderPageSize { get; init; }

    [QueryParam, BindFrom("assetPage")]
    public int? AssetPage { get; init; }

    [QueryParam, BindFrom("assetSize")]
    public int? AssetPageSize { get; init; }

    [QueryParam, BindFrom("sort")]
    public string? Sort { get; init; }
}

public sealed class GetPhotoFolderChildrenRequestValidator : Validator<GetPhotoFolderChildrenRequest>
{
    public GetPhotoFolderChildrenRequestValidator()
    {
        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.FolderId).GreaterThan(0);
        RuleFor(x => x.FolderPage).GreaterThan(0).When(x => x.FolderPage.HasValue);
        RuleFor(x => x.FolderPageSize)
            .InclusiveBetween(1, MediaQueryFilter.MaximumPageSize)
            .When(x => x.FolderPageSize.HasValue);
        RuleFor(x => x.AssetPage).GreaterThan(0).When(x => x.AssetPage.HasValue);
        RuleFor(x => x.AssetPageSize)
            .InclusiveBetween(1, MediaQueryFilter.MaximumPageSize)
            .When(x => x.AssetPageSize.HasValue);
        RuleFor(x => x)
            .Must(x =>
                (long)((x.FolderPage ?? 1) - 1) * (x.FolderPageSize ?? MediaQueryFilter.MaximumPageSize) <= int.MaxValue
                && (long)((x.AssetPage ?? 1) - 1) * (x.AssetPageSize ?? MediaQueryFilter.MaximumPageSize)
                    <= int.MaxValue
            )
            .WithMessage("The requested page offset is too large.");
    }
}

public sealed class GetPhotoFolderChildrenEndpoint : Endpoint<GetPhotoFolderChildrenRequest, PhotoFolderChildrenDTO>
{
    private readonly ICommandExecutor _commandExecutor;

    public GetPhotoFolderChildrenEndpoint(ICommandExecutor commandExecutor)
    {
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexPhotoController + "/libraries/{PlexLibraryId}/folders/{FolderId}/children");
        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PhotoFolderChildrenDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(GetPhotoFolderChildrenRequest req, CancellationToken ct)
    {
        var sort = req.Sort.ParsePhotoSort();
        var result = await _commandExecutor.Send(
            new GetPhotoFolderChildrenQuery(
                req.PlexLibraryId,
                req.FolderId,
                req.FolderPage ?? 1,
                req.FolderPageSize ?? MediaQueryFilter.MaximumPageSize,
                req.AssetPage ?? 1,
                req.AssetPageSize ?? MediaQueryFilter.MaximumPageSize,
                sort.Sort,
                sort.Descending
            ),
            ct
        );
        await Send.FluentResult(result, ct);
    }
}
