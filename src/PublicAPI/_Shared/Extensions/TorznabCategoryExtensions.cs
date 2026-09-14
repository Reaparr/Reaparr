namespace Reaparr.PublicAPI;

public static class TorznabCategoryExtensions
{
    public static IReadOnlyList<TorznabCategoryId> ToTorznabCategories(this TorznabFeedItemProjection item)
    {
        var isSd = item.Source == ReleaseSource.DVD || item.VideoResolution is VideoQuality.SD or VideoQuality.DVD;
        var isUhd = !isSd && item.VideoResolution is VideoQuality.UHD_4K or VideoQuality.UHD_8K;

        if (item.MediaType == PlexMediaType.Movie)
        {
            var categories = new List<TorznabCategoryId>
            {
                TorznabCategoryId.Movies,
                isSd ? TorznabCategoryId.Movies_SD
                : isUhd ? TorznabCategoryId.Movies_UHD
                : TorznabCategoryId.Movies_HD,
            };

            if (item.GenreTypes.Contains(PlexGenreType.Foreign))
                categories.Add(TorznabCategoryId.Movies_Foreign);
            if (item.Source is ReleaseSource.BluRay or ReleaseSource.BluRayRemux)
                categories.Add(TorznabCategoryId.Movies_BluRay);
            if (item.Source is ReleaseSource.WebDl or ReleaseSource.WebRip)
                categories.Add(TorznabCategoryId.Movies_WEBDL);

            return categories;
        }

        if (item.MediaType == PlexMediaType.Episode)
        {
            var categories = new List<TorznabCategoryId>
            {
                TorznabCategoryId.TV,
                isSd ? TorznabCategoryId.TV_SD
                : isUhd ? TorznabCategoryId.TV_UHD
                : TorznabCategoryId.TV_HD,
            };

            if (item.GenreTypes.Contains(PlexGenreType.Foreign))
                categories.Add(TorznabCategoryId.TV_Foreign);
            if (item.GenreTypes.Contains(PlexGenreType.Sport))
                categories.Add(TorznabCategoryId.TV_Sport);
            if (item.GenreTypes.Contains(PlexGenreType.Anime))
                categories.Add(TorznabCategoryId.TV_Anime);
            if (item.GenreTypes.Contains(PlexGenreType.Documentary))
                categories.Add(TorznabCategoryId.TV_Documentary);

            return categories;
        }

        return [];
    }
}
