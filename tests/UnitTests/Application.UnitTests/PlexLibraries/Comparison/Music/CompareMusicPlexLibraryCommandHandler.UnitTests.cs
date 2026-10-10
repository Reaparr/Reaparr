using Reaparr.Data;

namespace Reaparr.Application.UnitTests;

public class CompareMusicPlexLibraryCommandHandlerUnitTests : BaseCommandUnitTest<CompareMusicPlexLibraryCommand>
{
    [Test]
    public async Task ShouldPersistTypedHitsAndScope_WhenStableIdsMatch()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync();
        var dbContext = IDbContext;
        var artists = await dbContext.PlexArtists.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        var albums = await dbContext.PlexAlbums.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        var tracks = await dbContext.PlexTracks.OrderBy(x => x.PlexLibraryId).ThenBy(x => x.TrackNumber).ToListAsync(CancellationToken);
        (await dbContext.PlexMusicArtistComparisons.CountAsync(CancellationToken)).ShouldBe(0);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var artistHit = await dbContext.PlexMusicArtistComparisons.SingleAsync(CancellationToken);
        (artistHit.RemotePlexLibraryId, artistHit.OwnedPlexLibraryId, artistHit.RemotePlexMediaId, artistHit.OwnedPlexMediaId, artistHit.MatchType)
            .ShouldBe((remote.Id, owned.Id, artists[0].Id, artists[1].Id, PlexMediaComparisonMatchType.MusicBrainzArtistId));
        var albumHit = await dbContext.PlexMusicAlbumComparisons.SingleAsync(CancellationToken);
        (albumHit.RemotePlexMediaId, albumHit.OwnedPlexMediaId, albumHit.MatchType)
            .ShouldBe((albums[0].Id, albums[1].Id, PlexMediaComparisonMatchType.MusicBrainzReleaseId));
        var hits = await dbContext.PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId).ToListAsync(CancellationToken);
        hits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType)).ShouldBe([
            (tracks[0].Id, tracks[2].Id, PlexMediaComparisonMatchType.MusicBrainzReleaseTrackId),
            (tracks[1].Id, tracks[3].Id, PlexMediaComparisonMatchType.MusicBrainzReleaseTrackId),
        ]);
        var scope = await dbContext.PlexComparisonScopes.SingleAsync(CancellationToken);
        (scope.RemotePlexLibraryId, scope.OwnedPlexLibraryId, scope.MediaType, scope.CompletedAt)
            .ShouldBe((remote.Id, owned.Id, PlexMediaType.MusicArtist, artistHit.ComparedAt));
        albumHit.ComparedAt.ShouldBe(scope.CompletedAt);
        hits.ShouldAllBe(x => x.ComparedAt == scope.CompletedAt);
    }

    [Test]
    [Arguments("artist")]
    [Arguments("release")]
    [Arguments("release-group")]
    [Arguments("label")]
    [Arguments("country")]
    [Arguments("date")]
    [Arguments("disc-count")]
    [Arguments("disc-layout")]
    [Arguments("live")]
    [Arguments("remaster")]
    [Arguments("deluxe")]
    public async Task ShouldRejectConflictingIdentityOrEdition_WhenFallbackMetadataAgreesOtherwise(string conflict)
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(fallback: true);
        var dbContext = IDbContext;
        if (conflict == "artist")
        {
            var artists = await dbContext.PlexArtists.AsTracking().OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
            artists[0].MusicBrainzArtistId = "artist-a";
            artists[1].MusicBrainzArtistId = "artist-b";
        }
        else
        {
            var albums = await dbContext.PlexAlbums.AsTracking().OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
            switch (conflict)
            {
                case "release": albums[0].MusicBrainzReleaseId = "a"; albums[1].MusicBrainzReleaseId = "b"; break;
                case "release-group": albums[0].MusicBrainzReleaseGroupId = "a"; albums[1].MusicBrainzReleaseGroupId = "b"; break;
                case "label": albums[1].RecordLabel = "different label"; break;
                case "country": albums[1].Country = "GB"; break;
                case "date": albums[1].ReleaseDate = new DateTime(2000, 2, 1); break;
                case "disc-count": albums[1].DiscCount = 2; break;
                case "disc-layout":
                    albums[1].DiscCount = null;
                    await dbContext.PlexTracks.Where(x => x.PlexLibraryId == owned.Id)
                        .ExecuteUpdateAsync(x => x.SetProperty(y => y.DiscNumber, 2), CancellationToken);
                    break;
                default: albums[1].UpdateInitProperty(nameof(PlexMusicAlbum.SearchTitle), $"album {conflict}"); break;
            }
        }
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var artistHits = await dbContext.PlexMusicArtistComparisons.ToListAsync(CancellationToken);
        artistHits.Select(x => (x.RemotePlexLibraryId, x.OwnedPlexLibraryId)).ShouldBe(conflict == "artist" ? [] : [(remote.Id, owned.Id)]);
        (await dbContext.PlexMusicAlbumComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexComparisonScopes.SingleAsync(CancellationToken)).MediaType.ShouldBe(PlexMediaType.MusicArtist);
    }

    [Test]
    public async Task ShouldMatchNonConflictingPartialSubset_WhenStableIdsAreMissing()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(fallback: true);
        var dbContext = IDbContext;
        var albums = await dbContext.PlexAlbums.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        var remoteTracks = await dbContext.PlexTracks.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.TrackNumber).ToListAsync(CancellationToken);
        var ownedTracks = await dbContext.PlexTracks.Where(x => x.PlexLibraryId == owned.Id).OrderBy(x => x.TrackNumber).ToListAsync(CancellationToken);
        await dbContext.PlexTracks.Where(x => x.Id == ownedTracks[1].Id).ExecuteDeleteAsync(CancellationToken);
        await dbContext.PlexAlbums.Where(x => x.Id == albums[1].Id).ExecuteUpdateAsync(x => x.SetProperty(y => y.TrackCount, 1), CancellationToken);
        await dbContext.PlexTracks.Where(x => x.Id == ownedTracks[0].Id).ExecuteUpdateAsync(x => x.SetProperty(y => y.DiscNumber, (int?)null), CancellationToken);
        (await dbContext.PlexTracks.CountAsync(x => x.PlexLibraryId == owned.Id, CancellationToken)).ShouldBe(1);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await dbContext.PlexMusicArtistComparisons.SingleAsync(CancellationToken)).MatchType.ShouldBe(PlexMediaComparisonMatchType.NormalizedTitleAndYear);
        var albumHit = await dbContext.PlexMusicAlbumComparisons.SingleAsync(CancellationToken);
        (albumHit.RemotePlexMediaId, albumHit.OwnedPlexMediaId, albumHit.MatchType)
            .ShouldBe((albums[0].Id, albums[1].Id, PlexMediaComparisonMatchType.NormalizedTitleAndYear));
        var trackHit = await dbContext.PlexMusicTrackComparisons.SingleAsync(CancellationToken);
        (trackHit.RemotePlexMediaId, trackHit.OwnedPlexMediaId, trackHit.MatchType)
            .ShouldBe((remoteTracks[0].Id, ownedTracks[0].Id, PlexMediaComparisonMatchType.ParentAndChildNumbers));
    }

    [Test]
    [Arguments("release-track")]
    [Arguments("recording")]
    [Arguments("title")]
    [Arguments("duplicate-position")]
    public async Task ShouldRejectConflictingLayout_WhenTracksOccupyTheSamePosition(string conflict)
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(fallback: true);
        var dbContext = IDbContext;
        var tracks = await dbContext.PlexTracks.AsTracking().OrderBy(x => x.PlexLibraryId).ThenBy(x => x.TrackNumber).ToListAsync(CancellationToken);
        switch (conflict)
        {
            case "release-track": tracks[0].MusicBrainzReleaseTrackId = "a"; tracks[2].MusicBrainzReleaseTrackId = "b"; break;
            case "recording": tracks[0].MusicBrainzRecordingId = "a"; tracks[2].MusicBrainzRecordingId = "b"; break;
            case "title": tracks[2].UpdateInitProperty(nameof(PlexMusicTrack.SearchTitle), "different recording"); break;
            default: tracks[3].TrackNumber = 1; break;
        }
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await dbContext.PlexMusicArtistComparisons.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.PlexMusicAlbumComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldMatchRecordingOnlyInsideEdition_WhenReleaseTrackIdIsMissing()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync();
        var dbContext = IDbContext;
        await dbContext.PlexTracks.ExecuteUpdateAsync(x => x.SetProperty(y => y.MusicBrainzReleaseTrackId, (string?)null), CancellationToken);
        var remoteTracks = await dbContext.PlexTracks.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.TrackNumber).ToListAsync(CancellationToken);
        var ownedTracks = await dbContext.PlexTracks.Where(x => x.PlexLibraryId == owned.Id).OrderBy(x => x.TrackNumber).ToListAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var hits = await dbContext.PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId).ToListAsync(CancellationToken);
        hits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType)).ShouldBe([
            (remoteTracks[0].Id, ownedTracks[0].Id, PlexMediaComparisonMatchType.MusicBrainzRecordingId),
            (remoteTracks[1].Id, ownedTracks[1].Id, PlexMediaComparisonMatchType.MusicBrainzRecordingId),
        ]);
    }

    [Test]
    public async Task ShouldLeaveTracksUnmatched_WhenMissingDiscNumberIsUnsafe()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(fallback: true);
        var dbContext = IDbContext;
        await dbContext.PlexAlbums.ExecuteUpdateAsync(x => x.SetProperty(y => y.DiscCount, 2), CancellationToken);
        await dbContext.PlexTracks.ExecuteUpdateAsync(x => x.SetProperty(y => y.DiscNumber, (int?)null), CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await dbContext.PlexMusicAlbumComparisons.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    [Arguments("artist")]
    [Arguments("album")]
    public async Task ShouldLeaveAmbiguousFallbackUnmatched_WhenMultipleCandidatesRemain(string level)
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(fallback: true, artists: 2);
        var dbContext = IDbContext;
        if (level == "artist")
            await dbContext.PlexArtists.ExecuteUpdateAsync(x => x.SetProperty(y => y.SearchTitle, "ambiguous artist"), CancellationToken);
        else
        {
            var ownedArtists = await dbContext.PlexArtists.Where(x => x.PlexLibraryId == owned.Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);
            await dbContext.PlexAlbums.Where(x => x.PlexLibraryId == owned.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.PlexArtistId, ownedArtists[0].Id).SetProperty(y => y.SearchTitle, "album 0"), CancellationToken);
        }

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var artistHits = await dbContext.PlexMusicArtistComparisons.OrderBy(x => x.RemotePlexMediaId).ToListAsync(CancellationToken);
        artistHits.Select(x => (x.RemotePlexLibraryId, x.OwnedPlexLibraryId)).ShouldBe(level == "artist" ? [] : [(remote.Id, owned.Id), (remote.Id, owned.Id)]);
        (await dbContext.PlexMusicAlbumComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldPersistEveryExactArtistDuplicate_WhenStableIdDisambiguates()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(artists: 2);
        var dbContext = IDbContext;
        await dbContext.PlexArtists.Where(x => x.PlexLibraryId == owned.Id).ExecuteUpdateAsync(x => x.SetProperty(y => y.MusicBrainzArtistId, "artist-0"), CancellationToken);
        var remoteArtist = await dbContext.PlexArtists.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var ownedArtists = await dbContext.PlexArtists.Where(x => x.PlexLibraryId == owned.Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var hits = await dbContext.PlexMusicArtistComparisons.OrderBy(x => x.OwnedPlexMediaId).ToListAsync(CancellationToken);
        hits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType)).ShouldBe([
            (remoteArtist.Id, ownedArtists[0].Id, PlexMediaComparisonMatchType.MusicBrainzArtistId),
            (remoteArtist.Id, ownedArtists[1].Id, PlexMediaComparisonMatchType.MusicBrainzArtistId),
        ]);
    }

    [Test]
    public async Task ShouldReplaceAllPairTablesAndScope_WhenRecomparisonSucceeds()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync();
        var dbContext = IDbContext;
        var first = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));
        first.IsSuccess.ShouldBeTrue();
        first.Errors.Count.ShouldBe(0);
        var originalScope = await dbContext.PlexComparisonScopes.SingleAsync(CancellationToken);
        (await dbContext.PlexMusicArtistComparisons.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.PlexMusicAlbumComparisons.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.PlexMusicTrackComparisons.CountAsync(CancellationToken)).ShouldBe(2);
        await dbContext.PlexArtists.Where(x => x.PlexLibraryId == owned.Id).ExecuteUpdateAsync(x =>
            x.SetProperty(y => y.MusicBrainzArtistId, "other-artist").SetProperty(y => y.SearchTitle, "other artist"), CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await dbContext.PlexMusicArtistComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicAlbumComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        var scope = await dbContext.PlexComparisonScopes.SingleAsync(CancellationToken);
        scope.Id.ShouldBe(originalScope.Id);
        scope.CompletedAt.ShouldBeGreaterThanOrEqualTo(originalScope.CompletedAt);
        (scope.RemotePlexLibraryId, scope.OwnedPlexLibraryId, scope.MediaType).ShouldBe((remote.Id, owned.Id, PlexMediaType.MusicArtist));
    }

    [Test]
    [Arguments("remote-content")]
    [Arguments("owned-content")]
    [Arguments("outdated")]
    [Arguments("cancel")]
    [Arguments("owned-role")]
    [Arguments("remote-role")]
    [Arguments("disabled")]
    [Arguments("unsynced")]
    [Arguments("wrong-type")]
    [Arguments("failure")]
    public async Task ShouldRollBackReplacementAndRetainOldScope_WhenWriterBecomesStaleOrCancelled(string change)
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync();
        var dbContext = IDbContext;
        var first = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));
        first.IsSuccess.ShouldBeTrue();
        first.Errors.Count.ShouldBe(0);
        var beforeArtists = await dbContext.PlexMusicArtistComparisons.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var beforeAlbums = await dbContext.PlexMusicAlbumComparisons.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var beforeTracks = await dbContext.PlexMusicTrackComparisons.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var beforeScope = await dbContext.PlexComparisonScopes.SingleAsync(CancellationToken);
        beforeArtists.Count.ShouldBe(1);
        beforeAlbums.Count.ShouldBe(1);
        beforeTracks.Count.ShouldBe(2);
        using var cancellation = new CancellationTokenSource();
        using var writerContext = (ReaparrDbContext)IDbContext;
        var injected = false;
        writerContext.SavingChanges += (_, _) =>
        {
            if (injected)
                return;
            injected = true;
            if (change == "cancel")
                cancellation.Cancel();
            else if (change == "failure")
                throw new InvalidOperationException("Injected write failure");
            else if (change == "outdated")
                writerContext.PlexLibraries.Where(x => x.Id == owned.Id).ExecuteUpdate(x => x.SetProperty(y => y.Outdated, true));
            else if (change == "owned-role" || change == "remote-role")
                writerContext.PlexServers.Where(x => x.Id == (change == "owned-role" ? owned.PlexServerId : remote.PlexServerId))
                    .ExecuteUpdate(x => x.SetProperty(y => y.OwnedOverride, change == "remote-role"));
            else if (change == "disabled")
                writerContext.PlexLibraries.Where(x => x.Id == owned.Id).ExecuteUpdate(x => x.SetProperty(y => y.IsEnabled, false));
            else if (change == "unsynced")
                writerContext.PlexLibraries.Where(x => x.Id == remote.Id).ExecuteUpdate(x => x.SetProperty(y => y.SyncedAt, (DateTime?)null));
            else if (change == "wrong-type")
                writerContext.PlexLibraries.Where(x => x.Id == remote.Id).ExecuteUpdate(x => x.SetProperty(y => y.Type, PlexMediaType.Movie));
            else
                writerContext.PlexLibraries.Where(x => x.Id == (change == "remote-content" ? remote.Id : owned.Id))
                    .ExecuteUpdate(x => x.SetProperty(y => y.ContentChangedAt, y => y.ContentChangedAt + 1)
                        .SetProperty(y => y.SyncedContentChangedAt, y => y.ContentChangedAt + 1));
        };
        var handler = new CompareMusicPlexLibraryCommandHandler(writerContext, Mock.Container.Resolve<ILogger>());

        // Act
        var result = await handler.ExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id), cancellation.Token);

        // Assert
        injected.ShouldBeTrue();
        if (change == "cancel")
            result.IsCancelled.ShouldBeTrue();
        else
            result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        var afterArtists = await dbContext.PlexMusicArtistComparisons.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        afterArtists.Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt)).ShouldBe(beforeArtists.Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt)));
        var afterAlbums = await dbContext.PlexMusicAlbumComparisons.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        afterAlbums.Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt)).ShouldBe(beforeAlbums.Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt)));
        var afterTracks = await dbContext.PlexMusicTrackComparisons.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        afterTracks.Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt)).ShouldBe(beforeTracks.Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt)));
        var afterScope = await dbContext.PlexComparisonScopes.SingleAsync(CancellationToken);
        (afterScope.Id, afterScope.CompletedAt).ShouldBe((beforeScope.Id, beforeScope.CompletedAt));
    }

    [Test]
    [Arguments("owned-role")]
    [Arguments("remote-role")]
    [Arguments("disabled")]
    [Arguments("outdated")]
    [Arguments("unsynced")]
    [Arguments("stale-stamp")]
    [Arguments("missing")]
    public async Task ShouldRejectIneligiblePairWithoutPublishing_WhenCurrentLibraryStateIsInvalid(string invalid)
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync();
        var dbContext = IDbContext;
        switch (invalid)
        {
            case "owned-role": await SetOwnedOverrideAsync(owned.PlexServerId, false); break;
            case "remote-role": await SetOwnedOverrideAsync(remote.PlexServerId, true); break;
            case "disabled": await dbContext.PlexLibraries.Where(x => x.Id == owned.Id).ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken); break;
            case "outdated": await dbContext.PlexLibraries.Where(x => x.Id == remote.Id).ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, true), CancellationToken); break;
            case "unsynced": await dbContext.PlexLibraries.Where(x => x.Id == remote.Id).ExecuteUpdateAsync(x => x.SetProperty(y => y.SyncedAt, (DateTime?)null), CancellationToken); break;
            case "stale-stamp": await dbContext.PlexLibraries.Where(x => x.Id == remote.Id).ExecuteUpdateAsync(x => x.SetProperty(y => y.ContentChangedAt, 2), CancellationToken); break;
        }
        var command = new CompareMusicPlexLibraryCommand(owned.Id, invalid == "missing" ? 999999 : remote.Id);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        (await dbContext.PlexComparisonScopes.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicArtistComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicAlbumComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldUseConservativeFallback_WhenOnlyOneSideHasStableIds(bool remoteMissing)
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync();
        var dbContext = IDbContext;
        var missingLibraryId = remoteMissing ? remote.Id : owned.Id;
        await dbContext.PlexArtists.Where(x => x.PlexLibraryId == missingLibraryId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.MusicBrainzArtistId, (string?)null), CancellationToken);
        await dbContext.PlexAlbums.Where(x => x.PlexLibraryId == missingLibraryId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.MusicBrainzReleaseId, (string?)null), CancellationToken);
        await dbContext.PlexTracks.Where(x => x.PlexLibraryId == missingLibraryId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.MusicBrainzReleaseTrackId, (string?)null)
                .SetProperty(y => y.MusicBrainzRecordingId, (string?)null), CancellationToken);
        var remoteTracks = await dbContext.PlexTracks.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var ownedTracks = await dbContext.PlexTracks.Where(x => x.PlexLibraryId == owned.Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await dbContext.PlexMusicArtistComparisons.SingleAsync(CancellationToken)).MatchType.ShouldBe(PlexMediaComparisonMatchType.NormalizedTitleAndYear);
        (await dbContext.PlexMusicAlbumComparisons.SingleAsync(CancellationToken)).MatchType.ShouldBe(PlexMediaComparisonMatchType.NormalizedTitleAndYear);
        var hits = await dbContext.PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId).ToListAsync(CancellationToken);
        hits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType)).ShouldBe([
            (remoteTracks[0].Id, ownedTracks[0].Id, PlexMediaComparisonMatchType.ParentAndChildNumbers),
            (remoteTracks[1].Id, ownedTracks[1].Id, PlexMediaComparisonMatchType.ParentAndChildNumbers),
        ]);
    }

    [Test]
    public async Task ShouldNotMatchRecordingAcrossEditions_WhenReleaseIdsConflict()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync();
        var dbContext = IDbContext;
        await dbContext.PlexAlbums.Where(x => x.PlexLibraryId == owned.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.MusicBrainzReleaseId, "another edition"), CancellationToken);
        await dbContext.PlexTracks.ExecuteUpdateAsync(x => x.SetProperty(y => y.MusicBrainzReleaseTrackId, (string?)null), CancellationToken);
        var recordings = await dbContext.PlexTracks.OrderBy(x => x.PlexLibraryId).ThenBy(x => x.TrackNumber)
            .Select(x => x.MusicBrainzRecordingId).ToListAsync(CancellationToken);
        recordings.ShouldBe(["recording-0-0", "recording-0-1", "recording-0-0", "recording-0-1"]);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await dbContext.PlexMusicArtistComparisons.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.PlexMusicAlbumComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldLeaveAmbiguousPositionFallbackUnmatched_WhenEquivalentTracksHaveNoIds()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(fallback: true);
        var dbContext = IDbContext;
        await dbContext.PlexTracks.Where(x => x.PlexLibraryId == owned.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.TrackNumber, 1).SetProperty(y => y.SearchTitle, "track 0"), CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await dbContext.PlexMusicAlbumComparisons.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldCompareEveryArtistExactlyOnce_WhenLibraryCrossesKeysetBatchBoundary()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(artists: 101);
        var dbContext = IDbContext;
        var remoteArtists = await dbContext.PlexArtists.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var ownedArtists = await dbContext.PlexArtists.Where(x => x.PlexLibraryId == owned.Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        remoteArtists.Count.ShouldBe(101);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var hits = await dbContext.PlexMusicArtistComparisons.OrderBy(x => x.RemotePlexMediaId).ToListAsync(CancellationToken);
        hits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId)).ShouldBe(remoteArtists.Zip(ownedArtists, (r, o) => (r.Id, o.Id)));
        var remoteAlbums = await dbContext.PlexAlbums.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var ownedAlbums = await dbContext.PlexAlbums.Where(x => x.PlexLibraryId == owned.Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var albumHits = await dbContext.PlexMusicAlbumComparisons.OrderBy(x => x.RemotePlexMediaId).ToListAsync(CancellationToken);
        albumHits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId)).ShouldBe(remoteAlbums.Zip(ownedAlbums, (r, o) => (r.Id, o.Id)));
        var remoteTracks = await dbContext.PlexTracks.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var ownedTracks = await dbContext.PlexTracks.Where(x => x.PlexLibraryId == owned.Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var trackHits = await dbContext.PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId).ToListAsync(CancellationToken);
        trackHits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId)).ShouldBe(remoteTracks.Zip(ownedTracks, (r, o) => (r.Id, o.Id)));
        (await dbContext.PlexComparisonScopes.SingleAsync(CancellationToken)).MediaType.ShouldBe(PlexMediaType.MusicArtist);
    }

    [Test]
    public async Task ShouldRetainOtherPairHitsAndScope_WhenTargetPairIsReplaced()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(servers: 3);
        var dbContext = IDbContext;
        var control = await dbContext.PlexLibraries.OrderBy(x => x.Id).LastAsync(CancellationToken);
        await SetOwnedOverrideAsync(control.PlexServerId, true);
        var controlResult = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(control.Id, remote.Id));
        controlResult.IsSuccess.ShouldBeTrue();
        controlResult.Errors.Count.ShouldBe(0);
        var targetResult = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));
        targetResult.IsSuccess.ShouldBeTrue();
        targetResult.Errors.Count.ShouldBe(0);
        var controlArtist = await dbContext.PlexMusicArtistComparisons.SingleAsync(x => x.OwnedPlexLibraryId == control.Id, CancellationToken);
        var controlAlbum = await dbContext.PlexMusicAlbumComparisons.SingleAsync(x => x.OwnedPlexLibraryId == control.Id, CancellationToken);
        var controlTracks = await dbContext.PlexMusicTrackComparisons.Where(x => x.OwnedPlexLibraryId == control.Id).OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var controlScope = await dbContext.PlexComparisonScopes.SingleAsync(x => x.OwnedPlexLibraryId == control.Id, CancellationToken);
        (await dbContext.PlexMusicArtistComparisons.CountAsync(CancellationToken)).ShouldBe(2);
        controlTracks.Count.ShouldBe(2);
        await dbContext.PlexArtists.Where(x => x.PlexLibraryId == owned.Id).ExecuteUpdateAsync(x =>
            x.SetProperty(y => y.MusicBrainzArtistId, "different").SetProperty(y => y.SearchTitle, "different"), CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var artist = await dbContext.PlexMusicArtistComparisons.SingleAsync(CancellationToken);
        (artist.Id, artist.RemotePlexMediaId, artist.OwnedPlexMediaId, artist.ComparedAt)
            .ShouldBe((controlArtist.Id, controlArtist.RemotePlexMediaId, controlArtist.OwnedPlexMediaId, controlArtist.ComparedAt));
        var album = await dbContext.PlexMusicAlbumComparisons.SingleAsync(CancellationToken);
        (album.Id, album.RemotePlexMediaId, album.OwnedPlexMediaId, album.ComparedAt)
            .ShouldBe((controlAlbum.Id, controlAlbum.RemotePlexMediaId, controlAlbum.OwnedPlexMediaId, controlAlbum.ComparedAt));
        var tracks = await dbContext.PlexMusicTrackComparisons.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        tracks.Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt))
            .ShouldBe(controlTracks.Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt)));
        var scope = await dbContext.PlexComparisonScopes.SingleAsync(x => x.OwnedPlexLibraryId == control.Id, CancellationToken);
        (scope.Id, scope.CompletedAt).ShouldBe((controlScope.Id, controlScope.CompletedAt));
        (await dbContext.PlexComparisonScopes.CountAsync(CancellationToken)).ShouldBe(2);
    }

    [Test]
    [Arguments(0, 1)]
    [Arguments(1, 0)]
    [Arguments(1, 1)]
    public async Task ShouldRejectInvalidPairIdsWithoutWriting_WhenCommandValidationFails(int ownedId, int remoteId)
    {
        // Arrange
        await SetupPairAsync();
        var dbContext = IDbContext;

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(ownedId, remoteId));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        (await dbContext.PlexComparisonScopes.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicArtistComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicAlbumComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    private async Task<(PlexLibrary Remote, PlexLibrary Owned)> SetupPairAsync(bool fallback = false, int artists = 1, int servers = 2)
    {
        await SetupDatabase(76201, config =>
        {
            config.PlexServerCount = servers;
            config.PlexAccountCount = 1;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = artists;
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 2;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remote = libraries[0];
        var owned = libraries[1];
        await SetOwnedOverrideAsync(remote.PlexServerId, false);
        await SetOwnedOverrideAsync(owned.PlexServerId, true);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, true)
            .SetProperty(y => y.Outdated, false).SetProperty(y => y.ContentChangedAt, 1)
            .SetProperty(y => y.SyncedContentChangedAt, (long?)1).SetProperty(y => y.SyncedAt, DateTime.UtcNow), CancellationToken);
        foreach (var library in libraries)
        {
            var roots = await dbContext.PlexArtists.AsTracking().Where(x => x.PlexLibraryId == library.Id)
                .Include(x => x.Albums).ThenInclude(x => x.Tracks).OrderBy(x => x.Id).ToListAsync(CancellationToken);
            for (var i = 0; i < roots.Count; i++)
            {
                var artist = roots[i];
                artist.Title = $"Artist {i}";
                artist.UpdateInitProperty(nameof(PlexMusicArtist.SearchTitle), $"artist {i}");
                artist.MusicBrainzArtistId = fallback ? null : $"artist-{i}";
                var album = artist.Albums.Single();
                album.Title = $"Album {i}";
                album.UpdateInitProperty(nameof(PlexMusicAlbum.SearchTitle), $"album {i}");
                album.Year = 2000;
                album.MusicBrainzReleaseId = fallback ? null : $"release-{i}";
                album.MusicBrainzReleaseGroupId = fallback ? null : $"group-{i}";
                album.ReleaseDate = new DateTime(2000, 1, 1);
                album.RecordLabel = "Label";
                album.Country = "US";
                album.DiscCount = 1;
                album.TrackCount = 2;
                var tracks = album.Tracks.OrderBy(x => x.Id).ToList();
                for (var j = 0; j < tracks.Count; j++)
                {
                    var track = tracks[j];
                    track.Title = $"Track {j}";
                    track.UpdateInitProperty(nameof(PlexMusicTrack.SearchTitle), $"track {j}");
                    track.MusicBrainzRecordingId = fallback ? null : $"recording-{i}-{j}";
                    track.MusicBrainzReleaseTrackId = fallback ? null : $"release-track-{i}-{j}";
                    track.DiscNumber = 1;
                    track.TrackNumber = j + 1;
                }
            }
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        return (remote, owned);
    }
}
