using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

namespace Reaparr.Application;

public sealed record GetPhotoAssetsRequest
{
    [QueryParam, BindFrom("plexLibraryId")]
    public int? PlexLibraryId { get; init; }

    [QueryParam, BindFrom("page")]
    public int? Page { get; init; }

    [QueryParam, BindFrom("size")]
    public int? PageSize { get; init; }

    [QueryParam, BindFrom("sort")]
    public string? Sort { get; init; }

    [QueryParam, BindFrom("unmappedOnly")]
    public bool? UnmappedOnly { get; init; }
}

public sealed class GetPhotoAssetsRequestValidator : Validator<GetPhotoAssetsRequest>
{
    public GetPhotoAssetsRequestValidator()
    {
        RuleFor(x => x.PlexLibraryId).GreaterThan(0).When(x => x.PlexLibraryId.HasValue);
        RuleFor(x => x.Page).GreaterThan(0).When(x => x.Page.HasValue);
        RuleFor(x => x.PageSize).InclusiveBetween(1, MediaQueryFilter.MaximumPageSize).When(x => x.PageSize.HasValue);
        RuleFor(x => x.Sort)
            .Must(sort =>
                sort.IsSupportedSort(
                    SortType.Title,
                    SortType.AddedAt,
                    SortType.UpdatedAt,
                    SortType.Duration,
                    SortType.MediaSize
                )
            )
            .WithMessage("Unsupported photo asset sort.");
        RuleFor(x => x)
            .Must(x => x.UnmappedOnly != true || x.PlexLibraryId.HasValue)
            .WithMessage("Unmapped-only assets require an explicit photo library.");
        RuleFor(x => x)
            .Must(x => (long)((x.Page ?? 1) - 1) * (x.PageSize ?? MediaQueryFilter.MaximumPageSize) <= int.MaxValue)
            .WithMessage("The requested page offset is too large.");
    }
}

public sealed class GetPhotoAssetsEndpoint : Endpoint<GetPhotoAssetsRequest, PhotoAssetPageDTO>
{
    private readonly ICommandExecutor _commandExecutor;

    public GetPhotoAssetsEndpoint(ICommandExecutor commandExecutor)
    {
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexPhotoController + "/assets");
        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PhotoAssetPageDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(GetPhotoAssetsRequest req, CancellationToken ct)
    {
        var sort = QueryOptionsParser.Parse(new FlexQueryParameters { Sort = req.Sort }).ResolveSort()!.Value;
        var result = await _commandExecutor.Send(
            new GetPhotoAssetsQuery(
                req.PlexLibraryId,
                req.Page ?? 1,
                req.PageSize ?? MediaQueryFilter.MaximumPageSize,
                sort.Field,
                sort.Direction,
                req.UnmappedOnly ?? false
            ),
            ct
        );
        await Send.FluentResult(result, ct);
    }
}
