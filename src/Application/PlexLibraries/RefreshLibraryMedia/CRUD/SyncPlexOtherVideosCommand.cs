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
        var current = await _dbContext
            .PlexOtherVideos.Where(x => x.PlexLibraryId == library.Id)
            .Select(x => new CurrentVideo(x.Id, x.PlexApiRatingKey, x.UpdatedAt, x.MediaSize))
            .ToListAsync(ct);
        var currentByKey = current.ToDictionary(x => x.PlexApiRatingKey);
        var incomingKeys = incoming.Select(x => x.PlexApiRatingKey).ToHashSet();
        var created = new List<PlexOtherVideo>();
        var updated = new List<PlexOtherVideo>();
        var mediaSize = 0L;
        foreach (var video in incoming)
        {
            video.PlexLibraryId = library.Id;
            video.PlexServerId = library.PlexServerId;
            if (!currentByKey.TryGetValue(video.PlexApiRatingKey, out var existing))
            {
                video.Id = 0;
                created.Add(video);
            }
            else
            {
                video.Id = existing.Id;
                if (video.UpdatedAt != existing.UpdatedAt)
                    updated.Add(video);
            }
            mediaSize +=
                !command.ForceMediaRefresh && existing is not null && video.UpdatedAt == existing.UpdatedAt
                    ? existing.MediaSize
                    : video.MediaSize;
        }
        var deleted = current.Where(x => !incomingKeys.Contains(x.PlexApiRatingKey)).ToList();
        var report = new CrudOtherVideosReport
        {
            CreatedOtherVideos = created.Count,
            UpdatedOtherVideos = updated.Count,
            DeletedOtherVideos = deleted.Count,
            UnchangedOtherVideos = incoming.Count - created.Count - updated.Count,
        };

        var result = await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                if (command.ForceMediaRefresh)
                {
                    await ctx.PlexOtherVideos.Where(x => x.PlexLibraryId == library.Id).ExecuteDeleteAsync(txCt);
                    created = incoming;
                    updated = [];
                    deleted = current;
                    foreach (var video in created)
                        video.Id = 0;
                    report.CreatedOtherVideos = created.Count;
                    report.UpdatedOtherVideos = 0;
                    report.DeletedOtherVideos = deleted.Count;
                    report.UnchangedOtherVideos = 0;
                }
                await ctx.BulkDeleteByIdsAsync(
                    updated.Select(x => x.Id).ToList(),
                    (db, ids) => db.PlexOtherVideoData.Where(x => ids.Contains(x.PlexOtherVideoId)),
                    txCt
                );
                await ctx.BulkDeleteByIdsAsync(
                    deleted.Select(x => x.Id).ToList(),
                    (db, ids) => db.PlexOtherVideos.Where(x => ids.Contains(x.Id)),
                    txCt
                );
                if (updated.Count > 0)
                    await ctx.BulkUpdateAsync(updated, _bulkConfig, txCt);
                if (created.Count > 0)
                    await ctx.BulkInsertAsync(created, _bulkConfig, txCt);
                var mediaData = created
                    .Concat(updated)
                    .SelectMany(video =>
                    {
                        foreach (var data in video.MediaDataList)
                        {
                            data.Id = 0;
                            data.PlexOtherVideoId = video.Id;
                            data.PlexLibraryId = library.Id;
                            data.PlexServerId = library.PlexServerId;
                        }
                        return video.MediaDataList;
                    })
                    .ToList();
                if (mediaData.Count > 0)
                    await ctx.BulkInsertAsync(mediaData, _bulkConfig, txCt);
                await ctx.SetOtherVideoMediaMetrics(library.Id, incoming.Count, mediaSize, txCt);
            },
            ct
        );
        if (result.IsCancelled)
            return result.LogWarning();
        if (result.IsFailed)
            return result.LogError();
        _log.Here().Information("Synchronized Other Videos library {PlexLibraryId}: {@Report}", library.Id, report);
        return Result.Ok(report);
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
