namespace Reaparr.Application.UnitTests;

public class GetOtherVideoDownloadPreviewCommandUnitTests : BaseCommandUnitTest<GetOtherVideoDownloadPreviewCommand>
{
    [Test]
    public async Task ShouldReturnOtherVideoPreviewWithSelectedQualities()
    {
        // Arrange
        await SetupDatabase(
            23901,
            config =>
            {
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoCount = 2;
            }
        );
        var videos = await IDbContext
            .PlexOtherVideos.Include(x => x.MediaDataList)
            .AsNoTracking()
            .ToListAsync(CancellationToken);
        var query = new GetOtherVideoDownloadPreviewCommand([
            new DownloadMediaDTO
            {
                Type = PlexMediaType.OtherVideos,
                SelectionKind = null,
                PlexServerId = videos[0].PlexServerId,
                PlexLibraryId = videos[0].PlexLibraryId,
                MediaIds = videos.Select(x => x.Id).ToList(),
                Qualities = videos
                    .SelectMany(x =>
                        x.MediaDataList.Take(1)
                            .Select(data => new PlexMediaQualityDTO
                            {
                                MediaId = x.Id,
                                DataId = data.Id,
                                MediaDataType = PlexMediaType.OtherVideos,
                                VideoQuality = VideoQuality.HD,
                            })
                    )
                    .ToList(),
            },
        ]);

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count().ShouldBe(2);
        result.Value.ShouldAllBe(x => x.MediaType == PlexMediaType.OtherVideos);
        result.Value.ShouldAllBe(x => x.Qualities.Count == 1);
        result.Value.ShouldAllBe(x => x.Size > 0);
    }

    [Test]
    [Arguments(DownloadStatus.Queued)]
    [Arguments(DownloadStatus.Completed)]
    public async Task ShouldPreviewSelectedOtherVideo_WhenMatchingDownloadTaskAlreadyExists(DownloadStatus status)
    {
        // Arrange
        await SetupDatabase(
            23913,
            config =>
            {
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoCount = 2;
            }
        );
        var dbContext = IDbContext;
        var videos = await dbContext
            .PlexOtherVideos.Include(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var video = videos[0];
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.OtherVideos,
            SelectionKind = null,
            PlexServerId = video.PlexServerId,
            PlexLibraryId = video.PlexLibraryId,
            MediaIds = [video.Id],
            Qualities = [],
        };
        var creator = Mock.Create<GenerateDownloadTaskOtherVideosCommandHandler>();
        var created = await creator.ExecuteAsync(
            new GenerateDownloadTaskOtherVideosCommand([selection]),
            CancellationToken
        );
        created.IsSuccess.ShouldBeTrue();
        created.Errors.Count.ShouldBe(0);
        created.Value.ShouldBe(new DownloadTaskCreationReport { OtherVideos = 1 });
        var task = await dbContext
            .DownloadTaskOtherVideos.AsTracking().Include(x => x.Children)
            .SingleAsync(CancellationToken);
        var data = video.MediaDataList.Single();
        task.PlexApiRatingKey.ShouldBe(video.PlexApiRatingKey);
        task.PlexServerId.ShouldBe(video.PlexServerId);
        task.PlexLibraryId.ShouldBe(video.PlexLibraryId);
        task.Children.Single().PlexApiRatingKey.ShouldBe(data.PlexApiRatingKey);
        task.DownloadStatus = status;
        task.Children.Single().DownloadStatus = status;
        await dbContext.SaveChangesAsync(CancellationToken);
        var persistedTask = await dbContext.DownloadTaskOtherVideos.Include(x => x.Children).SingleAsync(CancellationToken);
        persistedTask.DownloadStatus.ShouldBe(status);
        persistedTask.Children.Single().DownloadStatus.ShouldBe(status);
        var expectedSize = video.MediaDataList.Sum(x => x.Size);
        var expectedPartCount = video.MediaDataList.Count;

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(
            new GetOtherVideoDownloadPreviewCommand([selection])
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Select(x => (x.Id, x.Title, x.MediaType, x.ChildCount, x.Size))
            .ShouldBe([(video.Id, video.Title, PlexMediaType.OtherVideos, expectedPartCount, expectedSize)]);
        var preview = result.Value.Single();
        preview.Qualities
            .Select(x => (x.MediaId, x.DataId, x.MediaDataType, x.VideoQuality, x.AudioQuality))
            .ShouldBe([(video.Id, data.Id, PlexMediaType.OtherVideos, data.VideoQuality, AudioQuality.None)]);
    }
}
