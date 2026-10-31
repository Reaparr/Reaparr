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
        var result = await _dbContext.ExecuteTransactionAsync(
            async (ctx, ct) =>
            {
                var eligible = ctx.PlexLibraries.Where(x =>
                    x.Type == PlexMediaType.MusicArtist
                    && x.IsEnabled
                    && !x.Outdated
                    && x.SyncedAt != null
                    && x.SyncedContentChangedAt == x.ContentChangedAt
                );
                var remoteEligible = await eligible
                    .WhereIsNotOwned()
                    .AnyAsync(x => x.Id == command.RemotePlexLibraryId, CancellationToken.None);
                var ownedEligible = await eligible
                    .WhereIsOwned()
                    .AnyAsync(x => x.Id == command.OwnedPlexLibraryId, CancellationToken.None);

                if (!remoteEligible || !ownedEligible)
                    throw new InvalidOperationException(
                        "Music comparison requires current enabled remote and owned Music libraries."
                    );

                var comparedAt = DateTime.UtcNow;
                await ctx
                    .PlexMusicTrackComparisons.Where(x =>
                        x.RemotePlexLibraryId == command.RemotePlexLibraryId
                        && x.OwnedPlexLibraryId == command.OwnedPlexLibraryId
                    )
                    .ExecuteDeleteAsync(ct);
                await ctx
                    .PlexMusicAlbumComparisons.Where(x =>
                        x.RemotePlexLibraryId == command.RemotePlexLibraryId
                        && x.OwnedPlexLibraryId == command.OwnedPlexLibraryId
                    )
                    .ExecuteDeleteAsync(ct);
                await ctx
                    .PlexMusicArtistComparisons.Where(x =>
                        x.RemotePlexLibraryId == command.RemotePlexLibraryId
                        && x.OwnedPlexLibraryId == command.OwnedPlexLibraryId
                    )
                    .ExecuteDeleteAsync(ct);

                var lastArtistId = 0;
                while (true)
                {
                    var remoteArtists = await ctx
                        .PlexMusicArtists.Where(x =>
                            x.PlexLibraryId == command.RemotePlexLibraryId && x.Id > lastArtistId
                        )
                        .OrderBy(x => x.Id)
                        .Take(BATCH_SIZE)
                        .ToListAsync(ct);
                    if (remoteArtists.Count == 0)
                        break;

                    await WriteBatchAsync(ctx, command, remoteArtists, comparedAt, ct);
                    await ctx.SaveChangesAsync(ct);
                    ctx.ClearChangeTracker();
                    lastArtistId = remoteArtists[^1].Id;
                }

                var scope = await ctx
                    .PlexComparisonScopes.AsTracking()
                    .SingleOrDefaultAsync(
                        x =>
                            x.RemotePlexLibraryId == command.RemotePlexLibraryId
                            && x.OwnedPlexLibraryId == command.OwnedPlexLibraryId
                            && x.MediaType == PlexMediaType.MusicArtist,
                        CancellationToken.None
                    );
                if (scope is null)
                    ctx.PlexComparisonScopes.Add(
                        new PlexComparisonState
                        {
                            RemotePlexLibraryId = command.RemotePlexLibraryId,
                            OwnedPlexLibraryId = command.OwnedPlexLibraryId,
                            MediaType = PlexMediaType.MusicArtist,
                            CompletedAt = comparedAt,
                        }
                    );
                else
                    scope.CompletedAt = comparedAt;
                await ctx.SaveChangesAsync(ct);
            },
            cancellationToken
        );

        if (result.IsFailed)
            return result.LogIfFailed();

        _log.Here()
            .Information(
                "Completed Music comparison: remote library {RemoteLibraryId} vs owned library {OwnedLibraryId}",
                command.RemotePlexLibraryId,
                command.OwnedPlexLibraryId
            );
        return result;
    }

    private static async Task WriteBatchAsync(
        IReaparrDbContext ctx,
        CompareMusicPlexLibraryCommand command,
        List<PlexMusicArtist> remoteArtists,
        DateTime comparedAt,
        CancellationToken ct
    )
    {
        var nativeArtistIds = remoteArtists
            .Where(x => x.PlexApiIdentityId is not null)
            .Select(x => x.PlexApiIdentityId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var artistIds = remoteArtists
            .Where(x => x.MusicBrainzArtistId is not null)
            .Select(x => x.MusicBrainzArtistId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var titles = remoteArtists
            .Where(x => !string.IsNullOrWhiteSpace(x.SearchTitle))
            .Select(x => x.SearchTitle)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var ownedArtists = await ctx
            .PlexMusicArtists.Where(x =>
                x.PlexLibraryId == command.OwnedPlexLibraryId
                && (
                    (x.PlexApiIdentityId != null && nativeArtistIds.Contains(x.PlexApiIdentityId))
                    || (x.MusicBrainzArtistId != null && artistIds.Contains(x.MusicBrainzArtistId))
                    || titles.Contains(x.SearchTitle)
                )
            )
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        var byNativeId = ownedArtists
            .Where(x => x.PlexApiIdentityId is not null)
            .ToLookup(x => x.PlexApiIdentityId!, StringComparer.Ordinal);
        var byId = ownedArtists
            .Where(x => x.MusicBrainzArtistId is not null)
            .ToLookup(x => x.MusicBrainzArtistId!, StringComparer.Ordinal);
        var byTitle = ownedArtists.ToLookup(x => x.SearchTitle, StringComparer.OrdinalIgnoreCase);
        var remoteTitleCounts = await ctx
            .PlexMusicArtists.Where(x =>
                x.PlexLibraryId == command.RemotePlexLibraryId && titles.Contains(x.SearchTitle)
            )
            .GroupBy(x => x.SearchTitle)
            .Select(g => new { Title = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Title, x => x.Count, StringComparer.OrdinalIgnoreCase, ct);
        var artistPairs = new List<(PlexMusicArtist Remote, PlexMusicArtist Owned)>();
        foreach (var remote in remoteArtists)
        {
            var native = remote.PlexApiIdentityId is { } nativeId ? byNativeId[nativeId] : [];
            var exact = remote.MusicBrainzArtistId is { } artistId ? byId[artistId] : [];
            IEnumerable<PlexMusicArtist> matches;
            PlexMediaComparisonMatchType matchType;
            if (native.Any())
            {
                matches = native;
                matchType = PlexMediaComparisonMatchType.PlexArtistGuid;
            }
            else if (exact.Any())
            {
                matches = exact;
                matchType = PlexMediaComparisonMatchType.MusicBrainzArtistId;
            }
            else
            {
                if (!remoteTitleCounts.TryGetValue(remote.SearchTitle, out var count) || count != 1)
                    continue;

                var fallback = byTitle[remote.SearchTitle]
                    .Where(x => !HaveConflictingMusicIds(remote.MusicBrainzArtistId, x.MusicBrainzArtistId))
                    .ToArray();
                if (fallback.Length != 1)
                    continue;

                matches = fallback;
                matchType = PlexMediaComparisonMatchType.NormalizedTitleAndYear;
            }

            foreach (var owned in matches)
            {
                ctx.PlexMusicArtistComparisons.Add(
                    new PlexMusicArtistComparison
                    {
                        RemotePlexLibraryId = command.RemotePlexLibraryId,
                        OwnedPlexLibraryId = command.OwnedPlexLibraryId,
                        RemotePlexMediaId = remote.Id,
                        OwnedPlexMediaId = owned.Id,
                        ComparedAt = comparedAt,
                        MatchType = matchType,
                    }
                );
                artistPairs.Add((remote, owned));
            }
        }

        if (artistPairs.Count == 0)
            return;

        var remoteArtistIds = artistPairs.Select(x => x.Remote.Id).Distinct().ToArray();
        var ownedArtistIds = artistPairs.Select(x => x.Owned.Id).Distinct().ToArray();
        var remoteAlbums = await ctx
            .PlexMusicAlbums.Where(x =>
                x.PlexLibraryId == command.RemotePlexLibraryId && remoteArtistIds.Contains(x.PlexArtistId)
            )
            .ToListAsync(ct);
        var ownedAlbums = await ctx
            .PlexMusicAlbums.Where(x =>
                x.PlexLibraryId == command.OwnedPlexLibraryId && ownedArtistIds.Contains(x.PlexArtistId)
            )
            .ToListAsync(ct);
        var remoteAlbumIds = remoteAlbums.Select(x => x.Id).ToArray();
        var ownedAlbumIds = ownedAlbums.Select(x => x.Id).ToArray();
        var remoteTracks = await ctx
            .PlexMusicTracks.Include(x => x.MediaDataList)
            .Where(x => x.PlexLibraryId == command.RemotePlexLibraryId && remoteAlbumIds.Contains(x.PlexAlbumId))
            .ToListAsync(ct);
        var ownedTracks = await ctx
            .PlexMusicTracks.Include(x => x.MediaDataList)
            .Where(x => x.PlexLibraryId == command.OwnedPlexLibraryId && ownedAlbumIds.Contains(x.PlexAlbumId))
            .ToListAsync(ct);
        var remoteAlbumsByArtist = remoteAlbums.ToLookup(x => x.PlexArtistId);
        var ownedAlbumsByArtist = ownedAlbums.ToLookup(x => x.PlexArtistId);
        var tracksByAlbum = remoteTracks
            .Concat(ownedTracks)
            .GroupBy(x => x.PlexAlbumId)
            .ToDictionary(x => x.Key, x => new AlbumTracks(x.ToArray()));
        var compatiblePairs = new Dictionary<(int Remote, int Owned), bool>();
        var audioSelections = new Dictionary<int, (PlexMusicTrackMediaData? File, AudioQuality audioQuality)>();

        AlbumTracks Tracks(PlexMusicAlbum album) =>
            tracksByAlbum.TryGetValue(album.Id, out var tracks) ? tracks : AlbumTracks.Empty;

        bool Compatible(PlexMusicAlbum remote, PlexMusicAlbum owned)
        {
            var key = (remote.Id, owned.Id);
            if (!compatiblePairs.TryGetValue(key, out var compatible))
            {
                compatible = CompatibleEdition(remote, owned, Tracks(remote), Tracks(owned));
                compatiblePairs.Add(key, compatible);
            }

            return compatible;
        }

        bool Fallback(PlexMusicAlbum remote, PlexMusicAlbum owned) =>
            remote.Year == owned.Year
            && !HaveConflictingMusicIds(remote.MusicBrainzReleaseId, owned.MusicBrainzReleaseId)
            && Compatible(remote, owned);

        foreach (var (remoteArtist, ownedArtist) in artistPairs)
        {
            ct.ThrowIfCancellationRequested();
            var remoteEditions = remoteAlbumsByArtist[remoteArtist.Id];
            var ownedEditions = ownedAlbumsByArtist[ownedArtist.Id];
            var nativeAlbums = ownedEditions
                .Where(x => x.PlexApiIdentityId is not null)
                .ToLookup(x => x.PlexApiIdentityId!, StringComparer.Ordinal);
            var releases = ownedEditions
                .Where(x => x.MusicBrainzReleaseId is not null)
                .ToLookup(x => x.MusicBrainzReleaseId!, StringComparer.Ordinal);
            var editionTitles = ownedEditions.ToLookup(x => x.SearchTitle, StringComparer.OrdinalIgnoreCase);
            var remoteEditionTitles = remoteEditions.ToLookup(x => x.SearchTitle, StringComparer.OrdinalIgnoreCase);
            foreach (var remote in remoteEditions)
            {
                ct.ThrowIfCancellationRequested();
                IEnumerable<PlexMusicAlbum> native = remote.PlexApiIdentityId is { } nativeId
                    ? nativeAlbums[nativeId]
                    : [];
                IEnumerable<PlexMusicAlbum> matches;
                PlexMediaComparisonMatchType matchType;
                if (native.Any())
                {
                    matches = native;
                    matchType = PlexMediaComparisonMatchType.PlexAlbumGuid;
                }
                else
                {
                    var exact = remote.MusicBrainzReleaseId is { } releaseId
                        ? releases[releaseId].Where(x => Compatible(remote, x)).ToArray()
                        : [];
                    if (exact.Length > 0)
                    {
                        matches = exact;
                        matchType = PlexMediaComparisonMatchType.MusicBrainzReleaseId;
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(remote.SearchTitle))
                            continue;

                        var fallback = editionTitles[remote.SearchTitle].Where(x => Fallback(remote, x)).ToArray();
                        if (
                            fallback.Length != 1
                            || remoteEditionTitles[remote.SearchTitle].Count(x => Fallback(x, fallback[0])) != 1
                        )
                            continue;

                        matches = fallback;
                        matchType = PlexMediaComparisonMatchType.NormalizedTitleAndYear;
                    }
                }

                foreach (var owned in matches)
                {
                    ctx.PlexMusicAlbumComparisons.Add(
                        new PlexMusicAlbumComparison
                        {
                            RemotePlexLibraryId = command.RemotePlexLibraryId,
                            OwnedPlexLibraryId = command.OwnedPlexLibraryId,
                            RemotePlexMediaId = remote.Id,
                            OwnedPlexMediaId = owned.Id,
                            ComparedAt = comparedAt,
                            MatchType = matchType,
                        }
                    );
                    WriteTracks(
                        ctx,
                        command,
                        remote,
                        owned,
                        Tracks(remote),
                        Tracks(owned),
                        audioSelections,
                        comparedAt,
                        ct
                    );
                }
            }
        }
    }

    private static bool CompatibleEdition(
        PlexMusicAlbum remote,
        PlexMusicAlbum owned,
        AlbumTracks remoteTracks,
        AlbumTracks ownedTracks
    )
    {
        if (
            HaveConflictingMetadata(remote.RecordLabel, owned.RecordLabel)
            || (
                remote.OriginallyAvailableAt.HasValue
                && owned.OriginallyAvailableAt.HasValue
                && remote.OriginallyAvailableAt.Value.Date != owned.OriginallyAvailableAt.Value.Date
            )
        )
            return false;

        if (remoteTracks.HasConflictingDiscLayout(ownedTracks, remote.ChildCount, owned.ChildCount))
            return false;

        var singleDisc = SingleDisc(remoteTracks, ownedTracks);
        var remotePositions = remoteTracks.Positions(singleDisc);
        var ownedPositions = ownedTracks.Positions(singleDisc);
        foreach (var group in remotePositions.Concat(ownedPositions))
            if (group.Skip(1).Any(x => !SameTrackAtPosition(group.First(), x)))
                return false;

        foreach (var group in remotePositions)
        foreach (var remoteTrack in group)
        foreach (var ownedTrack in ownedPositions[group.Key])
            if (!SameTrackAtPosition(remoteTrack, ownedTrack))
                return false;

        return true;
    }

    private static bool SameTrackAtPosition(PlexMusicTrack remote, PlexMusicTrack owned) =>
        !HaveConflictingMusicIds(remote.MusicBrainzReleaseTrackId, owned.MusicBrainzReleaseTrackId)
        && (
            (
                remote.MusicBrainzReleaseTrackId is not null
                && remote.MusicBrainzReleaseTrackId == owned.MusicBrainzReleaseTrackId
            ) || TitlesEqual(remote.SearchTitle, owned.SearchTitle)
        );

    private static void WriteTracks(
        IReaparrDbContext ctx,
        CompareMusicPlexLibraryCommand command,
        PlexMusicAlbum remoteAlbum,
        PlexMusicAlbum ownedAlbum,
        AlbumTracks remoteTracks,
        AlbumTracks ownedTracks,
        Dictionary<int, (PlexMusicTrackMediaData? File, AudioQuality audioQuality)> audioSelections,
        DateTime comparedAt,
        CancellationToken ct
    )
    {
        var singleDisc = SingleDisc(remoteTracks, ownedTracks);
        foreach (var remote in remoteTracks.Tracks)
        {
            var native = remote.PlexApiIdentityId is { } nativeId ? ownedTracks.NativeIds[nativeId] : [];
            IEnumerable<PlexMusicTrack> matches;
            PlexMediaComparisonMatchType matchType;
            if (native.Any())
            {
                matches = native;
                matchType = PlexMediaComparisonMatchType.PlexTrackGuid;
            }
            else
            {
                var exact = remote.MusicBrainzReleaseTrackId is { } releaseId
                    ? ownedTracks
                        .ReleaseIds[releaseId]
                        .Where(x => CompatibleTrackNumbers(remote, x, singleDisc))
                        .ToArray()
                    : [];
                if (exact.Length > 0)
                {
                    matches = exact;
                    matchType = PlexMediaComparisonMatchType.MusicBrainzReleaseTrackId;
                }
                else
                {
                    var position = Position(remote, singleDisc);
                    if (position is null || remoteTracks.Positions(singleDisc)[position.Value].Count() != 1)
                        continue;

                    var fallback = ownedTracks
                        .Positions(singleDisc)[position.Value]
                        .Where(x =>
                            !HaveConflictingMusicIds(remote.MusicBrainzReleaseTrackId, x.MusicBrainzReleaseTrackId)
                            && TitlesEqual(remote.SearchTitle, x.SearchTitle)
                        )
                        .ToArray();
                    if (fallback.Length != 1)
                        continue;

                    matches = fallback;
                    matchType = PlexMediaComparisonMatchType.ParentAndChildNumbers;
                }
            }

            var remoteSelection = SelectAudio(remote, audioSelections);
            foreach (var owned in matches)
            {
                var ownedSelection = SelectAudio(owned, audioSelections);
                ctx.PlexMusicTrackComparisons.Add(
                    new PlexMusicTrackComparison
                    {
                        RemotePlexLibraryId = command.RemotePlexLibraryId,
                        OwnedPlexLibraryId = command.OwnedPlexLibraryId,
                        RemotePlexMediaId = remote.Id,
                        OwnedPlexMediaId = owned.Id,
                        HitState =
                            remoteSelection.File is not null
                            && ownedSelection.File is not null
                            && remoteSelection.File.IsHigherAudioQualityThan(ownedSelection.File)
                                ? PlexMediaComparisonHitState.HigherQuality
                                : PlexMediaComparisonHitState.Matched,
                        RemoteAudioQuality = remoteSelection.audioQuality,
                        OwnedAudioQuality = ownedSelection.audioQuality,
                        RemotePlexMediaDataId = remoteSelection.File?.Id,
                        OwnedPlexMediaDataId = ownedSelection.File?.Id,
                        MatchType = matchType,
                        ComparedAt = comparedAt,
                    }
                );
            }
        }
    }

    private static (PlexMusicTrackMediaData? File, AudioQuality audioQuality) SelectAudio(
        PlexMusicTrack track,
        Dictionary<int, (PlexMusicTrackMediaData? File, AudioQuality audioQuality)> selections
    )
    {
        if (!selections.TryGetValue(track.Id, out var selection))
        {
            var file = track.MediaDataList.GetBestAudioFile();
            selection = (file, file?.ClassifyAudioQuality() ?? AudioQuality.Unknown);
            selections.Add(track.Id, selection);
        }

        return selection;
    }

    private static bool CompatibleTrackNumbers(PlexMusicTrack remote, PlexMusicTrack owned, bool singleDisc) =>
        !(remote.DiscNumber > 0 && owned.DiscNumber > 0 && remote.DiscNumber != owned.DiscNumber)
        && !(remote.TrackNumber > 0 && owned.TrackNumber > 0 && remote.TrackNumber != owned.TrackNumber)
        && (singleDisc || (remote.DiscNumber > 0 && owned.DiscNumber > 0));

    private static bool SingleDisc(AlbumTracks remoteTracks, AlbumTracks ownedTracks) =>
        remoteTracks.HasOnlySingleDiscTracks && ownedTracks.HasOnlySingleDiscTracks;

    private static (int Disc, int Track)? Position(PlexMusicTrack track, bool singleDisc) =>
        track.TrackNumber > 0 && (track.DiscNumber > 0 || singleDisc)
            ? (track.DiscNumber > 0 ? track.DiscNumber.Value : 1, track.TrackNumber.Value)
            : null;

    private static bool HaveConflictingMusicIds(string? left, string? right) =>
        left is not null && right is not null && left != right;

    private static bool TitlesEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool HaveConflictingMetadata(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        return !string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class AlbumTracks(PlexMusicTrack[] tracks)
    {
        public static readonly AlbumTracks Empty = new([]);
        private ILookup<(int Disc, int Track), PlexMusicTrack>? _positions;
        private ILookup<(int Disc, int Track), PlexMusicTrack>? _singleDiscPositions;

        public PlexMusicTrack[] Tracks { get; } = tracks;
        private bool HasKnownDiscNumbers { get; } = tracks.Length > 0 && tracks.All(x => x.DiscNumber > 0);
        public bool HasOnlySingleDiscTracks { get; } = tracks.All(x => x.DiscNumber is null or 1);

        private bool HasCompleteDiscLayout(int childCount) => HasKnownDiscNumbers && Tracks.Length == childCount;

        // Advertised counts prevent partial snapshots from being mistaken for different editions.
        public bool HasConflictingDiscLayout(AlbumTracks other, int childCount, int otherChildCount) =>
            HasCompleteDiscLayout(childCount)
            && other.HasCompleteDiscLayout(otherChildCount)
            && HasOnlySingleDiscTracks != other.HasOnlySingleDiscTracks;

        public ILookup<string, PlexMusicTrack> NativeIds =>
            field ??= Tracks
                .Where(x => x.PlexApiIdentityId is not null)
                .ToLookup(x => x.PlexApiIdentityId!, StringComparer.Ordinal);

        public ILookup<string, PlexMusicTrack> ReleaseIds =>
            field ??= Tracks
                .Where(x => x.MusicBrainzReleaseTrackId is not null)
                .ToLookup(x => x.MusicBrainzReleaseTrackId!, StringComparer.Ordinal);

        public ILookup<(int Disc, int Track), PlexMusicTrack> Positions(bool singleDisc)
        {
            ref var positions = ref (singleDisc ? ref _singleDiscPositions : ref _positions);
            return positions ??= Tracks
                .Select(x => (Track: x, Position: Position(x, singleDisc)))
                .Where(x => x.Position is not null)
                .ToLookup(x => x.Position!.Value, x => x.Track);
        }
    }
}
