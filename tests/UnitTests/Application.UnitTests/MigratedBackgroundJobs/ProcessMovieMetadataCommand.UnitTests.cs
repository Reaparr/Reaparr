namespace Reaparr.Application.UnitTests;

public class ProcessMovieMetadataCommandUnitTests : BaseCommandUnitTest<ProcessMovieMetadataCommand>
{
    [Test]
    public async Task ShouldPersistGeneratedMovieNameAndTimestamp_WhenDefaultStreamIsAudio()
    {
        // Arrange
        var seed = await SetupDatabase(
            771001,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
            }
        );

        var dbContext = IDbContext;
        var targetServer = await dbContext.PlexServers.OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var target = await dbContext
            .PlexMovieData.AsTracking()
            .SingleAsync(x => x.PlexServerId == targetServer.Id, CancellationToken);
        var control = await dbContext
            .PlexMovieData.AsTracking()
            .SingleAsync(x => x.PlexServerId != targetServer.Id, CancellationToken);

        target.NeedsGeneratedName = true;
        target.GeneratedFilename = null;
        target.GeneratedNameSyncedAt = null;
        target.UpdateInitProperty(nameof(BasePlexMediaData.Source), ReleaseSource.None);
        control.NeedsGeneratedName = true;
        control.GeneratedFilename = null;
        control.GeneratedNameSyncedAt = null;
        await dbContext.SaveChangesAsync(CancellationToken);

        var metadata = FakeData.GetLibraryMediaItemDTO(seed, mediaType: PlexMediaType.Movie).Generate();
        var audioStream = FakeData.GetLibraryMediaItemStreamDTO(seed, StreamType.Audio).Generate() with
        {
            Default = true,
            Codec = "ac3",
            Language = "English",
            LanguageCode = "en",
        };
        var videoStream = FakeData.GetLibraryMediaItemStreamDTO(seed, StreamType.Video).Generate() with
        {
            Default = false,
            Codec = "hevc",
            DOVIPresent = true,
            Profile = "high",
            ColorPrimaries = "bt709",
            ColorTrc = "bt709",
        };
        var part = FakeData.GetLibraryMediaItemPartDTO(seed).Generate() with
        {
            Id = target.PlexApiPartId,
            Stream = [audioStream, videoStream],
        };
        var media = metadata.Media.Single() with
        {
            Id = target.PlexApiMediaId,
            VideoResolution = VideoQuality.FullHD,
            VideoCodec = "hevc",
            AudioCodec = "ac3",
            AudioProfile = "main",
            AudioChannels = 6,
            Parts = [part],
        };
        metadata = metadata with
        {
            RatingKey = target.PlexApiRatingKey,
            Title = "The Test Movie",
            Year = 2024,
            Media = [media],
        };

        const string expectedFilename = "The.Test.Movie.2024.1080p.DV.x265.AC3.5.1";
        var startedAt = DateTime.UtcNow;

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetDetailMetadataByRatingKeysCommand>(request =>
                        request.PlexServerId == targetServer.Id
                        && request.RatingKeys.SequenceEqual(new[] { target.PlexApiRatingKey.ToString() })
                    ),
                    It.Is<CancellationToken>(token => token == CancellationToken)
                )
            )
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO> { metadata }))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<int>(
            new ProcessMovieMetadataCommand(targetServer.Id, targetServer.Name)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(1);

        var persistedTarget = await IDbContext.PlexMovieData.SingleAsync(x => x.Id == target.Id, CancellationToken);
        persistedTarget.PlexServerId.ShouldBe(targetServer.Id);
        persistedTarget.PlexLibraryId.ShouldBe(target.PlexLibraryId);
        persistedTarget.GeneratedFilename.ShouldBe(expectedFilename);
        persistedTarget.NeedsGeneratedName.ShouldBeFalse();
        persistedTarget.GeneratedNameSyncedAt.ShouldNotBeNull();
        persistedTarget.GeneratedNameSyncedAt.Value.ShouldBeGreaterThanOrEqualTo(startedAt);
        persistedTarget.GeneratedNameSyncedAt.Value.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);

        var persistedControl = await IDbContext.PlexMovieData.SingleAsync(x => x.Id == control.Id, CancellationToken);
        persistedControl.PlexServerId.ShouldBe(control.PlexServerId);
        persistedControl.PlexLibraryId.ShouldBe(control.PlexLibraryId);
        persistedControl.GeneratedFilename.ShouldBeNull();
        persistedControl.NeedsGeneratedName.ShouldBeTrue();
        persistedControl.GeneratedNameSyncedAt.ShouldBeNull();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldLeaveMoviePendingAndReportZero_WhenMetadataHasNoVideoStream()
    {
        // Arrange
        var seed = await SetupDatabase(
            771002,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
            }
        );

        var dbContext = IDbContext;
        var targetServer = await dbContext.PlexServers.SingleAsync(CancellationToken);
        var target = await dbContext.PlexMovieData.AsTracking().SingleAsync(CancellationToken);
        target.NeedsGeneratedName = true;
        target.GeneratedFilename = null;
        target.GeneratedNameSyncedAt = null;
        await dbContext.SaveChangesAsync(CancellationToken);

        var metadata = FakeData.GetLibraryMediaItemDTO(seed, mediaType: PlexMediaType.Movie).Generate();
        var audioStream = FakeData.GetLibraryMediaItemStreamDTO(seed, StreamType.Audio).Generate() with
        {
            Default = true,
            Codec = "ac3",
            Language = "English",
            LanguageCode = "en",
        };
        var part = FakeData.GetLibraryMediaItemPartDTO(seed).Generate() with
        {
            Id = target.PlexApiPartId,
            Stream = [audioStream],
        };
        var media = metadata.Media.Single() with { Id = target.PlexApiMediaId, Parts = [part] };
        metadata = metadata with { RatingKey = target.PlexApiRatingKey, Media = [media] };

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GetDetailMetadataByRatingKeysCommand>(request =>
                        request.PlexServerId == targetServer.Id
                        && request.RatingKeys.SequenceEqual(new[] { target.PlexApiRatingKey.ToString() })
                    ),
                    It.Is<CancellationToken>(token => token == CancellationToken)
                )
            )
            .ReturnsAsync(Result.Ok(new List<LibraryMediaItemDTO> { metadata }))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<int>(
            new ProcessMovieMetadataCommand(targetServer.Id, targetServer.Name)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(0);

        var persisted = await IDbContext.PlexMovieData.SingleAsync(x => x.Id == target.Id, CancellationToken);
        persisted.GeneratedFilename.ShouldBeNull();
        persisted.NeedsGeneratedName.ShouldBeTrue();
        persisted.GeneratedNameSyncedAt.ShouldBeNull();
        Mock.Mock<ICommandExecutor>().Verify();
    }
}
