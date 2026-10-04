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
    [Arguments(PlexMediaType.Music, MediaType.Artist)]
    [Arguments(PlexMediaType.Album, MediaType.Album)]
    [Arguments(PlexMediaType.Track, MediaType.Track)]
    [Arguments(PlexMediaType.PhotoAlbum, MediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.Photos, MediaType.Photo)]
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
    [Arguments("8", PlexMediaType.Music)]
    [Arguments("9", PlexMediaType.Album)]
    [Arguments("10", PlexMediaType.Track)]
    [Arguments("13", PlexMediaType.Photos)]
    [Arguments("14", PlexMediaType.PhotoAlbum)]
    public void ShouldMapPlexMetadataTypeIdToDomainType(string input, PlexMediaType expected)
    {
        // Act
        input.ToPlexMediaTypeFromTypeInt().ShouldBe(expected);
    }
}
