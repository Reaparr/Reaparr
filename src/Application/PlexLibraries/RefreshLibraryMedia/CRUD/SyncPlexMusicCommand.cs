using EFCore.BulkExtensions;

namespace Reaparr.Application;

public record SyncPlexMusicCommand(InsertMediaMetaDataCommandResponse LibraryMetadata, bool ForceMediaRefresh = false)
    : ICommand<Result<CrudMusicReport>>;

public class SyncPlexMusicCommandValidator : AbstractValidator<SyncPlexMusicCommand>
{
    public SyncPlexMusicCommandValidator()
    {
        RuleFor(x => x.LibraryMetadata).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.LibraryMetadata.PlexLibrary.PlexServerId).GreaterThan(0);
        RuleForEach(x => x.LibraryMetadata.PlexLibrary.Music)
            .ChildRules(artist =>
            {
                artist.RuleFor(x => x.PlexApiRatingKey).GreaterThan(0);
                artist
                    .RuleForEach(x => x.Albums)
                    .ChildRules(album =>
                    {
                        album.RuleFor(x => x.PlexApiRatingKey).GreaterThan(0);
                        album
                            .RuleForEach(x => x.Tracks)
                            .ChildRules(track => track.RuleFor(x => x.PlexApiRatingKey).GreaterThan(0));
                    });
            });
    }
}

