namespace Reaparr.Domain.UnitTests;

public class DownloadTaskDirectoryExtensionsUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(DownloadTaskType.Movie, "Movies", "Movie")]
    [Arguments(DownloadTaskType.MovieData, "Movies", "Movie")]
    [Arguments(DownloadTaskType.MoviePart, "Movies", "Movie")]
    [Arguments(DownloadTaskType.TvShow, "TvShows", "Show")]
    [Arguments(DownloadTaskType.Season, "TvShows", "Show/Season 01")]
    [Arguments(DownloadTaskType.Episode, "TvShows", "Show/Season 01")]
    [Arguments(DownloadTaskType.EpisodeData, "TvShows", "Show/Season 01")]
    [Arguments(DownloadTaskType.EpisodePart, "TvShows", "Show/Season 01")]
    [Arguments(DownloadTaskType.MusicArtist, "Music", "Artist")]
    [Arguments(DownloadTaskType.MusicAlbum, "Music", "Artist/Album")]
    [Arguments(DownloadTaskType.MusicTrack, "Music", "Artist/Album")]
    [Arguments(DownloadTaskType.MusicTrackData, "Music", "Artist/Album")]
    [Arguments(DownloadTaskType.MusicTrackPart, "Music", "Artist/Album")]
    [Arguments(DownloadTaskType.PhotoAlbum, "Photos", "Photos")]
    [Arguments(DownloadTaskType.PhotoImage, "Photos", "Photos")]
    [Arguments(DownloadTaskType.PhotoData, "Photos", "Photos")]
    [Arguments(DownloadTaskType.PhotoPart, "Photos", "Photos")]
    [Arguments(DownloadTaskType.OtherVideo, "OtherVideos", "Video")]
    [Arguments(DownloadTaskType.OtherVideoData, "OtherVideos", "Video")]
    [Arguments(DownloadTaskType.OtherVideoPart, "OtherVideos", "Video")]
    public void ShouldResolveDownloadAndDestinationDirectories(
        DownloadTaskType type,
        string category,
        string relativeDirectory
    )
    {
        var directory = CreateDirectory();

        directory.GetDownloadCategoryDirectory(type).ShouldBe(Path.Combine("/downloads", category));
        directory.GetDownloadDirectory(type).ShouldBe(Path.Combine("/downloads", category, relativeDirectory));
        directory.GetDestinationDirectory(type).ShouldBe(Path.Combine("/destination", relativeDirectory));
    }

    [Test]
    public void ShouldReturnEmptyWhenDownloadRootIsMissing()
    {
        var directory = CreateDirectory();
        directory.DownloadRootPath = string.Empty;

        directory.GetDownloadCategoryDirectory(DownloadTaskType.Movie).ShouldBeEmpty();
        directory.GetDownloadDirectory(DownloadTaskType.Movie).ShouldBeEmpty();
    }

    [Test]
    public void ShouldReturnEmptyWhenDestinationRootIsMissing()
    {
        var directory = CreateDirectory();
        directory.DestinationRootPath = string.Empty;

        directory.GetDestinationDirectory(DownloadTaskType.Movie).ShouldBeEmpty();
    }

    [Test]
    public void ShouldRejectUnsupportedDownloadTaskType()
    {
        var directory = CreateDirectory();

        Should.Throw<ArgumentOutOfRangeException>(() => directory.GetDownloadDirectory((DownloadTaskType)999));
        Should.Throw<ArgumentOutOfRangeException>(() => directory.GetDestinationDirectory((DownloadTaskType)999));
    }

    private static DownloadTaskDirectory CreateDirectory() =>
        new()
        {
            DownloadRootPath = "/downloads",
            DestinationRootPath = "/destination",
            MovieFolder = "Movie",
            TvShowFolder = "Show",
            SeasonFolder = "Season 01",
            MusicArtistFolder = "Artist",
            MusicAlbumFolder = "Album",
            PhotoAlbumFolder = "Photos",
            OtherVideoFolder = "Video",
            KeepCompletedInDownloadFolder = false,
        };
}
