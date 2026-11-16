namespace Reaparr.Application;

public record CreateRadarrIntegrationRequest
{
    public required string Name { get; init; }
    public required string Url { get; init; }
    public required string ApiKey { get; init; }
    public required string Category { get; init; }
    public required int DownloadFolderId { get; init; }
}

public class CreateRadarrIntegrationRequestValidator : Validator<CreateRadarrIntegrationRequest>
{
    public CreateRadarrIntegrationRequestValidator()
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

public class CreateRadarrIntegrationEndpoint : Endpoint<CreateRadarrIntegrationRequest, ResultDTO<RadarrIntegrationDTO>>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ITelemetry _telemetry;

    public CreateRadarrIntegrationEndpoint(IReaparrDbContext dbContext, ITelemetry telemetry)
    {
        _dbContext = dbContext;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Post(ApiRoutes.IntegrationController + "/Radarr/Configure");
        Description(x =>
            x.Produces(StatusCodes.Status201Created, typeof(ResultDTO<RadarrIntegrationDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(CreateRadarrIntegrationRequest req, CancellationToken ct)
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

        var hasConflict = await _dbContext.RadarrIntegrations.AnyAsync(
            x => x.DisplayName == name || x.Category == category || x.BaseUrl == url,
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

        var integration = new RadarrIntegration
        {
            Id = Guid.NewGuid(),
            DisplayName = name,
            BaseUrl = url,
            RadarrApiKey = apiKey,
            QBittorrentApiKey = IntegrationApiKeyGenerator.GenerateQBittorrentApiKey(),
            TorznabApiKey = IntegrationApiKeyGenerator.GenerateTorznabApiKey(),
            Category = category,
            DownloadFolderId = req.DownloadFolderId,
            ProvisioningState = IntegrationProvisioningState.Unconfigured,
        };
        _dbContext.RadarrIntegrations.Add(integration);
        await _dbContext.SaveChangesAsync(ct);

        _telemetry.IntegrationChanged(integration, TelemetryAction.Create, connectionIdentityChanged: false);

        var result = Result.Ok(integration).Add201CreatedRequestSuccess("Radarr integration created successfully.");
        await Send.FluentResult(result, model => model.ToDTO(), ct);
    }
}
