namespace Reaparr.Application;

public record CreateSonarrIntegrationRequest
{
    public required string Name { get; init; }
    public required string Url { get; init; }
    public required string ApiKey { get; init; }
    public required string Category { get; init; }
    public required int DownloadFolderId { get; init; }
}

public class CreateSonarrIntegrationRequestValidator : Validator<CreateSonarrIntegrationRequest>
{
    public CreateSonarrIntegrationRequestValidator()
    {
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

public class CreateSonarrIntegrationEndpoint : Endpoint<CreateSonarrIntegrationRequest, ResultDTO<SonarrIntegrationDTO>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ITelemetry _telemetry;

    public CreateSonarrIntegrationEndpoint(IReaparrDbContext dbContext, ITelemetry telemetry)
    {
        _dbContext = dbContext;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.IntegrationController + "/Sonarr/Configure");
        Description(x =>
            x.Produces(StatusCodes.Status201Created, typeof(ResultDTO<SonarrIntegrationDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(CreateSonarrIntegrationRequest req, CancellationToken ct)
    {
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
            x => x.DisplayName == name || x.Category == category || x.BaseUrl == url,
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

        var integration = new SonarrIntegration
        {
            Id = Guid.NewGuid(),
            DisplayName = name,
            BaseUrl = url,
            SonarrApiKey = apiKey,
            QBittorrentApiKey = IntegrationApiKeyGenerator.GenerateQBittorrentApiKey(),
            TorznabApiKey = IntegrationApiKeyGenerator.GenerateTorznabApiKey(),
            Category = category,
            DownloadFolderId = req.DownloadFolderId,
            ProvisioningState = IntegrationProvisioningState.Unconfigured,
        };
        _dbContext.SonarrIntegrations.Add(integration);
        await _dbContext.SaveChangesAsync(ct);

        _telemetry.IntegrationChanged(integration, TelemetryAction.Create, connectionIdentityChanged: false);

        var result = Result.Ok(integration).Add201CreatedRequestSuccess("Sonarr integration created successfully.");
        await Send.FluentResult(result, model => model.ToDTO(), ct);
    }
}
