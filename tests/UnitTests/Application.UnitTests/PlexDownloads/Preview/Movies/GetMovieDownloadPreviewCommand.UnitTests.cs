namespace Reaparr.Application.UnitTests;

public class GetMovieDownloadPreviewCommandUnitTests : BaseCommandUnitTest<GetMovieDownloadPreviewCommand>
{
    [Test]
    public async Task ShouldReturnMoviePreviewWithSelectedQualities()
    {
        // Arrange
        await SetupDatabase(
            23456,
            config =>
            {
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
            }
        );
        var movies = await IDbContext
            .PlexMovies.Include(x => x.MediaDataList)
            .AsNoTracking()
            .ToListAsync(CancellationToken);
        var query = new GetMovieDownloadPreviewCommand([
            new DownloadMediaDTO
            {
                Type = PlexMediaType.Movie,
                SelectionKind = null,
                PlexServerId = movies[0].PlexServerId,
                PlexLibraryId = movies[0].PlexLibraryId,
                MediaIds = movies.Select(x => x.Id).ToList(),
                Qualities = movies
                    .SelectMany(x =>
                        x.MediaDataList.Take(1)
                            .Select(data => new PlexMediaQualityDTO
                            {
                                MediaId = x.Id,
                                DataId = data.Id,
                                MediaDataType = PlexMediaType.Movie,
                                VideoQuality = VideoQuality.UHD_4K,
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
        result.Value.ShouldAllBe(x => x.MediaType == PlexMediaType.Movie);
        result.Value.ShouldAllBe(x => x.Qualities.Count == 1);
        result.Value.ShouldAllBe(x => x.Size > 0);
    }
}
