namespace Reaparr.Application;

/// <summary>
/// Set the default media destination where the media will be stored after the download process is finished.
/// </summary>
/// <param name="PlexLibraryId">The id of the <see cref="PlexLibrary"/> to update.</param>
/// <param name="FolderPathId">The id of the <see cref="FolderPath"/> to set as the default destination.</param>
public record SetPlexLibraryDefaultDestinationByIdEndpointRequest(int PlexLibraryId, int FolderPathId);

public class SetPlexLibraryDefaultDestinationByIdEndpointRequestValidator
    : Validator<SetPlexLibraryDefaultDestinationByIdEndpointRequest>
{
    public SetPlexLibraryDefaultDestinationByIdEndpointRequestValidator()
    {
        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.FolderPathId).GreaterThan(0);
    }
}

public class SetPlexLibraryDefaultDestinationByIdEndpoint
    : Endpoint<SetPlexLibraryDefaultDestinationByIdEndpointRequest, BaseResultDTO>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ITelemetry _telemetry;

    public SetPlexLibraryDefaultDestinationByIdEndpoint(IReaparrDbContext dbContext, ITelemetry telemetry)
    {
        _dbContext = dbContext;
        _telemetry = telemetry;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexLibraryController + "/{PlexLibraryId}/default/destination/{FolderPathId}");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(
        SetPlexLibraryDefaultDestinationByIdEndpointRequest req,
        CancellationToken ct
    )
    {
        var library = await _dbContext
            .PlexLibraries.Where(x => x.Id == req.PlexLibraryId)
            .Select(x => new
            {
                x.Type,
                x.PlexServerId,
                x.DefaultDestinationId,
            })
            .SingleOrDefaultAsync(ct);
        if (library is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexLibrary), req.PlexLibraryId), ct);
            return;
        }

        var folder = await _dbContext.FolderPaths.Where(x => x.Id == req.FolderPathId).SingleOrDefaultAsync(ct);
        if (folder is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(FolderPath), req.FolderPathId), ct);
            return;
        }

        if (folder.MediaType != library.Type || !IsDestinationFolderType(library.Type, folder.FolderType))
        {
            await Send.FluentResult(
                ResultExtensions.Create400BadRequestResult(
                    "The selected folder is not a compatible media destination."
                ),
                ct
            );
            return;
        }

        if (library.DefaultDestinationId == req.FolderPathId)
        {
            await Send.FluentResult(Result.Ok(), ct);
            return;
        }

        var plexLibraryDb = await _dbContext
            .PlexLibraries.Where(x => x.Id == req.PlexLibraryId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.DefaultDestinationId, req.FolderPathId), ct);

        if (plexLibraryDb == 0)
        {
            await Send.FluentResult(
                Result.Fail(
                    "No library found with id {ReqPlexLibraryId} that could have its default folder destination updated",
                    req.PlexLibraryId
                ),
                ct
            );
            return;
        }

        _telemetry.PlexLibraryChanged(library.PlexServerId, req.PlexLibraryId, library.Type, folder);
        await Send.FluentResult(Result.Ok(), ct);
    }

    private static bool IsDestinationFolderType(PlexMediaType mediaType, FolderType folderType) =>
        (mediaType, folderType)
            is
                (PlexMediaType.Movie, FolderType.MovieFolder)
                or
                (PlexMediaType.TvShow, FolderType.TvShowFolder)
                or
                (PlexMediaType.MusicArtist, FolderType.MusicFolder)
                or
                (PlexMediaType.PhotoAlbum, FolderType.PhotosFolder)
                or
                (PlexMediaType.OtherVideos, FolderType.OtherVideosFolder)
                or
                (PlexMediaType.Games, FolderType.GamesVideosFolder);
}
