using EFCore.BulkExtensions;

namespace Reaparr.Application;

public record SyncPlexPhotosCommand(InsertMediaMetaDataCommandResponse LibraryMetadata, bool ForceMediaRefresh = false)
    : ICommand<Result<CrudPhotosReport>>;

public class SyncPlexPhotosCommandValidator : AbstractValidator<SyncPlexPhotosCommand>
{
    public SyncPlexPhotosCommandValidator()
    {
        RuleFor(x => x.LibraryMetadata).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.LibraryMetadata.PlexLibrary.PlexServerId).GreaterThan(0);
        RuleFor(x => x.LibraryMetadata.PlexLibrary.PhotoAlbums).NotNull();
        RuleForEach(x => x.LibraryMetadata.PlexLibrary.PhotoAlbums)
            .ChildRules(album => album.RuleFor(x => x.PlexApiRatingKey).GreaterThan(0));
        RuleForEach(x => x.LibraryMetadata.PlexLibrary.Photos).ChildRules(photo =>
        {
            photo.RuleFor(x => x.PlexApiRatingKey).GreaterThan(0);
            photo.RuleFor(x => x.PlexPhotoAlbum).NotNull();
        });
        RuleFor(x => x.LibraryMetadata.PhotoClipCount).GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(x => x.LibraryMetadata.PlexLibrary.Photos.Count);
    }
}

