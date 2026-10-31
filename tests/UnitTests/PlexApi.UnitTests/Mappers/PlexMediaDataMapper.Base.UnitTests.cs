using Reaparr.BaseTests;

namespace Reaparr.PlexApi.UnitTests;

public class PlexMediaDataMapperBaseUnitTests : BaseUnitTest
{
    [Test]
    [Arguments("flac", true, ReleaseSource.WebRip)]
    [Arguments("mkv", true, ReleaseSource.WebDl)]
    [Arguments("mp4", false, ReleaseSource.WebRip)]
    public void ShouldInferWebReleaseSource_FromContainerAndStreamingOptimization(
        string container,
        bool optimizedForStreaming,
        ReleaseSource expectedSource
    )
    {
        // Arrange
        var source = FakeData.GetLibraryMediaItemDTO(new Seed(625802), mediaType: PlexMediaType.Movie).Generate();
        var media = source.Media.ShouldHaveSingleItem();
        source.Media[0] = media with { Container = container, OptimizedForStreaming = optimizedForStreaming };

        // Act
        var movie = source.ToPlexMovie();

        // Assert
        movie.MediaDataList.ShouldHaveSingleItem().Source.ShouldBe(expectedSource);
    }

    [Test]
    [Arguments("vob", "aac", "", ReleaseSource.DVD)]
    [Arguments("m2ts", "truehd", "", ReleaseSource.BluRayRemux)]
    [Arguments("m2ts", "aac", "", ReleaseSource.BluRay)]
    [Arguments("ts", "aac", "", ReleaseSource.HDTV)]
    public void ShouldRetainReleaseSourceHints_ForDiscOrBroadcastContainers(
        string container,
        string audioCodec,
        string audioProfile,
        ReleaseSource expectedSource
    )
    {
        // Arrange
        var source = FakeData.GetLibraryMediaItemDTO(new Seed(625803), mediaType: PlexMediaType.Movie).Generate();
        var media = source.Media.ShouldHaveSingleItem();
        source.Media[0] = media with { Container = container, AudioCodec = audioCodec, AudioProfile = audioProfile };

        // Act
        var movie = source.ToPlexMovie();

        // Assert
        movie.MediaDataList.ShouldHaveSingleItem().Source.ShouldBe(expectedSource);
    }
}
