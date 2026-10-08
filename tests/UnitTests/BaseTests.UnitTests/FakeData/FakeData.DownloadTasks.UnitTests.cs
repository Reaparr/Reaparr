using ByteSizeLib;

namespace Reaparr.BaseTests.UnitTests;

[NotInParallel]
public class FakeDataDownloadTasksUnitTests : BaseUnitTest
{
    [Test]
    public void MovieDownloadTask_ShouldGenerateAllRequiredProperties()
    {
        // Arrange
        var seed = new Seed(12345);

        // Act
        var movieTask = FakeData.GetMovieDownloadTask(seed).Generate();

        // Assert
        movieTask.ShouldNotBeNull();
        movieTask.PlexApiRatingKey.ShouldBeGreaterThan(0);
        movieTask.Title.ShouldNotBeNullOrEmpty();
        movieTask.FullTitle.ShouldNotBeNullOrEmpty();
        movieTask.FullTitle.ShouldContain("Movie");
        movieTask.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        movieTask.CreatedAt.ShouldNotBe(default);
        movieTask.Year.ShouldBeInRange(1900, 2030);
        movieTask.DataTotal.ShouldBeGreaterThan(0);
        movieTask.MediaType.ShouldBe(PlexMediaType.Movie);
        movieTask.DownloadTaskType.ShouldBe(DownloadTaskType.Movie);
        movieTask.PlexServerId.ShouldBe(0); // Should be 0 since it's ignored
        movieTask.PlexLibraryId.ShouldBe(0); // Should be 0 since it's ignored
    }

    [Test]
    public void MovieDownloadTask_ShouldGenerateChildren()
    {
        // Arrange
        var seed = new Seed(12345);
        var config = new FakeDataConfig { IncludeMultiPartMovies = false };

        // Act
        var movieTask = FakeData
            .GetMovieDownloadTask(seed, options => options.IncludeMultiPartMovies = config.IncludeMultiPartMovies)
            .Generate();

        // Assert
        movieTask.Children.ShouldNotBeNull();
        movieTask.Children.Count.ShouldBe(1);
    }

    [Test]
    public void MovieDownloadTask_ShouldGenerateMultipleChildrenWhenConfigured()
    {
        // Arrange
        var seed = new Seed(12345);
        var config = new FakeDataConfig { IncludeMultiPartMovies = true };

        // Act
        var movieTask = FakeData
            .GetMovieDownloadTask(seed, options => options.IncludeMultiPartMovies = config.IncludeMultiPartMovies)
            .Generate();

        // Assert
        movieTask.Children.ShouldNotBeNull();
        movieTask.Children.Count.ShouldBe(2);
    }

    [Test]
    public void MovieDownloadTask_ShouldKeepMultipartIndexesAligned_WhenConfigured()
    {
        // Arrange
        var seed = new Seed(12345);

        // Act
        var movieTask = FakeData
            .GetMovieDownloadTask(seed, options => options.IncludeMultiPartMovies = true)
            .Generate();
        var movieFiles = movieTask.Children.ToList();

        // Assert
        movieFiles.Count.ShouldBe(2);
        movieFiles[0].Title.ShouldEndWith(" 1");
        movieFiles[0].FullTitle.ShouldContain("/1-");
        movieFiles[1].Title.ShouldEndWith(" 2");
        movieFiles[1].FullTitle.ShouldContain("/2-");
    }

    [Test]
    public void MovieFileDownloadTask_ShouldGenerateAllRequiredProperties()
    {
        // Arrange
        var seed = new Seed(12345);

        // Act
        var movieFileTask = FakeData.GetDownloadTaskMovieFile(seed).Generate();

        // Assert
        movieFileTask.ShouldNotBeNull();
        movieFileTask.PlexApiRatingKey.ShouldBeGreaterThan(0);
        movieFileTask.Title.ShouldNotBeNullOrEmpty();
        movieFileTask.FullTitle.ShouldNotBeNullOrEmpty();
        movieFileTask.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        movieFileTask.CreatedAt.ShouldNotBe(default);
        movieFileTask.FileName.ShouldNotBeNullOrEmpty();
        movieFileTask.FileLocationUrl.ShouldNotBeNullOrEmpty();
        movieFileTask.Quality.ShouldNotBe(VideoQuality.None);
        movieFileTask.Quality.ShouldNotBe(VideoQuality.Unknown);
        movieFileTask.DataTotal.ShouldBeGreaterThan(0);
        movieFileTask.DirectoryMeta.ShouldNotBeNull();
        movieFileTask.DirectoryMeta.DownloadRootPath.ShouldNotBeNullOrEmpty();
        movieFileTask.DirectoryMeta.DestinationRootPath.ShouldNotBeNullOrEmpty();
        movieFileTask.DirectoryMeta.MovieFolder.ShouldNotBeNullOrEmpty();
        movieFileTask.MediaType.ShouldBe(PlexMediaType.Movie);
        movieFileTask.DownloadTaskType.ShouldBe(DownloadTaskType.MovieData);
    }

