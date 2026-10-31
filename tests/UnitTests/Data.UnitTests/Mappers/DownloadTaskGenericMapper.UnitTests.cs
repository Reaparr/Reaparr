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
    [Arguments(DownloadTaskType.PhotoFolder, "Photos", "Photos", "")]
    [Arguments(DownloadTaskType.PhotoImage, "Photos", "Photos", "")]
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
        file.DirectoryMeta.PhotoFolder = type is DownloadTaskType.PhotoFolder or DownloadTaskType.PhotoImage
            ? "Photos"
            : string.Empty;
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
        result.Id.ShouldBe(task.Id);
        result.ParentId.ShouldBe(task.ToParentKey()?.Id ?? Guid.Empty);
        result.DownloadTaskType.ShouldBe(type);
        result.MediaType.ShouldBe(task.MediaType);
        result.RatingKey.ShouldBe(task.PlexApiRatingKey);
        result.Title.ShouldBe(task.Title);
        result.FullTitle.ShouldBe(task.FullTitle);
        result.PlexServerId.ShouldBe(task.PlexServerId);
        result.PlexLibraryId.ShouldBe(task.PlexLibraryId);
        result.IsDownloadable.ShouldBe(task.IsDownloadable);
        var mappedFile = new[] { result }
            .Flatten(x => x.Children)
            .Single(x => x.Id == file.Id && x.DownloadTaskType == file.DownloadTaskType);
        mappedFile.ParentId.ShouldBe(file.ToParentKey()!.Id);
        mappedFile.DownloadTaskType.ShouldBe(file.DownloadTaskType);
        mappedFile.MediaType.ShouldBe(file.MediaType);
        mappedFile.IsDownloadable.ShouldBeTrue();
        mappedFile.Children.ShouldBeEmpty();
        mappedFile.FileName.ShouldBe(file.FileName);
        mappedFile.FileLocationUrl.ShouldBe(file.FileLocationUrl);
        mappedFile.DownloadStatus.ShouldBe(file.DownloadStatus);
        mappedFile.DataTotal.ShouldBe(file.DataTotal);
        mappedFile.DataReceived.ShouldBe(file.DataReceived);
        mappedFile.DownloadSpeed.ShouldBe(file.DownloadSpeed);
        mappedFile.FileTransferSpeed.ShouldBe(file.FileTransferSpeed);
        mappedFile.FileDataTransferred.ShouldBe(file.FileDataTransferred);
        mappedFile.CurrentFileTransferBytesOffset.ShouldBe(file.CurrentFileTransferBytesOffset);
    }

    [Test]
    public void ShouldMapNestedPhotoFolderAndFileWithDirectParentKeys_WhenFolderContainsSubtree()
    {
        // Arrange
        var root = FakeData
            .GetDownloadTaskPhotoFolder(new Seed(74003), config => config.PhotoImageDownloadTasksCount = 1)
            .RuleFor(x => x.Id, _ => Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))
            .Generate();
        root.DownloadRootPath = "/downloads";
        root.DestinationRootPath = "/destination";
        root.RelativePath = string.Empty;
        var rootFile = root.Files.Single();
        rootFile.UpdateInitProperty(nameof(rootFile.Id), Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));
        rootFile.DirectoryMeta.DownloadRootPath = root.DownloadRootPath;
        rootFile.DirectoryMeta.DestinationRootPath = root.DestinationRootPath;
        rootFile.DirectoryMeta.PhotoFolder = "root";
        var child = FakeData
            .GetDownloadTaskPhotoFolder(new Seed(74004), config => config.PhotoImageDownloadTasksCount = 1)
            .RuleFor(x => x.Id, _ => Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"))
            .Generate();
        child.Parent = root;
        child.ParentId = root.Id;
        child.RelativePath = "nested";
        child.DownloadRootPath = root.DownloadRootPath;
        child.DestinationRootPath = root.DestinationRootPath;
        child.Files.Clear();
        var childFile = FakeData
            .GetDownloadTaskPhotoImageFile(new Seed(74005))
            .RuleFor(x => x.Id, _ => Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"))
            .Generate();
        childFile.UpdateInitProperty(nameof(childFile.ParentId), child.Id);
        childFile.UpdateInitProperty(nameof(childFile.Parent), child);
        child.Files.Add(childFile);
        childFile.DirectoryMeta.DownloadRootPath = child.DownloadRootPath;
        childFile.DirectoryMeta.DestinationRootPath = child.DestinationRootPath;
        childFile.DirectoryMeta.PhotoFolder = "root/nested";
        root.Children.Add(child);

        // Act
        var result = root.ToGeneric();

        // Assert
        result.ParentId.ShouldBe(Guid.Empty);
        result.DownloadTaskType.ShouldBe(DownloadTaskType.PhotoFolder);
        result
            .Children.Select(x => (x.Id, x.ParentId, x.DownloadTaskType, x.IsDownloadable))
            .OrderBy(x => x.Id)
            .ShouldBe(
                new (Guid Id, Guid ParentId, DownloadTaskType DownloadTaskType, bool IsDownloadable)[]
                {
                    (child.Id, root.Id, DownloadTaskType.PhotoFolder, false),
                    (rootFile.Id, root.Id, DownloadTaskType.PhotoImage, true),
                }.OrderBy(x => x.Id)
            );
        var mappedChild = result.Children.Single(x => x.Id == child.Id);
        mappedChild.DownloadDirectory.ShouldBe(Path.Combine("/downloads", "Photos", "root/nested"));
        mappedChild.DestinationDirectory.ShouldBe(Path.Combine("/destination", "root/nested"));
        mappedChild
            .Children.Select(x => (x.Id, x.ParentId, x.DownloadTaskType, x.IsDownloadable))
            .ShouldBe([(childFile.Id, child.Id, DownloadTaskType.PhotoImage, true)]);
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
                var show = FakeData
                    .GetDownloadTaskTvShow(
                        seed,
                        config =>
                        {
                            config.TvShowSeasonDownloadTasksCount = 1;
                            config.TvShowEpisodeDownloadTasksCount = 1;
                        }
                    )
                    .Generate();
                var season = show.Children.Single();
                var episode = season.Children.Single();
                var episodeFile = episode.Children.Single();
                return (
                    type switch
                    {
                        DownloadTaskType.TvShow => show,
                        DownloadTaskType.Season => season,
                        DownloadTaskType.Episode => episode,
                        _ => episodeFile,
                    },
                    episodeFile
                );
            case DownloadTaskType.MusicArtist:
            case DownloadTaskType.MusicAlbum:
            case DownloadTaskType.MusicTrack:
            case DownloadTaskType.MusicTrackData:
                var artist = FakeData
                    .GetDownloadTaskMusicArtist(
                        seed,
                        config =>
                        {
                            config.MusicAlbumDownloadTasksCount = 1;
                            config.MusicTrackDownloadTasksCount = 1;
                            config.MusicTrackFileDownloadTasksCount = 1;
                        }
                    )
                    .Generate();
                var album = artist.Children.Single();
                var track = album.Children.Single();
                var trackFile = track.Children.Single();
                return (
                    type switch
                    {
                        DownloadTaskType.MusicArtist => artist,
                        DownloadTaskType.MusicAlbum => album,
                        DownloadTaskType.MusicTrack => track,
                        _ => trackFile,
                    },
                    trackFile
                );
            case DownloadTaskType.PhotoFolder:
            case DownloadTaskType.PhotoImage:
                var photoFolder = FakeData
                    .GetDownloadTaskPhotoFolder(seed, config => config.PhotoImageDownloadTasksCount = 1)
                    .Generate();
                var imageFile = photoFolder.Files.Single();
                return (type == DownloadTaskType.PhotoFolder ? photoFolder : imageFile, imageFile);
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
