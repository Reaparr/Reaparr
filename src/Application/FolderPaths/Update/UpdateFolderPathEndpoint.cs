namespace Reaparr.Application;

public class UpdateFolderPathEndpointRequest
{
    [FromBody]
    public required FolderPathDTO FolderPathDTO { get; init; }
}

public class UpdateFolderPathEndpointRequestValidator : Validator<UpdateFolderPathEndpointRequest>
{
    public UpdateFolderPathEndpointRequestValidator()
    {
        RuleFor(x => x.FolderPathDTO)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(x => x.FolderPathDTO.DisplayName).NotEmpty();
                RuleFor(x => x.FolderPathDTO.Directory).NotEmpty();
                RuleFor(x => x.FolderPathDTO.FolderType).NotEqual(FolderType.None).NotEqual(FolderType.Unknown);
                RuleFor(x => x.FolderPathDTO.MediaType)
                    .NotEqual(PlexMediaType.Unknown)
                    .Must(
                        (request, mediaType) =>
                            request.FolderPathDTO.FolderType == FolderType.DownloadFolder
                            || mediaType != PlexMediaType.None
                    )
                    .WithMessage("Media type can only be None for download folders.");
            });
    }
}

public class UpdateFolderPathEndpoint : Endpoint<UpdateFolderPathEndpointRequest, ResultDTO<FolderPathDTO>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public UpdateFolderPathEndpoint(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<UpdateFolderPathEndpoint>();
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Put(ApiRoutes.FolderPathController + "/");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<FolderPathDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(UpdateFolderPathEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var folderPath = req.FolderPathDTO.ToModel();
        var folderPathDb = await _dbContext
            .FolderPaths.AsTracking()
            .FirstOrDefaultAsync(x => x.Id == folderPath.Id, ct);

        if (folderPathDb is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(FolderPath), folderPath.Id), ct);
            return;
        }

        if (
            (folderPathDb.FolderType, folderPathDb.MediaType) != (folderPath.FolderType, folderPath.MediaType)
            && !await CanChangeRoleAsync(folderPath)
        )
        {
            await Send.FluentResult(
                ResultExtensions.Create400BadRequestResult("The folder role is required by a default or assigned owner."),
                ct
            );
            return;
        }

        _dbContext.Entry(folderPathDb).CurrentValues.SetValues(folderPath);
        await _dbContext.SaveChangesAsync(ct);

        await Send.FluentResult(Result.Ok(folderPathDb), path => path.ToDTO(), ct);
    }

    private async Task<bool> CanChangeRoleAsync(FolderPath folderPath)
    {
        // Default resolvers use these IDs regardless of the stored role; reserved IDs 8–10 remain editable.
        if (
            folderPath.Id >= FolderTypeDefaults.DefaultDownloadFolderId
            && folderPath.Id <= PlexMediaType.Games.ToDefaultDestinationFolderId()
        )
            return false;

        // A library destination keeps its assigned role; editing its name or path remains supported.
        if (await _dbContext.PlexLibraries.IgnoreIsEnabledFilter().AnyAsync(x => x.DefaultDestinationId == folderPath.Id))
            return false;

        if (folderPath.FolderType == FolderType.DownloadFolder)
            return true;

        return !await IsAssignedDownloadFolderAsync(folderPath.Id);
    }

    private async Task<bool> IsAssignedDownloadFolderAsync(int folderPathId) =>
        await _dbContext.SonarrIntegrations.AnyAsync(x => x.DownloadFolderId == folderPathId)
        || await _dbContext.RadarrIntegrations.AnyAsync(x => x.DownloadFolderId == folderPathId);
}
