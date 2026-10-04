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
            var library = plexLibrary;
            if (command.MediaType is null)
            {
                await _librarySyncProgressStore.StartAsync(plexLibrary.Id, plexLibrary.Type, ct);

                // Retrieve an updated version of the PlexLibrary
                var sectionsResult = await _commandExecutor.Send(
                    new GetLibrarySectionsCommand(plexLibrary.PlexServerId),
                    ct
                );
                if (sectionsResult.IsFailed)
                    return sectionsResult.ToResult();

                library = sectionsResult.Value.Find(x => x.Key == plexLibrary.Key);
                if (library is null)
                    return ResultExtensions.EntityNotFound(nameof(PlexLibrary), plexLibrary.Id);

                library.Id = plexLibrary.Id;
                library.PlexServerId = plexLibrary.PlexServerId;
                library.DefaultDestinationId = library.Type.ToDefaultDestinationFolderId();
            }

            var mediaType = command.MediaType ?? library.Type;
            var mediaResult = await _commandExecutor.Send(
                new GetAllMediaByTypeFromPlexApiCommand(library, mediaType),
                ct
            );
            if (mediaResult.IsFailed)
                return mediaResult.ToResult().LogIfFailed();

            // Pre-sort the media list
            var media = mediaResult.Value.OrderByNatural(x => x.SortTitle).ToList();

            // Set Sort index based on OrderByNatural(x => x.SortTitle), per parent for descendants
            var sortIndices = new Dictionary<string, int>();
            foreach (var item in media)
            {
                var parentKey = command.MediaType is null ? string.Empty : item.ParentRatingKey;
                sortIndices.TryGetValue(parentKey, out var index);
                item.SortIndex = sortIndices[parentKey] = index + 1;
            }

            switch (mediaType)
            {
                case PlexMediaType.Movie:
                    library.Movies.AddRange(media.ToPlexMovies());
                    break;
                case PlexMediaType.TvShow:
                    library.TvShows.AddRange(media.ToPlexTvShows());
                    break;
                case PlexMediaType.MusicArtist:
                    library.Music.AddRange(media.ToPlexMusicArtists());
                    break;
                case PlexMediaType.MusicAlbum:
                {
                    var albums = media.ToPlexMusicAlbums();
                    library.Albums.AddRange(albums);
                    break;
                }
                case PlexMediaType.MusicTrack:
                {
                    var tracks = media.ToPlexMusicTracks();
                    library.Tracks.AddRange(tracks);
                    break;
                }
                case PlexMediaType.PhotoAlbum:
                    library.PhotoAlbums.AddRange(media.ToPlexPhotoAlbums());
                    break;
                case PlexMediaType.PhotoImage:
                {
                    var photos = media.ToPlexPhotos();
                    library.PhotoImages.AddRange(photos);
                    break;
                }
                case PlexMediaType.OtherVideos:
                    library.OtherVideos.AddRange(media.ToPlexOtherVideos());
                    break;
                default:
                    return Result.Fail("Type {PlexMediaType} is not supported for library retrieval", mediaType);
            }

            return Result.Ok(
                new LibraryMetadata(library)
                {
                    Countries = media.SelectMany(x => x.Country).ToPlexCountry(),
                    Genres = media.SelectMany(x => x.Genre).ToPlexGenre(),
                    Actors = media.SelectMany(x => x.Role).ToPlexActor(),
                    PhotoClipCount =
                        mediaType == PlexMediaType.PhotoImage
                            ? media.Count(x => x.Type is PlexMediaType.OtherVideos or PlexMediaType.Movie)
                            : 0,
                }
            );
        });

        result.LogIfFailed();

        if (result.IsFailed && command.MediaType is null)
            await _librarySyncProgressStore.UpdateErrorAsync(plexLibrary.Id, result.ToResult(), ct);

        return result;
    }
}
