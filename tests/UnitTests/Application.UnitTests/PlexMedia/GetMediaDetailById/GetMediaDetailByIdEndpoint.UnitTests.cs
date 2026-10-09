using Reaparr.Application.Contracts.Validators;

namespace Reaparr.Application.UnitTests;

public class GetMediaDetailByIdEndpointUnitTests
    : BaseEndpointUnitTest<GetMediaDetailByIdEndpoint, GetMediaDetailByIdEndpointRequest, ResultDTO<PlexMediaDTO>>
{
    private PlexMediaDTOValidator PlexMediaDtoValidator => new();

    [Test]
    public async Task ShouldHavePlexMediaData_WhenValidMediaIdAndPlexMediaTypeMovieIsRequested()
    {
        // Arrange
        var movieCount = 10;
        await SetupDatabase(
            45588,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.MovieCount = movieCount;
            }
        );

        var testMovie = IDbContext.PlexMovies.FirstOrDefault(x => x.HasThumb);
        testMovie.ShouldNotBeNull();

        var request = new GetMediaDetailByIdEndpointRequest(testMovie.Id, PlexMediaType.Movie);

        Mock.SetupCommand<Result>(x => x is ApplyComparisonStateCommand).ReturnsAsync(Result.Ok());

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldNotBeNull();

        var validationResult = await PlexMediaDtoValidator.ValidateAsync(result.Value, CancellationToken);
        validationResult.Errors.ShouldBeEmpty();
        result.Value.Children.ShouldBeEmpty();
        result.Value.DiscNumber.ShouldBeNull();
        result.Value.TrackNumber.ShouldBeNull();
    }

    [Test]
    public async Task ShouldHavePlexMediaData_WhenValidMediaIdAndPlexMediaTypeTvShowIsRequested()
    {
        // Arrange
        await SetupDatabase(
            53442,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 10;
                config.TvShowSeasonCount = 3;
                config.TvShowEpisodeCount = 5;
            }
        );

        var testTvShow = IDbContext.PlexTvShows.FirstOrDefault(x => x.HasThumb);
        testTvShow.ShouldNotBeNull();

        var request = new GetMediaDetailByIdEndpointRequest(testTvShow.Id, PlexMediaType.TvShow);

        Mock.SetupCommand<Result>(x => x is ApplyComparisonStateCommand).ReturnsAsync(Result.Ok());

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldNotBeNull();

        var validationResult = await PlexMediaDtoValidator.ValidateAsync(result.Value, CancellationToken);
        validationResult.Errors.ShouldBeEmpty();
        result.Value.Children.ShouldNotBeEmpty();
        result.Value.DiscNumber.ShouldBeNull();
        result.Value.TrackNumber.ShouldBeNull();
        foreach (var season in result.Value.Children)
        {
            var validationSeasonResult = await PlexMediaDtoValidator.ValidateAsync(season, CancellationToken);
            validationSeasonResult.Errors.ShouldBeEmpty();
            season.Children.ShouldNotBeEmpty();
            season.DiscNumber.ShouldBeNull();
            season.TrackNumber.ShouldBeNull();
            foreach (var episode in season.Children)
            {
                var validationEpisode = await PlexMediaDtoValidator.ValidateAsync(episode, CancellationToken);
                validationEpisode.Errors.ShouldBeEmpty();
                episode.Children.ShouldBeEmpty();
                episode.DiscNumber.ShouldBeNull();
                episode.TrackNumber.ShouldBeNull();
            }
        }
    }

    [Test]
    public async Task ShouldReturnPhotoAlbumImagesWithOriginalIds_WhenPhotoAlbumDetailIsRequested()
    {
        // Arrange
        await SetupDatabase(62307, config =>
        {
            config.PlexServerCount = 1;
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 2;
            config.PhotoCount = 2;
        });
        var dbContext = IDbContext;
        var album = await dbContext
            .PlexPhotoAlbums.Include(x => x.Photos)
            .ThenInclude(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        album.Photos.Count.ShouldBe(2);

        // Act
        var response = await TestEndpointHandleAsync(
            new GetMediaDetailByIdEndpointRequest(album.Id, PlexMediaType.PhotoAlbum)
        );

        // Assert
        response.IsValid.ShouldBeTrue();
        response.StatusCode.ShouldBe(200);
        var result = response.Response.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var dto = result.Value.ShouldNotBeNull();
        (dto.Id, dto.Type, dto.PlexLibraryId, dto.PlexServerId).ShouldBe(
            (album.Id, PlexMediaType.PhotoAlbum, album.PlexLibraryId, album.PlexServerId)
        );
        dto.ParentId.ShouldBeNull();
        dto.DiscNumber.ShouldBeNull();
        dto.TrackNumber.ShouldBeNull();
        dto.Children.Select(x => x.Id).ShouldBe(album.Photos.OrderBy(x => x.SortIndex).Select(x => x.Id));
        foreach (var photo in dto.Children)
        {
            var original = album.Photos.Single(x => x.Id == photo.Id);
            (photo.ParentId, photo.Type).ShouldBe(((int?)album.Id, PlexMediaType.PhotoImage));
            photo.Children.ShouldBeEmpty();
            photo.DiscNumber.ShouldBeNull();
            photo.TrackNumber.ShouldBeNull();
            photo.MediaData.Select(x => (x.Id, x.PlexApiMediaId, x.PlexApiPartId)).ShouldBe(
                original.MediaDataList.Select(x => (x.Id, x.PlexApiMediaId, x.PlexApiPartId))
            );
            photo.Qualities.Select(x => (x.DataId, x.MediaId, x.MediaDataType)).ShouldBe(
                original.MediaDataList.Select(x => (x.Id, original.Id, PlexMediaType.PhotoImage))
            );
            var validation = await PlexMediaDtoValidator.ValidateAsync(photo, CancellationToken);
            validation.Errors.ShouldBeEmpty();
        }
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
    }

    [Test]
    public async Task ShouldReturnMusicHierarchyWithStoredDiscAndTrackNumbers_WhenArtistDetailIsRequested()
    {
        // Arrange
        await SetupDatabase(62503, config =>
        {
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 2;
            config.MusicAlbumCount = 2;
            config.MusicTrackCount = 3;
        });
        var dbContext = IDbContext;
        var artist = await dbContext
            .PlexArtists.AsTracking()
            .Include(x => x.Albums)
            .ThenInclude(x => x.Tracks)
            .ThenInclude(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        artist.Albums.Count.ShouldBe(2);
        artist.Albums.ShouldAllBe(x => x.Tracks.Count == 3);
        var tracks = artist.Albums.SelectMany(x => x.Tracks).OrderBy(x => x.Id).ToList();
        var numbering = new (int? DiscNumber, int? TrackNumber)[]
        {
            (1, 1),
            (1, 2),
            (2, 1),
            (2, 2),
            (1, 1),
            (null, null),
        };
        for (var i = 0; i < tracks.Count; i++)
        {
            tracks[i].DiscNumber = numbering[i].DiscNumber;
            tracks[i].TrackNumber = numbering[i].TrackNumber;
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        tracks.Select(x => (x.DiscNumber, x.TrackNumber)).ShouldBe(numbering);
        var persistedTracks = await dbContext
            .PlexTracks.Where(x => x.PlexAlbum!.PlexArtistId == artist.Id)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.DiscNumber, x.TrackNumber })
            .ToListAsync(CancellationToken);
        persistedTracks.Select(x => x.Id).ShouldBe(tracks.Select(x => x.Id));
        persistedTracks.Select(x => (x.DiscNumber, x.TrackNumber)).ShouldBe(numbering);
        var isOwned = await dbContext.PlexLibraries.WhereIsOwned().AnyAsync(x => x.Id == artist.PlexLibraryId, CancellationToken);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<ApplyComparisonStateCommand>(c =>
                c.PlexLibraryId == artist.PlexLibraryId && c.MediaType == PlexMediaType.MusicArtist
                && c.Items.Count == 9), CancellationToken))
            .Returns<ICommand<Result>, CancellationToken>((cmd, ct) => isOwned
                ? new ApplyOwnedMusicComparisonStateCommandHandler(IDbContext, Mock.Mock<Quartz.IScheduler>().Object)
                    .ExecuteAsync(new ApplyOwnedMusicComparisonStateCommand(((ApplyComparisonStateCommand)cmd).Items, artist.PlexLibraryId), ct)
                : new ApplyRemoteMusicComparisonStateCommandHandler(IDbContext, Mock.Mock<Quartz.IScheduler>().Object)
                    .ExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(((ApplyComparisonStateCommand)cmd).Items, artist.PlexLibraryId), ct))
            .Verifiable(Times.Once());
        Mock.Mock<Quartz.IScheduler>().Setup(x => x.GetCurrentlyExecutingJobs(CancellationToken)).ReturnsAsync([]);
        Mock.Mock<Quartz.IScheduler>().Setup(x => x.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<Quartz.JobKey>.AnyGroup(), CancellationToken)).ReturnsAsync([]);

        // Act
        var response = await TestEndpointHandleAsync(
            new GetMediaDetailByIdEndpointRequest(artist.Id, PlexMediaType.MusicArtist)
        );

        // Assert
        response.IsValid.ShouldBeTrue();
        response.StatusCode.ShouldBe(200);
        var result = response.Response.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var dto = result.Value.ShouldNotBeNull();
        (dto.Id, dto.Type, dto.PlexLibraryId, dto.PlexServerId).ShouldBe(
            (artist.Id, PlexMediaType.MusicArtist, artist.PlexLibraryId, artist.PlexServerId)
        );
        dto.ParentId.ShouldBeNull();
        dto.DiscNumber.ShouldBeNull();
        dto.TrackNumber.ShouldBeNull();
        dto.Children.Select(x => x.Id).ShouldBe(artist.Albums.OrderBy(x => x.SortIndex).ThenBy(x => x.Id).Select(x => x.Id));
        dto.Children.ShouldAllBe(x => x.ParentId == artist.Id && x.Type == PlexMediaType.MusicAlbum);
        dto.GrandChildCount.ShouldBe(6);
        dto.Children.ShouldAllBe(x => x.DiscNumber == null && x.TrackNumber == null);
        foreach (var album in dto.Children)
        {
            var originalAlbum = artist.Albums.Single(x => x.Id == album.Id);
            album.Children.Select(x => x.Id).ShouldBe(
                originalAlbum.Tracks.OrderBy(x => x.SortIndex).ThenBy(x => x.Id).Select(x => x.Id)
            );
            foreach (var track in album.Children)
            {
                var originalTrack = originalAlbum.Tracks.Single(x => x.Id == track.Id);
                track.ParentId.ShouldBe(originalTrack.PlexAlbumId);
                track.Type.ShouldBe(PlexMediaType.MusicTrack);
                (track.Id, track.DiscNumber, track.TrackNumber).ShouldBe(
                    (originalTrack.Id, originalTrack.DiscNumber, originalTrack.TrackNumber)
                );
                track.Children.ShouldBeEmpty();
                track.ComparisonId.ShouldBe(PlexMediaComparisonState.NotCompared.ToComparisonId());
                track.MediaData.Select(x => (x.Id, x.PlexApiMediaId, x.PlexApiPartId, x.AudioCodec)).ShouldBe(
                    originalTrack.MediaDataList.Select(x => (x.Id, x.PlexApiMediaId, x.PlexApiPartId, x.AudioCodec))
                );
                track.Qualities.Select(x => (x.DataId, x.MediaId, x.MediaDataType, x.Quality)).ShouldBe(
                    originalTrack.MediaDataList.Select(x => (x.Id, originalTrack.Id, PlexMediaType.MusicTrack, VideoQuality.Unknown))
                );
                var validation = await PlexMediaDtoValidator.ValidateAsync(track, CancellationToken);
                validation.Errors.ShouldBeEmpty();
            }
        }
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReturnOtherVideoOriginalPartsAndVersionSelectors_WhenDetailIsRequested()
    {
        // Arrange
        var seed = await SetupDatabase(62504, config =>
        {
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoCount = 1;
        });
        using var dbContext = IDbContext;
        var video = await dbContext.PlexOtherVideos.Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var original = video.MediaDataList.Single();
        var parts = FakeData.GetPlexOtherVideoMediaData(seed).Generate(2);
        foreach (var part in parts)
        {
            part.PlexOtherVideoId = video.Id;
            part.UpdateInitProperty(nameof(part.PlexLibraryId), video.PlexLibraryId);
            part.UpdateInitProperty(nameof(part.PlexServerId), video.PlexServerId);
            part.UpdateInitProperty(nameof(part.PlexApiRatingKey), video.PlexApiRatingKey);
        }
        parts[0].UpdateInitProperty(nameof(original.PlexApiMediaId), original.PlexApiMediaId);
        parts[0].PartIndex = 1;
        parts[1].UpdateInitProperty(nameof(original.PlexApiMediaId), original.PlexApiMediaId + 1);
        dbContext.PlexOtherVideoData.AddRange(parts);
        await dbContext.SaveChangesAsync(CancellationToken);
        (await dbContext.PlexOtherVideoData.CountAsync(x => x.PlexOtherVideoId == video.Id, CancellationToken)).ShouldBe(3);

        // Act
        var response = await TestEndpointHandleAsync(
            new GetMediaDetailByIdEndpointRequest(video.Id, PlexMediaType.OtherVideos)
        );

        // Assert
        response.IsValid.ShouldBeTrue();
        response.StatusCode.ShouldBe(200);
        response.Response.ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        response.Response.Errors.Count.ShouldBe(0);
        var dto = response.Response.ShouldNotBeNull().Value.ShouldNotBeNull();
        dto.Type.ShouldBe(PlexMediaType.OtherVideos);
        dto.ParentId.ShouldBeNull();
        dto.Children.ShouldBeEmpty();
        dto.DiscNumber.ShouldBeNull();
        dto.TrackNumber.ShouldBeNull();
        dto.ComparisonId.ShouldBe(PlexMediaComparisonState.NotCompared.ToComparisonId());
        dto.MediaData.Select(x => (x.Id, x.PlexApiMediaId, x.PlexApiPartId)).Order().ShouldBe(
            new[] { original }.Concat(parts).Select(x => (x.Id, x.PlexApiMediaId, x.PlexApiPartId)).Order()
        );
        dto.Qualities.Select(x => (x.DataId, x.MediaId, x.MediaDataType)).Order().ShouldBe(
            new[] { original, parts[1] }.Select(x => (x.Id, video.Id, PlexMediaType.OtherVideos)).Order()
        );
        dto.MediaData.Single(x => x.Id == original.Id).FileName.ShouldBe(original.OriginalFilename.GetFileName());
        var validation = await PlexMediaDtoValidator.ValidateAsync(dto, CancellationToken);
        validation.Errors.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
    }

    [Test]
    [Arguments(PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    public async Task ShouldReturnRootWithoutChildren_WhenHierarchyIsEmpty(PlexMediaType type)
    {
        // Arrange
        await SetupDatabase(62308, config =>
        {
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 0;
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 0;
        });
        var dbContext = IDbContext;
        BasePlexMedia root = type == PlexMediaType.MusicArtist
            ? await dbContext.PlexArtists.SingleAsync(CancellationToken)
            : await dbContext.PlexPhotoAlbums.SingleAsync(CancellationToken);
        root.ChildCount.ShouldBe(0);
        if (type == PlexMediaType.MusicArtist)
        {
            var isOwned = await dbContext.PlexLibraries.WhereIsOwned().AnyAsync(x => x.Id == root.PlexLibraryId, CancellationToken);
            Mock.Mock<ICommandExecutor>()
                .Setup(x => x.Send(It.Is<ApplyComparisonStateCommand>(c => c.PlexLibraryId == root.PlexLibraryId
                    && c.MediaType == PlexMediaType.MusicArtist && c.Items.Count == 1), CancellationToken))
                .Returns<ICommand<Result>, CancellationToken>((cmd, ct) => isOwned
                    ? new ApplyOwnedMusicComparisonStateCommandHandler(IDbContext, Mock.Mock<Quartz.IScheduler>().Object)
                        .ExecuteAsync(new ApplyOwnedMusicComparisonStateCommand(((ApplyComparisonStateCommand)cmd).Items, root.PlexLibraryId), ct)
                    : new ApplyRemoteMusicComparisonStateCommandHandler(IDbContext, Mock.Mock<Quartz.IScheduler>().Object)
                        .ExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(((ApplyComparisonStateCommand)cmd).Items, root.PlexLibraryId), ct))
                .Verifiable(Times.Once());
            Mock.Mock<Quartz.IScheduler>().Setup(x => x.GetCurrentlyExecutingJobs(CancellationToken)).ReturnsAsync([]);
            Mock.Mock<Quartz.IScheduler>().Setup(x => x.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<Quartz.JobKey>.AnyGroup(), CancellationToken)).ReturnsAsync([]);
        }

        // Act
        var response = await TestEndpointHandleAsync(new GetMediaDetailByIdEndpointRequest(root.Id, type));

        // Assert
        response.IsValid.ShouldBeTrue();
        response.StatusCode.ShouldBe(200);
        var result = response.Response.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var dto = result.Value.ShouldNotBeNull();
        (dto.Id, dto.Type).ShouldBe((root.Id, type));
        dto.ParentId.ShouldBeNull();
        dto.ChildCount.ShouldBe(0);
        dto.Children.ShouldBeEmpty();
        dto.ComparisonId.ShouldBe(PlexMediaComparisonState.NotCompared.ToComparisonId());
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()),
            type == PlexMediaType.MusicArtist ? Times.Once() : Times.Never());
    }

    [Test]
    [Arguments(PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldReturnNotFound_WhenRequestedNewFamilyMediaDoesNotExist(PlexMediaType type)
    {
        // Arrange
        await SetupDatabase(62505, _ => { });

        // Act
        var response = await TestEndpointHandleAsync(new GetMediaDetailByIdEndpointRequest(int.MaxValue, type));

        // Assert
        response.IsValid.ShouldBeTrue();
        response.StatusCode.ShouldBe(404);
        response.Response.ShouldNotBeNull().IsSuccess.ShouldBeFalse();
        response.Response.Errors.Count.ShouldBe(1);
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
    }

    [Test]
    [Arguments(PlexMediaType.None)]
    [Arguments(PlexMediaType.Unknown)]
    [Arguments(PlexMediaType.Games)]
    [Arguments(PlexMediaType.Season)]
    [Arguments(PlexMediaType.Episode)]
    [Arguments(PlexMediaType.MusicAlbum)]
    [Arguments(PlexMediaType.MusicTrack)]
    [Arguments(PlexMediaType.PhotoImage)]
    [Arguments((PlexMediaType)999)]
    public async Task ShouldRejectUnsupportedDetailTypesWithoutExecutingEndpoint_WhenValidatingRequest(PlexMediaType type)
    {
        // Arrange
        var request = new GetMediaDetailByIdEndpointRequest(1, type);

        // Act
        var response = await TestEndpointHandleAsync(request);

        // Assert
        response.IsValid.ShouldBeFalse();
        response.ValidationResult.ShouldNotBeNull().Errors.Select(x => x.PropertyName).ShouldBe(
            new[] { nameof(GetMediaDetailByIdEndpointRequest.Type) }
        );
        response.Response.ShouldBeNull();
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
    }

    [Test]
    public async Task ShouldProjectEpisodeComparisonStates_WhenRemoteTvShowDetailHasCurrentOwnedComparisonScope()
    {
        // Arrange
        await SetupDatabase(
            53443,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 3;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 7, 21, 17, 24, 15, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 7, 21, 14, 8, 33, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteTvShow = await GetLibraryTvShowAsync(remoteLibrary.Id);
        var ownedTvShow = await GetLibraryTvShowAsync(ownedLibrary.Id);
        var ownedEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.TvShowId == ownedTvShow.Id)
            .OrderBy(x => x.Id)
            .Take(2)
            .ToListAsync(CancellationToken);
        var remoteEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.TvShowId == remoteTvShow.Id)
            .OrderBy(x => x.Id)
            .Take(3)
            .ToListAsync(CancellationToken);
        remoteEpisodes.Count.ShouldBe(3);
        ownedEpisodes.Count.ShouldBe(2);

        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary);
        dbContext.PlexTvShowComparisons.Add(
            CreateTvShowComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteTvShow.Id,
                ownedTvShow.Id,
                PlexMediaComparisonHitState.HigherQuality
            )
        );
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisodes[0].Id,
                ownedEpisodes[0].Id,
                PlexMediaComparisonHitState.Matched
            )
        );
        dbContext.PlexEpisodeComparisons.Add(
            CreateEpisodeComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteEpisodes[1].Id,
                ownedEpisodes[1].Id,
                PlexMediaComparisonHitState.HigherQuality
            )
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var request = new GetMediaDetailByIdEndpointRequest(remoteTvShow.Id, PlexMediaType.TvShow);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<Result>, CancellationToken>(
                (cmd, _) =>
                {
                    var items = ((ApplyComparisonStateCommand)cmd).Items;
                    items[0].ComparisonId = PlexMediaComparisonState.HigherQuality.ToComparisonId();
                    items[1].ComparisonId = PlexMediaComparisonState.Owned.ToComparisonId();
                    items[2].ComparisonId = PlexMediaComparisonState.HigherQuality.ToComparisonId();
                    items[3].ComparisonId = PlexMediaComparisonState.Missing.ToComparisonId();
                }
            )
            .ReturnsAsync(Result.Ok());

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldNotBeNull();
        result.Value.ComparisonId.ShouldBe(PlexMediaComparisonState.HigherQuality.ToComparisonId());
        var episodes = result.Value.Children.SelectMany(x => x.Children).OrderBy(x => x.Id).ToList();
        episodes.Count.ShouldBe(3);
        episodes[0].ComparisonId.ShouldBe(PlexMediaComparisonState.Owned.ToComparisonId());
        episodes[1].ComparisonId.ShouldBe(PlexMediaComparisonState.HigherQuality.ToComparisonId());
        episodes[2].ComparisonId.ShouldBe(PlexMediaComparisonState.Missing.ToComparisonId());
    }

    [Test]
    public async Task ShouldProjectShowHigherQualityAndEpisodesOwned_WhenRemoteTvShowDetailHasUpgradeAvailableButAllEpisodesMatched()
    {
        // Arrange
        await SetupDatabase(
            53444,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 3;
            }
        );

        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        var remoteLibrary = libraries[0];
        var ownedLibrary = libraries[1];
        await SetOwnedOverrideAsync(remoteLibrary.PlexServerId, false);
        await SetOwnedOverrideAsync(ownedLibrary.PlexServerId, true);
        await SetLibraryUpdatedAtAsync(remoteLibrary.Id, new DateTime(2026, 7, 21, 17, 24, 15, DateTimeKind.Utc));
        await SetLibraryUpdatedAtAsync(ownedLibrary.Id, new DateTime(2026, 7, 21, 14, 8, 33, DateTimeKind.Utc));
        remoteLibrary = await GetLibraryAsync(remoteLibrary.Id);
        ownedLibrary = await GetLibraryAsync(ownedLibrary.Id);

        var remoteTvShow = await GetLibraryTvShowAsync(remoteLibrary.Id);
        var ownedTvShow = await GetLibraryTvShowAsync(ownedLibrary.Id);
        var remoteEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.TvShowId == remoteTvShow.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var ownedEpisodes = await dbContext
            .PlexTvShowEpisodes.Where(x => x.TvShowId == ownedTvShow.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        remoteEpisodes.Count.ShouldBe(3);
        ownedEpisodes.Count.ShouldBe(3);
        await dbContext
            .PlexTvShowSeason.Where(x => x.TvShowId == remoteTvShow.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.ChildCount, remoteEpisodes.Count), CancellationToken);

        await AddCurrentScopeAsync(remoteLibrary, ownedLibrary);
        dbContext.PlexTvShowComparisons.Add(
            CreateTvShowComparison(
                remoteLibrary.Id,
                ownedLibrary.Id,
                remoteTvShow.Id,
                ownedTvShow.Id,
                PlexMediaComparisonHitState.HigherQuality
            )
        );
        for (var i = 0; i < remoteEpisodes.Count; i++)
        {
            dbContext.PlexEpisodeComparisons.Add(
                CreateEpisodeComparison(
                    remoteLibrary.Id,
                    ownedLibrary.Id,
                    remoteEpisodes[i].Id,
                    ownedEpisodes[i].Id,
                    PlexMediaComparisonHitState.Matched
                )
            );
        }

        await dbContext.SaveChangesAsync(CancellationToken);

        var request = new GetMediaDetailByIdEndpointRequest(remoteTvShow.Id, PlexMediaType.TvShow);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<Result>, CancellationToken>(
                (cmd, _) =>
                {
                    var items = ((ApplyComparisonStateCommand)cmd).Items;
                    items[0].ComparisonId = PlexMediaComparisonState.HigherQuality.ToComparisonId();
                    for (var i = 1; i < items.Count; i++)
                        items[i].ComparisonId = PlexMediaComparisonState.Owned.ToComparisonId();
                }
            )
            .ReturnsAsync(Result.Ok());

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var result = endpointResult.Response;

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldNotBeNull();
        result.Value.ComparisonId.ShouldBe(PlexMediaComparisonState.HigherQuality.ToComparisonId());
        var episodes = result.Value.Children.SelectMany(x => x.Children).OrderBy(x => x.Id).ToList();
        episodes.Count.ShouldBe(3);
        episodes.ShouldAllBe(x => x.ComparisonId == PlexMediaComparisonState.Owned.ToComparisonId());
    }

}

