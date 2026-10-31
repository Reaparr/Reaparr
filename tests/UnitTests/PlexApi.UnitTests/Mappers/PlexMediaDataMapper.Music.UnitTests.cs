namespace Reaparr.PlexApi.UnitTests;

public class PlexMediaDataMapperMusicUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(PlexMediaType.MusicArtist, "artist")]
    [Arguments(PlexMediaType.MusicAlbum, "album")]
    [Arguments(PlexMediaType.MusicTrack, "track")]
    public void ShouldRetainNormalizedSourceIdentity_WhenMappingMusic(PlexMediaType mediaType, string sourceType)
    {
        var source = FakeData.GetLibraryMediaItemDTO(new Seed(623625)).Generate() with
        {
            Type = mediaType,
            Guid = $"plex://{sourceType}/stable-music-identity",
        };

        var identity = mediaType switch
        {
            PlexMediaType.MusicArtist => source.ToPlexMusicArtist().PlexApiIdentityId,
            PlexMediaType.MusicAlbum => source.ToPlexMusicAlbum().PlexApiIdentityId,
            PlexMediaType.MusicTrack => source.ToPlexMusicTrack().PlexApiIdentityId,
            _ => throw new ArgumentOutOfRangeException(nameof(mediaType)),
        };

        identity.ShouldBe("stable-music-identity");
    }
}
