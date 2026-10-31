using System.Net;
using LukeHagar.PlexAPI.SDK.Models.Components;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stream = LukeHagar.PlexAPI.SDK.Models.Components.Stream;
using StreamType = LukeHagar.PlexAPI.SDK.Models.Components.StreamType;

namespace Reaparr.BaseTests.UnitTests;

public class MockPlexApiJsonSerializerUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldRoundTripStreamThroughSdk_WhenOptionalUnionFieldsAreAbsent(bool canAutoSync)
    {
        // Arrange
        var stream = new Stream
        {
            Id = 123,
            Key = "/library/streams/123",
            StreamType = StreamType.Subtitle,
            Codec = "srt",
            DisplayTitle = "English (SRT)",
            CanAutoSync = CanAutoSync.CreateBoolean(canAutoSync),
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://plex.test/library/metadata/1");

        // Act
        using var response = stream.ToJsonHttpResponse(request, HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync(CancellationToken);
        var restored = JsonConvert.DeserializeObject<Stream>(json);

        // Assert
        restored.ShouldNotBeNull();
        (restored.Id, restored.Key, restored.StreamType, restored.Codec, restored.DisplayTitle).ShouldBe(
            (123, "/library/streams/123", StreamType.Subtitle, "srt", "English (SRT)")
        );
        var body = JObject.Parse(json);
        body.Value<bool>("canAutoSync").ShouldBe(canAutoSync);
        body.Properties()
            .Select(x => x.Name)
            .OrderBy(x => x)
            .ToArray()
            .ShouldBe(
                new[] { "id", "key", "streamType", "codec", "displayTitle", "canAutoSync" }.OrderBy(x => x).ToArray()
            );
    }
}
