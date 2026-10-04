namespace Reaparr.Domain;

public static class PlexMediaTypeMappers
{
    private static readonly ILogger _log = LogFactory.Create(typeof(PlexMediaTypeMappers));

    /// <summary>
    /// Converts string to <see cref="PlexMediaType"/> by a fast method.
    /// </summary>
    /// <param name="value">The string representation of <see cref="PlexMediaType"/>.</param>
    /// <returns>The converted enum of type <see cref="PlexMediaType"/>.</returns>
    /// <exception cref="NotImplementedException">Throws exception if string has no enum <see cref="PlexMediaType"/> equivalent.</exception>
    public static PlexMediaType ToPlexMediaType(this string value)
    {
        return value switch
        {
            nameof(PlexMediaType.None) => PlexMediaType.None,
            nameof(PlexMediaType.Movie) => PlexMediaType.Movie,
            "movie" => PlexMediaType.Movie,
            nameof(PlexMediaType.TvShow) => PlexMediaType.TvShow,
            "show" => PlexMediaType.TvShow,
            nameof(PlexMediaType.Season) => PlexMediaType.Season,
            "season" => PlexMediaType.Season,
            nameof(PlexMediaType.Episode) => PlexMediaType.Episode,
            "episode" => PlexMediaType.Episode,
            nameof(PlexMediaType.Music) => PlexMediaType.Music,
            "artist" => PlexMediaType.Music,
            nameof(PlexMediaType.Album) => PlexMediaType.Album,
            "album" => PlexMediaType.Album,
            nameof(PlexMediaType.Track) => PlexMediaType.Track,
            "track" => PlexMediaType.Track,
            nameof(PlexMediaType.PhotoAlbum) => PlexMediaType.PhotoAlbum,
            "photoalbum" => PlexMediaType.PhotoAlbum,
            nameof(PlexMediaType.Photos) => PlexMediaType.Photos,
            "photo" => PlexMediaType.Photos,
            nameof(PlexMediaType.OtherVideos) => PlexMediaType.OtherVideos,
            nameof(PlexMediaType.Games) => PlexMediaType.Games,
            nameof(PlexMediaType.Unknown) => PlexMediaType.Unknown,
            _ => DefaultException(),
        };

        PlexMediaType DefaultException()
        {
            var logResult = _log.Here()
                .ErrorResult(
                    "Failed to convert string \"{Value}\" to type {NameOfPlexMediaType}",
                    value,
                    nameof(PlexMediaType)
                );
            throw new NotImplementedException(logResult.ToString());
        }
    }

    /// <summary>
    /// Converts <see cref="PlexMediaType"/> to string by a fast method.
    /// </summary>
    /// <param name="source">The enum of type <see cref="PlexMediaType"/>.</param>
    /// <returns>The string value of the <see cref="PlexMediaType"/> property.</returns>
    /// <exception cref="NotImplementedException">Throws exception if enum <see cref="PlexMediaType"/> has no string equivalent.</exception>
    public static string ToPlexMediaTypeString(this PlexMediaType source)
    {
        return source switch
        {
            PlexMediaType.None => nameof(PlexMediaType.None),
            PlexMediaType.Movie => nameof(PlexMediaType.Movie),
            PlexMediaType.TvShow => nameof(PlexMediaType.TvShow),
            PlexMediaType.Season => nameof(PlexMediaType.Season),
            PlexMediaType.Episode => nameof(PlexMediaType.Episode),
            PlexMediaType.Music => nameof(PlexMediaType.Music),
            PlexMediaType.Album => nameof(PlexMediaType.Album),
            PlexMediaType.Track => nameof(PlexMediaType.Track),
            PlexMediaType.PhotoAlbum => nameof(PlexMediaType.PhotoAlbum),
            PlexMediaType.Photos => nameof(PlexMediaType.Photos),
            PlexMediaType.OtherVideos => nameof(PlexMediaType.OtherVideos),
            PlexMediaType.Games => nameof(PlexMediaType.Games),
            PlexMediaType.Unknown => nameof(PlexMediaType.Unknown),
            _ => DefaultException(),
        };

        string DefaultException()
        {
            var logResult = _log.Here()
                .ErrorResult(
                    "Failed to convert value \"{Value}\" to type {NameOfPlexMediaType}",
                    source,
                    nameof(PlexMediaType)
                );
            throw new NotImplementedException(logResult.ToString());
        }
    }

    /// <summary>
    /// Converts <see cref="PlexMediaType"/> to Plex API string representation.
    /// Only supports enum values that have corresponding Plex API strings.
    /// </summary>
    /// <param name="source">The enum of type <see cref="PlexMediaType"/>.</param>
    /// <returns>The Plex API string representation.</returns>
    /// <exception cref="NotImplementedException">Throws exception if enum value has no Plex API equivalent.</exception>
    public static string ToPlexApiString(this PlexMediaType source)
    {
        return source switch
        {
            PlexMediaType.Movie => "movie",
            PlexMediaType.TvShow => "show",
            PlexMediaType.Season => "season",
            PlexMediaType.Episode => "episode",
            PlexMediaType.Music => "artist",
            PlexMediaType.Album => "album",
            PlexMediaType.Track => "track",
            PlexMediaType.PhotoAlbum => "photoalbum",
            PlexMediaType.Photos => "photo",
            _ => DefaultException(),
        };

        string DefaultException()
        {
            var logResult = _log.Here()
                .ErrorResult(
                    "Failed to convert value \"{Value}\" to type {NameOfPlexMediaType}",
                    source,
                    nameof(PlexMediaType)
                );
            throw new NotImplementedException(logResult.ToString());
        }
    }
}
