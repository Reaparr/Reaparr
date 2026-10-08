namespace Reaparr.Data.UnitTests;

public class DownloadTaskGenericMapperUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(DownloadTaskType.Movie, "Movies", "Movie", "")]
    [Arguments(DownloadTaskType.MovieData, "Movies", "Movie", "")]
    [Arguments(DownloadTaskType.TvShow, "TvShows", "Show", "")]
    [Arguments(DownloadTaskType.Season, "TvShows", "Show", "Season 01")]
    [Arguments(DownloadTaskType.Episode, "TvShows", "Show", "Season 01")]
    [Arguments(DownloadTaskType.EpisodeData, "TvShows", "Show", "Season 01")]
    [Arguments(DownloadTaskType.MusicArtist, "Music", "Artist", "")]
    [Arguments(DownloadTaskType.MusicAlbum, "Music", "Artist", "Album")]
    [Arguments(DownloadTaskType.MusicTrack, "Music", "Artist", "Album")]
    [Arguments(DownloadTaskType.MusicTrackData, "Music", "Artist", "Album")]
    [Arguments(DownloadTaskType.PhotoAlbum, "Photos", "Photos", "")]
    [Arguments(DownloadTaskType.PhotoImage, "Photos", "Photos", "")]
    [Arguments(DownloadTaskType.PhotoData, "Photos", "Photos", "")]
    [Arguments(DownloadTaskType.OtherVideo, "OtherVideos", "Video", "")]
    [Arguments(DownloadTaskType.OtherVideoData, "OtherVideos", "Video", "")]
    public void ShouldMapOwnHierarchyDirectory_WhenFileMetadataIsLoaded(
        DownloadTaskType type,
        string category,
        string folder,
        string subfolder
    )
    {
        // Arrange
        var (task, file) = CreateTask(type);
        file.DirectoryMeta.DownloadRootPath = "/downloads";
        file.DirectoryMeta.DestinationRootPath = "/destination";
        file.DirectoryMeta.MovieFolder = "Movie";
        file.DirectoryMeta.TvShowFolder = "Show";
        file.DirectoryMeta.SeasonFolder = "Season 01";
        file.DirectoryMeta.MusicArtistFolder = "Artist";
        file.DirectoryMeta.MusicAlbumFolder = "Album";
        file.DirectoryMeta.PhotoAlbumFolder = "Photos";
        file.DirectoryMeta.OtherVideoFolder = "Video";

        // Act
        var result = task switch
        {
            DownloadTaskParentBase parent => parent.ToGeneric(),
            DownloadTaskFileBase leaf => leaf.ToGeneric(),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        // Assert
        result.DownloadDirectory.ShouldBe(Path.Combine("/downloads", category, folder, subfolder));
        result.DestinationDirectory.ShouldBe(Path.Combine("/destination", folder, subfolder));
    }

    [Test]
    [Arguments(DownloadTaskType.TvShow, "TvShows", "Show")]
    [Arguments(DownloadTaskType.MusicArtist, "Music", "Artist")]
    public void ShouldKeepRootMediaFolder_WhenLeafSubfolderIsEmpty(
        DownloadTaskType type,
        string category,
        string folder
    )
    {
        // Arrange
        var (task, file) = CreateTask(type);
        file.DirectoryMeta.DownloadRootPath = "/downloads";
        file.DirectoryMeta.DestinationRootPath = "/destination";
        file.DirectoryMeta.TvShowFolder = "Show";
        file.DirectoryMeta.SeasonFolder = string.Empty;
        file.DirectoryMeta.MusicArtistFolder = "Artist";
        file.DirectoryMeta.MusicAlbumFolder = string.Empty;

        // Act
        var result = ((DownloadTaskParentBase)task).ToGeneric();

        // Assert
        result.DownloadDirectory.ShouldBe(Path.Combine("/downloads", category, folder));
        result.DestinationDirectory.ShouldBe(Path.Combine("/destination", folder));
    }

    [Test]
    [Arguments(DownloadTaskType.TvShow, "TvShows", "Show")]
    [Arguments(DownloadTaskType.MusicArtist, "Music", "Artist")]
    public void ShouldResolveLoadedSiblingMetadata_WhenFirstChildHasNoFiles(
        DownloadTaskType type,
        string category,
        string folder
    )
    {
        // Arrange
        var (task, file) = CreateTask(type);
        file.DirectoryMeta.DownloadRootPath = "/downloads";
        file.DirectoryMeta.DestinationRootPath = "/destination";
        file.DirectoryMeta.TvShowFolder = "Show";
        file.DirectoryMeta.MusicArtistFolder = "Artist";
        if (task is DownloadTaskTvShow show)
        {
            var emptySeason = FakeData.GetDownloadTaskTvShowSeason(new Seed(74002)).Generate();
            emptySeason.Children.Clear();
            show.Children = [emptySeason, show.Children.Single()];
        }
        else
        {
            var artist = (DownloadTaskMusicArtist)task;
            var emptyAlbum = FakeData.GetDownloadTaskMusicAlbum(new Seed(74002)).Generate();
            emptyAlbum.Children.Clear();
            artist.Children = [emptyAlbum, artist.Children.Single()];
        }

        // Act
        var result = ((DownloadTaskParentBase)task).ToGeneric();

        // Assert
        result.DownloadDirectory.ShouldBe(Path.Combine("/downloads", category, folder));
        result.DestinationDirectory.ShouldBe(Path.Combine("/destination", folder));
        result.Children[0].DownloadDirectory.ShouldBe(string.Empty);
        result.Children[0].DestinationDirectory.ShouldBe(string.Empty);
    }

    [Test]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public void ShouldLeaveOnlyUnassignedRootPathsEmpty_WhenMappingParent(bool emptyDownload, bool emptyDestination)
    {
        // Arrange
        var (task, file) = CreateTask(DownloadTaskType.MusicArtist);
        file.DirectoryMeta.DownloadRootPath = emptyDownload ? string.Empty : "/downloads";
        file.DirectoryMeta.DestinationRootPath = emptyDestination ? string.Empty : "/destination";
        file.DirectoryMeta.MusicArtistFolder = "Artist";

        // Act
        var result = ((DownloadTaskParentBase)task).ToGeneric();

        // Assert
        result.DownloadDirectory.ShouldBe(emptyDownload ? string.Empty : Path.Combine("/downloads", "Music", "Artist"));
        result.DestinationDirectory.ShouldBe(emptyDestination ? string.Empty : Path.Combine("/destination", "Artist"));
    }

    [Test]
    [Arguments(DownloadTaskType.TvShow)]
    [Arguments(DownloadTaskType.MusicArtist)]
    public void ShouldLeaveDirectoriesEmpty_WhenNoDescendantFileIsLoaded(DownloadTaskType type)
    {
        // Arrange
        var (task, _) = CreateTask(type);
        if (task is DownloadTaskTvShow show)
            show.Children.Single().Children.Clear();
        else
            ((DownloadTaskMusicArtist)task).Children.Single().Children.Clear();

        // Act
        var result = ((DownloadTaskParentBase)task).ToGeneric();

        // Assert
        result.DownloadDirectory.ShouldBe(string.Empty);
        result.DestinationDirectory.ShouldBe(string.Empty);
    }

    private static (DownloadTaskBase Task, DownloadTaskFileBase File) CreateTask(DownloadTaskType type)
    {
        var seed = new Seed(74001);
        switch (type)
        {
            case DownloadTaskType.Movie:
            case DownloadTaskType.MovieData:
                var movie = FakeData.GetMovieDownloadTask(seed).Generate();
                var movieFile = movie.Children.Single();
                return (type == DownloadTaskType.Movie ? movie : movieFile, movieFile);
            case DownloadTaskType.TvShow:
            case DownloadTaskType.Season:
            case DownloadTaskType.Episode:
            case DownloadTaskType.EpisodeData:
                var show = FakeData.GetDownloadTaskTvShow(seed, config =>
                {
                    config.TvShowSeasonDownloadTasksCount = 1;
                    config.TvShowEpisodeDownloadTasksCount = 1;
                }).Generate();
                var season = show.Children.Single();
                var episode = season.Children.Single();
                var episodeFile = episode.Children.Single();
                return (type switch
                {
                    DownloadTaskType.TvShow => show,
                    DownloadTaskType.Season => season,
                    DownloadTaskType.Episode => episode,
                    _ => episodeFile,
                }, episodeFile);
            case DownloadTaskType.MusicArtist:
            case DownloadTaskType.MusicAlbum:
            case DownloadTaskType.MusicTrack:
            case DownloadTaskType.MusicTrackData:
                var artist = FakeData.GetDownloadTaskMusicArtist(seed, config =>
                {
                    config.MusicAlbumDownloadTasksCount = 1;
                    config.MusicTrackDownloadTasksCount = 1;
                    config.MusicTrackFileDownloadTasksCount = 1;
                }).Generate();
                var album = artist.Children.Single();
                var track = album.Children.Single();
                var trackFile = track.Children.Single();
                return (type switch
                {
                    DownloadTaskType.MusicArtist => artist,
                    DownloadTaskType.MusicAlbum => album,
                    DownloadTaskType.MusicTrack => track,
                    _ => trackFile,
                }, trackFile);
            case DownloadTaskType.PhotoAlbum:
            case DownloadTaskType.PhotoImage:
            case DownloadTaskType.PhotoData:
                var photoAlbum = FakeData.GetDownloadTaskPhotoAlbum(seed, config =>
                {
                    config.PhotoImageDownloadTasksCount = 1;
                    config.PhotoImageFileDownloadTasksCount = 1;
                }).Generate();
                var image = photoAlbum.Children.Single();
                var imageFile = image.Children.Single();
                return (type switch
                {
                    DownloadTaskType.PhotoAlbum => photoAlbum,
                    DownloadTaskType.PhotoImage => image,
                    _ => imageFile,
                }, imageFile);
            case DownloadTaskType.OtherVideo:
            case DownloadTaskType.OtherVideoData:
                var video = FakeData.GetDownloadTaskOtherVideo(seed).Generate();
                var videoFile = video.Children.Single();
                return (type == DownloadTaskType.OtherVideo ? video : videoFile, videoFile);
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
