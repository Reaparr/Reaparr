using System.Net;
using LukeHagar.PlexAPI.SDK;
using LukeHagar.PlexAPI.SDK.Models.Components;
using LukeHagar.PlexAPI.SDK.Models.Requests;
using Reaparr.Application.Contracts;

namespace Reaparr.PlexApi.UnitTests;

public class GetLibraryMediaTotalsCommandUnitTests : BaseUnitTest<GetLibraryMediaTotalsCommandHandler>
{
    [Test]
    public async Task ShouldPreflightEveryProgressLayerBeforeRetrieval()
    {
        // Arrange
        await SetupDatabase(9205, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 1;
            config.PlexMovieLibraryCount = 0;
            config.PlexTvShowLibraryCount = 1;
        });
        var library = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        var sectionId = long.Parse(library.Key);
        var expectedTotals = new Dictionary<PlexMediaType, int>
        {
            [PlexMediaType.TvShow] = 208,
            [PlexMediaType.Season] = 1088,
            [PlexMediaType.Episode] = 17185,
        };
        var sdk = new Mock<IPlexAPI>();

        foreach (var (mediaType, total) in expectedTotals)
        {
            sdk.Setup(x =>
                    x.Content.ListContentAsync(
                        It.Is<ListContentRequest>(q =>
                            q.SectionId == sectionId
                            && q.XPlexContainerStart == 0
                            && q.XPlexContainerSize == 0
                            && q.MediaQuery != null
                            && q.MediaQuery.Type == mediaType.ToMediaType()
                            && q.MediaQuery.Sort == "titleSort:asc"
                            && q.IncludeOptionalElements == null
                            && q.PmsApiVersion == null
                        )
                    )
                )
                .ReturnsAsync(CreateResponse(total))
                .Verifiable(Times.Once());
        }

        Mock.Mock<IPlexApiClientFactory>()
            .Setup(x => x.CreateClientAsync(library.PlexServerId, 10, 1, null, It.IsAny<CancellationToken>()))
            .Returns<int, int, int, Action<HttpRequestRetryProgress>?, CancellationToken>(
                (_, _, _, _, _) => Task.FromResult(Result.Ok(sdk.Object))
            )
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibraryMediaTotalsCommand(library), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expectedTotals);
        sdk.Verify();
        Mock.Mock<IPlexApiClientFactory>().Verify();
    }

    private static ListContentResponse CreateResponse(long total) =>
        new()
        {
            StatusCode = 200,
            RawResponse = new HttpResponseMessage(HttpStatusCode.OK),
            MediaContainerWithMetadata = new MediaContainerWithMetadata
            {
                MediaContainer = new MediaContainerWithMetadataMediaContainer
                {
                    TotalSize = total,
                    Offset = 0,
                    Size = 0,
                    Metadata = [],
                },
            },
        };
}
