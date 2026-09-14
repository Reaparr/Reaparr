namespace Reaparr.PublicAPI;

public static class TorznabCategoryQueryExtensions
{
    public static IQueryable<PlexMovieMediaData> ApplyTorznabCategories(
        this IQueryable<PlexMovieMediaData> query,
        int[] categories
    )
    {
        if (categories.Length == 0 || categories.Contains((int)TorznabCategoryId.Movies))
            return query;

        var known = categories.Where(x => x is > 2000 and < 3000).ToArray();
        if (known.Length == 0)
            return query.Where(_ => false);

        return query.Where(x =>
            (
                known.AsEnumerable().Contains((int)TorznabCategoryId.Movies_Foreign)
                && x.PlexMovie!.Genres.Any(genre => genre.Type == PlexGenreType.Foreign)
            )
            || (
                known.AsEnumerable().Contains((int)TorznabCategoryId.Movies_SD)
                && (
                    x.Source == ReleaseSource.DVD
                    || x.VideoResolution == VideoQuality.SD
                    || x.VideoResolution == VideoQuality.DVD
                )
            )
            || (
                known.AsEnumerable().Contains((int)TorznabCategoryId.Movies_UHD)
                && x.Source != ReleaseSource.DVD
                && (x.VideoResolution == VideoQuality.UHD_4K || x.VideoResolution == VideoQuality.UHD_8K)
            )
            || (
                known.AsEnumerable().Contains((int)TorznabCategoryId.Movies_HD)
                && x.Source != ReleaseSource.DVD
                && x.VideoResolution != VideoQuality.SD
                && x.VideoResolution != VideoQuality.DVD
                && x.VideoResolution != VideoQuality.UHD_4K
                && x.VideoResolution != VideoQuality.UHD_8K
            )
            || (
                known.AsEnumerable().Contains((int)TorznabCategoryId.Movies_BluRay)
                && (x.Source == ReleaseSource.BluRay || x.Source == ReleaseSource.BluRayRemux)
            )
            || (
                known.AsEnumerable().Contains((int)TorznabCategoryId.Movies_WEBDL)
                && (x.Source == ReleaseSource.WebDl || x.Source == ReleaseSource.WebRip)
            )
        );
    }

    public static IQueryable<PlexTvShowEpisodeMediaData> ApplyTorznabCategories(
        this IQueryable<PlexTvShowEpisodeMediaData> query,
        int[] categories
    )
    {
        if (categories.Length == 0 || categories.Contains((int)TorznabCategoryId.TV))
            return query;

        var known = categories.Where(x => x is > 5000 and < 6000).ToArray();
        if (known.Length == 0)
            return query.Where(_ => false);

        return query.Where(x =>
            (
                known.AsEnumerable().Contains((int)TorznabCategoryId.TV_Foreign)
                && x.PlexTvShowEpisode!.TvShow!.Genres.Any(genre => genre.Type == PlexGenreType.Foreign)
            )
            || (
                known.AsEnumerable().Contains((int)TorznabCategoryId.TV_Anime)
                && x.PlexTvShowEpisode!.TvShow!.Genres.Any(genre => genre.Type == PlexGenreType.Anime)
            )
            || (
                known.AsEnumerable().Contains((int)TorznabCategoryId.TV_Documentary)
                && x.PlexTvShowEpisode!.TvShow!.Genres.Any(genre => genre.Type == PlexGenreType.Documentary)
            )
            || (
                known.AsEnumerable().Contains((int)TorznabCategoryId.TV_Sport)
                && x.PlexTvShowEpisode!.TvShow!.Genres.Any(genre => genre.Type == PlexGenreType.Sport)
            )
            || (
                known.AsEnumerable().Contains((int)TorznabCategoryId.TV_SD)
                && (
                    x.Source == ReleaseSource.DVD
                    || x.VideoResolution == VideoQuality.SD
                    || x.VideoResolution == VideoQuality.DVD
                )
            )
            || (
                known.AsEnumerable().Contains((int)TorznabCategoryId.TV_UHD)
                && x.Source != ReleaseSource.DVD
                && (x.VideoResolution == VideoQuality.UHD_4K || x.VideoResolution == VideoQuality.UHD_8K)
            )
            || (
                known.AsEnumerable().Contains((int)TorznabCategoryId.TV_HD)
                && x.Source != ReleaseSource.DVD
                && x.VideoResolution != VideoQuality.SD
                && x.VideoResolution != VideoQuality.DVD
                && x.VideoResolution != VideoQuality.UHD_4K
                && x.VideoResolution != VideoQuality.UHD_8K
            )
        );
    }
}
