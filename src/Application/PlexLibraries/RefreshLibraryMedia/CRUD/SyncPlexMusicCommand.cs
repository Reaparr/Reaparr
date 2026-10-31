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
        var incomingArtists = library.Music.ToList();
        var artists = new List<PlexMusicArtist>();
        var albums = new List<PlexMusicAlbum>();
        var tracks = new List<PlexMusicTrack>();
        var retainedArtistKeys = new HashSet<int>();
        var retainedAlbumKeys = new HashSet<int>();
        var retainedTrackKeys = new HashSet<int>();
        void RetainArtist(PlexMusicArtist artist)
        {
            if (retainedArtistKeys.Add(artist.PlexApiRatingKey))
                artists.Add(artist);
        }
        void RetainAlbum(PlexMusicAlbum album)
        {
            if (retainedAlbumKeys.Add(album.PlexApiRatingKey))
                albums.Add(album);
        }
        void RetainTrack(PlexMusicTrack track)
        {
            if (retainedTrackKeys.Add(track.PlexApiRatingKey))
                tracks.Add(track);
        }
        var bulkConfig = new BulkConfig { BatchSize = 500 };
        var result = await Result.Try(async Task<Result<CrudMusicReport>> () =>
        {
            var ctx = _dbContext;
            var report = new CrudMusicReport();
            List<PlexMusicArtist> currentArtists;
            List<PlexMusicAlbum> currentAlbums;
            List<PlexMusicTrack> currentTracks;

            if (command.ForceMediaRefresh)
            {
                var currentCounts = await ctx
                    .PlexMusicArtists.AsNoTracking()
                    .Where(x => x.PlexLibraryId == library.Id)
                    .Select(x => new
                    {
                        AlbumCount = x.Albums.Count,
                        TrackCount = x.Albums.SelectMany(album => album.Tracks).Count(),
                    })
                    .ToListAsync(ct);
                report.DeletedArtists = currentCounts.Count;
                report.DeletedAlbums = currentCounts.Sum(x => x.AlbumCount);
                report.DeletedTracks = currentCounts.Sum(x => x.TrackCount);
                await ctx.PlexMusicArtists.Where(x => x.PlexLibraryId == library.Id).ExecuteDeleteAsync(ct);
                currentArtists = [];
                currentAlbums = [];
                currentTracks = [];
            }
            else
            {
                currentArtists = await ctx
                    .PlexMusicArtists.AsTracking()
                    .Where(x => x.PlexLibraryId == library.Id)
                    .Include(x => x.Albums)
                        .ThenInclude(x => x.Tracks)
                            .ThenInclude(x => x.MediaDataList)
                    .ToListAsync(ct);
                currentAlbums = currentArtists.SelectMany(x => x.Albums).ToList();
                currentTracks = currentAlbums.SelectMany(x => x.Tracks).ToList();
            }

            var artistByKey = currentArtists.ToDictionary(x => x.PlexApiRatingKey);
            var albumByKey = currentAlbums.ToDictionary(x => x.PlexApiRatingKey);
            var trackByKey = currentTracks.ToDictionary(x => x.PlexApiRatingKey);
            var refreshedArtists = new List<(PlexMusicArtist Incoming, PlexMusicArtist Stored)>();
            var refreshedAlbums = new List<(PlexMusicAlbum Incoming, PlexMusicAlbum Stored)>();
            var createdTracks = new List<(PlexMusicTrack Incoming, PlexMusicTrack Stored)>();
            var refreshedMediaData = new List<PlexMusicTrackMediaData>();
            var createdMediaData = new List<(PlexMusicTrackMediaData Incoming, PlexMusicTrackMediaData Stored)>();
            foreach (var incomingArtist in incomingArtists)
            {
                artistByKey.TryGetValue(incomingArtist.PlexApiRatingKey, out var artist);
                if (artist is not null)
                    incomingArtist.Id = artist.Id;
                if (artist is not null && incomingArtist.UpdatedAt == artist.UpdatedAt)
                {
                    RetainArtist(artist);
                    foreach (var storedAlbum in artist.Albums)
                        RetainAlbum(storedAlbum);
                    foreach (var storedTrack in artist.Albums.SelectMany(x => x.Tracks))
                        RetainTrack(storedTrack);
                    continue;
                }

                incomingArtist.PlexLibraryId = library.Id;
                incomingArtist.PlexServerId = library.PlexServerId;
                if (artist is null)
                {
                    artist = (PlexMusicArtist)ctx.Entry(incomingArtist).CurrentValues.ToObject();
                    ctx.Entry(artist).State = EntityState.Added;
                    report.CreatedArtists++;
                }
                else
                {
                    ctx.Entry(artist).CurrentValues.SetValues(incomingArtist);
                    report.UpdatedArtists++;
                }

                RetainArtist(artist);
                refreshedArtists.Add((incomingArtist, artist));
                foreach (var incomingAlbum in incomingArtist.Albums)
                {
                    albumByKey.TryGetValue(incomingAlbum.PlexApiRatingKey, out var album);
                    if (album is not null)
                        incomingAlbum.Id = album.Id;
                    if (album is not null && incomingAlbum.UpdatedAt == album.UpdatedAt)
                    {
                        RetainAlbum(album);
                        foreach (var storedTrack in album.Tracks)
                            RetainTrack(storedTrack);
                        continue;
                    }

                    incomingAlbum.PlexArtist = incomingArtist;
                    incomingAlbum.PlexArtistId = artist.Id;
                    incomingAlbum.ParentKey = artist.PlexApiRatingKey;
                    incomingAlbum.PlexLibraryId = library.Id;
                    incomingAlbum.PlexServerId = library.PlexServerId;
                    if (album is null)
                    {
                        album = (PlexMusicAlbum)ctx.Entry(incomingAlbum).CurrentValues.ToObject();
                        album.PlexArtist = artist;
                        ctx.Entry(album).State = EntityState.Added;
                        report.CreatedAlbums++;
                    }
                    else
                    {
                        ctx.Entry(album).CurrentValues.SetValues(incomingAlbum);
                        album.PlexArtist = artist;
                        report.UpdatedAlbums++;
                    }

                    RetainAlbum(album);
                    refreshedAlbums.Add((incomingAlbum, album));
                    foreach (var incomingTrack in incomingAlbum.Tracks)
                    {
                        trackByKey.TryGetValue(incomingTrack.PlexApiRatingKey, out var track);
                        if (track is not null)
                            incomingTrack.Id = track.Id;
                        if (track is not null && incomingTrack.UpdatedAt == track.UpdatedAt)
                        {
                            RetainTrack(track);
                            continue;
                        }

                        incomingTrack.PlexAlbum = incomingAlbum;
                        incomingTrack.PlexAlbumId = album.Id;
                        incomingTrack.ParentKey = album.PlexApiRatingKey;
                        incomingTrack.PlexLibraryId = library.Id;
                        incomingTrack.PlexServerId = library.PlexServerId;
                        if (track is null)
                        {
                            track = (PlexMusicTrack)ctx.Entry(incomingTrack).CurrentValues.ToObject();
                            track.PlexAlbum = album;
                            ctx.Entry(track).State = EntityState.Added;
                            createdTracks.Add((incomingTrack, track));
                            report.CreatedTracks++;
                        }
                        else
                        {
                            ctx.Entry(track).CurrentValues.SetValues(incomingTrack);
                            track.PlexAlbum = album;
                            report.UpdatedTracks++;
                        }

                        ReconcileTrackMedia(
                            ctx,
                            incomingTrack,
                            track,
                            library.Id,
                            library.PlexServerId,
                            refreshedMediaData,
                            createdMediaData
                        );
                        RetainTrack(track);
                    }
                }
            }

            // Resolve moves before severing required relationships, using each retained child's actual owner.
            var albumsByArtist = albums.GroupBy(x => x.PlexArtist!).ToDictionary(x => x.Key, x => x.ToList());
            var tracksByAlbum = tracks.GroupBy(x => x.PlexAlbum!).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var (_, stored) in refreshedAlbums)
            {
                stored.Tracks = tracksByAlbum.GetValueOrDefault(stored) ?? [];
                stored.ChildCount = stored.Tracks.Count;
                stored.Duration = stored.Tracks.Sum(x => x.Duration);
                stored.MediaSize = stored.Tracks.Sum(x => x.MediaSize);
            }
            foreach (var (_, stored) in refreshedArtists)
            {
                stored.Albums = albumsByArtist.GetValueOrDefault(stored) ?? [];
                stored.ChildCount = stored.Albums.Count;
                stored.Duration = stored.Albums.Sum(x => x.Duration);
                stored.MediaSize = stored.Albums.Sum(x => x.MediaSize);
            }

            var artistKeys = artists.Select(x => x.PlexApiRatingKey).ToHashSet();
            var albumKeys = albums.Select(x => x.PlexApiRatingKey).ToHashSet();
            var trackKeys = tracks.Select(x => x.PlexApiRatingKey).ToHashSet();
            var deletedTracks = currentTracks.Where(x => !trackKeys.Contains(x.PlexApiRatingKey)).ToList();
            var deletedAlbums = currentAlbums.Where(x => !albumKeys.Contains(x.PlexApiRatingKey)).ToList();
            var deletedArtists = currentArtists.Where(x => !artistKeys.Contains(x.PlexApiRatingKey)).ToList();
            ctx.PlexMusicTracks.RemoveRange(deletedTracks);
            ctx.PlexMusicAlbums.RemoveRange(deletedAlbums);
            ctx.PlexMusicArtists.RemoveRange(deletedArtists);
            report.DeletedTracks += deletedTracks.Count;
            report.DeletedAlbums += deletedAlbums.Count;
            report.DeletedArtists += deletedArtists.Count;
            report.UnchangedArtists = artists.Count - report.CreatedArtists - report.UpdatedArtists;
            report.UnchangedAlbums = albums.Count - report.CreatedAlbums - report.UpdatedAlbums;
            report.UnchangedTracks = tracks.Count - report.CreatedTracks - report.UpdatedTracks;
            await ctx.SaveChangesAsync(ct);

            foreach (var (incoming, stored) in refreshedArtists)
                incoming.Id = stored.Id;
            foreach (var (incoming, stored) in refreshedAlbums)
            {
                incoming.Id = stored.Id;
                incoming.PlexArtistId = stored.PlexArtistId;
            }

            foreach (var (incoming, stored) in createdTracks)
            {
                incoming.Id = stored.Id;
                incoming.PlexAlbumId = stored.PlexAlbumId;
            }
            foreach (var (incoming, stored) in createdMediaData)
            {
                incoming.Id = stored.Id;
                incoming.PlexTrackId = stored.PlexTrackId;
            }
            if (refreshedMediaData.Count > 0)
                await ReconcileMusicTrackStreamsAsync(ctx, refreshedMediaData, ct);
            if (refreshedArtists.Count > 0)
            {
                var metadataArtists = refreshedArtists.Select(x => x.Incoming).ToList();
                await SyncArtistCountries(ctx, metadataArtists, command.LibraryMetadata, bulkConfig, ct);
                await SyncArtistGenres(ctx, metadataArtists, command.LibraryMetadata, bulkConfig, ct);
                await SyncArtistActors(ctx, metadataArtists, command.LibraryMetadata, bulkConfig, ct);
            }
            if (refreshedAlbums.Count > 0)
                await SyncAlbumCountries(
                    ctx,
                    refreshedAlbums.Select(x => x.Incoming).ToList(),
                    command.LibraryMetadata,
                    bulkConfig,
                    ct
                );
            if (refreshedArtists.Count > 0 || refreshedAlbums.Count > 0)
                await PersistMusicAudioQualities(
                    ctx,
                    refreshedArtists.Select(x => x.Stored).ToList(),
                    refreshedAlbums.Select(x => x.Stored).ToList(),
                    library.Id,
                    bulkConfig,
                    ct
                );

            // Refresh library metrics for every completed authoritative snapshot, including empty no-op snapshots.
            await ctx.SetMusicMediaMetrics(
                library.Id,
                artists.Count,
                albums.Count,
                tracks.Count,
                tracks.Sum(x => x.MediaSize),
                ct
            );
            if (report.ChangedItemCount > 0)
                await ctx
                    .PlexLibraries.Where(x => x.Id == library.Id)
                    .ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, true), ct);
            return Result.Ok(report);
        });

        if (result.IsFailed)
            return result.LogIfFailed();

        library.Music.AddRange(artists);
        library.Albums.AddRange(albums);
        library.Tracks.AddRange(tracks);
        _log.Here().Information("Synchronized Music library {PlexLibraryId}: {@Report}", library.Id, result.Value);
        return result;
    }

    private static Task ReconcileMusicTrackStreamsAsync(
        IReaparrDbContext dbContext,
        IReadOnlyCollection<PlexMusicTrackMediaData> mediaData,
        CancellationToken cancellationToken
    ) =>
        PlexMediaStreamReconciler.SyncDataStreamsAsync(
            dbContext,
            mediaData,
            static data => data.Streams,
            context => context.PlexMusicTrackDataStreams,
            static (sourceStream, ownerId, languageId) =>
                new PlexMusicTrackDataStream
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

    private static void ReconcileTrackMedia(
        IReaparrDbContext ctx,
        PlexMusicTrack incoming,
        PlexMusicTrack track,
        int libraryId,
        int serverId,
        List<PlexMusicTrackMediaData> refreshedMediaData,
        List<(PlexMusicTrackMediaData Incoming, PlexMusicTrackMediaData Stored)> createdMediaData
    )
    {
        var remainingRows = track.MediaDataList.ToDictionary(DataKey);
        var seenKeys = new HashSet<(int MediaId, int PartId)>();
        var mediaRows = new List<PlexMusicTrackMediaData>(incoming.MediaDataList.Count);
        foreach (var row in incoming.MediaDataList)
        {
            var key = DataKey(row);
            if (!seenKeys.Add(key))
                continue;

            row.PlexTrackId = track.Id;
            row.PlexLibraryId = libraryId;
            row.PlexServerId = serverId;
            row.AudioQuality = row.ClassifyAudioQuality();
            if (remainingRows.Remove(key, out var stored))
            {
                row.Id = stored.Id;
                ctx.Entry(stored).CurrentValues.SetValues(row);
            }
            else
            {
                stored = (PlexMusicTrackMediaData)ctx.Entry(row).CurrentValues.ToObject();
                stored.PlexTrack = track;
                ctx.Entry(stored).State = EntityState.Added;
                createdMediaData.Add((row, stored));
            }

            refreshedMediaData.Add(row);
            mediaRows.Add(stored);
        }

        ctx.PlexMusicTrackData.RemoveRange(remainingRows.Values);
        track.MediaDataList = mediaRows;
        track.MediaSize = mediaRows.Sum(x => x.Size);
    }

    private static async Task PersistMusicAudioQualities(
        IReaparrDbContext ctx,
        List<PlexMusicArtist> artists,
        List<PlexMusicAlbum> albums,
        int libraryId,
        BulkConfig bulkConfig,
        CancellationToken ct
    )
    {
        var albumQualityKeys = new HashSet<(int AlbumId, AudioQuality AudioQuality)>();
        foreach (var album in albums)
        foreach (var track in album.Tracks)
        foreach (var data in track.MediaDataList)
            albumQualityKeys.Add((album.Id, data.AudioQuality));
        var artistQualityKeys = new HashSet<(int ArtistId, AudioQuality AudioQuality)>();
        foreach (var artist in artists)
        foreach (var album in artist.Albums)
        foreach (var track in album.Tracks)
        foreach (var data in track.MediaDataList)
            artistQualityKeys.Add((artist.Id, data.AudioQuality));
        var albumIds = albums.Select(x => x.Id).ToList();
        var artistIds = artists.Select(x => x.Id).ToList();

        var existingAlbumQualities = await ctx
            .PlexMusicAlbumMediaQualities.Where(x =>
                x.PlexLibraryId == libraryId && albumIds.Contains(x.PlexMusicAlbumId)
            )
            .Select(x => new
            {
                x.Id,
                x.PlexMusicAlbumId,
                Quality = x.AudioQuality,
            })
            .ToListAsync(ct);
        var obsoleteAlbumQualityIds = new List<int>();
        foreach (var quality in existingAlbumQualities)
            if (!albumQualityKeys.Remove((quality.PlexMusicAlbumId, quality.Quality)))
                obsoleteAlbumQualityIds.Add(quality.Id);
        if (obsoleteAlbumQualityIds.Count > 0)
            await ctx
                .PlexMusicAlbumMediaQualities.Where(x => obsoleteAlbumQualityIds.Contains(x.Id))
                .ExecuteDeleteAsync(ct);
        if (albumQualityKeys.Count > 0)
            await ctx.BulkInsertAsync(
                albumQualityKeys
                    .Select(quality => new PlexMusicAlbumMediaQuality
                    {
                        PlexLibraryId = libraryId,
                        PlexMusicAlbumId = quality.AlbumId,
                        AudioQuality = quality.AudioQuality,
                    })
                    .ToList(),
                bulkConfig,
                ct
            );

        var existingArtistQualities = await ctx
            .PlexMusicArtistMediaQualities.Where(x =>
                x.PlexLibraryId == libraryId && artistIds.Contains(x.PlexMusicArtistId)
            )
            .Select(x => new
            {
                x.Id,
                x.PlexMusicArtistId,
                x.AudioQuality,
            })
            .ToListAsync(ct);
        var obsoleteArtistQualityIds = new List<int>();
        foreach (var quality in existingArtistQualities)
            if (!artistQualityKeys.Remove((quality.PlexMusicArtistId, quality.AudioQuality)))
                obsoleteArtistQualityIds.Add(quality.Id);
        if (obsoleteArtistQualityIds.Count > 0)
            await ctx
                .PlexMusicArtistMediaQualities.Where(x => obsoleteArtistQualityIds.Contains(x.Id))
                .ExecuteDeleteAsync(ct);
        if (artistQualityKeys.Count > 0)
            await ctx.BulkInsertAsync(
                artistQualityKeys
                    .Select(quality => new PlexMusicArtistMediaQuality
                    {
                        PlexLibraryId = libraryId,
                        PlexMusicArtistId = quality.ArtistId,
                        AudioQuality = quality.AudioQuality,
                    })
                    .ToList(),
                bulkConfig,
                ct
            );
    }

    private static async Task SyncArtistActors(
        IReaparrDbContext ctx,
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
                rows.Add(new PlexMusicArtistActors(stored.Id, artist.Id));

        var artistIds = artists.Select(x => x.Id).ToList();
        await ctx.PlexMusicArtistActors.Where(x => artistIds.Contains(x.PlexMusicArtistId)).ExecuteDeleteAsync(ct);
        if (rows.Count > 0)
            await ctx.BulkInsertAsync(rows, bulkConfig, ct);
    }

    private static async Task SyncArtistGenres(
        IReaparrDbContext ctx,
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
            if (metadata.PlexGenresAudio.TryGetValue(genre.Key, out var stored) && keys.Add((stored.Id, artist.Id)))
                rows.Add(new PlexMusicArtistGenres(stored.Id, artist.Id));

        var artistIds = artists.Select(x => x.Id).ToList();
        await ctx.PlexMusicArtistGenres.Where(x => artistIds.Contains(x.PlexMusicArtistId)).ExecuteDeleteAsync(ct);
        if (rows.Count > 0)
            await ctx.BulkInsertAsync(rows, bulkConfig, ct);
    }

    private static async Task SyncArtistCountries(
        IReaparrDbContext ctx,
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
                rows.Add(new PlexMusicArtistCountries(stored.Id, artist.Id));

        var artistIds = artists.Select(x => x.Id).ToList();
        await ctx.PlexMusicArtistCountries.Where(x => artistIds.Contains(x.PlexMusicArtistId)).ExecuteDeleteAsync(ct);
        if (rows.Count > 0)
            await ctx.BulkInsertAsync(rows, bulkConfig, ct);
    }

    private static async Task SyncAlbumCountries(
        IReaparrDbContext ctx,
        List<PlexMusicAlbum> albums,
        InsertMediaMetaDataCommandResponse metadata,
        BulkConfig bulkConfig,
        CancellationToken ct
    )
    {
        var rows = new List<PlexMusicAlbumCountries>();
        var keys = new HashSet<(int CountryId, int AlbumId)>();
        foreach (var album in albums)
        foreach (var country in album.Countries)
            if (metadata.PlexCountries.TryGetValue(country.Key, out var stored) && keys.Add((stored.Id, album.Id)))
                rows.Add(new PlexMusicAlbumCountries(stored.Id, album.Id));

        var albumIds = albums.Select(x => x.Id).ToList();
        await ctx.PlexMusicAlbumCountries.Where(x => albumIds.Contains(x.PlexMusicAlbumId)).ExecuteDeleteAsync(ct);
        if (rows.Count > 0)
            await ctx.BulkInsertAsync(rows, bulkConfig, ct);
    }

    private static (int MediaId, int PartId) DataKey(PlexMusicTrackMediaData data) =>
        (data.PlexApiMediaId, data.PlexApiPartId);
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
