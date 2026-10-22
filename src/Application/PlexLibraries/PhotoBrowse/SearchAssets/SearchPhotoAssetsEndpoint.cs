namespace Reaparr.Application;

public sealed record SearchPhotoAssetsRequest
{
    [QueryParam, BindFrom("q")]
    public string? Query { get; init; }

    [QueryParam, BindFrom("page")]
    public int? Page { get; init; }

    [QueryParam, BindFrom("size")]
    public int? PageSize { get; init; }

    [QueryParam, BindFrom("sort")]
    public string? Sort { get; init; }
}

public sealed class SearchPhotoAssetsRequestValidator : Validator<SearchPhotoAssetsRequest>
{
    public SearchPhotoAssetsRequestValidator()
    {
        RuleFor(x => x.Query).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x));
        RuleFor(x => x.Page).GreaterThan(0).When(x => x.Page.HasValue);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MediaQueryFilter.MaximumPageSize).When(x => x.PageSize.HasValue);
        RuleFor(x => x)
            .Must(x => (long)((x.Page ?? 1) - 1) * (x.PageSize ?? MediaQueryFilter.MaximumPageSize) <= int.MaxValue)
            .WithMessage("The requested page offset is too large.");
    }
}

public sealed class SearchPhotoAssetsEndpoint : Endpoint<SearchPhotoAssetsRequest, PhotoAssetPageDTO>
{
    private readonly ICommandExecutor _commandExecutor;

    public SearchPhotoAssetsEndpoint(ICommandExecutor commandExecutor)
    {
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexPhotoController + "/assets/search");
        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PhotoAssetPageDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(SearchPhotoAssetsRequest req, CancellationToken ct)
    {
        var sort = req.Sort.ParsePhotoSort();
        var result = await _commandExecutor.Send(
            new SearchPhotoAssetsQuery(
                req.Query ?? string.Empty,
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
