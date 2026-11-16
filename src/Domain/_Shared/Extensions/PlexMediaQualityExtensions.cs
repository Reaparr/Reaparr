namespace Reaparr.Domain;

public static class PlexMediaQualityExtensions
{
    public static T? PickMediaQuality<T>(this ICollection<T> qualities)
        where T : BasePlexMediaQuality
    {
        if (!qualities.Any())
            return default(T);

        var nonUnknown = qualities.SortByQuality().ToList();

        return nonUnknown.Last();
    }

    public static (PlexMusicTrackMediaData? File, AudioQuality Quality) PickAudioQuality(
        this ICollection<PlexMusicTrackMediaData> files,
        int? selectedDataId = null
    )
    {
        var selected = selectedDataId is { } id ? files.FirstOrDefault(x => x.Id == id) : null;
        var best = selected ?? files.GetBestAudioFile();
        if (best is not null)
            return (best, best.AudioQuality);

        var representative = files.MinBy(x => ((int)x.Source, x.PlexApiMediaId, x.PlexApiPartId, x.PartIndex, x.Id));
        return (representative, AudioQuality.Unknown);
    }

    /// <summary>
    /// Sorts a collection of BasePlexMediaQuality objects from lowest to highest resolution.
    /// </summary>
    /// <param name="qualities">The collection of BasePlexMediaQuality instances to sort.</param>
    /// <returns>A sorted sequence of BasePlexMediaQuality instances, ordered by resolution.</returns>
    public static IEnumerable<T> SortByQuality<T>(this IEnumerable<T> qualities)
        where T : BasePlexMediaQuality => qualities.OrderBy(q => (int)q.VideoQuality);
}
