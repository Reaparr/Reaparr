using System.Linq.Expressions;
using FlexQuery.NET;
using FlexQuery.NET.Models;

namespace Reaparr.Data.Contracts;

public static partial class DbSetExtensions
{
    public static IQueryable<PlexMusicArtist> IncludeAll(this IQueryable<PlexMusicArtist> artists) =>
        artists
            .Include(x => x.PlexServer)
            .Include(x => x.PlexLibrary)
            .Include(x => x.Genres)
            .Include(x => x.Qualities)
            .Include(x => x.Albums)
                .ThenInclude(x => x.Qualities)
            .Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
                    .ThenInclude(x => x.MediaDataList);

    public static IQueryable<PlexMusicAlbum> IncludeAll(this IQueryable<PlexMusicAlbum> albums) =>
        albums
            .Include(x => x.PlexServer)
            .Include(x => x.PlexLibrary)
            .Include(x => x.PlexArtist)
            .Include(x => x.Qualities)
            .Include(x => x.Tracks)
                .ThenInclude(x => x.MediaDataList);

    /// <summary>
    /// Applies all requested music-artist sort nodes and deterministic title/ID tie breakers in SQL.
    /// </summary>
    /// <param name="source">The filtered music-artist query.</param>
    /// <param name="options">The normalized query options.</param>
    /// <param name="allLibraries">Whether the query spans all accessible libraries.</param>
    /// <returns>The ordered query, or the original query when no sort nodes are present.</returns>
    /// <remarks>
    /// Audio quality is the highest persisted quality value for the artist and falls back to zero when no
    /// quality rows exist. Keeping the aggregate in the expression tree lets EF Core order before paging.
    /// </remarks>
    public static IQueryable<PlexMusicArtist> ApplyMusicArtistSortIfRequired(
        this IQueryable<PlexMusicArtist> source,
        QueryOptions options,
        bool allLibraries
    )
    {
        if (!options.Sort.Any(sort => sort.Field.ToSortType() == SortType.AudioQuality))
            return source.ApplySort(options);

        IOrderedQueryable<PlexMusicArtist>? ordered = null;
        var hasTitleSort = false;
        var titleField = allLibraries ? nameof(BasePlexMedia.SearchTitle) : SortType.Title.ToSortValue();
        foreach (var sort in options.Sort)
        {
            if (sort.Field == titleField)
            {
                hasTitleSort = true;
                ordered = ApplyTitleOrder(ordered, source, allLibraries, sort.Descending);
                continue;
            }

            ordered = ApplySortNode(ordered, source, sort);
        }

        if (ordered is null)
            return source;

        if (!hasTitleSort)
            ordered = ApplyTitleOrder(ordered, source, allLibraries, descending: false);

        return ApplyOrder(ordered, source, artist => artist.Id, descending: false);
    }

    /// <summary>
    /// Applies a title ordering key using the normalized field for the current library scope.
    /// </summary>
    /// <param name="ordered">The current ordering chain, or <see langword="null"/> for the first node.</param>
    /// <param name="source">The unmodified source query used for the first ordering node.</param>
    /// <param name="allLibraries">Whether the query spans all accessible libraries.</param>
    /// <param name="descending">Whether to order the title descending.</param>
    /// <returns>The ordering chain including the title node.</returns>
    private static IOrderedQueryable<PlexMusicArtist> ApplyTitleOrder(
        IOrderedQueryable<PlexMusicArtist>? ordered,
        IQueryable<PlexMusicArtist> source,
        bool allLibraries,
        bool descending
    ) =>
        allLibraries
            ? ApplyOrder(ordered, source, artist => artist.SearchTitle, descending)
            : ApplyOrder(ordered, source, artist => artist.SortIndex, descending);

    /// <summary>
    /// Applies one non-title sort node while preserving the caller's existing ordering chain.
    /// </summary>
    /// <param name="ordered">The current ordering chain, or <see langword="null"/> for the first node.</param>
    /// <param name="source">The unmodified source query used for the first ordering node.</param>
    /// <param name="sort">The normalized sort node to apply.</param>
    /// <returns>The ordering chain including the supplied node.</returns>
    private static IOrderedQueryable<PlexMusicArtist> ApplySortNode(
        IOrderedQueryable<PlexMusicArtist>? ordered,
        IQueryable<PlexMusicArtist> source,
        SortNode sort
    )
    {
        var sortType = sort.Field.ToSortType();
        var descending = sort.Descending;
        switch (sortType)
        {
            case SortType.AddedAt:
                return ApplyOrder(ordered, source, artist => artist.AddedAt, descending);
            case SortType.UpdatedAt:
                return ApplyOrder(ordered, source, artist => artist.UpdatedAt, descending);
            case SortType.Duration:
                return ApplyOrder(ordered, source, artist => artist.Duration, descending);
            case SortType.MediaSize:
                return ApplyOrder(ordered, source, artist => artist.MediaSize, descending);
            case SortType.AudioQuality:
                return ApplyOrder(
                    ordered,
                    source,
                    artist => artist.Qualities.Select(quality => (int?)quality.AudioQuality).Max() ?? 0,
                    descending
                );
            default:
                throw new ArgumentOutOfRangeException(nameof(sort), sortType, "Unsupported music-artist sort type.");
        }
    }

    /// <summary>
    /// Adds the first ordering key or a stable secondary key to the existing ordered query.
    /// </summary>
    /// <typeparam name="TKey">The key type used by the ordering expression.</typeparam>
    /// <param name="ordered">The existing ordered query, or <see langword="null"/> for its first key.</param>
    /// <param name="source">The unmodified source query used for the first ordering key.</param>
    /// <param name="keySelector">The SQL-translatable key expression.</param>
    /// <param name="descending">Whether to order this key descending.</param>
    /// <returns>The query ordered by the supplied key.</returns>
    private static IOrderedQueryable<PlexMusicArtist> ApplyOrder<TKey>(
        IOrderedQueryable<PlexMusicArtist>? ordered,
        IQueryable<PlexMusicArtist> source,
        Expression<Func<PlexMusicArtist, TKey>> keySelector,
        bool descending
    )
    {
        if (ordered is null)
            return descending ? source.OrderByDescending(keySelector) : source.OrderBy(keySelector);

        return descending ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector);
    }
}
