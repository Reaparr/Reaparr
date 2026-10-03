using LukeHagar.PlexAPI.SDK.Models.Components;

namespace Reaparr.PlexApi.Contracts;

public static class PlexMediaTypeMappers
{
    /// <summary>
    /// Converts a string representation of a Plex media type to the corresponding PlexMediaType enum value.
    /// </summary>
    /// <remarks>NOTE: This method assumes the string is given as an int in string format.</remarks>
    /// <example> 1 => PlexMediaType.Movie</example>
    /// <example> 2 => PlexMediaType.TvShow</example>
    /// <example> 3 => PlexMediaType.Season</example>
    /// <example> 4 => PlexMediaType.Episode</example>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when the provided string does not match any known Plex media type.</exception>
    public static PlexMediaType ToPlexMediaTypeFromTypeInt(this string apiType)
    {
        if (!int.TryParse(apiType, out var i))
            return PlexMediaType.Unknown;

        return i switch
        {
            1 => PlexMediaType.Movie,
            2 => PlexMediaType.TvShow,
            3 => PlexMediaType.Season,
            4 => PlexMediaType.Episode,
            8 => PlexMediaType.Artist,
            9 => PlexMediaType.Album,
            10 => PlexMediaType.Song,
            13 => PlexMediaType.Photos,
            14 => PlexMediaType.PhotoAlbum,
            _ => throw new ArgumentOutOfRangeException(nameof(apiType), $"Unknown media type value: {apiType}"),
        };
    }

    public static MediaType ToPlexApiMediaType(this PlexMediaType source) =>
        source switch
        {
            PlexMediaType.Movie => MediaType.Movie,
            PlexMediaType.TvShow => MediaType.TvShow,
            PlexMediaType.Season => MediaType.Season,
            PlexMediaType.Episode => MediaType.Episode,
            PlexMediaType.Artist => MediaType.Artist,
            PlexMediaType.Album => MediaType.Album,
            PlexMediaType.Song => MediaType.Track,
            PlexMediaType.PhotoAlbum => MediaType.PhotoAlbum,
            PlexMediaType.Photos => MediaType.Photo,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unsupported PlexMediaType"),
        };
}
