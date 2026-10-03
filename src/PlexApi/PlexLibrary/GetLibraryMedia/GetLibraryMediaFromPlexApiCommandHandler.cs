namespace Reaparr.PlexApi;

/// <summary>
/// Retrieves all media metadata from the PlexApi for a given <see cref="PlexLibrary"/> and returns it as a <see cref="LibraryMetadata"/>.
/// This service is an extra layer of abstraction to convert incoming DTO's from the PlexAPI to workable entities.
/// This was done in order to keep all PlexApi related DTO's in the infrastructure layer.
/// </summary>
public class GetLibraryMediaFromPlexApiCommandHandler
    : ICommandHandler<GetLibraryMediaFromPlexApiCommand, Result<LibraryMetadata>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILibrarySyncProgressStore _librarySyncProgressStore;

    public GetLibraryMediaFromPlexApiCommandHandler(
        ILogger log,
        ICommandExecutor commandExecutor,
        ILibrarySyncProgressStore librarySyncProgressStore
    )
    {
        _log = log.ForContext<GetLibraryMediaFromPlexApiCommandHandler>();
        _commandExecutor = commandExecutor;
        _librarySyncProgressStore = librarySyncProgressStore;
    }

    public async Task<Result<LibraryMetadata>> ExecuteAsync(
        GetLibraryMediaFromPlexApiCommand command,
        CancellationToken ct
    )
    {
        var plexLibrary = command.PlexLibrary;

        var result = await Result.Try(async Task<Result<LibraryMetadata>> () =>
        {
            await _librarySyncProgressStore.StartAsync(plexLibrary.Id, plexLibrary.Type, ct);

            // Retrieve an updated version of the PlexLibrary
            var sectionsResult = await _commandExecutor.Send(
                new GetLibrarySectionsCommand(plexLibrary.PlexServerId),
                ct
            );
            if (sectionsResult.IsFailed)
                return sectionsResult.ToResult();

            var library = sectionsResult.Value.Find(x => x.Key == plexLibrary.Key);
            if (library is null)
                return ResultExtensions.EntityNotFound(nameof(PlexLibrary), plexLibrary.Id);

            library.Id = plexLibrary.Id;
            library.PlexServerId = plexLibrary.PlexServerId;
            library.DefaultDestinationId = library.Type.ToDefaultDestinationFolderId();

            var mediaResult = await _commandExecutor.Send(
                new GetAllMediaByTypeFromPlexApiCommand(library, library.Type),
                ct
            );
            if (mediaResult.IsFailed)
                return mediaResult.ToResult().LogIfFailed();

            // Pre-sort the media list
            var media = mediaResult.Value.OrderByNatural(x => x.SortTitle).ToList();

            // Set Sort index based on OrderByNatural(x => x.TitleSort)
            for (var i = 0; i < media.Count; i++)
                media[i].SortIndex = i + 1;

            switch (library.Type)
            {
                case PlexMediaType.Movie:
                    library.Movies.AddRange(media.ToPlexMovies());
                    break;
                case PlexMediaType.TvShow:
                    library.TvShows.AddRange(media.ToPlexTvShows());
                    break;
                case PlexMediaType.Music:
                    library.Artists.AddRange(media.Select(x => x.ToPlexMusicArtist(library)));
                    break;
                case PlexMediaType.Photos:
                    library.PhotoAlbums.AddRange(media.Select(x => x.ToPlexPhotoAlbum(library)));
                    break;
                case PlexMediaType.OtherVideos:
                    library.OtherVideos.AddRange(media.Select(x => x.ToPlexOtherVideo(library)));
                    break;
            }

            return Result.Ok(
                new LibraryMetadata(library)
                {
                    Countries = media.SelectMany(x => x.Country).ToList(),
                    Genres = media.SelectMany(x => x.Genre).ToList(),
                    Actors = media.SelectMany(x => x.Role).ToList(),
                }
            );
        });

        result.LogIfFailed();

        if (result.IsFailed)
            await _librarySyncProgressStore.UpdateErrorAsync(plexLibrary.Id, result.ToResult(), ct);

        return result;
    }
}
