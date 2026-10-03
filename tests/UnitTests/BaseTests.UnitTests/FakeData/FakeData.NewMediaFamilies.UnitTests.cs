namespace Reaparr.BaseTests.UnitTests;

public class FakeDataNewMediaFamiliesUnitTests : BaseUnitTest
{
    public enum DescendantCount
    {
        MusicAlbums,
        MusicTracks,
        Photos,
        PhotoClips,
    }

    [Test]
    public void ShouldKeepConfigurationsAndSeedsIsolated_WhenFactoriesAreCreatedBeforeGeneration()
    {
        // Arrange
        Action<FakeDataConfig> small = config =>
        {
            config.MusicAlbumCount = 2;
            config.MusicTrackCount = 3;
            config.PhotoCount = 2;
            config.PhotoClipCount = 1;
            config.DownloadFileSizeInMb = 1;
        };
        Action<FakeDataConfig> large = config =>
        {
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 4;
            config.PhotoCount = 1;
            config.PhotoClipCount = 3;
            config.DownloadFileSizeInMb = 2;
        };
        var expectedVideoTitle = FakeData.GetPlexOtherVideos(new Seed(82001), small).Generate().Title;
        var defaultTrackSize = FakeData.GetPlexMusicTrackMediaData(new Seed(82002)).Generate().Size;
        var defaultPhotoSize = FakeData.GetPlexPhotoMediaData(new Seed(82003)).Generate().Size;
        var defaultVideoSize = FakeData.GetPlexOtherVideoMediaData(new Seed(82004)).Generate().Size;
        var smallMusic = FakeData.GetPlexMusicArtists(new Seed(82005), small);
        var largeMusic = FakeData.GetPlexMusicArtists(new Seed(82006), large);
        var smallPhotos = FakeData.GetPlexPhotoAlbums(new Seed(82007), small);
        var largePhotos = FakeData.GetPlexPhotoAlbums(new Seed(82008), large);
        var smallVideos = FakeData.GetPlexOtherVideos(new Seed(82001), small);
        var largeVideos = FakeData.GetPlexOtherVideos(new Seed(82009), large);

        // Act
        var smallArtist = smallMusic.Generate();
        var largeArtist = largeMusic.Generate();
        var smallAlbum = smallPhotos.Generate();
        var largeAlbum = largePhotos.Generate();
        var smallVideo = smallVideos.Generate();
        var largeVideo = largeVideos.Generate();
        var defaultTrack = FakeData.GetPlexMusicTrackMediaData(new Seed(82010)).Generate();
        var defaultPhoto = FakeData.GetPlexPhotoMediaData(new Seed(82011)).Generate();
        var defaultVideo = FakeData.GetPlexOtherVideoMediaData(new Seed(82012)).Generate();

        // Assert
        smallArtist.Albums.Select(x => x.Tracks.Count).ShouldBe([3, 3]);
        largeArtist.Albums.Select(x => x.Tracks.Count).ShouldBe([4]);
        smallArtist
            .Albums.SelectMany(x => x.Tracks)
            .Select(x => x.MediaDataList.Single().Size)
            .ShouldBe(Enumerable.Repeat(1024L * 1024, 6));
        largeArtist
            .Albums.SelectMany(x => x.Tracks)
            .Select(x => x.MediaDataList.Single().Size)
            .ShouldBe(Enumerable.Repeat(2L * 1024 * 1024, 4));
        smallAlbum
            .Photos.Select(x => x.MediaDataList.Single())
            .Select(x => (x.Container, x.Size))
            .ShouldBe([("jpg", 1024L * 1024), ("jpg", 1024L * 1024), ("mp4", 1024L * 1024)]);
        largeAlbum
            .Photos.Select(x => x.MediaDataList.Single())
            .Select(x => (x.Container, x.Size))
            .ShouldBe([
                ("jpg", 2L * 1024 * 1024),
                ("mp4", 2L * 1024 * 1024),
                ("mp4", 2L * 1024 * 1024),
                ("mp4", 2L * 1024 * 1024),
            ]);
        smallVideo.Title.ShouldBe(expectedVideoTitle);
        smallVideo.MediaDataList.ShouldHaveSingleItem().Size.ShouldBe(1024L * 1024);
        largeVideo.MediaDataList.ShouldHaveSingleItem().Size.ShouldBe(2L * 1024 * 1024);
        defaultTrack.Size.ShouldBe(defaultTrackSize);
        defaultPhoto.Size.ShouldBe(defaultPhotoSize);
        defaultVideo.Size.ShouldBe(defaultVideoSize);
    }

