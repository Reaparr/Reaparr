namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static PlexMusicAlbum ToPlexMusicAlbum(
        this LibraryMediaItemDTO source,
        PlexMusicArtist artist,
        PlexLibrary library
    ) =>
        new()
        {
            PlexApiRatingKey = source.RatingKey,
            PlexApiMetaDataKey = RetrieveMetaDataKey(source),
            Title = source.Title,
            Year = source.Year,
            SortIndex = source.SortIndex,
            SearchTitle = source.SearchTitle,
            Duration = source.Duration,
            MediaSize = 0,
            Studio = source.Studio,
            Summary = source.Summary,
            ContentRating = source.ContentRating,
            Rating = source.Rating,
            ChildCount = 0,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            OriginallyAvailableAt = source.OriginallyAvailableAt.ToDateTime(),
            HasThumb = !string.IsNullOrEmpty(source.Thumb),
            HasArt = !string.IsNullOrEmpty(source.Art),
            HasTheme = !string.IsNullOrEmpty(source.Theme),
            FullTitle = source.Title,
            Guid = source.Guid,
            Guid_IMDB = null,
            Guid_TMDB = null,
            Guid_TVDB = null,
            Type = PlexMediaType.Album,
            PlexLibraryId = library.Id,
            PlexServerId = library.PlexServerId,
            PlexArtistId = artist.Id,
            PlexArtist = artist,
            ReleaseDate = source.OriginallyAvailableAt.ToDateTime(),
            DiscCount = null,
            TrackCount = null,
            MusicBrainzReleaseId = null,
            MusicBrainzReleaseGroupId = null,
            RecordLabel = null,
            Country = null,
        };
}