public class GetMediaDetailByIdMusicComparisonEndpointUnitTests
    : BaseEndpointUnitTest<GetMediaDetailByIdEndpoint, GetMediaDetailByIdEndpointRequest, ResultDTO<PlexMediaDTO>>
{
    [Test]
    public async Task ShouldMapActualMusicCoverageByTypedIdentity_WhenArtistAlbumAndTrackIdsCollide()
    {
        // Arrange
        await SetupDatabase(963530, config =>
        {
            config.PlexServerCount = 2; config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1; config.MusicAlbumCount = 1; config.MusicTrackCount = 2;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remote = libraries[0];
        var owned = libraries[1];
        await SetOwnedOverrideAsync(remote.PlexServerId, false);
        await SetOwnedOverrideAsync(owned.PlexServerId, true);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, false), CancellationToken);
        var remoteArtist = await dbContext.PlexArtists.SingleAsync(x => x.PlexLibraryId == remote.Id, CancellationToken);
        var ownedArtist = await dbContext.PlexArtists.SingleAsync(x => x.PlexLibraryId == owned.Id, CancellationToken);
        var remoteAlbum = await dbContext.PlexAlbums.SingleAsync(x => x.PlexArtistId == remoteArtist.Id, CancellationToken);
        var ownedAlbum = await dbContext.PlexAlbums.SingleAsync(x => x.PlexArtistId == ownedArtist.Id, CancellationToken);
        var remoteTracks = await dbContext.PlexTracks.Where(x => x.PlexAlbumId == remoteAlbum.Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var ownedTrack = await dbContext.PlexTracks.Where(x => x.PlexAlbumId == ownedAlbum.Id).OrderBy(x => x.Id).FirstAsync(CancellationToken);
        remoteArtist.Id.ShouldBe(remoteAlbum.Id);
        remoteArtist.Id.ShouldBe(remoteTracks[0].Id);
        dbContext.PlexComparisonScopes.Add(new PlexComparisonState
        {
            RemotePlexLibraryId = remote.Id, OwnedPlexLibraryId = owned.Id,
            MediaType = PlexMediaType.MusicArtist, CompletedAt = DateTime.UtcNow,
        });
        dbContext.PlexMusicArtistComparisons.Add(new PlexMusicArtistComparison
        {
            RemotePlexLibraryId = remote.Id, OwnedPlexLibraryId = owned.Id,
            RemotePlexMediaId = remoteArtist.Id, OwnedPlexMediaId = ownedArtist.Id,
            MatchType = PlexMediaComparisonMatchType.MusicBrainzArtistId, ComparedAt = DateTime.UtcNow,
        });
        dbContext.PlexMusicAlbumComparisons.Add(new PlexMusicAlbumComparison
        {
            RemotePlexLibraryId = remote.Id, OwnedPlexLibraryId = owned.Id,
            RemotePlexMediaId = remoteAlbum.Id, OwnedPlexMediaId = ownedAlbum.Id,
            MatchType = PlexMediaComparisonMatchType.MusicBrainzReleaseId, ComparedAt = DateTime.UtcNow,
        });
        dbContext.PlexMusicTrackComparisons.Add(new PlexMusicTrackComparison
        {
            RemotePlexLibraryId = remote.Id, OwnedPlexLibraryId = owned.Id,
            RemotePlexMediaId = remoteTracks[0].Id, OwnedPlexMediaId = ownedTrack.Id,
            MatchType = PlexMediaComparisonMatchType.MusicBrainzReleaseTrackId, ComparedAt = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync(CancellationToken);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<ApplyComparisonStateCommand>(c => c.MediaType == PlexMediaType.MusicArtist
                && c.PlexLibraryId == remote.Id && c.Items.Count == 4), CancellationToken))
            .Returns<ICommand<Result>, CancellationToken>((cmd, ct) =>
                new ApplyRemoteMusicComparisonStateCommandHandler(IDbContext, Mock.Mock<Quartz.IScheduler>().Object)
                    .ExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(((ApplyComparisonStateCommand)cmd).Items, remote.Id), ct))
            .Verifiable(Times.Once());

        // Act
        var response = await TestEndpointHandleAsync(new GetMediaDetailByIdEndpointRequest(remoteArtist.Id, PlexMediaType.MusicArtist));

        // Assert
        response.IsValid.ShouldBeTrue();
        response.StatusCode.ShouldBe(200);
        var result = response.Response.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var root = result.Value.ShouldNotBeNull();
        (root.Id, root.Type, root.ComparisonId).ShouldBe((remoteArtist.Id, PlexMediaType.MusicArtist, PlexMediaComparisonState.Partial.ToComparisonId()));
        var album = root.Children.ShouldHaveSingleItem();
        (album.Id, album.Type, album.ComparisonId).ShouldBe((remoteAlbum.Id, PlexMediaType.MusicAlbum, PlexMediaComparisonState.Partial.ToComparisonId()));
        album.Children.OrderBy(x => x.Id).Select(x => (x.Id, x.Type, x.ComparisonId)).ShouldBe(
            remoteTracks.Select(x => (x.Id, PlexMediaType.MusicTrack,
                (x.Id == remoteTracks[0].Id ? PlexMediaComparisonState.Owned : PlexMediaComparisonState.Missing).ToComparisonId())));
        Mock.Mock<ICommandExecutor>().Verify();
    }
}
