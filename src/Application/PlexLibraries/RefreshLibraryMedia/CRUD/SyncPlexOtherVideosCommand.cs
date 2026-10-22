using EFCore.BulkExtensions;

namespace Reaparr.Application;

public record SyncPlexOtherVideosCommand(
    InsertMediaMetaDataCommandResponse LibraryMetadata,
    bool ForceMediaRefresh = false
) : ICommand<Result<CrudOtherVideosReport>>;

public class SyncPlexOtherVideosCommandValidator : AbstractValidator<SyncPlexOtherVideosCommand>
{
    public SyncPlexOtherVideosCommandValidator()
    {
        RuleFor(x => x.LibraryMetadata).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.LibraryMetadata.PlexLibrary.PlexServerId).GreaterThan(0);
        RuleFor(x => x.LibraryMetadata.PlexLibrary.OtherVideos).NotNull();
        RuleForEach(x => x.LibraryMetadata.PlexLibrary.OtherVideos)
            .ChildRules(video => video.RuleFor(x => x.PlexApiRatingKey).GreaterThan(0));
    }
}

public class SyncPlexOtherVideosCommandHandler
    : ICommandHandler<SyncPlexOtherVideosCommand, Result<CrudOtherVideosReport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private BulkConfig _bulkConfig = new BulkConfig
    {
        BatchSize = 500,
        SetOutputIdentity = true,
        PreserveInsertOrder = true,
    };

    public SyncPlexOtherVideosCommandHandler(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<SyncPlexOtherVideosCommandHandler>();
        _dbContext = dbContext;
    }

    public async Task<Result<CrudOtherVideosReport>> ExecuteAsync(
        SyncPlexOtherVideosCommand command,
        CancellationToken ct
    )
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.TaskIsCancelled(nameof(SyncPlexOtherVideosCommand)).LogWarning();

        var library = command.LibraryMetadata.PlexLibrary;
        var incoming = library.OtherVideos.ToList();

        var incomingKeys = incoming.Select(video => video.PlexApiRatingKey).ToHashSet();
        var changedVideos = new List<PlexOtherVideo>();
        var report = new CrudOtherVideosReport();

        var result = await Result.Try(async Task<Result> () =>
        {
            var ctx = _dbContext;
            var current = await ctx
                .PlexOtherVideos.Where(x => x.PlexLibraryId == library.Id)
                .Select(x => new CurrentVideo(x.Id, x.PlexApiRatingKey, x.UpdatedAt, x.MediaSize))
                .ToListAsync(ct);
            var currentByKey = current.ToDictionary(x => x.PlexApiRatingKey);
            var created = new List<PlexOtherVideo>();
            var updated = new List<PlexOtherVideo>();
            List<CurrentVideo> deleted;
            var mediaSize = 0L;

            foreach (var video in incoming)
            {
                video.PlexLibraryId = library.Id;
                video.PlexServerId = library.PlexServerId;
            }

            if (command.ForceMediaRefresh)
            {
                mediaSize = incoming.Sum(video => video.MediaSize);
                await ctx.PlexOtherVideos.Where(x => x.PlexLibraryId == library.Id).ExecuteDeleteAsync(ct);
                created = incoming;
                deleted = current;
                report.CreatedOtherVideos = created.Count;
                report.UpdatedOtherVideos = 0;
                report.DeletedOtherVideos = deleted.Count;
                report.UnchangedOtherVideos = 0;
            }
            else
            {
                foreach (var video in incoming)
                {
                    if (!currentByKey.TryGetValue(video.PlexApiRatingKey, out var existing))
                    {
                        created.Add(video);
                    }
                    else
                    {
                        video.Id = existing.Id;
                        if (video.UpdatedAt != existing.UpdatedAt)
                            updated.Add(video);
                    }

                    mediaSize +=
                        existing is not null && video.UpdatedAt == existing.UpdatedAt
                            ? existing.MediaSize
                            : video.MediaSize;
                }

                deleted = current.Where(x => !incomingKeys.Contains(x.PlexApiRatingKey)).ToList();
                report.CreatedOtherVideos = created.Count;
                report.UpdatedOtherVideos = updated.Count;
                report.DeletedOtherVideos = deleted.Count;
                report.UnchangedOtherVideos = incoming.Count - created.Count - updated.Count;
            }

            if (updated.Count > 0)
                await ctx.BulkDeleteByIdsAsync(
                    updated.Select(x => x.Id).ToList(),
                    (db, ids) => db.PlexOtherVideoData.Where(x => ids.Contains(x.PlexOtherVideoId)),
                    ct
                );
            if (!command.ForceMediaRefresh)
                await ctx.BulkDeleteByIdsAsync(
                    deleted.Select(x => x.Id).ToList(),
                    (db, ids) => db.PlexOtherVideos.Where(x => ids.Contains(x.Id)),
                    ct
                );
            if (updated.Count > 0)
                await ctx.BulkUpdateAsync(updated, _bulkConfig, ct);
            if (created.Count > 0)
                await ctx.BulkInsertAsync(created, _bulkConfig, ct);
            var mediaData = created
                .Concat(updated)
                .SelectMany(video =>
                {
                    foreach (var data in video.MediaDataList)
                    {
                        data.PlexOtherVideoId = video.Id;
                        data.PlexLibraryId = library.Id;
                        data.PlexServerId = library.PlexServerId;
                    }
                    return video.MediaDataList;
                })
                .ToList();
            if (mediaData.Count > 0)
                await ctx.BulkInsertAsync(mediaData, _bulkConfig, ct);
            if (mediaData.Count > 0)
                await ReconcileOtherVideoStreamsAsync(ctx, mediaData, ct);
            changedVideos = created.Concat(updated).ToList();
            // Refresh library metrics for every completed authoritative snapshot, including empty no-op snapshots.
            await ctx.SetOtherVideoMediaMetrics(library.Id, incoming.Count, mediaSize, ct);
            return Result.Ok();
        });

        if (result.IsCancelled)
            return result.ToResult<CrudOtherVideosReport>().LogWarning();
        if (result.IsFailed)
            return result.ToResult<CrudOtherVideosReport>().LogError();

        if (changedVideos.Count > 0)
        {
            var metadataResult = Result.Merge(
                await SyncOtherVideoCountries(changedVideos, command.LibraryMetadata, ct),
                await SyncOtherVideoGenres(changedVideos, command.LibraryMetadata, ct),
                await SyncOtherVideoActors(changedVideos, command.LibraryMetadata, ct)
            );

            if (metadataResult.IsCancelled)
                return metadataResult.ToResult<CrudOtherVideosReport>().LogWarning();
            if (metadataResult.IsFailed)
                return metadataResult.ToResult<CrudOtherVideosReport>().LogError();
        }

        _log.Here().Information("Synchronized Other Videos library {PlexLibraryId}: {@Report}", library.Id, report);
        return Result.Ok(report);
    }

    private static Task ReconcileOtherVideoStreamsAsync(
        IReaparrDbContext dbContext,
        IReadOnlyCollection<PlexOtherVideoMediaData> mediaData,
        CancellationToken cancellationToken
    ) =>
        PlexMediaStreamReconciler.SyncDataStreamsAsync(
            dbContext,
            mediaData,
            static data => data.Streams,
            context => context.PlexOtherVideoDataStreams,
            static (sourceStream, ownerId, languageId) =>
                new PlexOtherVideoDataStream
                {
                    Id = 0,
                    MediaDataId = ownerId,
                    PlexApiStreamId = sourceStream.PlexApiStreamId,
                    StreamType = sourceStream.StreamType,
                    LanguageId = languageId,
                    IsExternal = sourceStream.IsExternal,
                    SidecarFormat = sourceStream.SidecarFormat,
                },
            cancellationToken
        );

    private async Task<Result> SyncOtherVideoActors(
        List<PlexOtherVideo> videos,
        InsertMediaMetaDataCommandResponse metadata,
        CancellationToken ct
    )
    {
        if (videos.Count == 0)
            return Result.Ok();

        var videoIds = videos.Select(video => video.Id).ToList();
        var rows = new List<PlexOtherVideoActors>();
        var keys = new HashSet<(int ActorId, int VideoId)>();
        foreach (var video in videos)
        foreach (var actor in video.Actors)
            if (metadata.PlexActors.TryGetValue(actor.Key, out var stored) && keys.Add((stored.Id, video.Id)))
                rows.Add(new PlexOtherVideoActors(stored.Id, video.Id));

        return await Result.Try(async Task () =>
        {
            await _dbContext
                .PlexOtherVideoActors.Where(x => videoIds.Contains(x.PlexOtherVideoId))
                .ExecuteDeleteAsync(ct);
            if (rows.Count > 0)
                await _dbContext.BulkInsertAsync(rows, _bulkConfig, ct);
        });
    }

    private async Task<Result> SyncOtherVideoGenres(
        List<PlexOtherVideo> videos,
        InsertMediaMetaDataCommandResponse metadata,
        CancellationToken ct
    )
    {
        if (videos.Count == 0)
            return Result.Ok();

        var videoIds = videos.Select(video => video.Id).ToList();
        var rows = new List<PlexOtherVideoGenres>();
        var keys = new HashSet<(int GenreId, int VideoId)>();
        foreach (var video in videos)
        foreach (var genre in video.Genres)
            if (metadata.PlexGenresVideo.TryGetValue(genre.Key, out var stored) && keys.Add((stored.Id, video.Id)))
                rows.Add(new PlexOtherVideoGenres(stored.Id, video.Id));

        return await Result.Try(async Task () =>
        {
            await _dbContext
                .PlexOtherVideoGenres.Where(x => videoIds.Contains(x.PlexOtherVideoId))
                .ExecuteDeleteAsync(ct);
            if (rows.Count > 0)
                await _dbContext.BulkInsertAsync(rows, _bulkConfig, ct);
        });
    }

    private async Task<Result> SyncOtherVideoCountries(
        List<PlexOtherVideo> videos,
        InsertMediaMetaDataCommandResponse metadata,
        CancellationToken ct
    )
    {
        if (videos.Count == 0)
            return Result.Ok();

        var videoIds = videos.Select(video => video.Id).ToList();
        var rows = new List<PlexOtherVideoCountries>();
        var keys = new HashSet<(int CountryId, int VideoId)>();
        foreach (var video in videos)
        foreach (var country in video.Countries)
            if (metadata.PlexCountries.TryGetValue(country.Key, out var stored) && keys.Add((stored.Id, video.Id)))
                rows.Add(new PlexOtherVideoCountries(stored.Id, video.Id));

        return await Result.Try(async Task () =>
        {
            await _dbContext
                .PlexOtherVideoCountries.Where(x => videoIds.Contains(x.PlexOtherVideoId))
                .ExecuteDeleteAsync(ct);
            if (rows.Count > 0)
                await _dbContext.BulkInsertAsync(rows, _bulkConfig, ct);
        });
    }



    private sealed record CurrentVideo(int Id, int PlexApiRatingKey, DateTime? UpdatedAt, long MediaSize);
}

public record CrudOtherVideosReport
{
    public int CreatedOtherVideos { get; set; }
    public int UpdatedOtherVideos { get; set; }
    public int DeletedOtherVideos { get; set; }
    public int UnchangedOtherVideos { get; set; }
    public int ChangedItemCount => CreatedOtherVideos + UpdatedOtherVideos + DeletedOtherVideos;
}
