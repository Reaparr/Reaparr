using LukeHagar.PlexAPI.SDK.Models.Components;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.PlexApi.UnitTests;

public class PlexMediaTypeMappersToPlexApiMediaTypeUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(PlexMediaType.Movie, MediaType.Movie)]
    [Arguments(PlexMediaType.TvShow, MediaType.TvShow)]
    [Arguments(PlexMediaType.Season, MediaType.Season)]
    [Arguments(PlexMediaType.Episode, MediaType.Episode)]
    [Arguments(PlexMediaType.MusicArtist, MediaType.Artist)]
    [Arguments(PlexMediaType.MusicAlbum, MediaType.Album)]
    [Arguments(PlexMediaType.MusicTrack, MediaType.Track)]
    [Arguments(PlexMediaType.PhotoAlbum, MediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.PhotoImage, MediaType.Photo)]
    public void ShouldMapPlexMediaTypeToMediaType(PlexMediaType input, MediaType expected)
    {
        // Act
        input.ToPlexApiMediaType().ShouldBe(expected);
    }

    [Test]
    [Arguments("1", PlexMediaType.Movie)]
    [Arguments("2", PlexMediaType.TvShow)]
    [Arguments("3", PlexMediaType.Season)]
    [Arguments("4", PlexMediaType.Episode)]
    [Arguments("8", PlexMediaType.MusicArtist)]
    [Arguments("9", PlexMediaType.MusicAlbum)]
    [Arguments("10", PlexMediaType.MusicTrack)]
    [Arguments("13", PlexMediaType.PhotoImage)]
    [Arguments("14", PlexMediaType.PhotoAlbum)]
    public void ShouldMapPlexMetadataTypeIdToDomainType(string input, PlexMediaType expected)
    {
        // Act
        input.ToPlexMediaTypeFromTypeInt().ShouldBe(expected);
    }
}
