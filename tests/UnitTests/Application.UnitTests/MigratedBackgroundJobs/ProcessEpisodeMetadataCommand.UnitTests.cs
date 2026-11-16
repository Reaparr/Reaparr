namespace Reaparr.Application.UnitTests;

public class ProcessEpisodeMetadataCommandUnitTests : BaseCommandUnitTest<ProcessEpisodeMetadataCommand>
{
    [Test]
    [Arguments("", -1, -1)]
    [Arguments("The Test Show", 2, 3)]
    public async Task ShouldPersistPlexEpisodeTitleAndTimestamp_WhenDefaultStreamIsAudio(
        string seriesTitle,
        int seasonNumber,
        int episodeNumber
    )
    {
        // Arrange
        var seed = await SetupDatabase(771003, config =>
        {
            config.PlexServerCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 1;
        });

        var dbContext = IDbContext;
        var targetServer = await dbContext.PlexServers.SingleAsync(CancellationToken);
        var target = await dbContext.PlexTvShowEpisodeData.AsTracking().SingleAsync(CancellationToken);
        target.NeedsGeneratedName = true;
        target.GeneratedFilename = null;
        target.GeneratedNameSyncedAt = null;
        target.UpdateInitProperty(nameof(BasePlexMediaData.Source), ReleaseSource.None);
        await dbContext.SaveChangesAsync(CancellationToken);

        var metadata = FakeData.GetLibraryMediaItemDTO(seed, mediaType: PlexMediaType.Episode).Generate();
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
            Title = "The Test Episode",
            GrandparentTitle = seriesTitle,
            ParentIndex = seasonNumber,
            Index = episodeNumber,
            Year = 2024,
            Media = [media],
        };

        const string expectedFilename = "The.Test.Episode.2024.1080p.DV.x265.AC3.5.1";
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
            new ProcessEpisodeMetadataCommand(targetServer.Id, targetServer.Name)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(1);

        var persisted = await IDbContext.PlexTvShowEpisodeData.SingleAsync(x => x.Id == target.Id, CancellationToken);
        persisted.PlexServerId.ShouldBe(targetServer.Id);
        persisted.PlexLibraryId.ShouldBe(target.PlexLibraryId);
        persisted.GeneratedFilename.ShouldBe(expectedFilename);
        persisted.NeedsGeneratedName.ShouldBeFalse();
        persisted.GeneratedNameSyncedAt.ShouldNotBeNull();
        persisted.GeneratedNameSyncedAt.Value.ShouldBeGreaterThanOrEqualTo(startedAt);
        persisted.GeneratedNameSyncedAt.Value.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
        Mock.Mock<ICommandExecutor>().Verify();
    }
}
