namespace Reaparr.Data.UnitTests;

public class DbContextExtensionsPlexMediaUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.MusicAlbum)]
    [Arguments(PlexMediaType.MusicTrack)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.PhotoImage)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldResolveExactSourceIdentityAndRejectWrongServer_WhenLookingUpNewMedia(PlexMediaType type)
    {
        // Arrange
        await SetupDatabase(
            62535,
            config =>
            {
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
                config.OtherVideoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var source = type switch
        {
            PlexMediaType.MusicArtist => await dbContext
                .PlexArtists.Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexServerId,
                })
                .SingleAsync(CancellationToken),
            PlexMediaType.MusicAlbum => await dbContext
                .PlexAlbums.Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexServerId,
                })
                .SingleAsync(CancellationToken),
            PlexMediaType.MusicTrack => await dbContext
                .PlexTracks.Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexServerId,
                })
                .SingleAsync(CancellationToken),
            PlexMediaType.PhotoAlbum => await dbContext
                .PlexPhotoAlbums.Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexServerId,
                })
                .SingleAsync(CancellationToken),
            PlexMediaType.PhotoImage => await dbContext
                .PlexPhotoImages.Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexServerId,
                })
                .SingleAsync(CancellationToken),
            PlexMediaType.OtherVideos => await dbContext
                .PlexOtherVideos.Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexServerId,
                })
                .SingleAsync(CancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        // Act
        var result = await dbContext.GetPlexMediaByRatingKeyAsync(source.PlexApiRatingKey, source.PlexServerId, type);
        var wrongServer = await dbContext.GetPlexMediaByRatingKeyAsync(source.PlexApiRatingKey, int.MaxValue, type);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(source.Id);
        wrongServer.IsFailed.ShouldBeTrue();
        wrongServer.Errors.Count.ShouldBe(1);
    }

    [Test]
    public async Task ShouldFindMovieId_WhenMediaKeyExists()
    {
        // Arrange
        await SetupDatabase(
            12400,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 5;
            }
        );
        var movie = IDbContext.PlexMovies.First();

        // Act
        var result = await IDbContext.GetPlexMediaByRatingKeyAsync(
            movie.PlexApiRatingKey,
            movie.PlexServerId,
            PlexMediaType.Movie
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(movie.Id);
    }

    [Test]
    public async Task ShouldFindTvShowId_WhenMediaKeyExists()
    {
        // Arrange
        await SetupDatabase(
            12401,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexTvShowLibraryCount = 1;
                cfg.TvShowCount = 5;
            }
        );
        var tvShow = IDbContext.PlexTvShows.First();

        // Act
        var result = await IDbContext.GetPlexMediaByRatingKeyAsync(
            tvShow.PlexApiRatingKey,
            tvShow.PlexServerId,
            PlexMediaType.TvShow
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(tvShow.Id);
    }

    [Test]
    public async Task ShouldFail_WhenMediaNotFound()
    {
        // Arrange
        await SetupDatabase(
            12402,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
            }
        );

        // Act
        var result = await IDbContext.GetPlexMediaByRatingKeyAsync(9999, 1, PlexMediaType.Movie);

        // Assert
        result.IsFailed.ShouldBeTrue();
    }
}
