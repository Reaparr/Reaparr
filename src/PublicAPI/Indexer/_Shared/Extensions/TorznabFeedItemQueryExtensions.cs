namespace Reaparr.PublicAPI;

public static class TorznabFeedItemQueryExtensions
{
    public static IOrderedQueryable<TorznabFeedItemProjection> OrderForTorznab(
        this IQueryable<TorznabFeedItemProjection> query
    ) =>
        query
            .OrderByDescending(x => x.AddedAt)
            .ThenBy(x => x.PlexServerMachineIdentifier)
            .ThenBy(x => x.PlexApiRatingKey)
            .ThenBy(x => x.PlexApiMediaId)
            .ThenBy(x => x.PlexApiPartId)
            .ThenBy(x => x.MediaType)
            .ThenBy(x => x.DataId);
}
