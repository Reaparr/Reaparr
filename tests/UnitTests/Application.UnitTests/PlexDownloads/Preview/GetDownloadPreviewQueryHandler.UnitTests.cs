namespace Reaparr.Application.UnitTests;

public class GetDownloadPreviewQueryHandlerUnitTests : BaseUnitTest<GetDownloadPreviewQueryHandler>
{
    [Test]
    public async Task ShouldReturnNoDownloadPreview_WhenEmptyListIsGiven()
    {
        // Arrange
        await SetupDatabase(15140);

        var request = new GetDownloadPreviewQuery([]);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Test]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.PhotoImage)]
    public async Task ShouldReturnAlbumImageHierarchyWithSourceIds_WhenPhotoSelectionIsRequested(PlexMediaType type)
    {
        // Arrange
        await SetupDatabase(62306, config =>
        {
            config.PlexServerCount = 1;
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 2;
        });
        var album = await IDbContext
            .PlexPhotoAlbums.Include(x => x.Photos)
            .ThenInclude(x => x.MediaDataList)
            .SingleAsync(CancellationToken);
        var selectedImages = type == PlexMediaType.PhotoAlbum ? album.Photos.ToList() : [album.Photos.First()];
        var query = new GetDownloadPreviewQuery([
            new DownloadMediaDTO
            {
                Type = type,
                PlexServerId = album.PlexServerId,
                PlexLibraryId = album.PlexLibraryId,
                MediaIds = type == PlexMediaType.PhotoAlbum ? [album.Id] : [selectedImages[0].Id],
                Qualities = [],
            },
        ]);

        // Act
        var result = await Sut.ExecuteAsync(query, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Count.ShouldBe(1);
        var previewAlbum = result.Value.Single();
        previewAlbum.Id.ShouldBe(album.Id);
        previewAlbum.MediaType.ShouldBe(PlexMediaType.PhotoAlbum);
        previewAlbum.Children.Count.ShouldBe(selectedImages.Count);
        previewAlbum.Children.Select(x => x.Id).Order().ShouldBe(selectedImages.Select(x => x.Id).Order());
        previewAlbum.Children.ShouldAllBe(x => x.PhotoAlbumId == album.Id);
        previewAlbum.Children.ShouldAllBe(x => x.MediaType == PlexMediaType.PhotoImage);
        previewAlbum.Children.ShouldAllBe(x => x.Qualities.Count == 1);
        previewAlbum.Children.SelectMany(x => x.Qualities).ShouldAllBe(x => x.MediaDataType == PlexMediaType.PhotoImage);
        previewAlbum.Size.ShouldBe(previewAlbum.Children.Sum(x => x.Size));
    }

    [Test]
    [Arguments(PlexMediaType.PhotoImage, false)]
    [Arguments(PlexMediaType.PhotoAlbum, false)]
    [Arguments(PlexMediaType.PhotoAlbum, true)]
    public async Task ShouldRejectPhotoOriginalOutsideSelection_WhenOriginalBelongsToUnselectedDescendant(
        PlexMediaType type,
        bool selectOutsiderSeparately
    )
    {
        // Arrange
        await SetupDatabase(62820, config =>
        {
            config.PlexServerCount = 1;
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 2;
            config.PhotoCount = 1;
        });
        var dbContext = IDbContext;
        var albums = await dbContext
            .PlexPhotoAlbums.Include(x => x.Photos)
            .ThenInclude(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var targetAlbum = albums[0];
        var target = targetAlbum.Photos.Single();
        var outsider = albums[1].Photos.Single();
        var outsiderOriginal = outsider.MediaDataList.Single();
        target.Id.ShouldNotBe(outsider.Id);
        target.PlexPhotoAlbumId.ShouldBe(targetAlbum.Id);
        outsider.PlexPhotoAlbumId.ShouldBe(albums[1].Id);
        outsiderOriginal.PlexPhotoId.ShouldBe(outsider.Id);
        outsider.PlexLibraryId.ShouldBe(target.PlexLibraryId);
        var selections = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = type,
                PlexServerId = target.PlexServerId,
                PlexLibraryId = target.PlexLibraryId,
                MediaIds = type == PlexMediaType.PhotoAlbum ? [targetAlbum.Id] : [target.Id],
                Qualities =
                [
                    new PlexMediaQualityDTO
                    {
                        MediaId = outsider.Id,
                        DataId = outsiderOriginal.Id,
                        MediaDataType = PlexMediaType.PhotoImage,
                        Quality = VideoQuality.Unknown,
                    },
                ],
            },
        };
        if (selectOutsiderSeparately)
        {
            selections.Add(
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.PhotoImage,
                    PlexServerId = outsider.PlexServerId,
                    PlexLibraryId = outsider.PlexLibraryId,
                    MediaIds = [outsider.Id],
                    Qualities = [],
                }
            );
        }
        var query = new GetDownloadPreviewQuery(selections);
        var pipeline = new ValidationPipeline<GetDownloadPreviewQuery, Result<List<DownloadPreview>>>(
            [new GetDownloadPreviewQueryValidator()]
        );

        // Act
        var result = await pipeline.ExecuteAsync(
            query,
            () => Sut.ExecuteAsync(query, CancellationToken),
            CancellationToken
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.ToResult().Has400BadRequestError().ShouldBeTrue();
    }

    [Test]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.PhotoImage)]
    public async Task ShouldKeepSelectedMultipartClipInPhotoHierarchy_WhenSelectorBelongsToSelection(PlexMediaType type)
    {
        // Arrange
        await SetupDatabase(62821, config =>
        {
            config.PlexServerCount = 1;
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 0;
            config.PhotoClipCount = 1;
        });
        var dbContext = IDbContext;
        var album = await dbContext
            .PlexPhotoAlbums.Include(x => x.Photos)
            .ThenInclude(x => x.MediaDataList)
            .SingleAsync(CancellationToken);
        var clip = album.Photos.Single();
        var original = clip.MediaDataList.Single();
        original.Container.ShouldBe("mp4");
        original.VideoCodec.ShouldBe("h264");
        var secondPart = FakeData.GetPlexPhotoMediaData(new Seed(62822), isClip: true)
            .RuleFor(x => x.Id, _ => 0)
            .RuleFor(x => x.PlexPhotoId, _ => clip.Id)
            .RuleFor(x => x.PlexServerId, _ => clip.PlexServerId)
            .RuleFor(x => x.PlexLibraryId, _ => clip.PlexLibraryId)
            .RuleFor(x => x.PlexApiRatingKey, _ => clip.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => original.PlexApiMediaId)
            .RuleFor(x => x.PlexApiPartId, _ => original.PlexApiPartId + 1)
            .Generate();
        var alternate = FakeData.GetPlexPhotoMediaData(new Seed(62823), isClip: true)
            .RuleFor(x => x.Id, _ => 0)
            .RuleFor(x => x.PlexPhotoId, _ => clip.Id)
            .RuleFor(x => x.PlexServerId, _ => clip.PlexServerId)
            .RuleFor(x => x.PlexLibraryId, _ => clip.PlexLibraryId)
            .RuleFor(x => x.PlexApiRatingKey, _ => clip.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => original.PlexApiMediaId + 1)
            .Generate();
        dbContext.PlexPhotoData.AddRange(secondPart, alternate);
        await dbContext.SaveChangesAsync(CancellationToken);
        var storedOriginalIds = await dbContext
            .PlexPhotoData.Where(x => x.PlexPhotoId == clip.Id)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        storedOriginalIds.ShouldBe([original.Id, secondPart.Id, alternate.Id]);
        var query = new GetDownloadPreviewQuery(
            [
                new DownloadMediaDTO
                {
                    Type = type,
                    PlexServerId = clip.PlexServerId,
                    PlexLibraryId = clip.PlexLibraryId,
                    MediaIds = type == PlexMediaType.PhotoAlbum ? [album.Id] : [clip.Id],
                    Qualities =
                    [
                        new PlexMediaQualityDTO
                        {
                            MediaId = clip.Id,
                            DataId = secondPart.Id,
                            MediaDataType = PlexMediaType.PhotoImage,
                            Quality = VideoQuality.Unknown,
                        },
                    ],
                },
            ]
        );
        var pipeline = new ValidationPipeline<GetDownloadPreviewQuery, Result<List<DownloadPreview>>>(
            [new GetDownloadPreviewQueryValidator()]
        );

        // Act
        var result = await pipeline.ExecuteAsync(
            query,
            () => Sut.ExecuteAsync(query, CancellationToken),
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var previewAlbum = result.Value.Single();
        (previewAlbum.Id, previewAlbum.MediaType, previewAlbum.ChildCount).ShouldBe((album.Id, PlexMediaType.PhotoAlbum, 1));
        var previewClip = previewAlbum.Children.Single();
        (previewClip.Id, previewClip.PhotoAlbumId, previewClip.MediaType).ShouldBe(
            (clip.Id, album.Id, PlexMediaType.PhotoImage)
        );
        previewClip.Qualities.Select(x => (x.MediaId, x.DataId, x.MediaDataType, x.Quality))
            .ShouldBe([(clip.Id, original.Id, PlexMediaType.PhotoImage, VideoQuality.Unknown)]);
        previewClip.Size.ShouldBe(original.Size + secondPart.Size);
        previewAlbum.Size.ShouldBe(previewClip.Size);
        previewClip.Children.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldRejectOriginalSelector_WhenDataIdBelongsToDifferentPhoto()
    {
        // Arrange
        await SetupDatabase(62311, config =>
        {
            config.PlexServerCount = 1;
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 2;
        });
        var images = await IDbContext.PlexPhotoImages.Include(x => x.MediaDataList).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var target = images[0];
        var wrongData = images[1].MediaDataList.Single();
        var query = new GetDownloadPreviewQuery([
            new DownloadMediaDTO
            {
                Type = PlexMediaType.PhotoImage,
                PlexServerId = target.PlexServerId,
                PlexLibraryId = target.PlexLibraryId,
                MediaIds = [target.Id],
                Qualities = [new PlexMediaQualityDTO
                {
                    MediaId = target.Id,
                    DataId = wrongData.Id,
                    MediaDataType = PlexMediaType.PhotoImage,
                    Quality = VideoQuality.Unknown,
                }],
            },
        ]);

        // Act
        var result = await Sut.ExecuteAsync(query, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Single().Message.ShouldBe("A selected photo original does not belong to that photo.");
    }

    [Test]
    [Arguments(PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.MusicAlbum)]
    [Arguments(PlexMediaType.MusicTrack)]
    public async Task ShouldReturnTypedAncestorsAndSelectedDescendants_WhenMusicIsRequested(PlexMediaType type)
    {
        // Arrange
        await SetupDatabase(62801, config =>
        {
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 2;
            config.MusicAlbumCount = 2;
            config.MusicTrackCount = 2;
        });
        var dbContext = IDbContext;
        var artist = await dbContext.PlexArtists
            .Include(x => x.Albums).ThenInclude(x => x.Tracks).ThenInclude(x => x.MediaDataList)
            .OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var album = artist.Albums.OrderBy(x => x.Id).First();
        var track = album.Tracks.OrderBy(x => x.Id).First();
        var selectedAlbums = type == PlexMediaType.MusicArtist ? artist.Albums.ToList() : [album];
        var selectedTracks = type == PlexMediaType.MusicTrack
            ? [track]
            : selectedAlbums.SelectMany(x => x.Tracks).ToList();
        var selectedId = type switch
        {
            PlexMediaType.MusicArtist => artist.Id,
            PlexMediaType.MusicAlbum => album.Id,
            _ => track.Id,
        };
        var request = new GetDownloadPreviewQuery([
            new DownloadMediaDTO
            {
                Type = type,
                PlexServerId = artist.PlexServerId,
                PlexLibraryId = artist.PlexLibraryId,
                MediaIds = [selectedId],
                Qualities = [],
            },
        ]);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var previewArtist = result.Value.Single();
        (previewArtist.Id, previewArtist.MediaType).ShouldBe((artist.Id, PlexMediaType.MusicArtist));
        previewArtist.ChildCount.ShouldBe(selectedAlbums.Count);
        previewArtist.Size.ShouldBe(selectedTracks.Sum(x => x.MediaDataList.Sum(y => y.Size)));
        previewArtist.Children.OrderBy(x => x.Id)
            .Select(x => (x.Id, x.MediaType, x.ArtistId))
            .ShouldBe(selectedAlbums.OrderBy(x => x.Id).Select(x => (x.Id, PlexMediaType.MusicAlbum, artist.Id)));
        foreach (var previewAlbum in previewArtist.Children)
        {
            var expectedTracks = selectedTracks.Where(x => x.PlexAlbumId == previewAlbum.Id).ToList();
            previewAlbum.ChildCount.ShouldBe(expectedTracks.Count);
            previewAlbum.Size.ShouldBe(expectedTracks.Sum(x => x.MediaDataList.Sum(y => y.Size)));
            previewAlbum.Children.OrderBy(x => x.Id)
                .Select(x => (x.Id, x.MediaType, x.ArtistId, x.AlbumId, x.Size, x.ChildCount))
                .ShouldBe(expectedTracks.OrderBy(x => x.Id).Select(x =>
                    (x.Id, PlexMediaType.MusicTrack, artist.Id, previewAlbum.Id,
                        x.MediaDataList.Sum(y => y.Size), x.MediaDataList.Count)));
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldKeepTypedTreesAndOriginalGroupsSeparate_WhenMediaIdsOverlap(bool selectOriginal)
    {
        // Arrange
        await SetupDatabase(62802, config =>
        {
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 1;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 1;
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 1;
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoCount = 1;
        });
        var dbContext = IDbContext;
        var movie = await dbContext.PlexMovies.SingleAsync(CancellationToken);
        var episode = await dbContext.PlexTvShowEpisodes.SingleAsync(CancellationToken);
        var track = await dbContext.PlexTracks.Include(x => x.PlexAlbum).ThenInclude(x => x!.PlexArtist)
            .Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var image = await dbContext.PlexPhotoImages.SingleAsync(CancellationToken);
        var video = await dbContext.PlexOtherVideos.Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        new[] { movie.Id, episode.Id, track.Id, image.Id, video.Id }.ShouldAllBe(x => x == movie.Id);
        var album = track.PlexAlbum!;
        var artist = album.PlexArtist!;
        var trackParts = FakeData.GetPlexMusicTrackMediaData(new Seed(62803))
            .RuleFor(x => x.Id, _ => 0)
            .RuleFor(x => x.PlexTrackId, _ => track.Id)
            .RuleFor(x => x.PlexLibraryId, _ => track.PlexLibraryId)
            .RuleFor(x => x.PlexServerId, _ => track.PlexServerId)
            .RuleFor(x => x.PlexApiRatingKey, _ => track.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => track.MediaDataList.Single().PlexApiMediaId + 1)
            .RuleFor(x => x.PartIndex, f => f.IndexFaker)
            .RuleFor(x => x.Size, _ => 100)
            .Generate(2);
        var videoParts = FakeData.GetPlexOtherVideoMediaData(new Seed(62804))
            .RuleFor(x => x.Id, _ => 0)
            .RuleFor(x => x.PlexOtherVideoId, _ => video.Id)
            .RuleFor(x => x.PlexLibraryId, _ => video.PlexLibraryId)
            .RuleFor(x => x.PlexServerId, _ => video.PlexServerId)
            .RuleFor(x => x.PlexApiRatingKey, _ => video.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => video.MediaDataList.Single().PlexApiMediaId + 1)
            .RuleFor(x => x.PartIndex, f => f.IndexFaker)
            .RuleFor(x => x.Quality, _ => VideoQuality.UHD_4K)
            .RuleFor(x => x.Size, _ => 300)
            .Generate(2);
        dbContext.PlexTrackData.AddRange(trackParts);
        dbContext.PlexOtherVideoData.AddRange(videoParts);
        await dbContext.SaveChangesAsync(CancellationToken);
        var trackData = await dbContext.PlexTrackData.Where(x => x.PlexTrackId == track.Id).ToListAsync(CancellationToken);
        var videoData = await dbContext.PlexOtherVideoData.Where(x => x.PlexOtherVideoId == video.Id).ToListAsync(CancellationToken);
        trackData.Count.ShouldBe(3);
        videoData.Count.ShouldBe(3);
        var expectedTrackData = selectOriginal ? trackParts : trackData;
        var expectedVideoData = selectOriginal ? videoParts : videoData;
        var request = new GetDownloadPreviewQuery([
            new DownloadMediaDTO
            {
                Type = PlexMediaType.Movie, MediaIds = [movie.Id],
                PlexServerId = movie.PlexServerId, PlexLibraryId = movie.PlexLibraryId, Qualities = [],
            },
            new DownloadMediaDTO
            {
                Type = PlexMediaType.Episode, MediaIds = [episode.Id],
                PlexServerId = episode.PlexServerId, PlexLibraryId = episode.PlexLibraryId, Qualities = [],
            },
            new DownloadMediaDTO
            {
                Type = PlexMediaType.MusicTrack, MediaIds = [track.Id],
                PlexServerId = track.PlexServerId, PlexLibraryId = track.PlexLibraryId,
                Qualities = selectOriginal
                    ? [new PlexMediaQualityDTO
                    {
                        MediaId = track.Id, DataId = trackParts[1].Id,
                        MediaDataType = PlexMediaType.MusicTrack, Quality = VideoQuality.Unknown,
                    }]
                    : [],
            },
            new DownloadMediaDTO
            {
                Type = PlexMediaType.PhotoImage, MediaIds = [image.Id],
                PlexServerId = image.PlexServerId, PlexLibraryId = image.PlexLibraryId, Qualities = [],
            },
            new DownloadMediaDTO
            {
                Type = PlexMediaType.OtherVideos, MediaIds = [video.Id],
                PlexServerId = video.PlexServerId, PlexLibraryId = video.PlexLibraryId,
                Qualities = selectOriginal
                    ? [new PlexMediaQualityDTO
                    {
                        MediaId = video.Id, DataId = videoParts[1].Id,
                        MediaDataType = PlexMediaType.OtherVideos, Quality = VideoQuality.UHD_4K,
                    }]
                    : [],
            },
        ]);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.OrderBy(x => x.MediaType).Select(x => (x.Id, x.MediaType))
            .ShouldBe(new[]
            {
                (movie.Id, PlexMediaType.Movie), (episode.TvShowId, PlexMediaType.TvShow),
                (artist.Id, PlexMediaType.MusicArtist), (image.PlexPhotoAlbumId, PlexMediaType.PhotoAlbum),
                (video.Id, PlexMediaType.OtherVideos),
            }.OrderBy(x => x.Item2));
        var previewShow = result.Value.Single(x => x.MediaType == PlexMediaType.TvShow);
        var previewSeason = previewShow.Children.Single();
        (previewSeason.Id, previewSeason.MediaType, previewSeason.TvShowId)
            .ShouldBe((episode.TvShowSeasonId, PlexMediaType.Season, episode.TvShowId));
        var previewEpisode = previewSeason.Children.Single();
        (previewEpisode.Id, previewEpisode.MediaType, previewEpisode.TvShowId, previewEpisode.SeasonId)
            .ShouldBe((episode.Id, PlexMediaType.Episode, episode.TvShowId, episode.TvShowSeasonId));
        var previewArtist = result.Value.Single(x => x.MediaType == PlexMediaType.MusicArtist);
        var previewAlbum = previewArtist.Children.Single();
        (previewAlbum.Id, previewAlbum.MediaType, previewAlbum.ArtistId)
            .ShouldBe((album.Id, PlexMediaType.MusicAlbum, artist.Id));
        var previewTrack = previewAlbum.Children.Single();
        (previewTrack.Id, previewTrack.MediaType, previewTrack.ArtistId, previewTrack.AlbumId)
            .ShouldBe((track.Id, PlexMediaType.MusicTrack, artist.Id, album.Id));
        previewTrack.Size.ShouldBe(expectedTrackData.Sum(x => x.Size));
        previewTrack.ChildCount.ShouldBe(expectedTrackData.Count);
        previewAlbum.Size.ShouldBe(previewTrack.Size);
        previewArtist.Size.ShouldBe(previewTrack.Size);
        previewTrack.Qualities.OrderBy(x => x.DataId).Select(x => (x.MediaId, x.DataId, x.MediaDataType, x.Quality))
            .ShouldBe(expectedTrackData.GroupBy(x => x.PlexApiMediaId).OrderBy(x => x.Min(y => y.Id))
                .Select(x => (track.Id, x.Min(y => y.Id), PlexMediaType.MusicTrack, VideoQuality.Unknown)));
        var previewPhotoAlbum = result.Value.Single(x => x.MediaType == PlexMediaType.PhotoAlbum);
        var previewImage = previewPhotoAlbum.Children.Single();
        (previewImage.Id, previewImage.MediaType, previewImage.PhotoAlbumId)
            .ShouldBe((image.Id, PlexMediaType.PhotoImage, image.PlexPhotoAlbumId));
        var previewVideo = result.Value.Single(x => x.MediaType == PlexMediaType.OtherVideos);
        previewVideo.Children.ShouldBeEmpty();
        previewVideo.Size.ShouldBe(expectedVideoData.Sum(x => x.Size));
        previewVideo.ChildCount.ShouldBe(expectedVideoData.Count);
        previewVideo.Qualities.OrderBy(x => x.DataId).Select(x => (x.MediaId, x.DataId, x.MediaDataType, x.Quality))
            .ShouldBe(expectedVideoData.GroupBy(x => x.PlexApiMediaId).OrderBy(x => x.Min(y => y.Id))
                .Select(x => (video.Id, x.Min(y => y.Id), PlexMediaType.OtherVideos, x.First().Quality)));
        result.Value.Single(x => x.MediaType == PlexMediaType.Movie).Children.ShouldBeEmpty();
        var dto = result.Value.ToDTO();
        dto.TotalSize.ShouldBe(result.Value.Sum(x => x.Size));
        dto.Previews.Select(x => x.Type).Order().ShouldBe(result.Value.Select(x => x.MediaType).Order());
        var dtoTrack = dto.Previews.Single(x => x.Type == PlexMediaType.MusicArtist)
            .Children.Single().Children.Single();
        dtoTrack.Type.ShouldBe(PlexMediaType.MusicTrack);
        dtoTrack.Qualities.OrderBy(x => x.DataId)
            .Select(x => (x.MediaId, x.DataId, x.MediaDataType, x.Quality))
            .ShouldBe(previewTrack.Qualities.OrderBy(x => x.DataId)
                .Select(x => (x.MediaId, x.DataId, x.MediaDataType, x.Quality)));
        var dtoPhoto = dto.Previews.Single(x => x.Type == PlexMediaType.PhotoAlbum).Children.Single();
        dtoPhoto.Type.ShouldBe(PlexMediaType.PhotoImage);
        dtoPhoto.Qualities.Select(x => (x.MediaId, x.DataId, x.MediaDataType, x.Quality))
            .ShouldBe(previewImage.Qualities.Select(x => (x.MediaId, x.DataId, x.MediaDataType, x.Quality)));
        var dtoVideo = dto.Previews.Single(x => x.Type == PlexMediaType.OtherVideos);
        dtoVideo.Qualities.OrderBy(x => x.DataId)
            .Select(x => (x.MediaId, x.DataId, x.MediaDataType, x.Quality))
            .ShouldBe(previewVideo.Qualities.OrderBy(x => x.DataId)
                .Select(x => (x.MediaId, x.DataId, x.MediaDataType, x.Quality)));
        var keys = dto.Previews.SelectMany(x => new[] { x }
            .Concat(x.Children)
            .Concat(x.Children.SelectMany(y => y.Children)))
            .Select(x => x.Key)
            .ToList();
        keys.Distinct().Count().ShouldBe(keys.Count);
    }
    [Test]
    public async Task ShouldReturnTheCorrectDownloadPreview_WhenMixedMediaTypes()
    {
        // Arrange
        await SetupDatabase(
            47561,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.TvShowCount = 5;
                config.TvShowSeasonCount = 5;
                config.TvShowEpisodeCount = 5;
            }
        );

        var tvShows = await IDbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .AsNoTracking()
            .ToListAsync(cancellationToken: CancellationToken);

        tvShows.Count.ShouldBe(5);

        // Verify the test data is set up correctly
        var actualSeasonsPerShow = tvShows.First().Seasons.Count;
        var actualEpisodesPerSeason = tvShows.First().Seasons.First().Episodes.Count;

        var downloadMedia = new List<DownloadMediaDTO>();

        downloadMedia.Add(
            new DownloadMediaDTO
            {
                MediaIds = tvShows.GetRange(0, 2).Select(x => x.Id).ToList(),
                Type = PlexMediaType.TvShow,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            }
        );

        downloadMedia.Add(
            new DownloadMediaDTO
            {
                MediaIds = tvShows[3]
                    .Seasons.ToList()
                    .GetRange(0, Math.Min(3, actualSeasonsPerShow))
                    .Select(x => x.Id)
                    .ToList(),
                Type = PlexMediaType.Season,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            }
        );

        downloadMedia.Add(
            new DownloadMediaDTO
            {
                MediaIds = tvShows[4]
                    .Seasons.ElementAt(Math.Min(2, actualSeasonsPerShow - 1))
                    .Episodes.Skip(1)
                    .Take(Math.Min(4, actualEpisodesPerSeason - 1))
                    .Select(x => x.Id)
                    .ToList(),
                Type = PlexMediaType.Episode,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            }
        );

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;

        value.ShouldNotBeEmpty();
        value.Count.ShouldBe(4);

        // Full tvShows should have been added (first 2 results should be TV shows)
        // First TV show: 3 seasons with 5 episodes each
        value[0].ShouldNotBeNull();
        value[0].Children.Count.ShouldBe(3);
        value[0].Children.ShouldAllBe(x => x.Children.Count == 5);

        // Second TV show: 1 season with 4 episodes
        value[1].ShouldNotBeNull();
        value[1].Children.Count.ShouldBe(1);
        value[1].Children.ShouldAllBe(x => x.Children.Count == 4);

        // Seasons check (seasons from 4th TV show): 5 seasons with 5 episodes each
        value[2].Children.Count.ShouldBe(5);
        value[2].Children.ShouldAllBe(x => x.Children.Count == 5);

        // Loose episodes (episodes from 5th TV show): 5 seasons with 5 episodes each
        value[3].Children.Count.ShouldBe(5);
        value[3].Children.ShouldAllBe(x => x.Children.Count == 5);
    }

    #region Movie Tests

    [Test]
    public async Task ShouldReturnMoviePreview_WhenMoviesWithoutQualitiesRequested()
    {
        // Arrange
        await SetupDatabase(
            12345,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
            }
        );

        var movies = await IDbContext.PlexMovies.AsNoTracking().ToListAsync(cancellationToken: CancellationToken);
        movies.Count.ShouldBe(3);

        var downloadMedia = new List<DownloadMediaDTO>
        {
            new()
            {
                MediaIds = movies.Select(x => x.Id).ToList(),
                Type = PlexMediaType.Movie,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.Count.ShouldBe(3);
        value.ShouldAllBe(x => x.MediaType == PlexMediaType.Movie);
        value.ShouldAllBe(x => x.Children.Count == 0);
        value.ShouldAllBe(x => x.Size > 0);
    }

    [Test]
    public async Task ShouldReturnMoviePreview_WhenMoviesWithSpecificQualitiesRequested()
    {
        // Arrange
        await SetupDatabase(
            23456,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );

        var movies = await IDbContext
            .PlexMovies.Include(x => x.MediaDataList)
            .AsNoTracking()
            .ToListAsync(cancellationToken: CancellationToken);

        var qualities = movies
            .SelectMany(movie =>
                movie.MediaDataList.Select(md => new PlexMediaQualityDTO
                {
                    MediaId = movie.Id,
                    DataId = md.Id,
                    MediaDataType = PlexMediaType.Movie,
                    Quality = VideoQuality.UHD_4K,
                })
            )
            .ToList();

        var downloadMedia = new List<DownloadMediaDTO>
        {
            new()
            {
                MediaIds = movies.Select(x => x.Id).ToList(),
                Type = PlexMediaType.Movie,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = qualities,
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.Count.ShouldBe(2);
        value.ShouldAllBe(x => x.MediaType == PlexMediaType.Movie);
        value.ShouldAllBe(x => x.Size > 0);
    }

    [Test]
    public async Task ShouldReturnMoviePreview_WhenMixedMoviesWithAndWithoutQualitiesRequested()
    {
        // Arrange
        await SetupDatabase(
            34567,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 4;
            }
        );

        var movies = await IDbContext
            .PlexMovies.Include(x => x.MediaDataList)
            .AsNoTracking()
            .ToListAsync(cancellationToken: CancellationToken);

        var moviesWithQuality = movies.Take(2).ToList();

        var qualities = moviesWithQuality
            .SelectMany(movie =>
                movie
                    .MediaDataList.Take(1)
                    .Select(md => new PlexMediaQualityDTO
                    {
                        MediaId = movie.Id,
                        DataId = md.Id,
                        MediaDataType = PlexMediaType.Movie,
                        Quality = VideoQuality.FullHD,
                    })
            )
            .ToList();

        var downloadMedia = new List<DownloadMediaDTO>
        {
            new()
            {
                MediaIds = movies.Select(x => x.Id).ToList(),
                Type = PlexMediaType.Movie,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = qualities,
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.Count.ShouldBe(4);
        value.ShouldAllBe(x => x.MediaType == PlexMediaType.Movie);
        value.ShouldAllBe(x => x.Size > 0);
    }

    [Test]
    public async Task ShouldReturnEmptyList_WhenMoviesWithEmptyMediaIdsRequested()
    {
        // Arrange
        await SetupDatabase(45678);

        var downloadMedia = new List<DownloadMediaDTO>
        {
            new()
            {
                MediaIds = [],
                Type = PlexMediaType.Movie,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    #endregion

    #region TV Show Tests

    [Test]
    public async Task ShouldReturnTvShowPreview_WhenFullTvShowsWithoutQualitiesRequested()
    {
        // Arrange
        await SetupDatabase(
            56789,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 3;
                config.TvShowEpisodeCount = 4;
            }
        );

        var tvShows = await IDbContext.PlexTvShows.AsNoTracking().ToListAsync(cancellationToken: CancellationToken);

        var downloadMedia = new List<DownloadMediaDTO>
        {
            new()
            {
                MediaIds = tvShows.Select(x => x.Id).ToList(),
                Type = PlexMediaType.TvShow,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.Count.ShouldBe(2);
        value.ShouldAllBe(x => x.MediaType == PlexMediaType.TvShow);
        value.ShouldAllBe(x => x.Children.Count == 3); // 3 seasons per show
        value.ShouldAllBe(x => x.Children.All(season => season.Children.Count == 4)); // 4 episodes per season
        value.ShouldAllBe(x => x.Size > 0);
        value.ShouldAllBe(x => x.ChildCount == 3);
    }

    [Test]
    public async Task ShouldReturnSeasonPreview_WhenSeasonsWithoutQualitiesRequested()
    {
        // Arrange
        await SetupDatabase(
            67890,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 3;
                config.TvShowEpisodeCount = 5;
            }
        );

        var seasons = await IDbContext
            .PlexTvShowSeason.AsNoTracking()
            .ToListAsync(cancellationToken: CancellationToken);

        var downloadMedia = new List<DownloadMediaDTO>
        {
            new()
            {
                MediaIds = seasons.Take(2).Select(x => x.Id).ToList(),
                Type = PlexMediaType.Season,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.Count.ShouldBe(1); // One TV show containing the seasons
        value[0].MediaType.ShouldBe(PlexMediaType.TvShow);
        value[0].Children.Count.ShouldBe(2); // 2 seasons requested
        value[0].Children.ShouldAllBe(x => x.MediaType == PlexMediaType.Season);
        value[0].Children.ShouldAllBe(x => x.Children.Count == 5); // 5 episodes per season
        value[0].Size.ShouldBeGreaterThan(0);
        value[0].ChildCount.ShouldBe(2);
    }

    [Test]
    public async Task ShouldReturnEpisodePreview_WhenEpisodesWithoutQualitiesRequested()
    {
        // Arrange
        await SetupDatabase(
            78901,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 6;
            }
        );

        var episodes = await IDbContext
            .PlexTvShowEpisodes.AsNoTracking()
            .ToListAsync(cancellationToken: CancellationToken);

        var downloadMedia = new List<DownloadMediaDTO>
        {
            new()
            {
                MediaIds = episodes.Take(3).Select(x => x.Id).ToList(),
                Type = PlexMediaType.Episode,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.Count.ShouldBe(1); // One TV show
        value[0].MediaType.ShouldBe(PlexMediaType.TvShow);

        // Should have seasons containing the episodes
        var totalEpisodesInSeasons = value[0].Children.Sum(season => season.Children.Count);
        totalEpisodesInSeasons.ShouldBe(3);

        value[0].Children.ShouldAllBe(x => x.MediaType == PlexMediaType.Season);
        value[0].Size.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task ShouldReturnEpisodePreview_WhenEpisodesWithSpecificQualitiesRequested()
    {
        // Arrange
        await SetupDatabase(
            89012,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 3;
            }
        );

        var episodes = await IDbContext
            .PlexTvShowEpisodes.Include(x => x.MediaDataList)
            .AsNoTracking()
            .ToListAsync(cancellationToken: CancellationToken);

        var qualities = episodes
            .SelectMany(episode =>
                episode
                    .MediaDataList.Take(1)
                    .Select(md => new PlexMediaQualityDTO
                    {
                        MediaId = episode.Id,
                        DataId = md.Id,
                        MediaDataType = PlexMediaType.Episode,
                        Quality = VideoQuality.HD,
                    })
            )
            .ToList();

        var downloadMedia = new List<DownloadMediaDTO>
        {
            new()
            {
                MediaIds = episodes.Select(x => x.Id).ToList(),
                Type = PlexMediaType.Episode,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = qualities,
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.Count.ShouldBe(1);
        value[0].MediaType.ShouldBe(PlexMediaType.TvShow);

        var totalEpisodes = value[0].Children.Sum(season => season.Children.Count);
        totalEpisodes.ShouldBe(3);

        value[0].Size.ShouldBeGreaterThan(0);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Test]
    public async Task ShouldReturnEmptyList_WhenInvalidMediaIdsProvided()
    {
        // Arrange
        await SetupDatabase(90123);

        var downloadMedia = new List<DownloadMediaDTO>
        {
            new()
            {
                MediaIds = [999999, 888888], // Non-existent IDs
                Type = PlexMediaType.Movie,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldHandleMultipleServersAndLibraries()
    {
        // Arrange
        await SetupDatabase(
            11223,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 2;
                config.MovieCount = 2;
            }
        );

        var movies = await IDbContext.PlexMovies.AsNoTracking().ToListAsync(cancellationToken: CancellationToken);

        var downloadMedia = new List<DownloadMediaDTO>
        {
            new()
            {
                MediaIds = movies.Take(2).Select(x => x.Id).ToList(),
                Type = PlexMediaType.Movie,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
            new()
            {
                MediaIds = movies.Skip(2).Select(x => x.Id).ToList(),
                Type = PlexMediaType.Movie,
                PlexServerId = 2,
                PlexLibraryId = 2,
                Qualities = [],
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.Count.ShouldBe(8); // 2 servers × 2 libraries × 2 movies each = 8 movies
        value.ShouldAllBe(x => x.MediaType == PlexMediaType.Movie);
    }

    [Test]
    public async Task ShouldReturnCorrectSizeCalculations_WhenTvShowHierarchyBuilt()
    {
        // Arrange
        await SetupDatabase(
            22334,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 3;
            }
        );

        var tvShows = await IDbContext.PlexTvShows.AsNoTracking().ToListAsync(cancellationToken: CancellationToken);

        var downloadMedia = new List<DownloadMediaDTO>
        {
            new()
            {
                MediaIds = tvShows.Select(x => x.Id).ToList(),
                Type = PlexMediaType.TvShow,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.Count.ShouldBe(1);

        var tvShow = value[0];
        var expectedSize = tvShow.Children.Sum(season => season.Size);
        tvShow.Size.ShouldBe(expectedSize);

        foreach (var season in tvShow.Children)
        {
            var expectedSeasonSize = season.Children.Sum(episode => episode.Size);
            season.Size.ShouldBe(expectedSeasonSize);
            season.ChildCount.ShouldBe(season.Children.Count);
        }

        tvShow.ChildCount.ShouldBe(tvShow.Children.Count);
    }

    [Test]
    public async Task ShouldReturnSortedResults_WhenMultipleMediaRequested()
    {
        // Arrange
        await SetupDatabase(
            33445,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 5;
                config.TvShowCount = 3;
                config.TvShowSeasonCount = 2;
                config.TvShowEpisodeCount = 2;
            }
        );

        var movies = await IDbContext.PlexMovies.AsNoTracking().ToListAsync(cancellationToken: CancellationToken);
        var tvShows = await IDbContext.PlexTvShows.AsNoTracking().ToListAsync(cancellationToken: CancellationToken);

        var downloadMedia = new List<DownloadMediaDTO>
        {
            new()
            {
                MediaIds = movies.Select(x => x.Id).ToList(),
                Type = PlexMediaType.Movie,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
            new()
            {
                MediaIds = tvShows.Select(x => x.Id).ToList(),
                Type = PlexMediaType.TvShow,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.Count.ShouldBe(8); // 5 movies + 3 TV shows

        // Check that results are grouped by media type and contain expected media
        var movieResults = value.Where(x => x.MediaType == PlexMediaType.Movie).ToList();
        var tvShowResults = value.Where(x => x.MediaType == PlexMediaType.TvShow).ToList();

        movieResults.Count.ShouldBe(5); // 5 movies as configured
        tvShowResults.Count.ShouldBe(3); // 3 TV shows as configured

        // Verify all results have valid titles and sizes
        value.ShouldAllBe(x => !string.IsNullOrEmpty(x.Title));
        value.ShouldAllBe(x => x.Size > 0);
    }

    [Test]
    public async Task ShouldHandleMixedQualitiesAcrossDifferentMediaTypes()
    {
        // Arrange
        await SetupDatabase(
            44556,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 2;
            }
        );

        var movies = await IDbContext
            .PlexMovies.Include(x => x.MediaDataList)
            .AsNoTracking()
            .ToListAsync(cancellationToken: CancellationToken);

        var episodes = await IDbContext
            .PlexTvShowEpisodes.Include(x => x.MediaDataList)
            .AsNoTracking()
            .ToListAsync(cancellationToken: CancellationToken);

        var movieQualities = movies
            .SelectMany(movie =>
                movie
                    .MediaDataList.Take(1)
                    .Select(md => new PlexMediaQualityDTO
                    {
                        MediaId = movie.Id,
                        DataId = md.Id,
                        MediaDataType = PlexMediaType.Movie,
                        Quality = VideoQuality.UHD_4K,
                    })
            )
            .ToList();

        var episodeQualities = episodes
            .SelectMany(episode =>
                episode
                    .MediaDataList.Take(1)
                    .Select(md => new PlexMediaQualityDTO
                    {
                        MediaId = episode.Id,
                        DataId = md.Id,
                        MediaDataType = PlexMediaType.Episode,
                        Quality = VideoQuality.HD,
                    })
            )
            .ToList();

        var downloadMedia = new List<DownloadMediaDTO>
        {
            new()
            {
                MediaIds = movies.Select(x => x.Id).ToList(),
                Type = PlexMediaType.Movie,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = movieQualities,
            },
            new()
            {
                MediaIds = episodes.Select(x => x.Id).ToList(),
                Type = PlexMediaType.Episode,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = episodeQualities,
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.Count.ShouldBe(3); // 2 movies + 1 TV show

        var movieResults = value.Where(x => x.MediaType == PlexMediaType.Movie).ToList();
        var tvShowResults = value.Where(x => x.MediaType == PlexMediaType.TvShow).ToList();

        movieResults.Count.ShouldBe(2);
        tvShowResults.Count.ShouldBe(1);

        movieResults.ShouldAllBe(x => x.Size > 0);
        tvShowResults.ShouldAllBe(x => x.Size > 0);
    }

    #endregion

    #region Complex Hierarchy Tests

    [Test]
    public async Task ShouldBuildCorrectHierarchy_WhenMixedTvShowSeasonsAndEpisodesRequested()
    {
        // Arrange
        await SetupDatabase(
            55667,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 3;
                config.TvShowEpisodeCount = 4;
            }
        );

        var tvShows = await IDbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
            .AsNoTracking()
            .ToListAsync(cancellationToken: CancellationToken);

        var firstShow = tvShows[0];
        var secondShow = tvShows[1];

        var downloadMedia = new List<DownloadMediaDTO>
        {
            // Full first TV show
            new()
            {
                MediaIds = [firstShow.Id],
                Type = PlexMediaType.TvShow,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
            // Some seasons from the second show
            new()
            {
                MediaIds = secondShow.Seasons.Take(2).Select(x => x.Id).ToList(),
                Type = PlexMediaType.Season,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
            // Some episodes from the second show's third season
            new()
            {
                MediaIds = secondShow.Seasons.ElementAt(2).Episodes.Take(2).Select(x => x.Id).ToList(),
                Type = PlexMediaType.Episode,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.Count.ShouldBe(2); // 2 TV shows

        // The first show should have all seasons and episodes
        var firstShowResult = value.First(x => x.Id == firstShow.Id);
        firstShowResult.Children.Count.ShouldBe(3); // All 3 seasons
        firstShowResult.Children.ShouldAllBe(x => x.Children.Count == 4); // All 4 episodes per season

        // The second show should have partial content
        var secondShowResult = value.First(x => x.Id == secondShow.Id);
        secondShowResult.Children.Count.ShouldBe(3); // 2 complete seasons + 1 partial season

        // The first two seasons should have all episodes
        secondShowResult.Children.Take(2).ShouldAllBe(x => x.Children.Count == 4);

        // The third season should have only the requested episodes
        secondShowResult.Children.ElementAt(2).Children.Count.ShouldBe(2);
    }

    [Test]
    public async Task ShouldNotDuplicateEpisodes_WhenSameEpisodeRequestedMultipleTimes()
    {
        // Arrange
        await SetupDatabase(
            66778,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 3;
            }
        );

        var episodes = await IDbContext
            .PlexTvShowEpisodes.AsNoTracking()
            .ToListAsync(cancellationToken: CancellationToken);
        var targetEpisode = episodes[0];

        var downloadMedia = new List<DownloadMediaDTO>
        {
            new()
            {
                MediaIds = [targetEpisode.Id],
                Type = PlexMediaType.Episode,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
            new()
            {
                MediaIds = [targetEpisode.Id], // Same episode requested again
                Type = PlexMediaType.Episode,
                PlexServerId = 1,
                PlexLibraryId = 1,
                Qualities = [],
            },
        };

        var request = new GetDownloadPreviewQuery(downloadMedia);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.Count.ShouldBe(1); // One TV show

        var tvShow = value[0];
        tvShow.Children.Count.ShouldBe(1); // One season
        tvShow.Children[0].Children.Count.ShouldBe(1); // One unique episode (no duplicates)
    }

    #endregion
}