public class SyncPlexMusicCommandHandler : ICommandHandler<SyncPlexMusicCommand, Result<CrudMusicReport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public SyncPlexMusicCommandHandler(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<SyncPlexMusicCommandHandler>();
        _dbContext = dbContext;
    }

    public async Task<Result<CrudMusicReport>> ExecuteAsync(SyncPlexMusicCommand command, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return ResultExtensions.TaskIsCancelled(nameof(SyncPlexMusicCommand)).LogWarning();
        var library = command.LibraryMetadata.PlexLibrary;
        var bulkConfig = new BulkConfig
        {
            BatchSize = 500,
            SetOutputIdentity = true,
            PreserveInsertOrder = true,
        };
        var artists = library.Music.ToList();
        var albums = artists.SelectMany(x => x.Albums).ToList();
        var tracks = albums.SelectMany(x => x.Tracks).ToList();
        var currentArtists = await _dbContext
            .PlexArtists.Where(x => x.PlexLibraryId == library.Id)
            .Select(x => new CurrentArtist(x.Id, x.PlexApiRatingKey, x.UpdatedAt))
            .ToListAsync(ct);
        var currentAlbums = await _dbContext
            .PlexAlbums.Where(x => x.PlexLibraryId == library.Id)
            .Select(x => new CurrentAlbum(x.Id, x.PlexApiRatingKey, x.UpdatedAt, x.PlexArtist!.PlexApiRatingKey))
            .ToListAsync(ct);
        var currentTracks = await _dbContext
            .PlexTracks.Where(x => x.PlexLibraryId == library.Id)
            .Select(x => new CurrentTrack(
                x.Id,
                x.PlexApiRatingKey,
                x.UpdatedAt,
                x.PlexAlbum!.PlexApiRatingKey,
                x.MediaSize
            ))
            .ToListAsync(ct);
        var artistByKey = currentArtists.ToDictionary(x => x.PlexApiRatingKey);
        var albumByKey = currentAlbums.ToDictionary(x => x.PlexApiRatingKey);
        var trackByKey = currentTracks.ToDictionary(x => x.PlexApiRatingKey);
        var createdArtists = new List<PlexMusicArtist>();
        var updatedArtists = new List<PlexMusicArtist>();
        var createdAlbums = new List<PlexMusicAlbum>();
        var updatedAlbums = new List<PlexMusicAlbum>();
        var createdTracks = new List<PlexMusicTrack>();
        var updatedTracks = new List<PlexMusicTrack>();
        var mediaSize = 0L;
        foreach (var artist in artists)
        {
            artist.PlexLibraryId = library.Id;
            artist.PlexServerId = library.PlexServerId;
            if (!artistByKey.TryGetValue(artist.PlexApiRatingKey, out var currentArtist))
            {
                artist.Id = 0;
                createdArtists.Add(artist);
            }
            else
            {
                artist.Id = currentArtist.Id;
                if (artist.UpdatedAt != currentArtist.UpdatedAt)
                    updatedArtists.Add(artist);
            }
            foreach (var album in artist.Albums)
            {
                album.PlexArtist = artist;
                album.ParentKey = artist.PlexApiRatingKey;
                album.PlexLibraryId = library.Id;
                album.PlexServerId = library.PlexServerId;
                if (!albumByKey.TryGetValue(album.PlexApiRatingKey, out var currentAlbum))
                {
                    album.Id = 0;
                    createdAlbums.Add(album);
                }
                else
                {
                    album.Id = currentAlbum.Id;
                    if (album.UpdatedAt != currentAlbum.UpdatedAt || album.ParentKey != currentAlbum.ParentKey)
                        updatedAlbums.Add(album);
                }
                foreach (var track in album.Tracks)
                {
                    track.PlexAlbum = album;
                    track.ParentKey = album.PlexApiRatingKey;
                    track.PlexLibraryId = library.Id;
                    track.PlexServerId = library.PlexServerId;
                    var changed = true;
                    if (!trackByKey.TryGetValue(track.PlexApiRatingKey, out var currentTrack))
                    {
                        track.Id = 0;
                        createdTracks.Add(track);
                    }
                    else
                    {
                        track.Id = currentTrack.Id;
                        changed =
                            track.UpdatedAt != currentTrack.UpdatedAt || track.ParentKey != currentTrack.ParentKey;
                        if (changed)
                            updatedTracks.Add(track);
                    }
                    mediaSize += !command.ForceMediaRefresh && !changed ? currentTrack!.MediaSize : track.MediaSize;
                }
            }
        }
        var artistKeys = artists.Select(x => x.PlexApiRatingKey).ToHashSet();
        var albumKeys = albums.Select(x => x.PlexApiRatingKey).ToHashSet();
        var trackKeys = tracks.Select(x => x.PlexApiRatingKey).ToHashSet();
        var deletedArtists = currentArtists.Where(x => !artistKeys.Contains(x.PlexApiRatingKey)).ToList();
        var deletedAlbums = currentAlbums.Where(x => !albumKeys.Contains(x.PlexApiRatingKey)).ToList();
        var deletedTracks = currentTracks.Where(x => !trackKeys.Contains(x.PlexApiRatingKey)).ToList();
        var report = new CrudMusicReport
        {
            CreatedArtists = createdArtists.Count,
            UpdatedArtists = updatedArtists.Count,
            DeletedArtists = deletedArtists.Count,
            UnchangedArtists = artists.Count - createdArtists.Count - updatedArtists.Count,
            CreatedAlbums = createdAlbums.Count,
            UpdatedAlbums = updatedAlbums.Count,
            DeletedAlbums = deletedAlbums.Count,
            UnchangedAlbums = albums.Count - createdAlbums.Count - updatedAlbums.Count,
            CreatedTracks = createdTracks.Count,
            UpdatedTracks = updatedTracks.Count,
            DeletedTracks = deletedTracks.Count,
            UnchangedTracks = tracks.Count - createdTracks.Count - updatedTracks.Count,
        };
        var result = await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                if (command.ForceMediaRefresh)
                {
                    await ctx.PlexArtists.Where(x => x.PlexLibraryId == library.Id).ExecuteDeleteAsync(txCt);
                    createdArtists = artists;
                    createdAlbums = albums;
                    createdTracks = tracks;
                    updatedArtists = [];
                    updatedAlbums = [];
                    updatedTracks = [];
                    deletedArtists = currentArtists;
                    deletedAlbums = currentAlbums;
                    deletedTracks = currentTracks;
                    foreach (var artist in artists)
                        artist.Id = 0;
                    foreach (var album in albums)
                        album.Id = 0;
                    foreach (var track in tracks)
                        track.Id = 0;
                    report.CreatedArtists = artists.Count;
                    report.UpdatedArtists = 0;
                    report.DeletedArtists = currentArtists.Count;
                    report.UnchangedArtists = 0;
                    report.CreatedAlbums = albums.Count;
                    report.UpdatedAlbums = 0;
                    report.DeletedAlbums = currentAlbums.Count;
                    report.UnchangedAlbums = 0;
                    report.CreatedTracks = tracks.Count;
                    report.UpdatedTracks = 0;
                    report.DeletedTracks = currentTracks.Count;
                    report.UnchangedTracks = 0;
                }
                await ctx.BulkDeleteByIdsAsync(
                    updatedTracks.Select(x => x.Id).ToList(),
                    (db, ids) => db.PlexTrackData.Where(x => ids.Contains(x.PlexTrackId)),
                    txCt
                );
                await ctx.BulkDeleteByIdsAsync(
                    deletedTracks.Select(x => x.Id).ToList(),
                    (db, ids) => db.PlexTracks.Where(x => ids.Contains(x.Id)),
                    txCt
                );
                if (updatedArtists.Count > 0)
                    await ctx.BulkUpdateAsync(updatedArtists, bulkConfig, txCt);
                if (createdArtists.Count > 0)
                    await ctx.BulkInsertAsync(createdArtists, bulkConfig, txCt);
                foreach (var album in albums)
                    album.PlexArtistId = album.PlexArtist!.Id;
                if (updatedAlbums.Count > 0)
                    await ctx.BulkUpdateAsync(updatedAlbums, bulkConfig, txCt);
                if (createdAlbums.Count > 0)
                    await ctx.BulkInsertAsync(createdAlbums, bulkConfig, txCt);
                foreach (var track in tracks)
                    track.PlexAlbumId = track.PlexAlbum!.Id;
                if (updatedTracks.Count > 0)
                    await ctx.BulkUpdateAsync(updatedTracks, bulkConfig, txCt);
                if (createdTracks.Count > 0)
                    await ctx.BulkInsertAsync(createdTracks, bulkConfig, txCt);
                var mediaData = createdTracks
                    .Concat(updatedTracks)
                    .SelectMany(track =>
                    {
                        foreach (var data in track.MediaDataList)
                        {
                            data.Id = 0;
                            data.PlexTrackId = track.Id;
                            data.PlexLibraryId = library.Id;
                            data.PlexServerId = library.PlexServerId;
                        }
                        return track.MediaDataList;
                    })
                    .ToList();
                if (mediaData.Count > 0)
                    await ctx.BulkInsertAsync(mediaData, bulkConfig, txCt);
                // Reparent surviving children before deleting obsolete parents with cascading foreign keys.
                await ctx.BulkDeleteByIdsAsync(
                    deletedAlbums.Select(x => x.Id).ToList(),
                    (db, ids) => db.PlexAlbums.Where(x => ids.Contains(x.Id)),
                    txCt
                );
                await ctx.BulkDeleteByIdsAsync(
                    deletedArtists.Select(x => x.Id).ToList(),
                    (db, ids) => db.PlexArtists.Where(x => ids.Contains(x.Id)),
                    txCt
                );
                await ctx.SetMusicMediaMetrics(library.Id, artists.Count, albums.Count, tracks.Count, mediaSize, txCt);
            },
            ct
        );

        if (result.IsFailed)
            return result.LogIfFailed();

        var metadataResult = Result.Merge(
            await SyncArtistCountries(artists, command.LibraryMetadata, bulkConfig, ct),
            await SyncArtistGenres(artists, command.LibraryMetadata, bulkConfig, ct),
            await SyncArtistActors(artists, command.LibraryMetadata, bulkConfig, ct)
        );
        if (metadataResult.IsCancelled)
            return metadataResult.LogWarning();
        if (metadataResult.IsFailed)
            return metadataResult.LogError();
        _log.Here().Information("Synchronized Music library {PlexLibraryId}: {@Report}", library.Id, report);
        return Result.Ok(report);
    }

    private Task<Result> SyncArtistActors(
        List<PlexMusicArtist> artists,
        InsertMediaMetaDataCommandResponse metadata,
        BulkConfig bulkConfig,
        CancellationToken ct
    )
    {
        var rows = new List<PlexMusicArtistActors>();
        var keys = new HashSet<(int ActorId, int ArtistId)>();
        foreach (var artist in artists)
        foreach (var actor in artist.Actors)
            if (metadata.PlexActors.TryGetValue(actor.Key, out var stored) && keys.Add((stored.Id, artist.Id)))
                rows.Add(new PlexMusicArtistActors(stored.Id, metadata.PlexLibraryId, artist.Id));

        return _dbContext.ExecuteTransactionAsync(
            async (ctx, token) =>
            {
                await ctx
                    .PlexMusicArtistActors.Where(x => x.PlexLibraryId == metadata.PlexLibraryId)
                    .ExecuteDeleteAsync(token);
                await ctx.BulkInsertAsync(rows, bulkConfig, token);
            },
            ct
        );
    }

    private Task<Result> SyncArtistGenres(
        List<PlexMusicArtist> artists,
        InsertMediaMetaDataCommandResponse metadata,
        BulkConfig bulkConfig,
        CancellationToken ct
    )
    {
        var rows = new List<PlexMusicArtistGenres>();
        var keys = new HashSet<(int GenreId, int ArtistId)>();
        foreach (var artist in artists)
        foreach (var genre in artist.Genres)
            if (metadata.PlexGenres.TryGetValue(genre.Key, out var stored) && keys.Add((stored.Id, artist.Id)))
                rows.Add(new PlexMusicArtistGenres(stored.Id, metadata.PlexLibraryId, artist.Id));

        return _dbContext.ExecuteTransactionAsync(
            async (ctx, token) =>
            {
                await ctx
                    .PlexMusicArtistGenres.Where(x => x.PlexLibraryId == metadata.PlexLibraryId)
                    .ExecuteDeleteAsync(token);
                await ctx.BulkInsertAsync(rows, bulkConfig, token);
            },
            ct
        );
    }

    private Task<Result> SyncArtistCountries(
        List<PlexMusicArtist> artists,
        InsertMediaMetaDataCommandResponse metadata,
        BulkConfig bulkConfig,
        CancellationToken ct
    )
    {
        var rows = new List<PlexMusicArtistCountries>();
        var keys = new HashSet<(int CountryId, int ArtistId)>();
        foreach (var artist in artists)
        foreach (var country in artist.Countries)
            if (metadata.PlexCountries.TryGetValue(country.Key, out var stored) && keys.Add((stored.Id, artist.Id)))
                rows.Add(new PlexMusicArtistCountries(stored.Id, metadata.PlexLibraryId, artist.Id));

        return _dbContext.ExecuteTransactionAsync(
            async (ctx, token) =>
            {
                await ctx
                    .PlexMusicArtistCountries.Where(x => x.PlexLibraryId == metadata.PlexLibraryId)
                    .ExecuteDeleteAsync(token);
                await ctx.BulkInsertAsync(rows, bulkConfig, token);
            },
            ct
        );
    }

    private sealed record CurrentArtist(int Id, int PlexApiRatingKey, DateTime? UpdatedAt);

    private sealed record CurrentAlbum(int Id, int PlexApiRatingKey, DateTime? UpdatedAt, int ParentKey);

    private sealed record CurrentTrack(
        int Id,
        int PlexApiRatingKey,
        DateTime? UpdatedAt,
        int ParentKey,
        long MediaSize
    );
}

public record CrudMusicReport
{
    public int CreatedArtists { get; set; }
    public int UpdatedArtists { get; set; }
    public int DeletedArtists { get; set; }
    public int UnchangedArtists { get; set; }
    public int CreatedAlbums { get; set; }
    public int UpdatedAlbums { get; set; }
    public int DeletedAlbums { get; set; }
    public int UnchangedAlbums { get; set; }
    public int CreatedTracks { get; set; }
    public int UpdatedTracks { get; set; }
    public int DeletedTracks { get; set; }
    public int UnchangedTracks { get; set; }
    public int ChangedItemCount =>
        CreatedArtists
        + UpdatedArtists
        + DeletedArtists
        + CreatedAlbums
        + UpdatedAlbums
        + DeletedAlbums
        + CreatedTracks
        + UpdatedTracks
        + DeletedTracks;
}
