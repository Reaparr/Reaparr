using Quartz;
using Reaparr.Data;

namespace Reaparr.Application.UnitTests;

public class CompareMusicPlexLibraryCommandHandlerUnitTests : BaseCommandUnitTest<CompareMusicPlexLibraryCommand>
{
    private static string MbId(int value) => $"00000000-0000-0000-0000-{value:D12}";

    private static string NativeId(int value) => value.ToString("x").PadLeft(24, '0');

    [Test]
    public async Task ShouldPersistTypedHitsAndScope_WhenStableIdsMatch()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync();
        var dbContext = IDbContext;
        var artists = await dbContext.PlexMusicArtists.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        var albums = await dbContext.PlexMusicAlbums.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        var tracks = await dbContext
            .PlexMusicTracks.OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.TrackNumber)
            .ToListAsync(CancellationToken);
        (await dbContext.PlexMusicArtistComparisons.CountAsync(CancellationToken)).ShouldBe(0);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var artistHit = await dbContext.PlexMusicArtistComparisons.SingleAsync(CancellationToken);
        (
            artistHit.RemotePlexLibraryId,
            artistHit.OwnedPlexLibraryId,
            artistHit.RemotePlexMediaId,
            artistHit.OwnedPlexMediaId,
            artistHit.MatchType
        ).ShouldBe(
            (remote.Id, owned.Id, artists[0].Id, artists[1].Id, PlexMediaComparisonMatchType.MusicBrainzArtistId)
        );
        var albumHit = await dbContext.PlexMusicAlbumComparisons.SingleAsync(CancellationToken);
        (albumHit.RemotePlexMediaId, albumHit.OwnedPlexMediaId, albumHit.MatchType).ShouldBe(
            (albums[0].Id, albums[1].Id, PlexMediaComparisonMatchType.MusicBrainzReleaseId)
        );
        var hits = await dbContext
            .PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        hits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType))
            .ShouldBe([
                (tracks[0].Id, tracks[2].Id, PlexMediaComparisonMatchType.MusicBrainzReleaseTrackId),
                (tracks[1].Id, tracks[3].Id, PlexMediaComparisonMatchType.MusicBrainzReleaseTrackId),
            ]);
        var scope = await dbContext.PlexComparisonScopes.SingleAsync(CancellationToken);
        (scope.RemotePlexLibraryId, scope.OwnedPlexLibraryId, scope.MediaType, scope.CompletedAt).ShouldBe(
            (remote.Id, owned.Id, PlexMediaType.MusicArtist, artistHit.ComparedAt)
        );
        albumHit.ComparedAt.ShouldBe(scope.CompletedAt);
        hits.ShouldAllBe(x => x.ComparedAt == scope.CompletedAt);
    }

    [Test]
    [Arguments("artist")]
    [Arguments("release")]
    [Arguments("label")]
    [Arguments("date")]
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
            var artists = await dbContext
                .PlexMusicArtists.AsTracking()
                .OrderBy(x => x.PlexLibraryId)
                .ToListAsync(CancellationToken);
            artists[0].MusicBrainzArtistId = MbId(401);
            artists[1].MusicBrainzArtistId = MbId(402);
        }
        else
        {
            var albums = await dbContext
                .PlexMusicAlbums.AsTracking()
                .OrderBy(x => x.PlexLibraryId)
                .ToListAsync(CancellationToken);
            switch (conflict)
            {
                case "release":
                    albums[0].UpdateInitProperty(nameof(PlexMusicAlbum.MusicBrainzReleaseId), MbId(403));
                    albums[1].UpdateInitProperty(nameof(PlexMusicAlbum.MusicBrainzReleaseId), MbId(404));
                    break;
                case "label":
                    albums[1].UpdateInitProperty(nameof(PlexMusicAlbum.RecordLabel), "different label");
                    break;
                case "date":
                    albums[1]
                        .UpdateInitProperty(nameof(PlexMusicAlbum.OriginallyAvailableAt), new DateTime(2000, 2, 1));
                    break;
                case "disc-layout":
                    await dbContext
                        .PlexMusicTracks.Where(x => x.PlexLibraryId == owned.Id)
                        .ExecuteUpdateAsync(x => x.SetProperty(y => y.DiscNumber, 2), CancellationToken);
                    break;
                default:
                    albums[1].UpdateInitProperty(nameof(PlexMusicAlbum.SearchTitle), $"album {conflict}");
                    break;
            }
        }
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var artistHits = await dbContext.PlexMusicArtistComparisons.ToListAsync(CancellationToken);
        artistHits
            .Select(x => (x.RemotePlexLibraryId, x.OwnedPlexLibraryId))
            .ShouldBe(conflict == "artist" ? [] : [(remote.Id, owned.Id)]);
        (await dbContext.PlexMusicAlbumComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexComparisonScopes.SingleAsync(CancellationToken)).MediaType.ShouldBe(
            PlexMediaType.MusicArtist
        );
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldMatchAlbumsAndTracks_WhenAlbumCountrySetsDiffer(bool fallback)
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(fallback: fallback);
        var dbContext = IDbContext;
        var albums = await dbContext
            .PlexMusicAlbums.AsTracking()
            .OrderBy(x => x.PlexLibraryId)
            .ToListAsync(CancellationToken);
        var tracks = await dbContext
            .PlexMusicTracks.AsTracking()
            .OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.TrackNumber)
            .ToListAsync(CancellationToken);
        var firstRemoteCountry = new PlexCountry
        {
            Name = "First Remote Comparison Country",
            Key = "comparison-first-remote",
        };
        var remoteCountry = new PlexCountry { Name = "Remote Comparison Country", Key = "comparison-remote" };
        var ownedCountry = new PlexCountry { Name = "Owned Comparison Country", Key = "comparison-owned" };
        dbContext.PlexCountries.AddRange(firstRemoteCountry, remoteCountry, ownedCountry);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.PlexMusicAlbumCountries.AddRange(
            new PlexMusicAlbumCountries(firstRemoteCountry.Id, albums[0].Id),
            new PlexMusicAlbumCountries(remoteCountry.Id, albums[0].Id),
            new PlexMusicAlbumCountries(ownedCountry.Id, albums[1].Id)
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var countryLinks = await dbContext
            .PlexMusicAlbumCountries.OrderBy(x => x.PlexMusicAlbumId)
            .ThenBy(x => x.CountryId)
            .Select(x => new { x.PlexMusicAlbumId, x.CountryId })
            .ToListAsync(CancellationToken);
        countryLinks
            .Select(x => (x.PlexMusicAlbumId, x.CountryId))
            .ShouldBe([
                (albums[0].Id, firstRemoteCountry.Id),
                (albums[0].Id, remoteCountry.Id),
                (albums[1].Id, ownedCountry.Id),
            ]);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var albumHit = await dbContext.PlexMusicAlbumComparisons.SingleAsync(CancellationToken);
        (albumHit.RemotePlexMediaId, albumHit.OwnedPlexMediaId, albumHit.MatchType).ShouldBe(
            (
                albums[0].Id,
                albums[1].Id,
                fallback
                    ? PlexMediaComparisonMatchType.NormalizedTitleAndYear
                    : PlexMediaComparisonMatchType.MusicBrainzReleaseId
            )
        );
        var trackHits = await dbContext
            .PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        trackHits
            .Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType))
            .ShouldBe([
                (
                    tracks[0].Id,
                    tracks[2].Id,
                    fallback
                        ? PlexMediaComparisonMatchType.ParentAndChildNumbers
                        : PlexMediaComparisonMatchType.MusicBrainzReleaseTrackId
                ),
                (
                    tracks[1].Id,
                    tracks[3].Id,
                    fallback
                        ? PlexMediaComparisonMatchType.ParentAndChildNumbers
                        : PlexMediaComparisonMatchType.MusicBrainzReleaseTrackId
                ),
            ]);
    }

    [Test]
    public async Task ShouldMatchNonConflictingPartialSubset_WhenStableIdsAreMissing()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(fallback: true);
        var dbContext = IDbContext;
        var albums = await dbContext.PlexMusicAlbums.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        var remoteTracks = await dbContext
            .PlexMusicTracks.Where(x => x.PlexLibraryId == remote.Id)
            .OrderBy(x => x.TrackNumber)
            .ToListAsync(CancellationToken);
        var ownedTracks = await dbContext
            .PlexMusicTracks.Where(x => x.PlexLibraryId == owned.Id)
            .OrderBy(x => x.TrackNumber)
            .ToListAsync(CancellationToken);
        await dbContext.PlexMusicTracks.Where(x => x.Id == ownedTracks[1].Id).ExecuteDeleteAsync(CancellationToken);
        await dbContext
            .PlexMusicTracks.Where(x => x.Id == ownedTracks[0].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.DiscNumber, (int?)null), CancellationToken);
        (await dbContext.PlexMusicTracks.CountAsync(x => x.PlexLibraryId == owned.Id, CancellationToken)).ShouldBe(1);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await dbContext.PlexMusicArtistComparisons.SingleAsync(CancellationToken)).MatchType.ShouldBe(
            PlexMediaComparisonMatchType.NormalizedTitleAndYear
        );
        var albumHit = await dbContext.PlexMusicAlbumComparisons.SingleAsync(CancellationToken);
        (albumHit.RemotePlexMediaId, albumHit.OwnedPlexMediaId, albumHit.MatchType).ShouldBe(
            (albums[0].Id, albums[1].Id, PlexMediaComparisonMatchType.NormalizedTitleAndYear)
        );
        var trackHit = await dbContext.PlexMusicTrackComparisons.SingleAsync(CancellationToken);
        (trackHit.RemotePlexMediaId, trackHit.OwnedPlexMediaId, trackHit.MatchType).ShouldBe(
            (remoteTracks[0].Id, ownedTracks[0].Id, PlexMediaComparisonMatchType.ParentAndChildNumbers)
        );
    }

    [Test]
    public async Task ShouldMatchPartialAlbum_WhenFetchedCountsMatchButChildCountIsLarger()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(fallback: true, tracks: 4);
        var dbContext = IDbContext;
        var albums = await dbContext.PlexMusicAlbums.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        var remoteTracks = await dbContext
            .PlexMusicTracks.AsTracking()
            .Where(x => x.PlexLibraryId == remote.Id)
            .OrderBy(x => x.TrackNumber)
            .ToListAsync(CancellationToken);
        var ownedTracks = await dbContext
            .PlexMusicTracks.AsTracking()
            .Where(x => x.PlexLibraryId == owned.Id)
            .OrderBy(x => x.TrackNumber)
            .ToListAsync(CancellationToken);
        remoteTracks[2].DiscNumber = 2;
        remoteTracks[2].TrackNumber = 1;
        remoteTracks[3].DiscNumber = 2;
        remoteTracks[3].TrackNumber = 2;
        ownedTracks[0].DiscNumber = 2;
        ownedTracks[1].DiscNumber = 2;
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext
            .PlexMusicTracks.Where(x => x.Id == remoteTracks[2].Id || x.Id == remoteTracks[3].Id)
            .ExecuteDeleteAsync(CancellationToken);
        await dbContext
            .PlexMusicTracks.Where(x => x.Id == ownedTracks[2].Id || x.Id == ownedTracks[3].Id)
            .ExecuteDeleteAsync(CancellationToken);
        var remoteTrackCount = await dbContext.PlexMusicTracks.CountAsync(
            x => x.PlexLibraryId == remote.Id,
            CancellationToken
        );
        var ownedTrackCount = await dbContext.PlexMusicTracks.CountAsync(
            x => x.PlexLibraryId == owned.Id,
            CancellationToken
        );
        remoteTrackCount.ShouldBe(2);
        ownedTrackCount.ShouldBe(2);
        var childCounts = await dbContext
            .PlexMusicAlbums.OrderBy(x => x.PlexLibraryId)
            .Select(x => x.ChildCount)
            .ToListAsync(CancellationToken);
        childCounts.ShouldBe([4, 4]);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var albumHit = await dbContext.PlexMusicAlbumComparisons.SingleAsync(CancellationToken);
        (albumHit.RemotePlexMediaId, albumHit.OwnedPlexMediaId, albumHit.MatchType).ShouldBe(
            (albums[0].Id, albums[1].Id, PlexMediaComparisonMatchType.NormalizedTitleAndYear)
        );
        (await dbContext.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    [Arguments("release-track")]
    [Arguments("title")]
    [Arguments("duplicate-position")]
    public async Task ShouldRejectConflictingLayout_WhenTracksOccupyTheSamePosition(string conflict)
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(fallback: true);
        var dbContext = IDbContext;
        var tracks = await dbContext
            .PlexMusicTracks.AsTracking()
            .OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.TrackNumber)
            .ToListAsync(CancellationToken);
        switch (conflict)
        {
            case "release-track":
                tracks[0].MusicBrainzReleaseTrackId = MbId(405);
                tracks[2].MusicBrainzReleaseTrackId = MbId(406);
                break;
            case "title":
                tracks[2].UpdateInitProperty(nameof(PlexMusicTrack.SearchTitle), "different recording");
                break;
            default:
                tracks[3].TrackNumber = 1;
                break;
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
    public async Task ShouldMatchTracksByPositionAndTitle_WhenReleaseTrackIdIsMissing()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync();
        var dbContext = IDbContext;
        await dbContext.PlexMusicTracks.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.MusicBrainzReleaseTrackId, (string?)null),
            CancellationToken
        );
        var remoteTracks = await dbContext
            .PlexMusicTracks.Where(x => x.PlexLibraryId == remote.Id)
            .OrderBy(x => x.TrackNumber)
            .ToListAsync(CancellationToken);
        var ownedTracks = await dbContext
            .PlexMusicTracks.Where(x => x.PlexLibraryId == owned.Id)
            .OrderBy(x => x.TrackNumber)
            .ToListAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var hits = await dbContext
            .PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        hits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType))
            .ShouldBe([
                (remoteTracks[0].Id, ownedTracks[0].Id, PlexMediaComparisonMatchType.ParentAndChildNumbers),
                (remoteTracks[1].Id, ownedTracks[1].Id, PlexMediaComparisonMatchType.ParentAndChildNumbers),
            ]);
    }

    [Test]
    public async Task ShouldLeaveTracksUnmatched_WhenMissingDiscNumberIsUnsafe()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(fallback: true);
        var dbContext = IDbContext;
        await dbContext
            .PlexMusicTracks.Where(x => x.PlexLibraryId == remote.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.DiscNumber, 2), CancellationToken);
        await dbContext
            .PlexMusicTracks.Where(x => x.PlexLibraryId == owned.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.DiscNumber, (int?)null), CancellationToken);

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
            await dbContext.PlexMusicArtists.ExecuteUpdateAsync(
                x => x.SetProperty(y => y.SearchTitle, "ambiguous artist"),
                CancellationToken
            );
        else
        {
            var ownedArtists = await dbContext
                .PlexMusicArtists.Where(x => x.PlexLibraryId == owned.Id)
                .OrderBy(x => x.Id)
                .ToListAsync(CancellationToken);
            await dbContext
                .PlexMusicAlbums.Where(x => x.PlexLibraryId == owned.Id)
                .ExecuteUpdateAsync(
                    x =>
                        x.SetProperty(y => y.PlexArtistId, ownedArtists[0].Id)
                            .SetProperty(y => y.SearchTitle, "album 0"),
                    CancellationToken
                );
        }

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var artistHits = await dbContext
            .PlexMusicArtistComparisons.OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        artistHits
            .Select(x => (x.RemotePlexLibraryId, x.OwnedPlexLibraryId))
            .ShouldBe(level == "artist" ? [] : [(remote.Id, owned.Id), (remote.Id, owned.Id)]);
        (await dbContext.PlexMusicAlbumComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldPersistEveryExactArtistDuplicate_WhenStableIdDisambiguates()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(artists: 2);
        var dbContext = IDbContext;
        await dbContext
            .PlexMusicArtists.Where(x => x.PlexLibraryId == owned.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.MusicBrainzArtistId, MbId(100000)), CancellationToken);
        var remoteArtist = await dbContext
            .PlexMusicArtists.Where(x => x.PlexLibraryId == remote.Id)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        var ownedArtists = await dbContext
            .PlexMusicArtists.Where(x => x.PlexLibraryId == owned.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var hits = await dbContext
            .PlexMusicArtistComparisons.OrderBy(x => x.OwnedPlexMediaId)
            .ToListAsync(CancellationToken);
        hits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType))
            .ShouldBe([
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
        await dbContext
            .PlexMusicArtists.Where(x => x.PlexLibraryId == owned.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.MusicBrainzArtistId, MbId(411))
                        .SetProperty(y => y.SearchTitle, "other artist"),
                CancellationToken
            );

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
        (scope.RemotePlexLibraryId, scope.OwnedPlexLibraryId, scope.MediaType).ShouldBe(
            (remote.Id, owned.Id, PlexMediaType.MusicArtist)
        );
    }

    [Test]
    [Arguments("cancel")]
    [Arguments("failure")]
    public async Task ShouldRollBackReplacement_WhenWriterIsCancelledOrWriteFails(string change)
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync();
        var dbContext = IDbContext;
        var first = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));
        first.IsSuccess.ShouldBeTrue();
        first.Errors.Count.ShouldBe(0);
        var beforeArtists = await dbContext
            .PlexMusicArtistComparisons.OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
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
        };
        var handler = new CompareMusicPlexLibraryCommandHandler(writerContext, Mock.Container.Resolve<ILogger>());

        // Act
        var result = await handler.ExecuteAsync(
            new CompareMusicPlexLibraryCommand(owned.Id, remote.Id),
            cancellation.Token
        );

        // Assert
        injected.ShouldBeTrue();
        if (change == "cancel")
            result.IsCancelled.ShouldBeTrue();
        else
            result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        var afterArtists = await dbContext.PlexMusicArtistComparisons.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        afterArtists
            .Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt))
            .ShouldBe(beforeArtists.Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt)));
        var afterAlbums = await dbContext.PlexMusicAlbumComparisons.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        afterAlbums
            .Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt))
            .ShouldBe(beforeAlbums.Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt)));
        var afterTracks = await dbContext.PlexMusicTrackComparisons.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        afterTracks
            .Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt))
            .ShouldBe(beforeTracks.Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt)));
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
            case "owned-role":
                await SetOwnedOverrideAsync(owned.PlexServerId, false);
                break;
            case "remote-role":
                await SetOwnedOverrideAsync(remote.PlexServerId, true);
                break;
            case "disabled":
                await dbContext
                    .PlexLibraries.Where(x => x.Id == owned.Id)
                    .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
                break;
            case "outdated":
                await dbContext
                    .PlexLibraries.Where(x => x.Id == remote.Id)
                    .ExecuteUpdateAsync(x => x.SetProperty(y => y.Outdated, true), CancellationToken);
                break;
            case "unsynced":
                await dbContext
                    .PlexLibraries.Where(x => x.Id == remote.Id)
                    .ExecuteUpdateAsync(x => x.SetProperty(y => y.SyncedAt, (DateTime?)null), CancellationToken);
                break;
            case "stale-stamp":
                await dbContext
                    .PlexLibraries.Where(x => x.Id == remote.Id)
                    .ExecuteUpdateAsync(x => x.SetProperty(y => y.ContentChangedAt, 2), CancellationToken);
                break;
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
        await dbContext
            .PlexMusicArtists.Where(x => x.PlexLibraryId == missingLibraryId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.MusicBrainzArtistId, (string?)null), CancellationToken);
        await dbContext
            .PlexMusicAlbums.Where(x => x.PlexLibraryId == missingLibraryId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.MusicBrainzReleaseId, (string?)null), CancellationToken);
        await dbContext
            .PlexMusicTracks.Where(x => x.PlexLibraryId == missingLibraryId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.MusicBrainzReleaseTrackId, (string?)null), CancellationToken);
        var remoteTracks = await dbContext
            .PlexMusicTracks.Where(x => x.PlexLibraryId == remote.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var ownedTracks = await dbContext
            .PlexMusicTracks.Where(x => x.PlexLibraryId == owned.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await dbContext.PlexMusicArtistComparisons.SingleAsync(CancellationToken)).MatchType.ShouldBe(
            PlexMediaComparisonMatchType.NormalizedTitleAndYear
        );
        (await dbContext.PlexMusicAlbumComparisons.SingleAsync(CancellationToken)).MatchType.ShouldBe(
            PlexMediaComparisonMatchType.NormalizedTitleAndYear
        );
        var hits = await dbContext
            .PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        hits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType))
            .ShouldBe([
                (remoteTracks[0].Id, ownedTracks[0].Id, PlexMediaComparisonMatchType.ParentAndChildNumbers),
                (remoteTracks[1].Id, ownedTracks[1].Id, PlexMediaComparisonMatchType.ParentAndChildNumbers),
            ]);
    }

    [Test]
    public async Task ShouldNotMatchTracksAcrossEditions_WhenReleaseIdsConflict()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync();
        var dbContext = IDbContext;
        await dbContext
            .PlexMusicAlbums.Where(x => x.PlexLibraryId == owned.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.MusicBrainzReleaseId, MbId(412)), CancellationToken);
        var releaseTrackIds = await dbContext
            .PlexMusicTracks.OrderBy(x => x.PlexLibraryId)
            .ThenBy(x => x.TrackNumber)
            .Select(x => x.MusicBrainzReleaseTrackId)
            .ToListAsync(CancellationToken);
        releaseTrackIds.ShouldBe([MbId(300000), MbId(300001), MbId(300000), MbId(300001)]);

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
        await dbContext
            .PlexMusicTracks.Where(x => x.PlexLibraryId == owned.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(y => y.TrackNumber, 1).SetProperty(y => y.SearchTitle, "track 0"),
                CancellationToken
            );

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
        var remoteArtists = await dbContext
            .PlexMusicArtists.Where(x => x.PlexLibraryId == remote.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var ownedArtists = await dbContext
            .PlexMusicArtists.Where(x => x.PlexLibraryId == owned.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        remoteArtists.Count.ShouldBe(101);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var hits = await dbContext
            .PlexMusicArtistComparisons.OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        hits.Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId))
            .ShouldBe(remoteArtists.Zip(ownedArtists, (r, o) => (r.Id, o.Id)));
        var remoteAlbums = await dbContext
            .PlexMusicAlbums.Where(x => x.PlexLibraryId == remote.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var ownedAlbums = await dbContext
            .PlexMusicAlbums.Where(x => x.PlexLibraryId == owned.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var albumHits = await dbContext
            .PlexMusicAlbumComparisons.OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        albumHits
            .Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId))
            .ShouldBe(remoteAlbums.Zip(ownedAlbums, (r, o) => (r.Id, o.Id)));
        var remoteTracks = await dbContext
            .PlexMusicTracks.Where(x => x.PlexLibraryId == remote.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var ownedTracks = await dbContext
            .PlexMusicTracks.Where(x => x.PlexLibraryId == owned.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var trackHits = await dbContext
            .PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        trackHits
            .Select(x => (x.RemotePlexMediaId, x.OwnedPlexMediaId))
            .ShouldBe(remoteTracks.Zip(ownedTracks, (r, o) => (r.Id, o.Id)));
        (await dbContext.PlexComparisonScopes.SingleAsync(CancellationToken)).MediaType.ShouldBe(
            PlexMediaType.MusicArtist
        );
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
        var controlArtist = await dbContext.PlexMusicArtistComparisons.SingleAsync(
            x => x.OwnedPlexLibraryId == control.Id,
            CancellationToken
        );
        var controlAlbum = await dbContext.PlexMusicAlbumComparisons.SingleAsync(
            x => x.OwnedPlexLibraryId == control.Id,
            CancellationToken
        );
        var controlTracks = await dbContext
            .PlexMusicTrackComparisons.Where(x => x.OwnedPlexLibraryId == control.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var controlScope = await dbContext.PlexComparisonScopes.SingleAsync(
            x => x.OwnedPlexLibraryId == control.Id,
            CancellationToken
        );
        (await dbContext.PlexMusicArtistComparisons.CountAsync(CancellationToken)).ShouldBe(2);
        controlTracks.Count.ShouldBe(2);
        await dbContext
            .PlexMusicArtists.Where(x => x.PlexLibraryId == owned.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(y => y.MusicBrainzArtistId, MbId(413)).SetProperty(y => y.SearchTitle, "different"),
                CancellationToken
            );

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var artist = await dbContext.PlexMusicArtistComparisons.SingleAsync(CancellationToken);
        (artist.Id, artist.RemotePlexMediaId, artist.OwnedPlexMediaId, artist.ComparedAt).ShouldBe(
            (
                controlArtist.Id,
                controlArtist.RemotePlexMediaId,
                controlArtist.OwnedPlexMediaId,
                controlArtist.ComparedAt
            )
        );
        var album = await dbContext.PlexMusicAlbumComparisons.SingleAsync(CancellationToken);
        (album.Id, album.RemotePlexMediaId, album.OwnedPlexMediaId, album.ComparedAt).ShouldBe(
            (controlAlbum.Id, controlAlbum.RemotePlexMediaId, controlAlbum.OwnedPlexMediaId, controlAlbum.ComparedAt)
        );
        var tracks = await dbContext.PlexMusicTrackComparisons.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        tracks
            .Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt))
            .ShouldBe(controlTracks.Select(x => (x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt)));
        var scope = await dbContext.PlexComparisonScopes.SingleAsync(
            x => x.OwnedPlexLibraryId == control.Id,
            CancellationToken
        );
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

    [Test]
    [Arguments("5d07bce8403c64029056b5a6")]
    public async Task ShouldPreferCanonicalNativeIdentity_WhenSecondaryMusicMetadataConflicts(
        string ownedArtistIdentity
    )
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync();
        var dbContext = IDbContext;
        var remoteArtist = await dbContext.PlexMusicArtists.AsTracking().SingleAsync(x => x.PlexLibraryId == remote.Id);
        remoteArtist.PlexApiIdentityId = "5d07bce8403c64029056b5a6";
        var ownedArtist = await dbContext.PlexMusicArtists.AsTracking().SingleAsync(x => x.PlexLibraryId == owned.Id);
        ownedArtist.PlexApiIdentityId = ownedArtistIdentity;
        ownedArtist.UpdateInitProperty(nameof(PlexMusicArtist.SearchTitle), "owned-only-artist");
        ownedArtist.MusicBrainzArtistId = MbId(414);
        var remoteAlbum = await dbContext
            .PlexMusicAlbums.AsTracking()
            .SingleAsync(x => x.PlexLibraryId == remote.Id, CancellationToken);
        remoteAlbum.UpdateInitProperty(nameof(PlexMusicAlbum.PlexApiIdentityId), NativeId(6000));
        var ownedAlbum = await dbContext
            .PlexMusicAlbums.AsTracking()
            .SingleAsync(x => x.PlexLibraryId == owned.Id, CancellationToken);
        ownedAlbum.PlexApiIdentityId = remoteAlbum.PlexApiIdentityId;
        ownedAlbum.UpdateInitProperty(nameof(PlexMusicAlbum.MusicBrainzReleaseId), MbId(415));
        ownedAlbum.UpdateInitProperty(nameof(PlexMusicAlbum.OriginallyAvailableAt), new DateTime(2001, 2, 3));
        ownedAlbum.UpdateInitProperty(nameof(PlexMusicAlbum.RecordLabel), "Different label");
        var remoteTracks = await dbContext
            .PlexMusicTracks.AsTracking()
            .Where(x => x.PlexLibraryId == remote.Id)
            .OrderBy(x => x.TrackNumber)
            .ToListAsync(CancellationToken);
        var ownedTracks = await dbContext
            .PlexMusicTracks.AsTracking()
            .Where(x => x.PlexLibraryId == owned.Id)
            .OrderBy(x => x.TrackNumber)
            .ToListAsync(CancellationToken);
        var trackIdentityIndex = 0;
        foreach (var (remoteTrack, ownedTrack) in remoteTracks.Zip(ownedTracks))
        {
            remoteTrack.PlexApiIdentityId = NativeId(7000 + trackIdentityIndex++);
            ownedTrack.PlexApiIdentityId = remoteTrack.PlexApiIdentityId;
            ownedTrack.MusicBrainzReleaseTrackId = MbId(416);
            ownedTrack.DiscNumber = 2;
            ownedTrack.TrackNumber = remoteTrack.TrackNumber + 10;
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        (await dbContext.PlexMusicArtists.SingleAsync(x => x.Id == ownedArtist.Id)).PlexApiIdentityId.ShouldBe(
            ownedArtistIdentity
        );
        ownedArtist.SearchTitle.ShouldNotBe(remoteArtist.SearchTitle);
        ownedArtist.MusicBrainzArtistId.ShouldNotBe(remoteArtist.MusicBrainzArtistId);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var artistHit = await dbContext.PlexMusicArtistComparisons.SingleAsync(CancellationToken);
        (
            artistHit.RemotePlexLibraryId,
            artistHit.OwnedPlexLibraryId,
            artistHit.RemotePlexMediaId,
            artistHit.OwnedPlexMediaId,
            artistHit.MatchType
        ).ShouldBe((remote.Id, owned.Id, remoteArtist.Id, ownedArtist.Id, PlexMediaComparisonMatchType.PlexArtistGuid));
        var albumHit = await dbContext.PlexMusicAlbumComparisons.SingleAsync(CancellationToken);
        (
            albumHit.RemotePlexLibraryId,
            albumHit.OwnedPlexLibraryId,
            albumHit.RemotePlexMediaId,
            albumHit.OwnedPlexMediaId,
            albumHit.MatchType
        ).ShouldBe((remote.Id, owned.Id, remoteAlbum.Id, ownedAlbum.Id, PlexMediaComparisonMatchType.PlexAlbumGuid));
        var hits = await dbContext
            .PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        hits.Select(x =>
                (x.RemotePlexLibraryId, x.OwnedPlexLibraryId, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType)
            )
            .ShouldBe(
                remoteTracks.Zip(
                    ownedTracks,
                    (remoteTrack, ownedTrack) =>
                        (remote.Id, owned.Id, remoteTrack.Id, ownedTrack.Id, PlexMediaComparisonMatchType.PlexTrackGuid)
                )
            );
    }

    [Test]
    public async Task ShouldNotTreatEqualResolutionFlacBitrateAsHigherQuality()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync();
        var dbContext = IDbContext;
        var remoteData = await dbContext
            .PlexMusicTrackData.AsTracking()
            .Where(x => x.PlexLibraryId == remote.Id)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        var ownedData = await dbContext
            .PlexMusicTrackData.AsTracking()
            .Where(x => x.PlexLibraryId == owned.Id)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        remoteData.AudioBitrateKbps = 794;
        ownedData.AudioBitrateKbps = 789;
        await dbContext.SaveChangesAsync(CancellationToken);
        (
            await dbContext
                .PlexMusicTrackData.Where(x => x.Id == remoteData.Id)
                .Select(x => x.AudioBitrateKbps)
                .SingleAsync(CancellationToken)
        ).ShouldBe(794);
        (
            await dbContext
                .PlexMusicTrackData.Where(x => x.Id == ownedData.Id)
                .Select(x => x.AudioBitrateKbps)
                .SingleAsync(CancellationToken)
        ).ShouldBe(789);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var hits = await dbContext
            .PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        hits.Count.ShouldBe(2);
        var targetHit = hits.Single(x =>
            x.RemotePlexMediaId == remoteData.PlexTrackId && x.OwnedPlexMediaId == ownedData.PlexTrackId
        );
        (targetHit.RemotePlexMediaDataId, targetHit.OwnedPlexMediaDataId).ShouldBe((remoteData.Id, ownedData.Id));
        hits.ShouldAllBe(x => x.HitState == PlexMediaComparisonHitState.Matched);
        hits.ShouldAllBe(x => x.RemoteAudioQuality == AudioQuality.FLAC_16Bit_44_1kHz);
        hits.ShouldAllBe(x => x.OwnedAudioQuality == AudioQuality.FLAC_16Bit_44_1kHz);
        hits.ShouldAllBe(x => x.RemotePlexMediaDataId.HasValue && x.OwnedPlexMediaDataId.HasValue);
    }

    [Test]
    public async Task ShouldNotMatchArtists_WhenNativeIdentityAndMatchingTitlesAreAbsent()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(fallback: true);
        await using var dbContext = (ReaparrDbContext)IDbContext;
        var remoteArtist = await dbContext
            .PlexMusicArtists.AsTracking()
            .SingleAsync(x => x.PlexLibraryId == remote.Id, CancellationToken);
        var ownedArtist = await dbContext
            .PlexMusicArtists.AsTracking()
            .SingleAsync(x => x.PlexLibraryId == owned.Id, CancellationToken);
        remoteArtist.PlexApiIdentityId = null;
        ownedArtist.PlexApiIdentityId = null;
        remoteArtist.UpdateInitProperty(nameof(PlexMusicArtist.SearchTitle), "remote-only");
        ownedArtist.UpdateInitProperty(nameof(PlexMusicArtist.SearchTitle), "owned-only");
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await new CompareMusicPlexLibraryCommandHandler(
            dbContext,
            Mock.Container.Resolve<ILogger>()
        ).ExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await dbContext.PlexMusicArtistComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicAlbumComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await dbContext.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldMatchAdeleNativeParentsAndTracksWithoutGlobalCrossAlbumMatches()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(albums: 9, tracks: 1);
        var dbContext = IDbContext;
        var remoteArtist = await dbContext
            .PlexMusicArtists.AsTracking()
            .SingleAsync(x => x.PlexLibraryId == remote.Id, CancellationToken);
        var ownedArtist = await dbContext
            .PlexMusicArtists.AsTracking()
            .SingleAsync(x => x.PlexLibraryId == owned.Id, CancellationToken);
        var remoteAlbums = await dbContext
            .PlexMusicAlbums.AsTracking()
            .Where(x => x.PlexLibraryId == remote.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var ownedAlbums = await dbContext
            .PlexMusicAlbums.AsTracking()
            .Where(x => x.PlexLibraryId == owned.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var remoteTracks = await dbContext
            .PlexMusicTracks.AsTracking()
            .Where(x => x.PlexLibraryId == remote.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var ownedTracks = await dbContext
            .PlexMusicTracks.AsTracking()
            .Where(x => x.PlexLibraryId == owned.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        remoteAlbums.Count.ShouldBe(9);
        ownedAlbums.Count.ShouldBe(9);
        remoteTracks.Count.ShouldBe(9);
        ownedTracks.Count.ShouldBe(9);
        ConfigureAdeleFixture(remoteArtist, ownedArtist, remoteAlbums, ownedAlbums, remoteTracks, ownedTracks);
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var artistHits = await dbContext.PlexMusicArtistComparisons.ToListAsync(CancellationToken);
        var albumHits = await dbContext
            .PlexMusicAlbumComparisons.OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        var trackHits = await dbContext
            .PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        artistHits.Count.ShouldBe(1);
        albumHits.Count.ShouldBe(7);
        albumHits.ShouldAllBe(x => x.MatchType == PlexMediaComparisonMatchType.PlexAlbumGuid);
        trackHits.Count.ShouldBe(7);
        trackHits.ShouldAllBe(x => x.MatchType == PlexMediaComparisonMatchType.PlexTrackGuid);
        trackHits.Select(x => x.RemotePlexMediaId).ShouldBe(remoteTracks.Take(7).Select(x => x.Id));
        trackHits.Select(x => x.RemotePlexMediaId).Distinct().Count().ShouldBe(7);
        trackHits.Select(x => x.OwnedPlexMediaId).Distinct().Count().ShouldBe(7);
        remoteTracks.Take(5).Select(x => x.PlexApiIdentityId).Distinct().Count().ShouldBe(1);
    }

    private static void ConfigureAdeleFixture(
        PlexMusicArtist remoteArtist,
        PlexMusicArtist ownedArtist,
        IReadOnlyList<PlexMusicAlbum> remoteAlbums,
        IReadOnlyList<PlexMusicAlbum> ownedAlbums,
        IReadOnlyList<PlexMusicTrack> remoteTracks,
        IReadOnlyList<PlexMusicTrack> ownedTracks
    )
    {
        remoteArtist.PlexApiIdentityId = NativeId(9000);
        ownedArtist.PlexApiIdentityId = remoteArtist.PlexApiIdentityId;
        for (var i = 0; i < remoteAlbums.Count; i++)
        {
            remoteAlbums[i].UpdateInitProperty(nameof(PlexMusicAlbum.PlexApiIdentityId), NativeId(10000 + i));
            remoteTracks[i].PlexApiIdentityId = NativeId(1000);
            ownedTracks[i].PlexApiIdentityId = remoteTracks[i].PlexApiIdentityId;
            if (i < 7)
                ownedAlbums[i].PlexApiIdentityId = remoteAlbums[i].PlexApiIdentityId;
            else
            {
                ownedAlbums[i].UpdateInitProperty(nameof(PlexMusicAlbum.PlexApiIdentityId), NativeId(2000 + i));
                ownedAlbums[i].MusicBrainzReleaseId = null;
                ownedAlbums[i].UpdateInitProperty(nameof(PlexMusicAlbum.SearchTitle), $"owned-only-album-{i}");
            }

            if (i < 4)
            {
                ownedAlbums[i].UpdateInitProperty(nameof(PlexMusicAlbum.MusicBrainzReleaseId), MbId(500000 + i));
                ownedAlbums[i]
                    .UpdateInitProperty(nameof(PlexMusicAlbum.OriginallyAvailableAt), new DateTime(2010 + i, 2, 3));
                ownedAlbums[i].UpdateInitProperty(nameof(PlexMusicAlbum.RecordLabel), $"Different label {i}");
                ownedTracks[i].MusicBrainzReleaseTrackId = MbId(510000 + i);
                ownedTracks[i].DiscNumber = 2;
                ownedTracks[i].TrackNumber = 11;
            }
        }
    }

    [Test]
    public async Task ShouldReportTwentyFiveTrackAlbumPartial_WhenOnlyFourNativeTracksMatch()
    {
        // Arrange
        var (remote, owned) = await SetupPairAsync(tracks: 25);
        var dbContext = IDbContext;
        var remoteArtist = await dbContext
            .PlexMusicArtists.AsTracking()
            .SingleAsync(x => x.PlexLibraryId == remote.Id, CancellationToken);
        var ownedArtist = await dbContext
            .PlexMusicArtists.AsTracking()
            .SingleAsync(x => x.PlexLibraryId == owned.Id, CancellationToken);
        remoteArtist.PlexApiIdentityId = NativeId(8000);
        ownedArtist.PlexApiIdentityId = remoteArtist.PlexApiIdentityId;
        var remoteAlbum = await dbContext
            .PlexMusicAlbums.AsTracking()
            .SingleAsync(x => x.PlexLibraryId == remote.Id, CancellationToken);
        var ownedAlbum = await dbContext
            .PlexMusicAlbums.AsTracking()
            .SingleAsync(x => x.PlexLibraryId == owned.Id, CancellationToken);
        ownedAlbum.PlexApiIdentityId = remoteAlbum.PlexApiIdentityId;
        var remoteTracks = await dbContext
            .PlexMusicTracks.AsTracking()
            .Where(x => x.PlexLibraryId == remote.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var ownedTracks = await dbContext
            .PlexMusicTracks.AsTracking()
            .Where(x => x.PlexLibraryId == owned.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        for (var i = 0; i < remoteTracks.Count; i++)
        {
            if (i < 4)
            {
                remoteTracks[i].PlexApiIdentityId = NativeId(3000 + i);
                ownedTracks[i].PlexApiIdentityId = remoteTracks[i].PlexApiIdentityId;
            }
            else
            {
                ownedTracks[i].PlexApiIdentityId = NativeId(4000 + i);
                ownedTracks[i].MusicBrainzReleaseTrackId = MbId(520000 + i);
                ownedTracks[i].UpdateInitProperty(nameof(PlexMusicTrack.SearchTitle), $"owned-only-track-{i}");
                ownedTracks[i].TrackNumber = i + 100;
            }
        }
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync(new CompareMusicPlexLibraryCommand(owned.Id, remote.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var albumHits = await dbContext.PlexMusicAlbumComparisons.ToListAsync(CancellationToken);
        var trackHits = await dbContext
            .PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId)
            .ToListAsync(CancellationToken);
        remoteTracks.Count.ShouldBe(25);
        albumHits.Count.ShouldBe(1);
        albumHits.Single().MatchType.ShouldBe(PlexMediaComparisonMatchType.PlexAlbumGuid);
        trackHits.Count.ShouldBe(4);
        trackHits.ShouldAllBe(x => x.MatchType == PlexMediaComparisonMatchType.PlexTrackGuid);
        trackHits.Select(x => x.RemotePlexMediaId).ShouldBe(remoteTracks.Take(4).Select(x => x.Id));
        var items = new List<PlexMediaSlimDTO> { remoteArtist.ToSlimDTOMapper(), remoteAlbum.ToSlimDTOMapper() };
        items.AddRange(remoteTracks.Select(x => x.ToSlimDTOMapper()));
        var stateResult = await new ApplyRemoteMusicComparisonStateCommandHandler(
            dbContext,
            Mock.Mock<IScheduler>().Object
        ).ExecuteAsync(new ApplyRemoteMusicComparisonStateCommand(items, remote.Id), CancellationToken);
        stateResult.IsSuccess.ShouldBeTrue();
        items[0].ComparisonId.ShouldBe(PlexMediaComparisonState.Partial.ToComparisonId());
        items[1].ComparisonId.ShouldBe(PlexMediaComparisonState.Partial.ToComparisonId());
        items.Skip(2).Take(4).ShouldAllBe(x => x.ComparisonId == PlexMediaComparisonState.Owned.ToComparisonId());
        items.Skip(6).ShouldAllBe(x => x.ComparisonId == PlexMediaComparisonState.Missing.ToComparisonId());
    }

    private async Task<(PlexLibrary Remote, PlexLibrary Owned)> SetupPairAsync(
        bool fallback = false,
        int artists = 1,
        int servers = 2,
        int albums = 1,
        int tracks = 2
    )
    {
        await SetupDatabase(
            76201,
            config =>
            {
                config.PlexServerCount = servers;
                config.PlexAccountCount = 1;
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = artists;
                config.MusicAlbumCount = albums;
                config.MusicTrackCount = tracks;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var remote = libraries[0];
        var owned = libraries[1];
        await SetOwnedOverrideAsync(remote.PlexServerId, false);
        await SetOwnedOverrideAsync(owned.PlexServerId, true);
        await dbContext.PlexLibraries.ExecuteUpdateAsync(
            x =>
                x.SetProperty(y => y.IsEnabled, true)
                    .SetProperty(y => y.Outdated, false)
                    .SetProperty(y => y.ContentChangedAt, 1)
                    .SetProperty(y => y.SyncedContentChangedAt, (long?)1)
                    .SetProperty(y => y.SyncedAt, DateTime.UtcNow),
            CancellationToken
        );
        foreach (var (library, libraryIndex) in libraries.Select((library, index) => (library, index)))
            await SeedMusicLibraryAsync(dbContext, library, libraryIndex, fallback);

        await dbContext.SaveChangesAsync(CancellationToken);
        return (remote, owned);
    }

    private async Task SeedMusicLibraryAsync(
        IReaparrDbContext dbContext,
        PlexLibrary library,
        int libraryIndex,
        bool fallback
    )
    {
        var roots = await dbContext
            .PlexMusicArtists.AsTracking()
            .Where(x => x.PlexLibraryId == library.Id)
            .Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        for (var i = 0; i < roots.Count; i++)
        {
            var artist = roots[i];
            artist.Title = $"Artist {i}";
            artist.UpdateInitProperty(nameof(PlexMusicArtist.SearchTitle), $"artist {i}");
            artist.PlexApiIdentityId = NativeId(1_000_000 + libraryIndex * 2_000_000 + i);
            artist.MusicBrainzArtistId = fallback ? null : MbId(100000 + i);
            foreach (var (album, albumIndex) in artist.Albums.OrderBy(x => x.Id).Select((x, index) => (x, index)))
            {
                var albumKey = artist.Albums.Count == 1 ? $"{i}" : $"{i}-{albumIndex}";
                album.Title = $"Album {albumIndex}";
                album.UpdateInitProperty(nameof(PlexMusicAlbum.SearchTitle), $"album {albumIndex}");
                album.Year = 2000;
                album.UpdateInitProperty(
                    nameof(PlexMusicAlbum.PlexApiIdentityId),
                    NativeId(10_000_000 + libraryIndex * 2_000_000 + i * 10_000 + albumIndex)
                );
                album.MusicBrainzReleaseId = fallback ? null : MbId(200000 + i * 100 + albumIndex);
                album.UpdateInitProperty(nameof(PlexMusicAlbum.OriginallyAvailableAt), new DateTime(2000, 1, 1));
                album.UpdateInitProperty(nameof(PlexMusicAlbum.RecordLabel), "Label");
                var albumTracks = album.Tracks.OrderBy(x => x.Id).ToList();
                for (var j = 0; j < albumTracks.Count; j++)
                {
                    var track = albumTracks[j];
                    track.Title = $"Track {j}";
                    track.UpdateInitProperty(nameof(PlexMusicTrack.SearchTitle), $"track {j}");
                    track.PlexApiIdentityId = NativeId(
                        20_000_000 + libraryIndex * 2_000_000 + i * 10_000 + albumIndex * 100 + j
                    );
                    track.MusicBrainzReleaseTrackId = fallback
                        ? null
                        : MbId(300000 + i * 10_000 + albumIndex * 100 + j);
                    track.DiscNumber = 1;
                    track.TrackNumber = j + 1;
                }
            }
        }
    }
}