    [Test]
    public void TvShowDownloadTask_ShouldGenerateAllRequiredProperties()
    {
        // Arrange
        var seed = new Seed(67890);

        // Act
        var tvShowTask = FakeData.GetDownloadTaskTvShow(seed).Generate();

        // Assert
        tvShowTask.ShouldNotBeNull();
        tvShowTask.PlexApiRatingKey.ShouldBeGreaterThan(0);
        tvShowTask.Title.ShouldNotBeNullOrEmpty();
        tvShowTask.FullTitle.ShouldNotBeNullOrEmpty();
        tvShowTask.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        tvShowTask.CreatedAt.ShouldNotBe(default);
        tvShowTask.Year.ShouldBeInRange(1900, 2030);
        tvShowTask.DataTotal.ShouldBeGreaterThan(0);
        tvShowTask.MediaType.ShouldBe(PlexMediaType.TvShow);
        tvShowTask.DownloadTaskType.ShouldBe(DownloadTaskType.TvShow);
    }

    [Test]
    public void TvShowDownloadTask_ShouldGenerateChildrenWithCorrectHierarchy()
    {
        // Arrange
        var seed = new Seed(67890);
        var config = new FakeDataConfig { TvShowSeasonDownloadTasksCount = 2, TvShowEpisodeDownloadTasksCount = 3 };

        // Act
        var tvShowTask = FakeData
            .GetDownloadTaskTvShow(
                seed,
                options =>
                {
                    options.TvShowSeasonDownloadTasksCount = config.TvShowSeasonDownloadTasksCount;
                    options.TvShowEpisodeDownloadTasksCount = config.TvShowEpisodeDownloadTasksCount;
                }
            )
            .Generate();

        // Assert
        tvShowTask.Children.ShouldNotBeNull();
        tvShowTask.Children.Count.ShouldBe(config.TvShowSeasonDownloadTasksCount);

        foreach (var season in tvShowTask.Children)
        {
            season.ShouldNotBeNull();
            season.Children.Count.ShouldBe(config.TvShowEpisodeDownloadTasksCount);
            season.FullTitle.ShouldStartWith(tvShowTask.FullTitle);

            foreach (var episode in season.Children)
            {
                episode.ShouldNotBeNull();
                episode.Children.Count.ShouldBe(1);
                episode.FullTitle.ShouldStartWith(season.FullTitle);

                foreach (var episodeFile in episode.Children)
                {
                    episodeFile.ShouldNotBeNull();
                    episodeFile.FullTitle.ShouldStartWith(episode.FullTitle);
                    episodeFile.DirectoryMeta.ShouldNotBeNull();
                    episodeFile.DirectoryMeta.TvShowFolder.ShouldBe(tvShowTask.Title);
                    episodeFile.DirectoryMeta.SeasonFolder.ShouldBe(season.Title);
                }
            }
        }
    }

    [Test]
    public void TvShowDownloadTask_ShouldIncrementEpisodeFileIndexes_WhenEpisodeHasMultipleFiles()
    {
        // Arrange
        var seed = new Seed(67890);

        // Act
        var tvShowTask = FakeData
            .GetDownloadTaskTvShow(
                seed,
                options =>
                {
                    options.TvShowSeasonDownloadTasksCount = 1;
                    options.TvShowEpisodeDownloadTasksCount = 1;
                    options.IncludeMultiPartEpisodes = true;
                }
            )
            .Generate();

        var episode = tvShowTask.Children.Single().Children.Single();
        var episodeFiles = episode.Children.ToList();

        // Assert
        episodeFiles.Count.ShouldBe(2);
        episodeFiles[0].FullTitle.ShouldContain("/1-");
        episodeFiles[1].FullTitle.ShouldContain("/2-");
    }

