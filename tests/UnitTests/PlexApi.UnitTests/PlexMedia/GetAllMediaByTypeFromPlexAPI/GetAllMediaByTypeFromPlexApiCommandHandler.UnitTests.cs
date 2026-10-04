using System.Net;
using LukeHagar.PlexAPI.SDK;
using LukeHagar.PlexAPI.SDK.Models.Components;
using LukeHagar.PlexAPI.SDK.Models.Requests;
using Reaparr.Application.Contracts;
using Reaparr.PlexApi.Contracts;
using Metadata = LukeHagar.PlexAPI.SDK.Models.Components.Metadata;

namespace Reaparr.PlexApi.UnitTests;

public class GetAllMediaByTypeFromPlexApiCommandHandlerUnitTests
    : BaseCommandUnitTest<GetAllMediaByTypeFromPlexApiCommand>
{
    [Test]
    public async Task ShouldAssembleAllPagesAndCompleteProgress_WhenFinalPageIsSmallerThanBatchSize()
    {
        // Arrange
        await SetupDatabase(
            9202,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexTvShowLibraryCount = 0;
                config.MovieCount = 0;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var sectionId = long.Parse(library.Key);
        var sdk = new Mock<IPlexAPI>();
        sdk.Setup(x =>
                x.Content.ListContentAsync(
                    It.Is<ListContentRequest>(q =>
                        q.SectionId == sectionId
                        && q.XPlexContainerStart == 0
                        && q.XPlexContainerSize == 0
                        && q.MediaType == 10
                        && q.Sort == "titleSort:asc"
                    )
                )
            )
            .ReturnsAsync(CreateResponse(3, 0))
            .Verifiable(Times.Once());
        sdk.Setup(x =>
                x.Content.ListContentAsync(
                    It.Is<ListContentRequest>(q =>
                        q.SectionId == sectionId
                        && q.XPlexContainerStart == 0
                        && q.XPlexContainerSize == 2
                        && q.MediaType == 10
                        && q.Sort == "titleSort:asc"
                    )
                )
            )
            .ReturnsAsync(CreateResponse(3, 0, CreateItem(1), CreateItem(2)))
            .Verifiable(Times.Once());
        sdk.Setup(x =>
                x.Content.ListContentAsync(
                    It.Is<ListContentRequest>(q =>
                        q.SectionId == sectionId
                        && q.XPlexContainerStart == 2
                        && q.XPlexContainerSize == 1
                        && q.MediaType == 10
                        && q.Sort == "titleSort:asc"
                    )
                )
            )
            .ReturnsAsync(CreateResponse(3, 2, CreateItem(3)))
            .Verifiable(Times.Once());
        Mock.Mock<IPlexApiClientFactory>()
            .Setup(x => x.CreateClient(It.IsAny<string>(), It.IsAny<PlexApiClientOptions>()))
            .Returns(sdk.Object)
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateItemAsync(
                    library.Id,
                    It.Is<LibraryProgressItem>(p =>
                        p.MediaType == PlexMediaType.Track && p.Received == 2 && p.Total == 3
                    ),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateItemAsync(
                    library.Id,
                    It.Is<LibraryProgressItem>(p =>
                        p.MediaType == PlexMediaType.Track && p.Received == 3 && p.Total == 3
                    ),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<List<LibraryMediaItemDTO>>(
            new GetAllMediaByTypeFromPlexApiCommand(library, PlexMediaType.Track, 2)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result
            .Value.Select(x => (x.RatingKey, x.Type, x.Title, x.Index))
            .ShouldBe([
                (1, PlexMediaType.Track, "Track 1", 1),
                (2, PlexMediaType.Track, "Track 2", 2),
                (3, PlexMediaType.Track, "Track 3", 3),
            ]);
        sdk.Verify();
        Mock.Mock<IPlexApiClientFactory>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }

    [Test]
    public async Task ShouldAcceptExplicitZeroWithoutFetchingPages_WhenLibraryIsConfirmedEmpty()
    {
        // Arrange
        await SetupDatabase(
            9203,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexTvShowLibraryCount = 0;
                config.MovieCount = 0;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var sectionId = long.Parse(library.Key);
        var sdk = new Mock<IPlexAPI>();
        sdk.Setup(x =>
                x.Content.ListContentAsync(
                    It.Is<ListContentRequest>(q =>
                        q.SectionId == sectionId
                        && q.XPlexContainerStart == 0
                        && q.XPlexContainerSize == 0
                        && q.MediaType == 1
                        && q.Sort == "titleSort:asc"
                    )
                )
            )
            .ReturnsAsync(CreateResponse(0, 0))
            .Verifiable(Times.Once());
        Mock.Mock<IPlexApiClientFactory>()
            .Setup(x => x.CreateClient(It.IsAny<string>(), It.IsAny<PlexApiClientOptions>()))
            .Returns(sdk.Object)
            .Verifiable(Times.Once());
        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x =>
                x.UpdateItemAsync(
                    library.Id,
                    It.Is<LibraryProgressItem>(p =>
                        p.MediaType == PlexMediaType.OtherVideos && p.Received == 0 && p.Total == 0
                    ),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<List<LibraryMediaItemDTO>>(
            new GetAllMediaByTypeFromPlexApiCommand(library, PlexMediaType.OtherVideos)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBeEmpty();
        sdk.Verify();
        Mock.Mock<IPlexApiClientFactory>().Verify();
        sdk.Verify(
            x => x.Content.ListContentAsync(It.Is<ListContentRequest>(q => q.XPlexContainerSize > 0)),
            Times.Never()
        );
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }

    private static Metadata CreateItem(int ratingKey) =>
        new()
        {
            RatingKey = ratingKey.ToString(),
            Key = $"/library/metadata/{ratingKey}",
            Type = "track",
            Title = $"Track {ratingKey}",
            Index = ratingKey,
            AddedAt = 1,
        };

    private static ListContentResponse CreateResponse(long? total, long offset, params Metadata[] items) =>
        new()
        {
            StatusCode = 200,
            RawResponse = new HttpResponseMessage(HttpStatusCode.OK),
            MediaContainerWithMetadata = new MediaContainerWithMetadata
            {
                MediaContainer = new MediaContainerWithMetadataMediaContainer
                {
                    TotalSize = total,
                    Offset = offset,
                    Size = items.Length,
                    Metadata = items.ToList(),
                },
            },
        };
}
