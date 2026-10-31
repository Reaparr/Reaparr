using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

namespace Reaparr.Application;

public sealed record GetPhotoAlbumsRequest
{
    [QueryParam, BindFrom("plexLibraryId")]
    public int? PlexLibraryId { get; init; }

    [QueryParam, BindFrom("page")]
    public int? Page { get; init; }

    [QueryParam, BindFrom("size")]
    public int? PageSize { get; init; }

    [QueryParam, BindFrom("sort")]
    public string? Sort { get; init; }
}

public sealed class GetPhotoAlbumsRequestValidator : Validator<GetPhotoAlbumsRequest>
{
    public GetPhotoAlbumsRequestValidator()
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
            .WithMessage("Unsupported photo album sort.");
        RuleFor(x => x)
            .Must(x => (long)((x.Page ?? 1) - 1) * (x.PageSize ?? MediaQueryFilter.MaximumPageSize) <= int.MaxValue)
            .WithMessage("The requested page offset is too large.");
    }
}

public sealed class GetPhotoAlbumsEndpoint : Endpoint<GetPhotoAlbumsRequest, PhotoAlbumPageDTO>
{
    private readonly ICommandExecutor _commandExecutor;

    public GetPhotoAlbumsEndpoint(ICommandExecutor commandExecutor)
    {
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexPhotoController + "/albums");
        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PhotoAlbumPageDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(GetPhotoAlbumsRequest req, CancellationToken ct)
    {
        var sort = QueryOptionsParser.Parse(new FlexQueryParameters { Sort = req.Sort }).ResolveSort()!.Value;
        var result = await _commandExecutor.Send(
            new GetPhotoAlbumsQueryCommand(
                req.PlexLibraryId,
                req.Page ?? 1,
                req.PageSize ?? MediaQueryFilter.MaximumPageSize,
                sort.Field,
                sort.Direction
            ),
            ct
        );
        await Send.FluentResult(result, ct);
    }
}
