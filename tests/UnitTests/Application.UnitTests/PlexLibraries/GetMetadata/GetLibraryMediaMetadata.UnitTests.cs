namespace Reaparr.Application.UnitTests;

public class GetLibraryMediaMetadataUnitTests
    : BaseEndpointUnitTest<GetLibraryMediaMetadata, GetLibraryMediaMetadataRequest, PlexMediaMetadataDTO>
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReturnExactOtherVideoQualityCounts_WhenBrowsingMetadata(bool allLibraries)
    {
        // Arrange
        await SetupDatabase(8239, cfg =>
        {
            cfg.PlexOtherVideoLibraryCount = 2;
            cfg.OtherVideoCount = 2;
            cfg.PlexMovieLibraryCount = 1;
            cfg.MovieCount = 1;
        });
        var dbContext = IDbContext;
        var libraryIds = await dbContext.PlexLibraries.Where(x => x.Type == PlexMediaType.OtherVideos)
            .OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(2);
        var targetId = libraryIds[0];
        var originals = await dbContext.PlexOtherVideoData.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexLibraryId }).ToListAsync(CancellationToken);
        originals.Select(x => x.PlexLibraryId).Order().ShouldBe(
            new[] { libraryIds[0], libraryIds[0], libraryIds[1], libraryIds[1] });
        await dbContext.PlexOtherVideoData.ExecuteUpdateAsync(x => x.SetProperty(y => y.Quality,
            y => y.PlexLibraryId == targetId ? VideoQuality.FullHD : VideoQuality.UHD_4K), CancellationToken);
        await dbContext.PlexMovieData.ExecuteUpdateAsync(x => x.SetProperty(y => y.Quality, VideoQuality.SD), CancellationToken);
        var request = new GetLibraryMediaMetadataRequest
        {
            PlexLibraryId = allLibraries ? 0 : targetId,
            MediaType = PlexMediaType.OtherVideos,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);
        var body = endpointResult.Endpoint.HttpContext.Response.Body;
        body.Position = 0;
        var response = await System.Text.Json.JsonSerializer.DeserializeAsync<ResultDTO<PlexMediaMetadataDTO>>(
            body, DefaultJsonSerializerOptions.ConfigStandard, CancellationToken);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(200);
        response.ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var metadata = response.Value.ShouldNotBeNull();
        metadata.MediaCount.ShouldBe(allLibraries ? 4 : 2);
        metadata.QualityCount.ShouldBe(allLibraries ? 2 : 1);
        metadata.Qualities.OrderBy(x => x.Quality).Select(x => (x.Quality, x.Name, x.Count)).ShouldBe(
            allLibraries
                ? new[]
                {
                    (VideoQuality.FullHD, ((int)VideoQuality.FullHD).ToString(), 2),
                    (VideoQuality.UHD_4K, ((int)VideoQuality.UHD_4K).ToString(), 2),
                }
                : new[] { (VideoQuality.FullHD, ((int)VideoQuality.FullHD).ToString(), 2) });
    }
}