    [Test]
    public void MusicArtistDownloadTask_ShouldGenerateConfiguredHierarchyAndMusicFiles()
    {
        // Arrange
        var seed = new Seed(24681);
        var config = new FakeDataConfig
        {
            MusicAlbumDownloadTasksCount = 2,
            MusicTrackDownloadTasksCount = 3,
            MusicTrackFileDownloadTasksCount = 2,
        };

        // Act
        var artist = FakeData
            .GetDownloadTaskMusicArtist(
                seed,
                options =>
                {
                    options.MusicAlbumDownloadTasksCount = config.MusicAlbumDownloadTasksCount;
                    options.MusicTrackDownloadTasksCount = config.MusicTrackDownloadTasksCount;
                    options.MusicTrackFileDownloadTasksCount = config.MusicTrackFileDownloadTasksCount;
                }
            )
            .Generate();
        var files = artist.Children.SelectMany(album => album.Children).SelectMany(track => track.Children).ToList();

        // Assert
        artist.DownloadTaskType.ShouldBe(DownloadTaskType.MusicArtist);
        artist.Children.Count.ShouldBe(config.MusicAlbumDownloadTasksCount);
        artist.Children.ShouldAllBe(album => album.Children.Count == config.MusicTrackDownloadTasksCount);
        files.Count.ShouldBe(
            config.MusicAlbumDownloadTasksCount
                * config.MusicTrackDownloadTasksCount
                * config.MusicTrackFileDownloadTasksCount
        );
        artist.DataTotal.ShouldBe(files.Sum(file => file.DataTotal));
        files.ShouldAllBe(
            file =>
                file.DownloadTaskType == DownloadTaskType.MusicTrackData
                && file.Quality == VideoQuality.None
                && file.FileName.EndsWith(".flac", StringComparison.Ordinal)
                && file.DirectoryMeta.MusicArtistFolder == artist.Title.SanitizeFolderName()
        );
    }

    [Test]
    public void OtherVideoDownloadTask_ShouldGenerateConfiguredFilesWithVideoSemantics()
    {
        // Arrange
        var seed = new Seed(24682);
        var config = new FakeDataConfig { OtherVideoFileDownloadTasksCount = 2 };

        // Act
        var video = FakeData
            .GetDownloadTaskOtherVideo(
                seed,
                options => options.OtherVideoFileDownloadTasksCount = config.OtherVideoFileDownloadTasksCount
            )
            .Generate();
        var files = video.Children.ToList();

        // Assert
        video.DownloadTaskType.ShouldBe(DownloadTaskType.OtherVideo);
        files.Count.ShouldBe(config.OtherVideoFileDownloadTasksCount);
        video.DataTotal.ShouldBe(files.Sum(file => file.DataTotal));
        files.ShouldAllBe(
            file =>
                file.DownloadTaskType == DownloadTaskType.OtherVideoData
                && file.Quality == VideoQuality.FullHD
                && file.FileName.EndsWith(".mp4", StringComparison.Ordinal)
                && file.DirectoryMeta.OtherVideoFolder == video.Title.SanitizeFolderName()
        );
    }

    [Test]
    public void DownloadTaskConfig_ShouldModifyFileSizeWhenConfigured()
    {
        // Arrange
        var seed = new Seed(12345);
        const int configuredSizeMb = 500;
        Action<FakeDataConfig> options = options =>
        {
            options.DownloadFileSizeInMb = configuredSizeMb;
        };

        // Act
        var movieFileTask = FakeData.GetDownloadTaskMovieFile(seed).Generate();
        var configuredMovieFileTask = FakeData.GetDownloadTaskMovieFile(seed, options).Generate();

        // Assert
        movieFileTask.DataTotal.ShouldNotBe(configuredMovieFileTask.DataTotal);
        var expectedBytes = ByteSize.FromMebiBytes(configuredSizeMb).Bytes;
        configuredMovieFileTask.DataTotal.ShouldBe((long)expectedBytes);
    }

    [Test]
    public void AllDownloadTaskTypes_ShouldGenerateCorrectTypes()
    {
        // Arrange
        var seed = new Seed(24680);

        // Act & Assert
        var movieTask = FakeData.GetMovieDownloadTask(seed).Generate();
        movieTask.DownloadTaskType.ShouldBe(DownloadTaskType.Movie);

        var movieFileTask = FakeData.GetDownloadTaskMovieFile(seed).Generate();
        movieFileTask.DownloadTaskType.ShouldBe(DownloadTaskType.MovieData);

        var tvShowTask = FakeData.GetDownloadTaskTvShow(seed).Generate();
        tvShowTask.DownloadTaskType.ShouldBe(DownloadTaskType.TvShow);

        var seasonTask = FakeData.GetDownloadTaskTvShowSeason(seed).Generate();
        seasonTask.DownloadTaskType.ShouldBe(DownloadTaskType.Season);

        var episodeTask = FakeData.GetDownloadTaskTvShowEpisode(seed).Generate();
        episodeTask.DownloadTaskType.ShouldBe(DownloadTaskType.Episode);

        var episodeFileTask = FakeData.GetDownloadTaskTvShowEpisodeFile(seed).Generate();
        episodeFileTask.DownloadTaskType.ShouldBe(DownloadTaskType.EpisodeData);
    }
}
