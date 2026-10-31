namespace Reaparr.Application.UnitTests;

public class GetTvShowDownloadPreviewCommandUnitTests : BaseCommandUnitTest<GetTvShowDownloadPreviewCommand>
{
    [Test]
    public async Task ShouldNotDuplicateEpisodes_WhenSameEpisodeIsRequestedTwice()
    {
        // Arrange
        await SetupDatabase(
            66778,
            config =>
            {
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 3;
            }
        );
        var episode = (await IDbContext.PlexTvShowEpisodes.ToListAsync(CancellationToken)).First();
        var query = new GetTvShowDownloadPreviewCommand([
            new DownloadMediaDTO
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                PlexServerId = episode.PlexServerId,
                PlexLibraryId = episode.PlexLibraryId,
                MediaIds = [episode.Id],
                Qualities = [],
            },
            new DownloadMediaDTO
            {
                Type = PlexMediaType.Episode,
                SelectionKind = null,
                PlexServerId = episode.PlexServerId,
                PlexLibraryId = episode.PlexLibraryId,
                MediaIds = [episode.Id],
                Qualities = [],
            },
        ]);

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count().ShouldBe(1);
        result.Value.Single().Children.Count.ShouldBe(1);
        result.Value.Single().Children.Single().Children.Count.ShouldBe(1);
    }
}
