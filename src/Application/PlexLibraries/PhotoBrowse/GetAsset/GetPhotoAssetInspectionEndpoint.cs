namespace Reaparr.Application;

public sealed record GetPhotoAssetInspectionRequest
{
    [RouteParam]
    public int PlexLibraryId { get; init; }

    [RouteParam]
    public int AssetId { get; init; }

    [QueryParam, BindFrom("membershipPage")]
    public int? MembershipPage { get; init; }

    [QueryParam, BindFrom("membershipSize")]
    public int? MembershipPageSize { get; init; }
}

public sealed class GetPhotoAssetInspectionRequestValidator : Validator<GetPhotoAssetInspectionRequest>
{
    public GetPhotoAssetInspectionRequestValidator()
    {
        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.AssetId).GreaterThan(0);
        RuleFor(x => x.MembershipPage).GreaterThan(0).When(x => x.MembershipPage.HasValue);
        RuleFor(x => x.MembershipPageSize)
            .InclusiveBetween(1, MediaQueryFilter.MaximumPageSize)
            .When(x => x.MembershipPageSize.HasValue);
        RuleFor(x => x)
            .Must(x =>
                (long)((x.MembershipPage ?? 1) - 1) * (x.MembershipPageSize ?? MediaQueryFilter.MaximumPageSize)
                <= int.MaxValue
            )
            .WithMessage("The requested membership page offset is too large.");
    }
}

public sealed class GetPhotoAssetInspectionEndpoint : Endpoint<GetPhotoAssetInspectionRequest, PhotoAssetInspectionDTO>
{
    private readonly ICommandExecutor _commandExecutor;

    public GetPhotoAssetInspectionEndpoint(ICommandExecutor commandExecutor)
    {
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexPhotoController + "/libraries/{PlexLibraryId}/assets/{AssetId}");
        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PhotoAssetInspectionDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(GetPhotoAssetInspectionRequest req, CancellationToken ct)
    {
        var result = await _commandExecutor.Send(
            new GetPhotoAssetInspectionQuery(
                req.PlexLibraryId,
                req.AssetId,
                req.MembershipPage ?? 1,
                req.MembershipPageSize ?? MediaQueryFilter.MaximumPageSize
            ),
            ct
        );
        await Send.FluentResult(result, ct);
    }
}
