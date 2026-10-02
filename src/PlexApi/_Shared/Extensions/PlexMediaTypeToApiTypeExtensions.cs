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
            case PlexMediaType.Artist:
                return (T)Enum.ToObject(typeof(T), "artist");
            case PlexMediaType.Album:
                return (T)Enum.ToObject(typeof(T), "album");
            case PlexMediaType.Song:
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

    public static PlexMediaType ToPlexMediaType(this MediaType value)
    {
        return value switch
        {
            MediaType.Movie => PlexMediaType.Movie,
            MediaType.TvShow => PlexMediaType.TvShow,
            MediaType.Season => PlexMediaType.Season,
            MediaType.Episode => PlexMediaType.Episode,
            MediaType.Artist => PlexMediaType.Artist,
            MediaType.Album => PlexMediaType.Album,
            MediaType.Track => PlexMediaType.Song,
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
            PlexMediaType.Artist => MediaType.Artist,
            PlexMediaType.Album => MediaType.Album,
            PlexMediaType.Song => MediaType.Track,
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
            PlexMediaType.Artist => MediaTypeString.Artist,
            PlexMediaType.Album => MediaTypeString.Album,
            PlexMediaType.Song => MediaTypeString.Track,
            PlexMediaType.PhotoAlbum => MediaTypeString.PhotoAlbum,
            PlexMediaType.OtherVideos => MediaTypeString.Movie,
            PlexMediaType.Photos => MediaTypeString.Photo,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }

    public static PlexMediaType ToPlexMediaType(this MediaTypeString value)
    {
        return value switch
        {
            MediaTypeString.Movie => PlexMediaType.Movie,
            MediaTypeString.TvShow => PlexMediaType.TvShow,
            MediaTypeString.Season => PlexMediaType.Season,
            MediaTypeString.Episode => PlexMediaType.Episode,
            MediaTypeString.Artist => PlexMediaType.Artist,
            MediaTypeString.Album => PlexMediaType.Album,
            MediaTypeString.Track => PlexMediaType.Song,
            MediaTypeString.PhotoAlbum => PlexMediaType.PhotoAlbum,
            MediaTypeString.Photo => PlexMediaType.Photos,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
        };
    }
}
