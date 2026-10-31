namespace Reaparr.Application;

/// <summary>
/// Synchronizes streams for media-data rows written by the existing library sync commands.
/// Callers supply the persisted owners directly; unchanged owners are not included.
/// </summary>
internal static class PlexMediaStreamReconciler
{
    internal static async Task SyncDataStreamsAsync<TData, TStream>(
        IReaparrDbContext dbContext,
        IReadOnlyCollection<TData> mediaData,
        Func<TData, ICollection<TStream>> streamSelector,
        Func<IReaparrDbContext, DbSet<TStream>> streamSetSelector,
        Func<TStream, int, int, TStream> create,
        CancellationToken cancellationToken
    )
        where TData : BasePlexMediaData
        where TStream : BasePlexMediaDataStream
    {
        foreach (var ownerChunk in mediaData.Chunk(500))
        {
            var ownerIds = ownerChunk.Select(data => data.Id).ToArray();
            var currentRows = await streamSetSelector(dbContext)
                .Where(stream => ownerIds.Contains(stream.MediaDataId))
                .Select(stream => new PersistedStreamRow(
                    stream.Id,
                    stream.MediaDataId,
                    stream.PlexApiStreamId,
                    stream.StreamType,
                    stream.LanguageId,
                    stream.IsExternal,
                    stream.SidecarFormat
                ))
                .ToDictionaryAsync(row => (row.MediaDataId, row.PlexApiStreamId), cancellationToken);
            var languageIds = await dbContext.InsertOrIgnoreStreamLanguagesAsync(
                ownerChunk
                    .SelectMany(streamSelector)
                    .Where(stream => IsPersistedStreamType(stream.StreamType))
                    .Select(stream => stream.Language),
                cancellationToken
            );
            var inserts = new List<TStream>();
            var updates = new List<TStream>();
            foreach (var data in ownerChunk)
            {
                foreach (var stream in streamSelector(data))
                {
                    if (!IsPersistedStreamType(stream.StreamType))
                        continue;

                    var languageId = languageIds[(stream.Language.LanguageCode, stream.Language.LanguageTag)];
                    if (!currentRows.Remove((data.Id, stream.PlexApiStreamId), out var current))
                    {
                        inserts.Add(create(stream, data.Id, languageId));
                        continue;
                    }

                    if (
                        current.StreamType != stream.StreamType
                        || current.LanguageId != languageId
                        || current.IsExternal != stream.IsExternal
                        || current.SidecarFormat != stream.SidecarFormat
                    )
                    {
                        var updated = create(stream, data.Id, languageId);
                        updated.Id = current.Id;
                        updates.Add(updated);
                    }
                }
            }

            if (inserts.Count > 0)
                await dbContext.BulkInsertAsync(inserts, BulkConfigPreset.Default, cancellationToken);
            if (updates.Count > 0)
                await dbContext.BulkUpdateAsync(updates, BulkConfigPreset.Default, cancellationToken);
            if (currentRows.Count > 0)
                await dbContext.BulkDeleteByIdsAsync(
                    currentRows.Values.Select(row => row.Id).ToList(),
                    (_, ids) => streamSetSelector(dbContext).Where(stream => ids.Contains(stream.Id)),
                    cancellationToken
                );
        }
    }

    private static bool IsPersistedStreamType(StreamType streamType) =>
        streamType is StreamType.Audio or StreamType.Subtitle or StreamType.Lyrics;

    private readonly record struct PersistedStreamRow(
        int Id,
        int MediaDataId,
        int PlexApiStreamId,
        StreamType StreamType,
        int LanguageId,
        bool? IsExternal,
        string? SidecarFormat
    );
}
