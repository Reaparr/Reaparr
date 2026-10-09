namespace Reaparr.Application;

public class CompareMusicPlexLibraryCommandValidator : AbstractValidator<CompareMusicPlexLibraryCommand>
{
    public CompareMusicPlexLibraryCommandValidator()
    {
        RuleFor(x => x).NotNull();
        RuleFor(x => x.OwnedPlexLibraryId).GreaterThan(0);
        RuleFor(x => x.RemotePlexLibraryId).GreaterThan(0).NotEqual(x => x.OwnedPlexLibraryId);
    }
}

public class CompareMusicPlexLibraryCommandHandler : ICommandHandler<CompareMusicPlexLibraryCommand, Result>
{
    private const int BATCH_SIZE = 100;
    private readonly IReaparrDbContext _dbContext;
    private readonly ILogger _log;

    public CompareMusicPlexLibraryCommandHandler(IReaparrDbContext dbContext, ILogger log)
    {
        _dbContext = dbContext;
        _log = log.ForContext<CompareMusicPlexLibraryCommandHandler>();
    }

    public async Task<Result> ExecuteAsync(CompareMusicPlexLibraryCommand command, CancellationToken cancellationToken)
    {
        var result = await Result.Try(new Func<Task<Result>>(async () =>
        {
            var snapshots = await LoadEligibleSnapshotsAsync(_dbContext, command, cancellationToken);
            var comparedAt = DateTime.UtcNow;
            return await _dbContext.ExecuteTransactionAsync(async (ctx, ct) =>
            {
                await EnsureCurrentAsync(ctx, command, snapshots, ct);
                await ctx.PlexMusicTrackComparisons.Where(x =>
                    x.RemotePlexLibraryId == command.RemotePlexLibraryId && x.OwnedPlexLibraryId == command.OwnedPlexLibraryId
                ).ExecuteDeleteAsync(ct);
                await ctx.PlexMusicAlbumComparisons.Where(x =>
                    x.RemotePlexLibraryId == command.RemotePlexLibraryId && x.OwnedPlexLibraryId == command.OwnedPlexLibraryId
                ).ExecuteDeleteAsync(ct);
                await ctx.PlexMusicArtistComparisons.Where(x =>
                    x.RemotePlexLibraryId == command.RemotePlexLibraryId && x.OwnedPlexLibraryId == command.OwnedPlexLibraryId
                ).ExecuteDeleteAsync(ct);

                var lastArtistId = 0;
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    var remoteArtists = await ctx.PlexArtists
                        .Where(x => x.PlexLibraryId == command.RemotePlexLibraryId && x.Id > lastArtistId)
                        .OrderBy(x => x.Id).Take(BATCH_SIZE).ToListAsync(ct);
                    if (remoteArtists.Count == 0)
                        break;
                    await WriteBatchAsync(ctx, command, remoteArtists, comparedAt, ct);
                    await ctx.SaveChangesAsync(ct);
                    ctx.ClearChangeTracker();
                    lastArtistId = remoteArtists[^1].Id;
                }

                await EnsureCurrentAsync(ctx, command, snapshots, ct);
                var scope = await ctx.PlexComparisonScopes.AsTracking().SingleOrDefaultAsync(x =>
                    x.RemotePlexLibraryId == command.RemotePlexLibraryId
                    && x.OwnedPlexLibraryId == command.OwnedPlexLibraryId
                    && x.MediaType == PlexMediaType.MusicArtist, ct);
                if (scope is null)
                    ctx.PlexComparisonScopes.Add(new PlexComparisonState
                    {
                        RemotePlexLibraryId = command.RemotePlexLibraryId,
                        OwnedPlexLibraryId = command.OwnedPlexLibraryId,
                        MediaType = PlexMediaType.MusicArtist,
                        CompletedAt = comparedAt,
                    });
                else
                    scope.CompletedAt = comparedAt;
                ct.ThrowIfCancellationRequested();
                await ctx.SaveChangesAsync(ct);
            }, cancellationToken);
        }));
        if (result.IsCancelled)
            return result.LogWarning();
        if (result.IsFailed)
            return result.LogError();
        _log.Here().Information("Completed Music comparison: remote library {RemoteLibraryId} vs owned library {OwnedLibraryId}",
            command.RemotePlexLibraryId, command.OwnedPlexLibraryId);
        return result;
    }

    private static async Task<(long Remote, long Owned)> LoadEligibleSnapshotsAsync(
        IReaparrDbContext ctx, CompareMusicPlexLibraryCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var eligible = ctx.PlexLibraries.Where(x => x.Type == PlexMediaType.MusicArtist
            && x.IsEnabled && !x.Outdated && x.SyncedAt != null
            && x.SyncedContentChangedAt == x.ContentChangedAt);
        var remote = await eligible.WhereIsNotOwned().Where(x => x.Id == command.RemotePlexLibraryId)
            .Select(x => new { x.Id, x.ContentChangedAt }).SingleOrDefaultAsync(ct);
        var owned = await eligible.WhereIsOwned().Where(x => x.Id == command.OwnedPlexLibraryId)
            .Select(x => new { x.Id, x.ContentChangedAt }).SingleOrDefaultAsync(ct);
        if (remote is null || owned is null)
            throw new InvalidOperationException("Music comparison requires current enabled remote and owned Music libraries.");
        return (remote.ContentChangedAt, owned.ContentChangedAt);
    }

    private static async Task EnsureCurrentAsync(IReaparrDbContext ctx, CompareMusicPlexLibraryCommand command,
        (long Remote, long Owned) snapshots, CancellationToken ct)
    {
        if (snapshots != await LoadEligibleSnapshotsAsync(ctx, command, ct))
            throw new InvalidOperationException("Music library content changed while comparison was running.");
    }

    private static async Task WriteBatchAsync(IReaparrDbContext ctx, CompareMusicPlexLibraryCommand command,
        List<PlexMusicArtist> remoteArtists, DateTime comparedAt, CancellationToken ct)
    {
        var ids = remoteArtists.Where(x => !string.IsNullOrWhiteSpace(x.MusicBrainzArtistId))
            .Select(x => x.MusicBrainzArtistId!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var titles = remoteArtists.Where(x => !string.IsNullOrWhiteSpace(x.SearchTitle))
            .Select(x => x.SearchTitle).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var ownedArtists = await ctx.PlexArtists.Where(x => x.PlexLibraryId == command.OwnedPlexLibraryId
            && ((x.MusicBrainzArtistId != null && ids.Contains(x.MusicBrainzArtistId)) || titles.Contains(x.SearchTitle)))
            .OrderBy(x => x.Id).ToListAsync(ct);
        var byId = ownedArtists.Where(x => HasId(x.MusicBrainzArtistId))
            .ToLookup(x => x.MusicBrainzArtistId!, StringComparer.OrdinalIgnoreCase);
        var byTitle = ownedArtists.ToLookup(x => x.SearchTitle, StringComparer.OrdinalIgnoreCase);
        var remoteTitleCounts = await ctx.PlexArtists
            .Where(x => x.PlexLibraryId == command.RemotePlexLibraryId && titles.Contains(x.SearchTitle))
            .GroupBy(x => x.SearchTitle).Select(g => new { Title = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Title, x => x.Count, StringComparer.OrdinalIgnoreCase, ct);
        var artistPairs = new List<(PlexMusicArtist Remote, PlexMusicArtist Owned)>();
        foreach (var remote in remoteArtists)
        {
            var exact = HasId(remote.MusicBrainzArtistId) ? byId[remote.MusicBrainzArtistId!].ToList() : [];
            var matches = exact.Count > 0 ? exact : byTitle[remote.SearchTitle]
                .Where(x => !Conflict(remote.MusicBrainzArtistId, x.MusicBrainzArtistId)).ToList();
            if (exact.Count == 0 && (matches.Count != 1 || !remoteTitleCounts.TryGetValue(remote.SearchTitle, out var count) || count != 1))
                continue;
            foreach (var owned in matches)
            {
                ctx.PlexMusicArtistComparisons.Add(new PlexMusicArtistComparison
                {
                    RemotePlexLibraryId = command.RemotePlexLibraryId, OwnedPlexLibraryId = command.OwnedPlexLibraryId,
                    RemotePlexMediaId = remote.Id, OwnedPlexMediaId = owned.Id, ComparedAt = comparedAt,
                    MatchType = exact.Count > 0 ? PlexMediaComparisonMatchType.MusicBrainzArtistId : PlexMediaComparisonMatchType.NormalizedTitleAndYear,
                });
                artistPairs.Add((remote, owned));
            }
        }
        if (artistPairs.Count == 0)
            return;
        var remoteArtistIds = artistPairs.Select(x => x.Remote.Id).Distinct().ToArray();
        var ownedArtistIds = artistPairs.Select(x => x.Owned.Id).Distinct().ToArray();
        var remoteAlbums = await ctx.PlexAlbums.Where(x => x.PlexLibraryId == command.RemotePlexLibraryId
            && remoteArtistIds.Contains(x.PlexArtistId)).ToListAsync(ct);
        var ownedAlbums = await ctx.PlexAlbums.Where(x => x.PlexLibraryId == command.OwnedPlexLibraryId
            && ownedArtistIds.Contains(x.PlexArtistId)).ToListAsync(ct);
        var remoteAlbumIds = remoteAlbums.Select(x => x.Id).ToArray();
        var ownedAlbumIds = ownedAlbums.Select(x => x.Id).ToArray();
        var remoteTracks = await ctx.PlexTracks.Where(x => x.PlexLibraryId == command.RemotePlexLibraryId
            && remoteAlbumIds.Contains(x.PlexAlbumId)).ToListAsync(ct);
        var ownedTracks = await ctx.PlexTracks.Where(x => x.PlexLibraryId == command.OwnedPlexLibraryId
            && ownedAlbumIds.Contains(x.PlexAlbumId)).ToListAsync(ct);
        var remoteAlbumsByArtist = remoteAlbums.ToLookup(x => x.PlexArtistId);
        var ownedAlbumsByArtist = ownedAlbums.ToLookup(x => x.PlexArtistId);
        var remoteTracksByAlbum = remoteTracks.ToLookup(x => x.PlexAlbumId);
        var ownedTracksByAlbum = ownedTracks.ToLookup(x => x.PlexAlbumId);
        foreach (var (remoteArtist, ownedArtist) in artistPairs)
        {
            ct.ThrowIfCancellationRequested();
            var remoteEditions = remoteAlbumsByArtist[remoteArtist.Id];
            var ownedEditions = ownedAlbumsByArtist[ownedArtist.Id];
            var releases = ownedEditions.Where(x => HasId(x.MusicBrainzReleaseId))
                .ToLookup(x => x.MusicBrainzReleaseId!, StringComparer.OrdinalIgnoreCase);
            var editionTitles = ownedEditions.ToLookup(x => x.SearchTitle, StringComparer.OrdinalIgnoreCase);
            var remoteEditionTitles = remoteEditions.ToLookup(x => x.SearchTitle, StringComparer.OrdinalIgnoreCase);
            foreach (var remote in remoteEditions)
            {
                var tracks = remoteTracksByAlbum[remote.Id];
                var exact = HasId(remote.MusicBrainzReleaseId) ? releases[remote.MusicBrainzReleaseId!]
                    .Where(x => CompatibleEdition(remote, x, tracks, ownedTracksByAlbum[x.Id])).ToList() : [];
                var matches = exact.Count > 0 ? exact : editionTitles[remote.SearchTitle]
                    .Where(x => AlbumFallback(remote, x, tracks, ownedTracksByAlbum[x.Id])).ToList();
                if (exact.Count == 0 && (matches.Count != 1 || remoteEditionTitles[remote.SearchTitle].Count(x => AlbumFallback(x, matches[0],
                    remoteTracksByAlbum[x.Id], ownedTracksByAlbum[matches[0].Id])) != 1))
                    continue;
                foreach (var owned in matches)
                {
                    ctx.PlexMusicAlbumComparisons.Add(new PlexMusicAlbumComparison
                    {
                        RemotePlexLibraryId = command.RemotePlexLibraryId, OwnedPlexLibraryId = command.OwnedPlexLibraryId,
                        RemotePlexMediaId = remote.Id, OwnedPlexMediaId = owned.Id, ComparedAt = comparedAt,
                        MatchType = exact.Count > 0 ? PlexMediaComparisonMatchType.MusicBrainzReleaseId : PlexMediaComparisonMatchType.NormalizedTitleAndYear,
                    });
                    WriteTracks(ctx, command, remote, owned, tracks, ownedTracksByAlbum[owned.Id], comparedAt);
                }
            }
        }
    }

    private static bool AlbumFallback(PlexMusicAlbum remote, PlexMusicAlbum owned,
        IEnumerable<PlexMusicTrack> remoteTracks, IEnumerable<PlexMusicTrack> ownedTracks) =>
        !string.IsNullOrWhiteSpace(remote.SearchTitle) && Equal(remote.SearchTitle, owned.SearchTitle)
        && remote.Year == owned.Year && !Conflict(remote.MusicBrainzReleaseId, owned.MusicBrainzReleaseId)
        && CompatibleEdition(remote, owned, remoteTracks, ownedTracks);

    private static bool CompatibleEdition(PlexMusicAlbum remote, PlexMusicAlbum owned,
        IEnumerable<PlexMusicTrack> remoteTracks, IEnumerable<PlexMusicTrack> ownedTracks)
    {
        if (Conflict(remote.MusicBrainzReleaseGroupId, owned.MusicBrainzReleaseGroupId)
            || Conflict(remote.RecordLabel, owned.RecordLabel) || Conflict(remote.Country, owned.Country)
            || (remote.ReleaseDate.HasValue && owned.ReleaseDate.HasValue && remote.ReleaseDate.Value.Date != owned.ReleaseDate.Value.Date)
            || (remote.DiscCount > 0 && owned.DiscCount > 0 && remote.DiscCount != owned.DiscCount))
            return false;
        if ((remote.DiscCount > 0 && ownedTracks.Any(x => x.DiscNumber > remote.DiscCount))
            || (owned.DiscCount > 0 && remoteTracks.Any(x => x.DiscNumber > owned.DiscCount)))
            return false;
        var singleDisc = SingleDisc(remote, owned, remoteTracks, ownedTracks);
        var remotePositions = remoteTracks.Where(x => Position(x, singleDisc) != null).ToLookup(x => Position(x, singleDisc)!.Value);
        var ownedPositions = ownedTracks.Where(x => Position(x, singleDisc) != null).ToLookup(x => Position(x, singleDisc)!.Value);
        foreach (var group in remotePositions.Concat(ownedPositions))
            if (group.Skip(1).Any(x => !SameTrackAtPosition(group.First(), x)))
                return false;
        foreach (var group in remotePositions)
            foreach (var remoteTrack in group)
                foreach (var ownedTrack in ownedPositions[group.Key])
                    if (!SameTrackAtPosition(remoteTrack, ownedTrack))
                        return false;
        // TrackCount measures present files: non-conflicting subsets are the same edition.
        return true;
    }

    private static bool SameTrackAtPosition(PlexMusicTrack remote, PlexMusicTrack owned) =>
        !Conflict(remote.MusicBrainzReleaseTrackId, owned.MusicBrainzReleaseTrackId)
        && !Conflict(remote.MusicBrainzRecordingId, owned.MusicBrainzRecordingId)
        && (Equal(remote.MusicBrainzReleaseTrackId, owned.MusicBrainzReleaseTrackId)
            || Equal(remote.MusicBrainzRecordingId, owned.MusicBrainzRecordingId)
            || Equal(remote.SearchTitle, owned.SearchTitle));

    private static void WriteTracks(IReaparrDbContext ctx, CompareMusicPlexLibraryCommand command,
        PlexMusicAlbum remoteAlbum, PlexMusicAlbum ownedAlbum, IEnumerable<PlexMusicTrack> remoteTracks,
        IEnumerable<PlexMusicTrack> ownedTracks, DateTime comparedAt)
    {
        var singleDisc = SingleDisc(remoteAlbum, ownedAlbum, remoteTracks, ownedTracks);
        var releases = ownedTracks.Where(x => HasId(x.MusicBrainzReleaseTrackId))
            .ToLookup(x => x.MusicBrainzReleaseTrackId!, StringComparer.OrdinalIgnoreCase);
        var recordings = ownedTracks.Where(x => HasId(x.MusicBrainzRecordingId))
            .ToLookup(x => x.MusicBrainzRecordingId!, StringComparer.OrdinalIgnoreCase);
        var positions = ownedTracks.Where(x => Position(x, singleDisc) != null).ToLookup(x => Position(x, singleDisc)!.Value);
        foreach (var remote in remoteTracks)
        {
            var matchType = PlexMediaComparisonMatchType.MusicBrainzReleaseTrackId;
            var matches = HasId(remote.MusicBrainzReleaseTrackId) ? releases[remote.MusicBrainzReleaseTrackId!]
                .Where(x => CompatibleTrack(remote, x, singleDisc)).ToList() : [];
            if (matches.Count == 0)
            {
                matchType = PlexMediaComparisonMatchType.MusicBrainzRecordingId;
                matches = HasId(remote.MusicBrainzRecordingId) ? recordings[remote.MusicBrainzRecordingId!]
                    .Where(x => CompatibleTrack(remote, x, singleDisc)).ToList() : [];
                if (matches.Count > 1 && matches.Select(x => Position(x, singleDisc)).Distinct().Count() > 1)
                    continue;
            }
            if (matches.Count == 0)
            {
                matchType = PlexMediaComparisonMatchType.ParentAndChildNumbers;
                var position = Position(remote, singleDisc);
                if (position is null)
                    continue;
                matches = positions[position.Value].Where(x => CompatibleTrack(remote, x, singleDisc)
                    && Equal(remote.SearchTitle, x.SearchTitle)).ToList();
                if (matches.Count != 1 || remoteTracks.Count(x => Position(x, singleDisc) == position) != 1)
                    continue;
            }
            foreach (var owned in matches)
                ctx.PlexMusicTrackComparisons.Add(new PlexMusicTrackComparison
                {
                    RemotePlexLibraryId = command.RemotePlexLibraryId, OwnedPlexLibraryId = command.OwnedPlexLibraryId,
                    RemotePlexMediaId = remote.Id, OwnedPlexMediaId = owned.Id, MatchType = matchType, ComparedAt = comparedAt,
                });
        }
    }

    private static bool CompatibleTrack(PlexMusicTrack remote, PlexMusicTrack owned, bool singleDisc) =>
        !Conflict(remote.MusicBrainzReleaseTrackId, owned.MusicBrainzReleaseTrackId)
        && !Conflict(remote.MusicBrainzRecordingId, owned.MusicBrainzRecordingId)
        && !(remote.DiscNumber > 0 && owned.DiscNumber > 0 && remote.DiscNumber != owned.DiscNumber)
        && !(remote.TrackNumber > 0 && owned.TrackNumber > 0 && remote.TrackNumber != owned.TrackNumber)
        && (singleDisc || (remote.DiscNumber > 0 && owned.DiscNumber > 0));

    private static bool SingleDisc(PlexMusicAlbum remote, PlexMusicAlbum owned,
        IEnumerable<PlexMusicTrack> remoteTracks, IEnumerable<PlexMusicTrack> ownedTracks) =>
        remote.DiscCount == 1 && owned.DiscCount == 1
        && remoteTracks.All(x => x.DiscNumber is null or 1)
        && ownedTracks.All(x => x.DiscNumber is null or 1);

    private static (int Disc, int Track)? Position(PlexMusicTrack track, bool singleDisc) =>
        track.TrackNumber > 0 && (track.DiscNumber > 0 || singleDisc)
            ? (track.DiscNumber > 0 ? track.DiscNumber.Value : 1, track.TrackNumber.Value) : null;

    private static bool HasId(string? value) => !string.IsNullOrWhiteSpace(value);
    private static bool Equal(string? left, string? right) => HasId(left) && HasId(right)
        && string.Equals(left!.Trim(), right!.Trim(), StringComparison.OrdinalIgnoreCase);
    private static bool Conflict(string? left, string? right) => HasId(left) && HasId(right) && !Equal(left, right);
}
