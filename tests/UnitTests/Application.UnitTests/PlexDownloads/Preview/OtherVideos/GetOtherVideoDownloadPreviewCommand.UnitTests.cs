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
}
