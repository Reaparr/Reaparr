namespace Reaparr.Application;

public record UpdateSonarrIntegrationRequest
{
    [RouteParam]
    public Guid IntegrationId { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public required string ApiKey { get; init; }
    public required string Category { get; init; }
    public required int DownloadFolderId { get; init; }
}

public class UpdateSonarrIntegrationRequestValidator : Validator<UpdateSonarrIntegrationRequest>
{
    public UpdateSonarrIntegrationRequestValidator()
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

public class UpdateSonarrIntegrationEndpoint : Endpoint<UpdateSonarrIntegrationRequest, ResultDTO<SonarrIntegrationDTO>>
{
    private readonly IReaparrDbContext _dbContext;

    public UpdateSonarrIntegrationEndpoint(IReaparrDbContext dbContext) => _dbContext = dbContext;

    public override void Configure()
    {
        Put(ApiRoutes.IntegrationController + "/Sonarr/{integrationId:guid}/Configure");
        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<SonarrIntegrationDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(UpdateSonarrIntegrationRequest req, CancellationToken ct)
    {
        var integration = await _dbContext
            .SonarrIntegrations.AsTracking()
            .SingleOrDefaultAsync(x => x.Id == req.IntegrationId, ct);
        if (integration is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(SonarrIntegration), req.IntegrationId), ct);
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

        var hasConflict = await _dbContext.SonarrIntegrations.AnyAsync(
            x => x.Id != integration.Id && (x.DisplayName == name || x.Category == category || x.BaseUrl == url),
            ct
        );
        if (hasConflict)
        {
            await Send.FluentResult(
                ResultExtensions.Create400BadRequestResult(
                    "A Sonarr integration with the same name, URL, or category already exists."
                ),
                ct
            );
            return;
        }

        var connectionIdentityChanged =
            integration.BaseUrl.Trim().TrimEnd('/') != url || integration.SonarrApiKey.Trim() != apiKey;

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
        integration.SonarrApiKey = apiKey;
        integration.Category = category;
        integration.DownloadFolderId = req.DownloadFolderId;
        await _dbContext.SaveChangesAsync(ct);

        await Send.FluentResult(Result.Ok(integration), model => model.ToDTO(), ct);
    }
}
