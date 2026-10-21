namespace Reaparr.Application;

public class GetDownloadPreviewEndpointRequestValidator : Validator<DownloadPreviewRequest>
{
    public GetDownloadPreviewEndpointRequestValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.DownloadMedias).NotEmpty();
        RuleForEach(x => x.DownloadMedias).NotNull().SetValidator(new DownloadMediaDTOValidator());
    }
}

public class GetDownloadPreviewEndpoint : Endpoint<DownloadPreviewRequest, DownloadPreviewContainerDTO>
{
    private readonly ICommandExecutor _commandExecutor;

    public GetDownloadPreviewEndpoint(ICommandExecutor commandExecutor)
    {
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Post(ApiRoutes.DownloadController + "/preview");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<DownloadPreviewContainerDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(DownloadPreviewRequest request, CancellationToken ct)
    {
        var result = await _commandExecutor.Send(new GetDownloadPreviewQuery(request), ct);

        await Send.FluentResult(result, x => x.ToDTO(), ct);
    }
}
