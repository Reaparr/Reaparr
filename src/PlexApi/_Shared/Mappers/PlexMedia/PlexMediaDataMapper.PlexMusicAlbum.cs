namespace Reaparr.PlexApi;

public static partial class PlexMediaDataMapper
{
    public static List<PlexMusicAlbum> ToPlexMusicAlbums(this List<LibraryMediaItemDTO> source) =>
        source.Select(value => value.ToPlexMusicAlbum()).ToList();

    public static PlexMusicAlbum ToPlexMusicAlbum(this LibraryMediaItemDTO source) =>
        new()
        {
            Id = 0,
            Title = source.Title,
            Year = source.Year,
            SortIndex = source.SortIndex,
            SearchTitle = source.SearchTitle,
            Guid = source.Guid,
            Guid_IMDB = source.Guids.GetImdbId(),
            Guid_TMDB = source.Guids.GetTmdbId(),
            Guid_TVDB = source.Guids.GetTvdbId(),
            Duration = source.Duration,
            MediaSize = 0,
            ChildCount = source.ChildCount,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            ParentKey = source.GetParentKey(),
            FullTitle = source.Title,
            PlexApiRatingKey = source.RatingKey,
            PlexApiMetaDataKey = RetrieveMetaDataKey(source),
            Studio = source.Studio,
            Summary = source.Summary,
            ContentRating = source.ContentRating,
            Rating = source.Rating,
            OriginallyAvailableAt = source.OriginallyAvailableAt.ToDateTime(),
            HasThumb = !string.IsNullOrEmpty(source.Thumb),
            HasArt = !string.IsNullOrEmpty(source.Art),
            HasTheme = !string.IsNullOrEmpty(source.Theme),
            PlexArtistId = 0,
            PlexArtist = default,
            PlexLibraryId = 0,
            PlexServerId = 0,
            ReleaseDate = source.OriginallyAvailableAt.ToDateTime(),
            DiscCount = null,
            TrackCount = null,
            MusicBrainzReleaseId = null,
            MusicBrainzReleaseGroupId = null,
            RecordLabel = null,
            Country = null,
        };
}
