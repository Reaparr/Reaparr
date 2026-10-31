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

    public SetPlexLibraryDefaultDestinationByIdEndpoint(IReaparrDbContext dbContext)
    {
        _dbContext = dbContext;
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
        var libraryType = await _dbContext
            .PlexLibraries.Where(x => x.Id == req.PlexLibraryId)
            .Select(x => (PlexMediaType?)x.Type)
            .SingleOrDefaultAsync(ct);
        if (libraryType is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexLibrary), req.PlexLibraryId), ct);
            return;
        }

        var folder = await _dbContext
            .FolderPaths.Where(x => x.Id == req.FolderPathId)
            .Select(x => new { x.MediaType, x.FolderType })
            .SingleOrDefaultAsync(ct);
        if (folder is null)
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(FolderPath), req.FolderPathId), ct);
            return;
        }

        if (folder.MediaType != libraryType.Value || !IsDestinationFolderType(libraryType.Value, folder.FolderType))
        {
            await Send.FluentResult(
                ResultExtensions.Create400BadRequestResult(
                    "The selected folder is not a compatible media destination."
                ),
                ct
            );
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
        }
        else
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
