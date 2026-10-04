namespace Reaparr.PlexApi.UnitTests;

public class PlexMediaDataMapperUnitTests : BaseUnitTest
{
    [Test]
    public void ShouldPreferPrimaryMusicBrainzId_WhenPrimaryAndSupplementaryIdsAreValid()
    {
        // Arrange
        var primaryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var supplementaryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var source = FakeData.GetLibraryMediaItemDTO(new Seed(9201), mediaType: PlexMediaType.Music).Generate() with
        {
            Guid = $"mbid://{primaryId}",
            Guids = [new MetaDataGuidsDTO($"mbid://{supplementaryId}")],
        };
        var library = FakeData.GetPlexLibrary(new Seed(9202), PlexMediaType.Music).Generate();

        // Act
        var result = source.ToPlexMusicArtist(library);

        // Assert
        result.MusicBrainzArtistId.ShouldBe(primaryId.ToString());
    }

    [Test]
    public void ShouldUseFirstValidSupplementaryMusicBrainzId_WhenPrimaryAndEarlierEntriesAreInvalid()
    {
        // Arrange
        var expectedId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var source = FakeData.GetLibraryMediaItemDTO(new Seed(9203), mediaType: PlexMediaType.Music).Generate() with
        {
            Guid = "plex://artist/primary",
            Guids =
            [
                new MetaDataGuidsDTO("mbid://not-a-uuid"),
                new MetaDataGuidsDTO("plex://artist/supplementary"),
                new MetaDataGuidsDTO($"mbid://{expectedId}"),
            ],
        };
        var library = FakeData.GetPlexLibrary(new Seed(9204), PlexMediaType.Music).Generate();

        // Act
        var result = source.ToPlexMusicArtist(library);

        // Assert
        result.MusicBrainzArtistId.ShouldBe(expectedId.ToString());
    }

    [Test]
    [Arguments(float.NaN)]
    [Arguments(float.PositiveInfinity)]
    [Arguments(float.MaxValue)]
    public void ShouldMapEachPartVideoFrameRateFromItsVideoStream_WithNumericMediaFallback(float unusableRate)
    {
        // Arrange
        var seed = new Seed(9205);
        var videoStream = FakeData.GetLibraryMediaItemStreamDTO(seed, StreamType.Video).Generate() with
        {
            FrameRate = 23.976f,
        };
        var ignoredAudioStream = FakeData.GetLibraryMediaItemStreamDTO(seed, StreamType.Audio).Generate() with
        {
            FrameRate = 120f,
        };
        var firstPart = FakeData.GetLibraryMediaItemPartDTO(seed).Generate() with
        {
            Id = 101,
            Stream = [ignoredAudioStream, videoStream],
        };
        var unusableVideoStream = videoStream with { FrameRate = unusableRate };
        var secondPart = FakeData.GetLibraryMediaItemPartDTO(seed).Generate() with
        {
            Id = 102,
            Stream = [ignoredAudioStream, unusableVideoStream],
        };
        var media = FakeData.GetLibraryMediaItemMediaDTO(seed).Generate() with
        {
            VideoFrameRate = "25.5",
            Parts = [firstPart, secondPart],
        };
        var source = FakeData.GetLibraryMediaItemDTO(seed, mediaType: PlexMediaType.OtherVideos).Generate() with
        {
            Media = [media],
        };
        var library = FakeData.GetPlexLibrary(seed, PlexMediaType.OtherVideos).Generate();

        // Act
        var result = source.ToPlexOtherVideo(library);

        // Assert
        result.MediaDataList.OrderBy(x => x.PartIndex).Select(x => (x.PlexApiPartId, x.VideoFrameRate)).ShouldBe(
            [(101, (decimal?)23.976m), (102, (decimal?)25.5m)]
        );
    }
}