public class SyncPlexPhotosCommandHandler : ICommandHandler<SyncPlexPhotosCommand, Result<CrudPhotosReport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public SyncPlexPhotosCommandHandler(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<SyncPlexPhotosCommandHandler>();
        _dbContext = dbContext;
    }

    public async Task<Result<CrudPhotosReport>> ExecuteAsync(SyncPlexPhotosCommand command, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.TaskIsCancelled(nameof(SyncPlexPhotosCommand)).LogWarning();
        var library = command.LibraryMetadata.PlexLibrary;
        var bulkConfig = new BulkConfig { BatchSize = 500, SetOutputIdentity = true, PreserveInsertOrder = true };
        var albums = library.PhotoAlbums.ToList();
        var photos = library.Photos.ToList();
        var currentAlbums = await _dbContext.PlexPhotoAlbums.Where(x => x.PlexLibraryId == library.Id)
            .Select(x => new CurrentAlbum(x.Id, x.PlexApiRatingKey, x.UpdatedAt)).ToListAsync(ct);
        var currentPhotos = await _dbContext.PlexPhotos.Where(x => x.PlexLibraryId == library.Id)
            .Select(x => new CurrentPhoto(x.Id, x.PlexApiRatingKey, x.UpdatedAt, x.PlexPhotoAlbum!.PlexApiRatingKey, x.MediaSize))
            .ToListAsync(ct);
        var albumByKey = currentAlbums.ToDictionary(x => x.PlexApiRatingKey);
        var photoByKey = currentPhotos.ToDictionary(x => x.PlexApiRatingKey);
        var createdAlbums = new List<PlexPhotoAlbum>();
        var updatedAlbums = new List<PlexPhotoAlbum>();
        foreach (var album in albums)
        {
            album.PlexLibraryId = library.Id;
            album.PlexServerId = library.PlexServerId;
            if (!albumByKey.TryGetValue(album.PlexApiRatingKey, out var existing))
            {
                album.Id = 0;
                createdAlbums.Add(album);
            }
            else
            {
                album.Id = existing.Id;
                if (album.UpdatedAt != existing.UpdatedAt)
                    updatedAlbums.Add(album);
            }
        }
        var incomingAlbumByKey = albums.ToDictionary(x => x.PlexApiRatingKey);
        var createdPhotos = new List<PlexPhoto>();
        var updatedPhotos = new List<PlexPhoto>();
        var mediaSize = 0L;
        foreach (var photo in photos)
        {
            photo.PlexLibraryId = library.Id;
            photo.PlexServerId = library.PlexServerId;
            photo.PlexPhotoAlbum = incomingAlbumByKey[photo.PlexPhotoAlbum!.PlexApiRatingKey];
            var changed = true;
            if (!photoByKey.TryGetValue(photo.PlexApiRatingKey, out var existing))
            {
                photo.Id = 0;
                createdPhotos.Add(photo);
            }
            else
            {
                photo.Id = existing.Id;
                changed = photo.UpdatedAt != existing.UpdatedAt || photo.PlexPhotoAlbum.PlexApiRatingKey != existing.ParentKey;
                if (changed)
                    updatedPhotos.Add(photo);
            }
            mediaSize += !command.ForceMediaRefresh && !changed ? existing!.MediaSize : photo.MediaSize;
        }
        var incomingPhotoKeys = photos.Select(x => x.PlexApiRatingKey).ToHashSet();
        var deletedAlbums = currentAlbums.Where(x => !incomingAlbumByKey.ContainsKey(x.PlexApiRatingKey)).ToList();
        var deletedPhotos = currentPhotos.Where(x => !incomingPhotoKeys.Contains(x.PlexApiRatingKey)).ToList();
        var report = new CrudPhotosReport
        {
            CreatedPhotoAlbums = createdAlbums.Count, UpdatedPhotoAlbums = updatedAlbums.Count,
            DeletedPhotoAlbums = deletedAlbums.Count, UnchangedPhotoAlbums = albums.Count - createdAlbums.Count - updatedAlbums.Count,
            CreatedPhotos = createdPhotos.Count, UpdatedPhotos = updatedPhotos.Count,
            DeletedPhotos = deletedPhotos.Count, UnchangedPhotos = photos.Count - createdPhotos.Count - updatedPhotos.Count,
        };
        var result = await _dbContext.ExecuteTransactionAsync(async (ctx, txCt) =>
        {
            if (command.ForceMediaRefresh)
            {
                await ctx.PlexPhotoAlbums.Where(x => x.PlexLibraryId == library.Id).ExecuteDeleteAsync(txCt);
                createdAlbums = albums;
                createdPhotos = photos;
                updatedAlbums = [];
                updatedPhotos = [];
                deletedAlbums = currentAlbums;
                deletedPhotos = currentPhotos;
                foreach (var album in albums)
                    album.Id = 0;
                foreach (var photo in photos)
                    photo.Id = 0;
                report.CreatedPhotoAlbums = albums.Count;
                report.UpdatedPhotoAlbums = 0;
                report.DeletedPhotoAlbums = currentAlbums.Count;
                report.UnchangedPhotoAlbums = 0;
                report.CreatedPhotos = photos.Count;
                report.UpdatedPhotos = 0;
                report.DeletedPhotos = currentPhotos.Count;
                report.UnchangedPhotos = 0;
            }
            await ctx.BulkDeleteByIdsAsync(updatedPhotos.Select(x => x.Id).ToList(),
                (db, ids) => db.PlexPhotoData.Where(x => ids.Contains(x.PlexPhotoId)), txCt);
            await ctx.BulkDeleteByIdsAsync(deletedPhotos.Select(x => x.Id).ToList(),
                (db, ids) => db.PlexPhotos.Where(x => ids.Contains(x.Id)), txCt);
            if (updatedAlbums.Count > 0)
                await ctx.BulkUpdateAsync(updatedAlbums, bulkConfig, txCt);
            if (createdAlbums.Count > 0)
                await ctx.BulkInsertAsync(createdAlbums, bulkConfig, txCt);
            foreach (var photo in photos)
                photo.PlexPhotoAlbumId = photo.PlexPhotoAlbum!.Id;
            if (updatedPhotos.Count > 0)
                await ctx.BulkUpdateAsync(updatedPhotos, bulkConfig, txCt);
            if (createdPhotos.Count > 0)
                await ctx.BulkInsertAsync(createdPhotos, bulkConfig, txCt);
            var mediaData = createdPhotos.Concat(updatedPhotos).SelectMany(photo =>
            {
                foreach (var data in photo.MediaDataList)
                {
                    data.Id = 0;
                    data.PlexPhotoId = photo.Id;
                    data.PlexLibraryId = library.Id;
                    data.PlexServerId = library.PlexServerId;
                }
                return photo.MediaDataList;
            }).ToList();
            if (mediaData.Count > 0)
                await ctx.BulkInsertAsync(mediaData, bulkConfig, txCt);
            // Reparent surviving photos before deleting albums with cascading foreign keys.
            await ctx.BulkDeleteByIdsAsync(deletedAlbums.Select(x => x.Id).ToList(),
                (db, ids) => db.PlexPhotoAlbums.Where(x => ids.Contains(x.Id)), txCt);
            await ctx.SetPhotoMediaMetrics(library.Id, albums.Count, photos.Count - command.LibraryMetadata.PhotoClipCount,
                command.LibraryMetadata.PhotoClipCount, mediaSize, txCt);
        }, ct);
        if (result.IsCancelled)
            return result.LogWarning();
        if (result.IsFailed)
            return result.LogError();
        _log.Here().Information("Synchronized Photos library {PlexLibraryId}: {@Report}", library.Id, report);
        return Result.Ok(report);
    }

    private sealed record CurrentAlbum(int Id, int PlexApiRatingKey, DateTime? UpdatedAt);
    private sealed record CurrentPhoto(int Id, int PlexApiRatingKey, DateTime? UpdatedAt, int ParentKey, long MediaSize);
}

public record CrudPhotosReport
{
    public int CreatedPhotoAlbums { get; set; }
    public int UpdatedPhotoAlbums { get; set; }
    public int DeletedPhotoAlbums { get; set; }
    public int UnchangedPhotoAlbums { get; set; }
    public int CreatedPhotos { get; set; }
    public int UpdatedPhotos { get; set; }
    public int DeletedPhotos { get; set; }
    public int UnchangedPhotos { get; set; }
    public int ChangedItemCount => CreatedPhotoAlbums + UpdatedPhotoAlbums + DeletedPhotoAlbums + CreatedPhotos + UpdatedPhotos + DeletedPhotos;
}
