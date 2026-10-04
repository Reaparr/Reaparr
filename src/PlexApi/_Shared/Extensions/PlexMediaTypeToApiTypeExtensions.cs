using LukeHagar.PlexAPI.SDK.Models.Components;

namespace Reaparr.PlexApi;

public static class PlexMediaTypeToApiTypeExtensions
{
    public static T ToApiTypeEnumFromString<T>(this PlexMediaType plexMediaType)
        where T : struct, Enum
    {
        var enumInt = 0;
        switch (plexMediaType)
        {
            case PlexMediaType.None:
                break;
            case PlexMediaType.Movie:
                return (T)Enum.ToObject(typeof(T), "movie");
            case PlexMediaType.TvShow:
                return (T)Enum.ToObject(typeof(T), "show");
            case PlexMediaType.Season:
                return (T)Enum.ToObject(typeof(T), "season");
            case PlexMediaType.Episode:
                return (T)Enum.ToObject(typeof(T), "episode");
            case PlexMediaType.Music:
                return (T)Enum.ToObject(typeof(T), "artist");
            case PlexMediaType.Album:
                return (T)Enum.ToObject(typeof(T), "album");
            case PlexMediaType.Track:
                return (T)Enum.ToObject(typeof(T), "track");
            case PlexMediaType.PhotoAlbum:
                return (T)Enum.ToObject(typeof(T), "photoalbum");
            case PlexMediaType.Photos:
                return (T)Enum.ToObject(typeof(T), "photo");
            default:
                throw new ArgumentOutOfRangeException(nameof(plexMediaType), plexMediaType, null);
        }

        return (T)Enum.ToObject(typeof(T), enumInt);
    }

    /// <summary>
    /// Converts media types to the numeric metadata IDs used by Plex content requests.
    /// </summary>
    public static int ToPlexMetadataTypeId(this PlexMediaType mediaType) =>
        mediaType switch
        {
            PlexMediaType.Movie or PlexMediaType.OtherVideos => 1,
            PlexMediaType.TvShow => 2,
            PlexMediaType.Season => 3,
            PlexMediaType.Episode => 4,
            PlexMediaType.Music => 8,
            PlexMediaType.Album => 9,
            PlexMediaType.Track => 10,
            PlexMediaType.Photos => 13,
            PlexMediaType.PhotoAlbum => 14,
            _ => throw new ArgumentOutOfRangeException(nameof(mediaType), mediaType, null),
        };

    public static PlexMediaType ToPlexMediaType(this MediaType value)
    {
        return value switch
        {
            MediaType.Movie => PlexMediaType.Movie,
            MediaType.TvShow => PlexMediaType.TvShow,
            MediaType.Season => PlexMediaType.Season,
            MediaType.Episode => PlexMediaType.Episode,
            MediaType.Artist => PlexMediaType.Music,
            MediaType.Album => PlexMediaType.Album,
            MediaType.Track => PlexMediaType.Track,
            MediaType.PhotoAlbum => PlexMediaType.PhotoAlbum,
            MediaType.Photo => PlexMediaType.Photos,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    public static MediaType ToMediaType(this PlexMediaType value)
    {
        return value switch
        {
            PlexMediaType.Movie => MediaType.Movie,
            PlexMediaType.TvShow => MediaType.TvShow,
            PlexMediaType.Season => MediaType.Season,
            PlexMediaType.Episode => MediaType.Episode,
            PlexMediaType.Music => MediaType.Artist,
            PlexMediaType.Album => MediaType.Album,
            PlexMediaType.Track => MediaType.Track,
            PlexMediaType.PhotoAlbum => MediaType.PhotoAlbum,
            PlexMediaType.Photos => MediaType.Photo,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    public static MediaTypeString ToMediaTypeString(this PlexMediaType value)
    {
        return value switch
        {
            PlexMediaType.Movie => MediaTypeString.Movie,
            PlexMediaType.TvShow => MediaTypeString.TvShow,
            PlexMediaType.Season => MediaTypeString.Season,
            PlexMediaType.Episode => MediaTypeString.Episode,
            PlexMediaType.Music => MediaTypeString.Artist,
            PlexMediaType.Album => MediaTypeString.Album,
            PlexMediaType.Track => MediaTypeString.Track,
            PlexMediaType.PhotoAlbum => MediaTypeString.PhotoAlbum,
            PlexMediaType.OtherVideos => MediaTypeString.Movie,
            PlexMediaType.Photos => MediaTypeString.Photo,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    /// <summary>
    /// Classifies library sections using their agent and scanner, independently of item type mapping.
    /// </summary>
    public static PlexMediaType ToPlexMediaType(this MediaTypeString value, string? agent, string? scanner) =>
        value switch
        {
            MediaTypeString.Artist => PlexMediaType.Music,
            MediaTypeString.Photo or MediaTypeString.PhotoAlbum => PlexMediaType.Photos,
            MediaTypeString.Movie
                when agent is "com.plexapp.agents.none" or "tv.plex.agents.none"
                    && scanner is "Plex Video Files Scanner" or "Plex Video Files" => PlexMediaType.OtherVideos,
            _ => value.ToPlexMediaType(),
        };

    public static PlexMediaType ToPlexMediaType(this MediaTypeString value)
    {
        return value switch
        {
            MediaTypeString.Movie => PlexMediaType.Movie,
            MediaTypeString.TvShow => PlexMediaType.TvShow,
            MediaTypeString.Season => PlexMediaType.Season,
            MediaTypeString.Episode => PlexMediaType.Episode,
            MediaTypeString.Artist => PlexMediaType.Music,
            MediaTypeString.Album => PlexMediaType.Album,
            MediaTypeString.Track => PlexMediaType.Track,
            MediaTypeString.PhotoAlbum => PlexMediaType.PhotoAlbum,
            MediaTypeString.Photo => PlexMediaType.Photos,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }
}
