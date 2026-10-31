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
            Duration = source.Duration,
            MediaSize = 0,
            ChildCount = source.ChildCount,
            AddedAt = source.AddedAt,
            UpdatedAt = source.UpdatedAt,
            ParentKey = source.GetParentKey(),
            PlexApiRatingKey = source.RatingKey,
            PlexApiIdentityId = source.Guid.Replace("plex://album/", string.Empty),
            PlexApiMetaDataKey = RetrieveMetaDataKey(source),
            Summary = source.Summary,
            Rating = source.Rating,
            OriginallyAvailableAt = source.OriginallyAvailableAt.ToDateTime(),
            HasThumb = !string.IsNullOrEmpty(source.Thumb),
            HasArt = !string.IsNullOrEmpty(source.Art),
            PlexArtistId = 0,
            PlexArtist = default,
            PlexLibraryId = 0,
            PlexServerId = 0,
            MusicBrainzReleaseId = GetMusicBrainzId(source),
            RecordLabel = string.IsNullOrWhiteSpace(source.Studio) ? null : source.Studio,
            Countries = source.Country.ToPlexCountry(),
        };
}
