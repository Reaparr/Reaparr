using System.Net;
using LukeHagar.PlexAPI.SDK;
using LukeHagar.PlexAPI.SDK.Models.Components;
using LukeHagar.PlexAPI.SDK.Models.Requests;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.PlexApi.UnitTests;

public class GetLibrarySectionsCommandHandlerUnitTests : BaseUnitTest<GetLibrarySectionsCommandHandler>
{
    [Test]
    public async Task ShouldClassifyLibraryFamiliesWithoutChangingItemTypes_WhenReadingSections()
    {
        // Arrange
        await SetupDatabase(9301, config =>
        {
            config.PlexServerCount = 1;
            config.PlexAccountCount = 1;
            config.PlexMovieLibraryCount = 0;
            config.PlexTvShowLibraryCount = 0;
        });
        var serverId = await IDbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var sections = new List<LibrarySection>
        {
            new() { Key = "1", Title = "Music", Type = MediaTypeString.Artist, Language = "en", Uuid = "music" },
            new() { Key = "2", Title = "Photos", Type = MediaTypeString.Photo, Language = "en", Uuid = "photos" },
            new() { Key = "3", Title = "Albums", Type = MediaTypeString.PhotoAlbum, Language = "en", Uuid = "albums" },
            new() { Key = "4", Title = "Movies", Type = MediaTypeString.Movie, Language = "en", Uuid = "movies" },
            new() { Key = "5", Title = "TV", Type = MediaTypeString.TvShow, Language = "en", Uuid = "tv" },
            new()
            {
                Key = "6", Title = "Videos", Type = MediaTypeString.Movie, Language = "en", Uuid = "videos",
                Agent = "com.plexapp.agents.none", Scanner = "Plex Video Files Scanner",
            },
            new()
            {
                Key = "7", Title = "Videos 2", Type = MediaTypeString.Movie, Language = "en", Uuid = "videos2",
                Agent = "tv.plex.agents.none", Scanner = "Plex Video Files",
            },
            new()
            {
                Key = "8", Title = "Movies 2", Type = MediaTypeString.Movie, Language = "en", Uuid = "movies2",
                Agent = "com.plexapp.agents.none", Scanner = "Plex Movie Scanner",
            },
        };
        var sdk = new Mock<IPlexAPI>();
        sdk.Setup(x => x.Library.GetSectionsAsync())
            .ReturnsAsync(new GetSectionsResponse
            {
                StatusCode = 200,
                RawResponse = new HttpResponseMessage(HttpStatusCode.OK),
                Object = new GetSectionsResponseBody
                {
                    MediaContainer = new GetSectionsMediaContainer { Directory = sections },
                },
            }).Verifiable(Times.Once());
        Mock.Mock<IPlexApiClientFactory>()
            .Setup(x => x.CreateClient(It.IsAny<string>(), It.IsAny<PlexApiClientOptions>()))
            .Returns(sdk.Object).Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new GetLibrarySectionsCommand(serverId), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Select(x => (x.Key, x.Type, x.PlexServerId)).ShouldBe([
            ("1", PlexMediaType.Music, serverId),
            ("2", PlexMediaType.Photos, serverId),
            ("3", PlexMediaType.Photos, serverId),
            ("4", PlexMediaType.Movie, serverId),
            ("5", PlexMediaType.TvShow, serverId),
            ("6", PlexMediaType.OtherVideos, serverId),
            ("7", PlexMediaType.OtherVideos, serverId),
            ("8", PlexMediaType.Movie, serverId),
        ]);
        MediaTypeString.Artist.ToPlexMediaType().ShouldBe(PlexMediaType.Music);
        MediaTypeString.Movie.ToPlexMediaType().ShouldBe(PlexMediaType.Movie);
        sdk.Verify();
        Mock.Mock<IPlexApiClientFactory>().Verify();
    }
}
