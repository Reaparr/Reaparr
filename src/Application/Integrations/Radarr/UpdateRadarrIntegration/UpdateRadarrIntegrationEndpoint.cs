namespace Reaparr.Application;

public record UpdateRadarrIntegrationRequest
{
    [RouteParam]
    public Guid IntegrationId { get; init; }

    public required string Name { get; init; }
    public required string Url { get; init; }
    public required string ApiKey { get; init; }
    public required string Category { get; init; }
    public required int DownloadFolderId { get; init; }
}

public class UpdateRadarrIntegrationRequestValidator : Validator<UpdateRadarrIntegrationRequest>
{
    public UpdateRadarrIntegrationRequestValidator()
    {
        RuleFor(x => x.IntegrationId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Url)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("URL must be an absolute http/https URL.")
            .Must(url => url is not null && url.Trim().IsValidHttpUrl())
            .WithMessage("URL must be an absolute http/https URL.");
        RuleFor(x => x.ApiKey).NotEmpty();
        RuleFor(x => x.Category).NotEmpty();
        RuleFor(x => x.DownloadFolderId).GreaterThan(0);
    }
}

public class UpdateRadarrIntegrationEndpoint : Endpoint<UpdateRadarrIntegrationRequest, ResultDTO<RadarrIntegrationDTO>>
{
    private readonly IReaparrDbContext _dbContext;

    public UpdateRadarrIntegrationEndpoint(IReaparrDbContext dbContext) => _dbContext = dbContext;

    public override void Configure()
    {
        Put(ApiRoutes.IntegrationController + "/Radarr/{integrationId:guid}/Configure");
        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<RadarrIntegrationDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(UpdateRadarrIntegrationRequest req, CancellationToken ct)
    {
        var integration = await _dbContext
            .RadarrIntegrations.AsTracking()
            .SingleOrDefaultAsync(x => x.Id == req.IntegrationId, ct);
        if (integration is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(RadarrIntegration), req.IntegrationId), ct);
            return;
        }

        var name = req.Name.Trim();
        var url = req.Url.Trim().TrimEnd('/');
        var apiKey = req.ApiKey.Trim();
        var category = req.Category.Trim();

        if (
            !await _dbContext.FolderPaths.AnyAsync(
                x => x.Id == req.DownloadFolderId && x.FolderType == FolderType.DownloadFolder,
                ct
            )
        )
        {
            await Send.FluentResult(
                ResultExtensions.Create400BadRequestResult("The selected download folder is invalid."),
                ct
            );
            return;
        }

        var hasConflict = await _dbContext.RadarrIntegrations.AnyAsync(
            x => x.Id != integration.Id && (x.DisplayName == name || x.Category == category || x.BaseUrl == url),
            ct
        );
        if (hasConflict)
        {
            await Send.FluentResult(
                ResultExtensions.Create400BadRequestResult(
                    "A Radarr integration with the same name, URL, or category already exists."
                ),
                ct
            );
            return;
        }

        var connectionIdentityChanged =
            integration.BaseUrl.Trim().TrimEnd('/') != url || integration.RadarrApiKey.Trim() != apiKey;

        if (
            integration.ProvisioningState == IntegrationProvisioningState.Configured
            && (integration.Category != category || connectionIdentityChanged)
        )
            integration.ProvisioningState = IntegrationProvisioningState.ChangesPending;

        if (connectionIdentityChanged)
        {
            integration.LastConnectionTestStatus = TestConnectionStatus.Unknown;
            integration.LastConnectionTestHttpStatusCode = null;
            integration.LastConnectionTestErrorMessage = null;
            integration.LastConnectionTestedAt = null;
        }

        integration.DisplayName = name;
        integration.BaseUrl = url;
        integration.RadarrApiKey = apiKey;
        integration.Category = category;
        integration.DownloadFolderId = req.DownloadFolderId;
        await _dbContext.SaveChangesAsync(ct);

        await Send.FluentResult(Result.Ok(integration), model => model.ToDTO(), ct);
    }
}
