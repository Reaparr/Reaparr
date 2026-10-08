namespace Reaparr.PublicAPI.UnitTests;

using Reaparr.Application.Contracts;

public class GetTorrentFilesEndpointUnitTests
    : BaseEndpointUnitTest<GetTorrentFilesEndpoint, GetTorrentFilesRequest, List<QBittorrentTorrentFile>>
{
    private async Task<IntegrationIdentity> SetupIntegrationDatabase(int seed, Action<FakeDataConfig> configure)
    {
        await SetupDatabase(
            seed,
            config =>
            {
                configure(config);
                config.RadarrIntegrationCount = 1;
                config.AssignUnownedDownloadTasksToRadarrIntegration = true;
            }
        );
        return (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
    }

    [Test]
    public async Task ShouldReturnEmptyList_WhenHashMatchesNoFiles()
    {
        // Arrange
        var integrationIdentity = await SetupIntegrationDatabase(
            4412,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
                config.TvShowCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
            }
        );

        var request = new GetTorrentFilesRequest { Hash = "missing-hash" };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request, integrationIdentity: integrationIdentity);
        var response = endpointResult.Response;

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        response.ShouldNotBeNull();
        response.ShouldBeEmpty();

        var movieRowsWithHash = await IDbContext.DownloadTaskMovieFile.CountAsync(
            x => x.HashId == request.Hash,
            CancellationToken
        );
        var episodeRowsWithHash = await IDbContext.DownloadTaskTvShowEpisodeFile.CountAsync(
            x => x.HashId == request.Hash,
            CancellationToken
        );
        movieRowsWithHash.ShouldBe(0);
        episodeRowsWithHash.ShouldBe(0);
    }

    [Test]
    public async Task ShouldReturnFileNameOnly_WhenDownloadRootPathIsEmpty()
    {
        // Arrange
        var integrationIdentity = await SetupIntegrationDatabase(
            4413,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
                config.TvShowCount = 0;
            }
        );

        var dbContext = IDbContext;
        var hash = "movie-hash-empty-root";
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);

        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, hash)
                        .SetProperty(p => p.FileName, "only-file-name.mkv")
                        .SetProperty(p => p.DirectoryMeta, BuildDirectoryMeta("", "Movie Folder", "", "")),
                CancellationToken
            );

        var request = new GetTorrentFilesRequest { Hash = hash };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request, integrationIdentity: integrationIdentity);
        var response = endpointResult.Response;

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        response.ShouldNotBeNull();
        response.Count.ShouldBe(1);
        response.Single().Name.ShouldBe("only-file-name.mkv");

        var persistedMovieFile = await IDbContext.DownloadTaskMovieFile.FirstAsync(
            x => x.Id == movieFile.Id,
            CancellationToken
        );
        persistedMovieFile.FileName.ShouldBe("only-file-name.mkv");
        persistedMovieFile.DirectoryMeta.DownloadRootPath.ShouldBe("");
        persistedMovieFile.DirectoryMeta.MovieFolder.ShouldBe("Movie Folder");
        persistedMovieFile.DirectoryMeta.TvShowFolder.ShouldBe("");
        persistedMovieFile.DirectoryMeta.SeasonFolder.ShouldBe("");
    }

    [Test]
    public async Task ShouldReturnFileNameOnly_WhenComputedDirectoryEscapesDownloadRoot()
    {
        // Arrange
        var integrationIdentity = await SetupIntegrationDatabase(
            4414,
            config =>
            {
                config.PlexServerCount = 1;
                config.TvShowCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
                config.MovieCount = 0;
            }
        );

        var dbContext = IDbContext;
        var hash = "episode-hash-outside-root";
        var episodeFile = await dbContext.DownloadTaskTvShowEpisodeFile.FirstAsync(CancellationToken);

        await dbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == episodeFile.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, hash)
                        .SetProperty(p => p.FileName, "outside-root-file.mkv")
                        .SetProperty(
                            p => p.DirectoryMeta,
                            BuildDirectoryMeta("/downloads", "", "/outside-root", "Season 01")
                        ),
                CancellationToken
            );

        var request = new GetTorrentFilesRequest { Hash = hash };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request, integrationIdentity: integrationIdentity);
        var response = endpointResult.Response;

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        response.ShouldNotBeNull();
        response.Count.ShouldBe(1);
        response.Single().Name.ShouldBe("outside-root-file.mkv");

        var persistedEpisodeFile = await IDbContext.DownloadTaskTvShowEpisodeFile.FirstAsync(
            x => x.Id == episodeFile.Id,
            CancellationToken
        );
        persistedEpisodeFile.FileName.ShouldBe("outside-root-file.mkv");
        persistedEpisodeFile.DirectoryMeta.DownloadRootPath.ShouldBe("/downloads");
        persistedEpisodeFile.DirectoryMeta.TvShowFolder.ShouldBe("/outside-root");
        persistedEpisodeFile.DirectoryMeta.SeasonFolder.ShouldBe("Season 01");
    }

    [Test]
    public void ShouldRequireHash_WhenHashIsEmpty()
    {
        // Arrange
        var validator = new GetTorrentFilesRequestValidator();

        // Act
        var result = validator.Validate(new GetTorrentFilesRequest { Hash = string.Empty });

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(x => x.PropertyName == nameof(GetTorrentFilesRequest.Hash));
    }

    [Test]
    public void ShouldPass_WhenHashIsProvided()
    {
        // Arrange
        var validator = new GetTorrentFilesRequestValidator();

        // Act
        var result = validator.Validate(new GetTorrentFilesRequest { Hash = "abc123" });

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    private static DownloadTaskDirectory BuildDirectoryMeta(
        string downloadRootPath,
        string movieFolder,
        string tvShowFolder,
        string seasonFolder
    ) =>
        new()
        {
            DestinationRootPath = "/destination",
            DownloadRootPath = downloadRootPath,
            MovieFolder = movieFolder,
            TvShowFolder = tvShowFolder,
            SeasonFolder = seasonFolder,
            MusicArtistFolder = string.Empty,
            MusicAlbumFolder = string.Empty,
            PhotoAlbumFolder = string.Empty,
            OtherVideoFolder = string.Empty,
            KeepCompletedInDownloadFolder = false,
        };
}
