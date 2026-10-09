namespace Reaparr.Data.Contracts;

public static partial class DbContextExtensions
{
    public static async Task<Result<int>> GetPlexMediaByRatingKeyAsync(
        this IReaparrDbContext dbContext,
        int plexApiRatingKey,
        int plexServerId,
        PlexMediaType mediaType
    )
    {
        Result<int> ToLookupResult(int id) =>
            id == 0
                ? Result.Fail<int>(
                    $"Media with rating key {plexApiRatingKey} was not found on server {plexServerId} for type {mediaType}"
                )
                : Result.Ok(id);

        switch (mediaType)
        {
            case PlexMediaType.Movie:
            {
                var id = await dbContext
                    .PlexMovies.Where(x => x.PlexApiRatingKey == plexApiRatingKey && x.PlexServerId == plexServerId)
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync(CancellationToken.None);
                return ToLookupResult(id);
            }
            case PlexMediaType.TvShow:
            {
                var id = await dbContext
                    .PlexTvShows.Where(x => x.PlexApiRatingKey == plexApiRatingKey && x.PlexServerId == plexServerId)
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync(CancellationToken.None);
                return ToLookupResult(id);
            }
            case PlexMediaType.Season:
            {
                var id = await dbContext
                    .PlexTvShowSeason.Where(x =>
                        x.PlexApiRatingKey == plexApiRatingKey && x.PlexServerId == plexServerId
                    )
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync(CancellationToken.None);
                return ToLookupResult(id);
            }
            case PlexMediaType.Episode:
            {
                var id = await dbContext
                    .PlexTvShowEpisodes.Where(x =>
                        x.PlexApiRatingKey == plexApiRatingKey && x.PlexServerId == plexServerId
                    )
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync(CancellationToken.None);
                return ToLookupResult(id);
            }
            case PlexMediaType.MusicArtist:
            {
                var id = await dbContext
                    .PlexArtists.Where(x => x.PlexApiRatingKey == plexApiRatingKey && x.PlexServerId == plexServerId)
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync(CancellationToken.None);
                return ToLookupResult(id);
            }
            case PlexMediaType.MusicAlbum:
            {
                var id = await dbContext
                    .PlexAlbums.Where(x => x.PlexApiRatingKey == plexApiRatingKey && x.PlexServerId == plexServerId)
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync(CancellationToken.None);
                return ToLookupResult(id);
            }
            case PlexMediaType.MusicTrack:
            {
                var id = await dbContext
                    .PlexTracks.Where(x => x.PlexApiRatingKey == plexApiRatingKey && x.PlexServerId == plexServerId)
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync(CancellationToken.None);
                return ToLookupResult(id);
            }
            case PlexMediaType.PhotoAlbum:
            {
                var id = await dbContext
                    .PlexPhotoAlbums.Where(x =>
                        x.PlexApiRatingKey == plexApiRatingKey && x.PlexServerId == plexServerId
                    )
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync(CancellationToken.None);
                return ToLookupResult(id);
            }
            case PlexMediaType.PhotoImage:
            {
                var id = await dbContext
                    .PlexPhotoImages.Where(x =>
                        x.PlexApiRatingKey == plexApiRatingKey && x.PlexServerId == plexServerId
                    )
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync(CancellationToken.None);
                return ToLookupResult(id);
            }
            case PlexMediaType.OtherVideos:
            {
                var id = await dbContext
                    .PlexOtherVideos.Where(x =>
                        x.PlexApiRatingKey == plexApiRatingKey && x.PlexServerId == plexServerId
                    )
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync(CancellationToken.None);
                return ToLookupResult(id);
            }
            default:
                return Result.Fail(
                    "Type {PlexMediaType} is not supported for retrieving the plexMediaId by key",
                    mediaType
                );
        }
    }

    /// <summary>
    /// Bulk inserts the Plex movies and the movie media data into the database.
    /// </summary>
    public static async Task<Result> BulkInsertPlexMoviesAsync(
        this IReaparrDbContext context,
        List<PlexMovie> plexMovies,
        int plexServerId,
        int plexLibraryId,
        CancellationToken ct = default
    )
    {
        if (!plexMovies.Any())
            return Result.Fail("No movies to insert").LogWarning();

        if (plexServerId == 0)
            return ResultExtensions.IsZero(nameof(plexServerId));

        if (plexLibraryId == 0)
            return ResultExtensions.IsZero(nameof(plexLibraryId));

        return await context.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                plexMovies.SetRelationshipIds(plexServerId, plexLibraryId);

                foreach (var movie in plexMovies)
                    movie.Quality =
                        movie.MediaDataList.Count == 0 ? VideoQuality.Unknown : movie.MediaDataList.Max(x => x.Quality);

                await ctx.BulkInsertAsync(plexMovies, BulkConfigPreset.Default, txCt);

                // Add movie media data for each movie
                var mediaData = plexMovies
                    .SelectMany(x =>
                    {
                        x.MediaDataList.SetRelationshipIds(x.PlexServerId, x.PlexLibraryId, x.Id);
                        return x.MediaDataList;
                    })
                    .ToList();

                await ctx.BulkInsertAsync(mediaData, BulkConfigPreset.Default, txCt);
            },
            ct
        );
    }
}