    [Test]
    [Arguments(1, 0)]
    [Arguments(2, 2)]
    public async Task ShouldSeedOwnedHierarchiesAndMetricsPerLibrary_WhenMediaCountsAreConfigured(
        int serverCount,
        int libraryCount
    )
    {
        // Arrange
        const long fileSize = 3 * 1024 * 1024;
        Action<FakeDataConfig> options = config =>
        {
            config.PlexServerCount = serverCount;
            config.PlexMusicLibraryCount = libraryCount;
            config.PlexPhotoLibraryCount = libraryCount;
            config.PlexOtherVideoLibraryCount = libraryCount;
            config.MusicArtistCount = 2;
            config.MusicAlbumCount = 2;
            config.MusicTrackCount = 3;
            config.PhotoAlbumCount = 3;
            config.PhotoCount = 4;
            config.PhotoClipCount = 2;
            config.OtherVideoCount = 5;
            config.MovieCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 2;
            config.DownloadFileSizeInMb = 3;
        };

        // Act
        await SetupDatabase(81001, options);

        // Assert
        var dbContext = IDbContext;
        var servers = await dbContext.PlexServers.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var libraries = await dbContext.PlexLibraries.ToListAsync(CancellationToken);
        var artists = await dbContext
            .PlexArtists.Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
                    .ThenInclude(x => x.MediaDataList)
            .ToListAsync(CancellationToken);
        var photoAlbums = await dbContext
            .PlexPhotoAlbums.Include(x => x.Photos)
                .ThenInclude(x => x.MediaDataList)
            .ToListAsync(CancellationToken);
        var videos = await dbContext.PlexOtherVideos.Include(x => x.MediaDataList).ToListAsync(CancellationToken);
        servers.Count.ShouldBe(serverCount);
        var expectedLibraryTypes = new[] { PlexMediaType.Movie, PlexMediaType.TvShow }
            .Concat(Enumerable.Repeat(PlexMediaType.Music, Math.Max(1, libraryCount)))
            .Concat(Enumerable.Repeat(PlexMediaType.Photos, Math.Max(1, libraryCount)))
            .Concat(Enumerable.Repeat(PlexMediaType.OtherVideos, Math.Max(1, libraryCount)))
            .OrderBy(x => x)
            .ToArray();
        foreach (var server in servers)
            libraries
                .Where(x => x.PlexServerId == server.Id)
                .Select(x => x.Type)
                .OrderBy(x => x)
                .ShouldBe(expectedLibraryTypes);

        foreach (var library in libraries.Where(x => x.Type == PlexMediaType.Music))
        {
            var libraryArtists = artists.Where(x => x.PlexLibraryId == library.Id).ToList();
            libraryArtists.Count.ShouldBe(2);
            foreach (var artist in libraryArtists)
            {
                artist.PlexServerId.ShouldBe(library.PlexServerId);
                artist.ChildCount.ShouldBe(2);
                artist.MediaSize.ShouldBe(6 * fileSize);
                artist.Albums.Count.ShouldBe(2);
                foreach (var album in artist.Albums)
                {
                    (album.PlexArtistId, album.PlexLibraryId, album.PlexServerId).ShouldBe(
                        (artist.Id, library.Id, library.PlexServerId)
                    );
                    (album.ChildCount, album.TrackCount, album.DiscCount).ShouldBe((3, (int?)3, (int?)1));
                    album.MediaSize.ShouldBe(3 * fileSize);
                    album.Tracks.Select(x => x.TrackNumber).OrderBy(x => x).ShouldBe(new int?[] { 1, 2, 3 });
                    foreach (var track in album.Tracks)
                    {
                        (track.PlexAlbumId, track.PlexLibraryId, track.PlexServerId).ShouldBe(
                            (album.Id, library.Id, library.PlexServerId)
                        );
                        track.DiscNumber.ShouldBe(1);
                        track.ChildCount.ShouldBe(0);
                        track.MediaSize.ShouldBe(fileSize);
                        var original = track.MediaDataList.ShouldHaveSingleItem();
                        (
                            original.PlexTrackId,
                            original.PlexLibraryId,
                            original.PlexServerId,
                            original.PlexApiRatingKey
                        ).ShouldBe((track.Id, library.Id, library.PlexServerId, track.PlexApiRatingKey));
                        original.PartIndex.ShouldBe(0);
                        original.Size.ShouldBe(fileSize);
                        original.VideoCodec.ShouldBeEmpty();
                        track.Duration.ShouldBe(original.Duration / 1000);
                    }
                }
            }

            (
                library.ArtistCount,
                library.AlbumCount,
                library.TrackCount,
                library.TrackMediaVersionCount,
                library.TrackFilePartCount,
                library.MediaSize
            ).ShouldBe((2, 4, 12, 12, 12, 12 * fileSize));
        }

        foreach (var library in libraries.Where(x => x.Type == PlexMediaType.Photos))
        {
            var libraryAlbums = photoAlbums.Where(x => x.PlexLibraryId == library.Id).ToList();
            libraryAlbums.Count.ShouldBe(3);
            foreach (var album in libraryAlbums)
            {
                album.PlexServerId.ShouldBe(library.PlexServerId);
                album.MediaSize.ShouldBe(6 * fileSize);
                album.Photos.Count.ShouldBe(6);
                album
                    .Photos.SelectMany(x => x.MediaDataList)
                    .Select(x => x.Container)
                    .OrderBy(x => x)
                    .ShouldBe(["jpg", "jpg", "jpg", "jpg", "mp4", "mp4"]);
                foreach (var photo in album.Photos)
                {
                    (photo.PlexPhotoAlbumId, photo.PlexLibraryId, photo.PlexServerId).ShouldBe(
                        (album.Id, library.Id, library.PlexServerId)
                    );
                    photo.ChildCount.ShouldBe(0);
                    photo.MediaSize.ShouldBe(fileSize);
                    var original = photo.MediaDataList.ShouldHaveSingleItem();
                    (
                        original.PlexPhotoId,
                        original.PlexLibraryId,
                        original.PlexServerId,
                        original.PlexApiRatingKey
                    ).ShouldBe((photo.Id, library.Id, library.PlexServerId, photo.PlexApiRatingKey));
                    original.Size.ShouldBe(fileSize);
                    photo.Duration.ShouldBe(original.Duration / 1000);
                    if (original.Container == "jpg")
                    {
                        original.Duration.ShouldBe(0);
                        original.VideoCodec.ShouldBeEmpty();
                    }
                    else
                        original.VideoCodec.ShouldBe("h264");
                }
            }

            (
                library.PhotoAlbumCount,
                library.PhotoCount,
                library.PhotoClipCount,
                library.PhotoMediaVersionCount,
                library.PhotoFilePartCount,
                library.MediaSize
            ).ShouldBe((3, 12, 6, 18, 18, 18 * fileSize));
        }

        foreach (var library in libraries.Where(x => x.Type == PlexMediaType.OtherVideos))
        {
            var libraryVideos = videos.Where(x => x.PlexLibraryId == library.Id).ToList();
            libraryVideos.Count.ShouldBe(5);
            foreach (var video in libraryVideos)
            {
                video.PlexServerId.ShouldBe(library.PlexServerId);
                video.ChildCount.ShouldBe(0);
                video.MediaSize.ShouldBe(fileSize);
                var original = video.MediaDataList.ShouldHaveSingleItem();
                (
                    original.PlexOtherVideoId,
                    original.PlexLibraryId,
                    original.PlexServerId,
                    original.PlexApiRatingKey
                ).ShouldBe((video.Id, library.Id, library.PlexServerId, video.PlexApiRatingKey));
                original.PartIndex.ShouldBe(0);
                original.Size.ShouldBe(fileSize);
                video.Duration.ShouldBe(original.Duration / 1000);
            }

            (
                library.OtherVideoCount,
                library.OtherVideoMediaVersionCount,
                library.OtherVideoFilePartCount,
                library.MediaSize
            ).ShouldBe((5, 5, 5, 5 * fileSize));
        }

        var tracks = artists.SelectMany(x => x.Albums).SelectMany(x => x.Tracks).ToArray();
        var photos = photoAlbums.SelectMany(x => x.Photos).ToArray();
        (await dbContext.PlexTrackData.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe(
            tracks.SelectMany(x => x.MediaDataList).Select(x => x.Id),
            ignoreOrder: true
        );
        (await dbContext.PlexPhotoData.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe(
            photos.SelectMany(x => x.MediaDataList).Select(x => x.Id),
            ignoreOrder: true
        );
        (await dbContext.PlexOtherVideoData.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe(
            videos.SelectMany(x => x.MediaDataList).Select(x => x.Id),
            ignoreOrder: true
        );
        (await dbContext.PlexAlbums.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe(
            artists.SelectMany(x => x.Albums).Select(x => x.Id),
            ignoreOrder: true
        );
        (await dbContext.PlexTracks.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe(
            tracks.Select(x => x.Id),
            ignoreOrder: true
        );
        (await dbContext.PlexPhotos.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe(
            photos.Select(x => x.Id),
            ignoreOrder: true
        );
        (await dbContext.PlexMovies.CountAsync(CancellationToken)).ShouldBe(serverCount);
        (await dbContext.PlexTvShows.CountAsync(CancellationToken)).ShouldBe(serverCount);
        (await dbContext.PlexTvShowEpisodes.CountAsync(CancellationToken)).ShouldBe(serverCount * 2);
        (await dbContext.DownloadTaskArtists.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotos.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    [Arguments(DescendantCount.MusicAlbums, PlexMediaType.Music)]
    [Arguments(DescendantCount.MusicTracks, PlexMediaType.Music)]
    [Arguments(DescendantCount.Photos, PlexMediaType.Photos)]
    [Arguments(DescendantCount.PhotoClips, PlexMediaType.Photos)]
    public async Task ShouldCreateOnlyTheRequiredLibrary_WhenOnlyDescendantCountsAreConfigured(
        DescendantCount descendant,
        PlexMediaType expectedLibraryType
    )
    {
        // Arrange
        Action<FakeDataConfig> options = config =>
        {
            switch (descendant)
            {
                case DescendantCount.MusicAlbums:
                    config.MusicAlbumCount = 3;
                    break;
                case DescendantCount.MusicTracks:
                    config.MusicTrackCount = 4;
                    break;
                case DescendantCount.Photos:
                    config.PhotoCount = 40;
                    break;
                case DescendantCount.PhotoClips:
                    config.PhotoClipCount = 2;
                    break;
            }
        };

        // Act
        await SetupDatabase(81002, options);

        // Assert
        var dbContext = IDbContext;
        var library = (await dbContext.PlexLibraries.ToListAsync(CancellationToken)).ShouldHaveSingleItem();
        library.Type.ShouldBe(expectedLibraryType);
        (
            library.ArtistCount,
            library.AlbumCount,
            library.TrackCount,
            library.PhotoAlbumCount,
            library.PhotoCount,
            library.PhotoClipCount,
            library.MediaSize
        ).ShouldBe((0, 0, 0, 0, 0, 0, 0L));
        (await dbContext.PlexArtists.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexAlbums.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexTracks.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexTrackData.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexPhotoAlbums.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexPhotos.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexPhotoData.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    [Arguments(0, 3)]
    [Arguments(2, 0)]
    public async Task ShouldRespectZeroCountsInsideMusicHierarchy_WhenParentsAreRequested(
        int albumCount,
        int trackCount
    )
    {
        // Arrange
        Action<FakeDataConfig> options = config =>
        {
            config.MusicArtistCount = 2;
            config.MusicAlbumCount = albumCount;
            config.MusicTrackCount = trackCount;
        };

        // Act
        await SetupDatabase(81003, options);

        // Assert
        var dbContext = IDbContext;
        var artists = await dbContext
            .PlexArtists.Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
            .ToListAsync(CancellationToken);
        artists
            .Select(x => (x.Albums.Count, x.ChildCount, x.MediaSize))
            .ShouldBe([(albumCount, albumCount, 0L), (albumCount, albumCount, 0L)]);
        foreach (var album in artists.SelectMany(x => x.Albums))
        {
            album.Tracks.ShouldBeEmpty();
            (album.TrackCount, album.DiscCount, album.ChildCount, album.MediaSize).ShouldBe(((int?)0, (int?)0, 0, 0L));
        }

        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        (
            library.ArtistCount,
            library.AlbumCount,
            library.TrackCount,
            library.TrackFilePartCount,
            library.MediaSize
        ).ShouldBe((2, 2 * albumCount, 0, 0, 0L));
        (await dbContext.PlexTrackData.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldGenerateEmptyPhotoAlbums_WhenBothLeafCountsAreZero()
    {
        // Arrange
        Action<FakeDataConfig> options = config => config.PhotoAlbumCount = 2;

        // Act
        await SetupDatabase(81004, options);

        // Assert
        var dbContext = IDbContext;
        var albums = await dbContext.PlexPhotoAlbums.Include(x => x.Photos).ToListAsync(CancellationToken);
        albums.Select(x => (x.Photos.Count, x.MediaSize)).ShouldBe([(0, 0L), (0, 0L)]);
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        (
            library.PhotoAlbumCount,
            library.PhotoCount,
            library.PhotoClipCount,
            library.PhotoFilePartCount,
            library.MediaSize
        ).ShouldBe((2, 0, 0, 0, 0L));
        (await dbContext.PlexPhotoData.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldKeepMediaEmpty_WhenOnlyLibrariesOrDefaultsAreRequested(bool requestLibraries)
    {
        // Arrange
        Action<FakeDataConfig> options = config =>
        {
            if (!requestLibraries)
                return;

            config.PlexMusicLibraryCount = 1;
            config.PlexPhotoLibraryCount = 1;
            config.PlexOtherVideoLibraryCount = 1;
        };

        // Act
        await SetupDatabase(81005, options);

        // Assert
        var dbContext = IDbContext;
        var types = await dbContext.PlexLibraries.Select(x => x.Type).ToListAsync(CancellationToken);
        types.ShouldBe(
            requestLibraries ? new[] { PlexMediaType.Music, PlexMediaType.Photos, PlexMediaType.OtherVideos } : [],
            ignoreOrder: true
        );
        (await dbContext.PlexArtists.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexPhotoAlbums.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexOtherVideos.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexTrackData.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexPhotoData.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexOtherVideoData.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }
}
