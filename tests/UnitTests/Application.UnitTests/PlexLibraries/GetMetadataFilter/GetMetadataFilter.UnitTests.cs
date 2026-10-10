namespace Reaparr.Application.UnitTests;

public class GetMetadataFilterUnitTests
    : BaseEndpointUnitTest<GetMetadataFilter, GetMetadataFilterRequest, ResultDTO<PlexMediaFilterMetadataDTO>>
{
    [Test]
    [Arguments(PlexMediaType.Movie, false)]
    [Arguments(PlexMediaType.Movie, true)]
    [Arguments(PlexMediaType.OtherVideos, false)]
    [Arguments(PlexMediaType.OtherVideos, true)]
    public async Task ShouldReturnOnlyScopedDistinctQualities_WhenBrowsingMetadata(
        PlexMediaType mediaType,
        bool allLibraries
    )
    {
        // Arrange
        await SetupDatabase(8238, cfg =>
        {
            cfg.PlexMovieLibraryCount = 2;
            cfg.PlexOtherVideoLibraryCount = 2;
            cfg.MovieCount = 2;
            cfg.OtherVideoCount = 2;
        });
        var dbContext = IDbContext;
        var libraryIds = await dbContext.PlexLibraries.Where(x => x.Type == mediaType)
            .OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        libraryIds.Count.ShouldBe(2);
        var targetId = libraryIds[0];
        await dbContext.PlexMovieData.ExecuteUpdateAsync(x => x.SetProperty(y => y.Quality,
            y => mediaType == PlexMediaType.Movie
                ? (y.PlexLibraryId == targetId ? VideoQuality.FullHD : VideoQuality.UHD_4K)
                : VideoQuality.SD), CancellationToken);
        await dbContext.PlexOtherVideoData.ExecuteUpdateAsync(x => x.SetProperty(y => y.Quality,
            y => mediaType == PlexMediaType.OtherVideos
                ? (y.PlexLibraryId == targetId ? VideoQuality.FullHD : VideoQuality.UHD_4K)
                : VideoQuality.SD), CancellationToken);
        var request = new GetMetadataFilterRequest
        {
            PlexLibraryId = allLibraries ? 0 : targetId,
            MediaType = mediaType,
        };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.StatusCode.ShouldBe(200);
        var response = endpointResult.Response.ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        response.Value.ShouldNotBeNull().Qualities.Order().ShouldBe(
            allLibraries
                ? new[] { VideoQuality.FullHD.ToId(), VideoQuality.UHD_4K.ToId() }.Order()
                : new[] { VideoQuality.FullHD.ToId() });
    }
}
