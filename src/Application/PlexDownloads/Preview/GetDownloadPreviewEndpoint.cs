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
    private readonly ITelemetry _telemetry;

    public GetDownloadPreviewEndpoint(ICommandExecutor commandExecutor, ITelemetry telemetry)
    {
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
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
        _telemetry.DownloadPreviewFinished(
            request,
            result,
            result.IsSuccess
                ? result.Value.Select(x => (x.Id, x.Title, x.Size, x.ChildCount, x.MediaType)).ToArray()
                : []
        );

        await Send.FluentResult(result, x => x.ToDTO(), ct);
    }
}
