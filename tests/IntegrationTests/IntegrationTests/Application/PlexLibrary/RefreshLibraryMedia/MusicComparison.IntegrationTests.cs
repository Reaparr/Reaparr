using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Moq;
using Quartz.Impl.Matchers;

namespace Reaparr.IntegrationTests;

public class MusicComparisonIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldCompareSyncedMusicThroughQuartzAndHttp_WhenLibrariesAreRefreshedResyncedAndDisabled()
    {
        // Arrange
        // Keep the generated Plex hierarchy and file payloads; control only external identity/layout metadata.
        var fixture = new MusicResponseFixture();
        using var container = await CreateContainer(10623, config =>
        {
            config.DatabaseOptions = database =>
            {
                database.PlexAccountCount = 1;
                database.PlexServerCount = 2;
                database.PlexMusicLibraryCount = 1;
                database.PlexMovieLibraryCount = 1;
                database.PlexTvShowLibraryCount = 1;
                database.MovieCount = 1;
                database.TvShowCount = 1;
                database.TvShowSeasonCount = 1;
                database.TvShowEpisodeCount = 1;
            };
            config.BaseMockHttpClientOptions = plex =>
            {
                plex.GenerateFromDatabase = true;
                plex.PlexServerAccessCount = 0;
                plex.MusicLibraryCount = 1;
                plex.ArtistsPerLibraryCount = 3;
                plex.AlbumsPerArtistCount = 1;
                plex.TracksPerAlbumCount = 2;
            };
            config.OverrideServices = builder =>
            {
                builder.Register(context => new HttpClient(new MusicFixtureHandler(
                    context.Resolve<Mock<HttpMessageHandler>>().Object, fixture)))
                    .As<HttpClient>().InstancePerDependency();
            };
        });
        var dbFactory = container.Resolve<IReaparrDbContextFactory>();
        var scheduler = container.BackgroundJobScheduler;
        var client = container.GetApiClient();
        await client.SignIn();
        using var before = await dbFactory.CreateAsync();
        var musicLibraries = await before.PlexLibraries.Where(x => x.Type == PlexMediaType.MusicArtist)
            .OrderBy(x => x.PlexServerId).ToListAsync(CancellationToken);
        musicLibraries.Count.ShouldBe(2);
        var owned = musicLibraries[0];
        var remote = musicLibraries[1];
        fixture.OwnedLibraryPath = $"/library/sections/{owned.Key}/all";
        fixture.RemoteLibraryPath = $"/library/sections/{remote.Key}/all";
        await before.PlexAccountServers.ExecuteUpdateAsync(x => x.SetProperty(y => y.IsServerOwned, false), CancellationToken);
        await before.PlexAccountLibraries.ExecuteUpdateAsync(x => x.SetProperty(y => y.IsLibraryOwned, false), CancellationToken);
        await before.PlexServers.ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, false), CancellationToken);
        await before.PlexServers.Where(x => x.Id == owned.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, true), CancellationToken);
        await before.PlexLibraries.Where(x => x.Type == PlexMediaType.MusicArtist).ExecuteUpdateAsync(x => x
            .SetProperty(y => y.SyncedAt, (DateTime?)null)
            .SetProperty(y => y.SyncedContentChangedAt, (long?)null)
            .SetProperty(y => y.Outdated, true), CancellationToken);
        (await before.PlexLibraries.WhereIsOwned().Where(x => x.Type == PlexMediaType.MusicArtist)
            .Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe([owned.Id]);
        (await before.PlexLibraries.WhereIsNotOwned().Where(x => x.Type == PlexMediaType.MusicArtist)
            .Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe([remote.Id]);
        (await before.PlexArtists.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await before.PlexAlbums.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await before.PlexTracks.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await before.PlexComparisonScopes.ToListAsync(CancellationToken)).ShouldBeEmpty();
        var movieControl = await before.PlexMovies.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Title, x.PlexLibraryId, x.PlexApiRatingKey, x.MediaSize }).ToListAsync(CancellationToken);
        var tvControl = await before.PlexTvShows.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Title, x.PlexLibraryId, x.PlexApiRatingKey, x.ChildCount }).ToListAsync(CancellationToken);
        var episodeControl = await before.PlexTvShowEpisodes.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Title, x.PlexLibraryId, x.TvShowSeasonId, x.MediaSize }).ToListAsync(CancellationToken);
        (movieControl.Count, tvControl.Count, episodeControl.Count).ShouldBe((2, 2, 2));
        // Produce the unrelated video scopes through real Quartz jobs too, never seeded comparison rows.
        var videoLibraries = await before.PlexLibraries.Where(x => x.Type == PlexMediaType.Movie || x.Type == PlexMediaType.TvShow)
            .ToListAsync(CancellationToken);
        foreach (var type in new[] { PlexMediaType.Movie, PlexMediaType.TvShow })
        {
            var ownedVideo = videoLibraries.Single(x => x.Type == type && x.PlexServerId == owned.PlexServerId);
            var remoteVideo = videoLibraries.Single(x => x.Type == type && x.PlexServerId == remote.PlexServerId);
            var scheduledVideo = await scheduler.ExecuteJob<PlexLibraryComparisonJob, PlexLibraryComparisonJobPayload>(
                PlexLibraryComparisonJob.GetJobKey(ownedVideo.Id, remoteVideo.Id),
                new PlexLibraryComparisonJobPayload(ownedVideo.Id, remoteVideo.Id), CancellationToken);
            scheduledVideo.IsSuccess.ShouldBeTrue();
            scheduledVideo.Errors.Count.ShouldBe(0);
        }
        await WaitForDatabaseConditionAsync(async () =>
        {
            using var context = await dbFactory.CreateAsync();
            return await context.PlexComparisonScopes.CountAsync(CancellationToken) == 2
                && (await scheduler.GetJobKeys(JobTypes.LibraryComparisonJob, CancellationToken)).Count == 0;
        }, 60, 100);
        var videoScopes = await before.PlexComparisonScopes.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.MediaType, x.RemotePlexLibraryId, x.OwnedPlexLibraryId, x.CompletedAt }).ToListAsync(CancellationToken);
        videoScopes.Select(x => x.MediaType).OrderBy(x => x).ShouldBe([PlexMediaType.Movie, PlexMediaType.TvShow]);
        var movieHitControl = await before.PlexMovieComparisons.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt, x.HitState }).ToListAsync(CancellationToken);
        var showHitControl = await before.PlexTvShowComparisons.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt, x.HitState }).ToListAsync(CancellationToken);
        var seasonHitControl = await before.PlexSeasonComparisons.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt, x.HitState }).ToListAsync(CancellationToken);
        var episodeHitControl = await before.PlexEpisodeComparisons.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt, x.HitState }).ToListAsync(CancellationToken);
        var jobKey = PlexLibraryComparisonJob.GetJobKey(owned.Id, remote.Id);
        var observer = new ComparisonJobObserver();
        scheduler.ListenerManager.AddJobListener(observer, KeyMatcher<JobKey>.KeyEquals(jobKey));
        var snapshotObserver = new ComparisonJobObserver(nameof(MediaOverviewSnapshotJob));
        scheduler.ListenerManager.AddJobListener(snapshotObserver,
            KeyMatcher<JobKey>.KeyEquals(MediaOverviewSnapshotJob.GetJobKey()));
        await scheduler.AddJob(JobBuilder.Create<MediaOverviewSnapshotJob>()
            .WithIdentity(MediaOverviewSnapshotJob.GetJobKey()).StoreDurably().Build(), true, CancellationToken);
        // Publish once both fixture libraries are synced, not halfway through the coalesced rebuild.
        var snapshotGroup = GroupMatcher<JobKey>.GroupEquals(MediaOverviewSnapshotJob.GetJobKey().Group);
        await scheduler.PauseJobs(snapshotGroup, CancellationToken);

        // Act
        foreach (var library in new[] { owned, remote })
        {
            var sync = await client.POSTAsync<RefreshLibraryMediaEndpoint, RefreshLibraryMediaEndpointRequest, ResultDTO<PlexLibraryDTO>>(
                new RefreshLibraryMediaEndpointRequest { PlexLibraryId = library.Id });
            sync.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
            sync.Result.IsSuccess.ShouldBeTrue();
            sync.Result.Errors.Count.ShouldBe(0);
            sync.Result.Value.ShouldNotBeNull();
            sync.Result.Value.Id.ShouldBe(library.Id);
            await WaitForDatabaseConditionAsync(async () =>
            {
                using var context = await dbFactory.CreateAsync();
                return await context.LibrarySyncJobQueues.AnyAsync(x => x.PlexLibraryId == library.Id
                    && x.Status == LibrarySyncJobStatus.Completed, CancellationToken);
            }, maxRetries: 60, delayMs: 200);
        }
        await scheduler.ResumeJobs(snapshotGroup, CancellationToken);
        await WaitForDatabaseConditionAsync(async () => await scheduler.CheckExists(jobKey, CancellationToken), 60, 100);
        var scheduled = await scheduler.GetJobDetail(jobKey, CancellationToken);
        scheduled.ShouldNotBeNull();
        scheduled.JobType.ShouldBe(typeof(PlexLibraryComparisonJob));
        scheduled.JobDataMap.GetPayload<PlexLibraryComparisonJobPayload>()
            .ShouldBe(new PlexLibraryComparisonJobPayload(owned.Id, remote.Id));
        (await scheduler.GetJobKeys(JobTypes.LibraryComparisonJob, CancellationToken)).ShouldBe([jobKey]);
        var trigger = (await scheduler.GetTriggersOfJob(jobKey, CancellationToken)).Single();
        trigger.GetNextFireTimeUtc().ShouldNotBeNull();
        trigger.GetNextFireTimeUtc()!.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        (await scheduler.RescheduleJob(trigger.Key, trigger.GetTriggerBuilder().StartNow().Build(), CancellationToken))
            .ShouldNotBeNull();
        await WaitForDatabaseConditionAsync(() => observer.Completed.Count == 1, 60, 100);

        // Assert
        observer.Started.ToArray().ShouldBe([new PlexLibraryComparisonJobPayload(owned.Id, remote.Id)]);
        observer.Completed.ToArray().ShouldBe([(JobStatus.Completed, (string?)null)]);
        using var compared = await dbFactory.CreateAsync();
        var synced = await compared.PlexLibraries.Where(x => x.Type == PlexMediaType.MusicArtist)
            .OrderBy(x => x.PlexServerId).ToListAsync(CancellationToken);
        synced.Select(x => (x.Id, x.MusicArtistCount, x.MusicAlbumCount, x.MusicTrackCount, x.Outdated,
            Current: x.SyncedAt != null && x.SyncedContentChangedAt == x.ContentChangedAt))
            .ShouldBe([(owned.Id, 3, 3, 5, false, true), (remote.Id, 3, 3, 6, false, true)]);
        var artists = await compared.PlexArtists.OrderBy(x => x.Title).ThenBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        var albums = await compared.PlexAlbums.ToListAsync(CancellationToken);
        var tracks = await compared.PlexTracks.ToListAsync(CancellationToken);
        var expectedArtistTitles = new[] { "Alpha Complete", "B Partial", "C Remote only" };
        artists.Where(x => x.PlexLibraryId == remote.Id).Select(x => x.Title).ShouldBe(expectedArtistTitles);
        artists.Where(x => x.PlexLibraryId == owned.Id).Select(x => x.Title)
            .ShouldBe(["Alpha Complete", "B Partial", "C Owned only"]);
        artists.Select(x => x.MusicBrainzArtistId).ShouldAllBe(x => x == null);
        albums.Select(x => (x.Title, x.Year, x.MusicBrainzReleaseId)).Distinct()
            .ShouldBe([("Studio edition", 2001, (string?)null)]);
        tracks.Select(x => (x.DiscNumber, x.TrackNumber, x.Title, x.MusicBrainzReleaseTrackId, x.MusicBrainzRecordingId))
            .Distinct().OrderBy(x => x.TrackNumber).ShouldBe([
                ((int?)1, (int?)1, "Track 1", (string?)null, (string?)null),
                ((int?)1, (int?)2, "Track 2", (string?)null, (string?)null)]);
        var remoteArtists = artists.Where(x => x.PlexLibraryId == remote.Id).ToDictionary(x => x.Title);
        var ownedArtists = artists.Where(x => x.PlexLibraryId == owned.Id).ToDictionary(x => x.Title);
        var matchedTitles = new[] { "Alpha Complete", "B Partial" };
        var artistHits = await compared.PlexMusicArtistComparisons.OrderBy(x => x.RemotePlexMediaId).ToListAsync(CancellationToken);
        artistHits.Select(x => (x.RemotePlexLibraryId, x.OwnedPlexLibraryId, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType))
            .ShouldBe(matchedTitles.Select(title => (remote.Id, owned.Id, remoteArtists[title].Id, ownedArtists[title].Id,
                PlexMediaComparisonMatchType.NormalizedTitleAndYear)).OrderBy(x => x.Item3));
        var albumHits = await compared.PlexMusicAlbumComparisons.OrderBy(x => x.RemotePlexMediaId).ToListAsync(CancellationToken);
        albumHits.Select(x => (x.RemotePlexLibraryId, x.OwnedPlexLibraryId, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType))
            .ShouldBe(matchedTitles.Select(title => (remote.Id, owned.Id,
                albums.Single(x => x.PlexArtistId == remoteArtists[title].Id).Id,
                albums.Single(x => x.PlexArtistId == ownedArtists[title].Id).Id,
                PlexMediaComparisonMatchType.NormalizedTitleAndYear)).OrderBy(x => x.Item3));
        var expectedTrackHits = albumHits.SelectMany(hit => tracks.Where(x => x.PlexAlbumId == hit.RemotePlexMediaId)
            .Join(tracks.Where(x => x.PlexAlbumId == hit.OwnedPlexMediaId), x => x.TrackNumber, x => x.TrackNumber,
                (source, target) => (remote.Id, owned.Id, source.Id, target.Id, PlexMediaComparisonMatchType.ParentAndChildNumbers)))
            .OrderBy(x => x.Item3).ToList();
        expectedTrackHits.Count.ShouldBe(3);
        var trackHits = await compared.PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId).ToListAsync(CancellationToken);
        trackHits.Select(x => (x.RemotePlexLibraryId, x.OwnedPlexLibraryId, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType))
            .ShouldBe(expectedTrackHits);
        var scope = await compared.PlexComparisonScopes.SingleAsync(x => x.MediaType == PlexMediaType.MusicArtist, CancellationToken);
        (scope.RemotePlexLibraryId, scope.OwnedPlexLibraryId, scope.MediaType).ShouldBe((remote.Id, owned.Id, PlexMediaType.MusicArtist));
        artistHits.Select(x => x.ComparedAt).Concat(albumHits.Select(x => x.ComparedAt)).Concat(trackHits.Select(x => x.ComparedAt))
            .ShouldAllBe(x => x == scope.CompletedAt);
        (await compared.PlexTrackData.CountAsync(CancellationToken)).ShouldBe(11);
        var requestCountBeforeBrowse = fixture.Requests.Count;
        requestCountBeforeBrowse.ShouldBe(12);

        // Wait for real snapshot publication so the sorted HTTP request must use the cached browse path.
        await WaitForDatabaseConditionAsync(() => Task.FromResult(!snapshotObserver.Completed.IsEmpty), 60, 200);
        snapshotObserver.Completed.ShouldAllBe(x => x.Status == JobStatus.Completed,
            string.Join(System.Environment.NewLine, snapshotObserver.Completed.Select(x => x.Error)));
        await WaitForDatabaseConditionAsync(async () =>
        {
            using var context = await dbFactory.CreateAsync();
            return await context.MediaOverviewMusicArtistSnapshots.CountAsync(CancellationToken) == 6;
        }, 60, 200);
        var expectedStates = new[] { PlexMediaComparisonState.Owned, PlexMediaComparisonState.Partial, PlexMediaComparisonState.Missing };
        foreach (var sort in new[] { (string?)null, "title:asc" })
        {
            for (var page = 1; page <= 3; page++)
            {
                var browse = await client.GETAsync<GetAllMediaByTypeEndpoint, GetAllMediaByTypeRequest, ResultDTO<PlexMediaStatisticsDTO>>(
                    new GetAllMediaByTypeRequest { MediaType = PlexMediaType.MusicArtist, PlexLibraryId = remote.Id,
                        Page = page, PageSize = 1, Sort = sort });
                browse.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
                browse.Result.IsSuccess.ShouldBeTrue();
                browse.Result.Errors.Count.ShouldBe(0);
                browse.Result.Value.ShouldNotBeNull();
                (browse.Result.Value.Page, browse.Result.Value.PageSize, browse.Result.Value.TotalCount).ShouldBe((page, 1, 3));
                (browse.Result.Value.MediaCount, browse.Result.Value.MediaSize, browse.Result.Value.TotalMediaSize)
                    .ShouldBe((3, artists.Where(x => x.PlexLibraryId == remote.Id).Sum(x => x.MediaSize),
                        artists.Where(x => x.PlexLibraryId == remote.Id).Sum(x => x.MediaSize)));
                browse.Result.Value.NavigationIndexes.Select(x => (x.Label, x.Index)).ShouldBe([("A", 0), ("B", 1), ("C", 2)]);
                browse.Result.Value.MediaList.Select(x => (x.Id, x.Type, x.ComparisonId))
                    .ShouldBe([(remoteArtists[expectedArtistTitles[page - 1]].Id, PlexMediaType.MusicArtist, expectedStates[page - 1].ToComparisonId())]);
            }
        }
        var allLibraries = await client.GETAsync<GetAllMediaByTypeEndpoint, GetAllMediaByTypeRequest, ResultDTO<PlexMediaStatisticsDTO>>(
            new GetAllMediaByTypeRequest { MediaType = PlexMediaType.MusicArtist, PlexLibraryId = 0,
                Page = 1, PageSize = 100, Sort = "title:asc" });
        allLibraries.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        allLibraries.Result.IsSuccess.ShouldBeTrue();
        allLibraries.Result.Errors.Count.ShouldBe(0);
        allLibraries.Result.Value.ShouldNotBeNull();
        (allLibraries.Result.Value.TotalCount, allLibraries.Result.Value.MediaCount).ShouldBe((6, 6));
        allLibraries.Result.Value.MediaList.OrderBy(x => x.Id).Select(x => (x.Id, x.PlexLibraryId, x.ComparisonId))
            .ShouldBe(artists.OrderBy(x => x.Id).Select(x => (x.Id, x.PlexLibraryId,
                (x.Title == "B Partial" ? PlexMediaComparisonState.Partial
                    : x.Title == "C Remote only" ? PlexMediaComparisonState.Missing
                    : PlexMediaComparisonState.Owned).ToComparisonId())));
        var remoteOnlyPage = await client.GETAsync<GetAllMediaByTypeEndpoint, GetAllMediaByTypeRequest, ResultDTO<PlexMediaStatisticsDTO>>(
            new GetAllMediaByTypeRequest { MediaType = PlexMediaType.MusicArtist, PlexLibraryId = 0,
                Page = 2, PageSize = 1, Sort = "title:asc", FilterOwnedMedia = true });
        remoteOnlyPage.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        remoteOnlyPage.Result.IsSuccess.ShouldBeTrue();
        remoteOnlyPage.Result.Errors.Count.ShouldBe(0);
        remoteOnlyPage.Result.Value.ShouldNotBeNull();
        (remoteOnlyPage.Result.Value.TotalCount, remoteOnlyPage.Result.Value.Page, remoteOnlyPage.Result.Value.PageSize).ShouldBe((3, 2, 1));
        remoteOnlyPage.Result.Value.MediaList.Select(x => (x.Id, x.PlexLibraryId, x.ComparisonId))
            .ShouldBe([(remoteArtists["B Partial"].Id, remote.Id, PlexMediaComparisonState.Partial.ToComparisonId())]);
        for (var i = 0; i < expectedStates.Length; i++)
        {
            var filtered = await client.GETAsync<GetAllMediaByTypeEndpoint, GetAllMediaByTypeRequest, ResultDTO<PlexMediaStatisticsDTO>>(
                new GetAllMediaByTypeRequest { MediaType = PlexMediaType.MusicArtist, PlexLibraryId = remote.Id,
                    Page = 1, PageSize = 1, Sort = "title:asc", ComparisonState = expectedStates[i] });
            filtered.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
            filtered.Result.IsSuccess.ShouldBeTrue();
            filtered.Result.Errors.Count.ShouldBe(0);
            filtered.Result.Value.ShouldNotBeNull();
            (filtered.Result.Value.TotalCount, filtered.Result.Value.Page, filtered.Result.Value.PageSize).ShouldBe((1, 1, 1));
            (filtered.Result.Value.MediaCount, filtered.Result.Value.MediaSize, filtered.Result.Value.TotalMediaSize)
                .ShouldBe((1, remoteArtists[expectedArtistTitles[i]].MediaSize, remoteArtists[expectedArtistTitles[i]].MediaSize));
            filtered.Result.Value.MediaList.Select(x => (x.Id, x.ComparisonId))
                .ShouldBe([(remoteArtists[expectedArtistTitles[i]].Id, expectedStates[i].ToComparisonId())]);
            var detail = await client.GETAsync<GetMediaDetailByIdEndpoint, GetMediaDetailByIdEndpointRequest, ResultDTO<PlexMediaDTO>>(
                new GetMediaDetailByIdEndpointRequest(remoteArtists[expectedArtistTitles[i]].Id, PlexMediaType.MusicArtist));
            detail.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
            detail.Result.IsSuccess.ShouldBeTrue();
            detail.Result.Errors.Count.ShouldBe(0);
            detail.Result.Value.ShouldNotBeNull();
            (detail.Result.Value.Id, detail.Result.Value.Type, detail.Result.Value.ComparisonId)
                .ShouldBe((remoteArtists[expectedArtistTitles[i]].Id, PlexMediaType.MusicArtist, expectedStates[i].ToComparisonId()));
            var remoteAlbum = albums.Single(x => x.PlexArtistId == remoteArtists[expectedArtistTitles[i]].Id);
            detail.Result.Value.Children.Select(x => (x.Id, x.Type, x.ComparisonId))
                .ShouldBe([(remoteAlbum.Id, PlexMediaType.MusicAlbum, expectedStates[i].ToComparisonId())]);
            var expectedChildren = tracks.Where(x => x.PlexAlbumId == remoteAlbum.Id).OrderBy(x => x.TrackNumber)
                .Select(x => (x.Id, PlexMediaType.MusicTrack, (i == 2 || (i == 1 && x.TrackNumber == 2)
                    ? PlexMediaComparisonState.Missing : PlexMediaComparisonState.Owned).ToComparisonId())).ToList();
            detail.Result.Value.Children.Single().Children.Select(x => (x.Id, x.Type, x.ComparisonId)).ShouldBe(expectedChildren);
            var comparisonDetail = await client.GETAsync<GetMediaComparisonDetailsEndpoint, GetMediaComparisonDetailsEndpointRequest, ResultDTO<PlexMediaComparisonDetailsDTO>>(
                new GetMediaComparisonDetailsEndpointRequest(remoteArtists[expectedArtistTitles[i]].Id, PlexMediaType.MusicArtist));
            comparisonDetail.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
            comparisonDetail.Result.IsSuccess.ShouldBeTrue();
            comparisonDetail.Result.Errors.Count.ShouldBe(0);
            comparisonDetail.Result.Value.ShouldNotBeNull();
            (comparisonDetail.Result.Value.PlexMediaId, comparisonDetail.Result.Value.Type, comparisonDetail.Result.Value.State)
                .ShouldBe((remoteArtists[expectedArtistTitles[i]].Id, PlexMediaType.MusicArtist, expectedStates[i]));
            comparisonDetail.Result.Value.Rows.Select(x => (x.PlexMediaId, x.Type, x.State, x.PlexLibraryId, x.PlexServerId,
                x.RemoteQuality, x.OwnedQuality)).ShouldBe([(remoteAlbum.Id, PlexMediaType.MusicAlbum, expectedStates[i],
                    remote.Id, remote.PlexServerId, VideoQuality.None, VideoQuality.None)]);
            comparisonDetail.Result.Value.Rows.Single().Children.Select(x => (x.PlexMediaId, x.Type, x.State.ToComparisonId()))
                .ShouldBe(expectedChildren);
        }
        (await scheduler.GetJobKeys(JobTypes.LibraryComparisonJob, CancellationToken)).ShouldBeEmpty();
        fixture.Requests.Count.ShouldBe(requestCountBeforeBrowse);

        // Real forced resync must purge the completed scope even though the Plex changestamp is unchanged.
        var resync = await client.POSTAsync<RefreshLibraryMediaEndpoint, RefreshLibraryMediaEndpointRequest, ResultDTO<PlexLibraryDTO>>(
            new RefreshLibraryMediaEndpointRequest { PlexLibraryId = remote.Id, ForceLibrarySync = true });
        resync.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        resync.Result.IsSuccess.ShouldBeTrue();
        resync.Result.Errors.Count.ShouldBe(0);
        await WaitForDatabaseConditionAsync(async () =>
        {
            using var context = await dbFactory.CreateAsync();
            return await context.LibrarySyncJobQueues.AnyAsync(x => x.PlexLibraryId == remote.Id
                && x.Status == LibrarySyncJobStatus.Completed && x.CompletedAt > scope.CompletedAt, CancellationToken);
        }, 60, 200);
        await WaitForDatabaseConditionAsync(async () => await scheduler.CheckExists(jobKey, CancellationToken), 60, 100);
        using var resynced = await dbFactory.CreateAsync();
        (await resynced.PlexComparisonScopes.Where(x => x.MediaType == PlexMediaType.MusicArtist).ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await resynced.PlexComparisonScopes.Where(x => x.MediaType != PlexMediaType.MusicArtist).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.MediaType, x.RemotePlexLibraryId, x.OwnedPlexLibraryId, x.CompletedAt })
            .ToListAsync(CancellationToken)).ShouldBe(videoScopes);
        (await resynced.PlexMusicArtistComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await resynced.PlexMusicAlbumComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await resynced.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        var resyncedLibrary = await resynced.PlexLibraries.SingleAsync(x => x.Id == remote.Id, CancellationToken);
        resyncedLibrary.Outdated.ShouldBeFalse();
        resyncedLibrary.SyncedContentChangedAt.ShouldBe(resyncedLibrary.ContentChangedAt);
        var currentArtistId = await resynced.PlexArtists.Where(x => x.PlexLibraryId == remote.Id && x.Title == "Alpha Complete")
            .Select(x => x.Id).SingleAsync(CancellationToken);
        var pending = await client.GETAsync<GetMediaComparisonDetailsEndpoint, GetMediaComparisonDetailsEndpointRequest, ResultDTO<PlexMediaComparisonDetailsDTO>>(
            new GetMediaComparisonDetailsEndpointRequest(currentArtistId, PlexMediaType.MusicArtist));
        pending.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        pending.Result.IsSuccess.ShouldBeTrue();
        pending.Result.Errors.Count.ShouldBe(0);
        pending.Result.Value.ShouldNotBeNull();
        pending.Result.Value.State.ShouldBe(PlexMediaComparisonState.Pending);
        pending.Result.Value.Rows.ShouldBeEmpty();
        observer.Completed.Count.ShouldBe(1);
        trigger = (await scheduler.GetTriggersOfJob(jobKey, CancellationToken)).Single();
        (await scheduler.RescheduleJob(trigger.Key, trigger.GetTriggerBuilder().StartNow().Build(), CancellationToken)).ShouldNotBeNull();
        await WaitForDatabaseConditionAsync(() => observer.Completed.Count == 2, 60, 100);
        observer.Completed.ToArray().ShouldBe([(JobStatus.Completed, (string?)null), (JobStatus.Completed, (string?)null)]);
        observer.Started.ToArray().ShouldBe([new PlexLibraryComparisonJobPayload(owned.Id, remote.Id),
            new PlexLibraryComparisonJobPayload(owned.Id, remote.Id)]);
        using var rebuilt = await dbFactory.CreateAsync();
        var newScope = await rebuilt.PlexComparisonScopes.SingleAsync(x => x.MediaType == PlexMediaType.MusicArtist, CancellationToken);
        newScope.CompletedAt.ShouldBeGreaterThan(scope.CompletedAt);
        (await rebuilt.PlexMusicArtistComparisons.OrderBy(x => x.RemotePlexMediaId)
            .Select(x => new { x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType }).ToListAsync(CancellationToken))
            .ShouldBe(artistHits.Select(x => new { x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType }));
        (await rebuilt.PlexMusicAlbumComparisons.OrderBy(x => x.RemotePlexMediaId)
            .Select(x => new { x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType }).ToListAsync(CancellationToken))
            .ShouldBe(albumHits.Select(x => new { x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType }));
        (await rebuilt.PlexMusicTrackComparisons.OrderBy(x => x.RemotePlexMediaId)
            .Select(x => new { x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType }).ToListAsync(CancellationToken))
            .ShouldBe(trackHits.Select(x => new { x.RemotePlexMediaId, x.OwnedPlexMediaId, x.MatchType }));
        var notifications = container.Resolve<INotificationHubService>() as MockNotificationHubService;
        notifications.ShouldNotBeNull();
        var refreshCountBeforeDisable = notifications.RefreshNotificationList.Count;

        var disable = await client.PUTAsync<SetLibraryEnabledEndpoint, SetLibraryEnabledRequest, ResultDTO<PlexLibraryDTO>>(
            new SetLibraryEnabledRequest { PlexLibraryId = owned.Id, IsEnabled = false });
        disable.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        disable.Result.IsSuccess.ShouldBeTrue();
        disable.Result.Errors.Count.ShouldBe(0);
        disable.Result.Value.ShouldNotBeNull();
        (disable.Result.Value.Id, disable.Result.Value.IsEnabled).ShouldBe((owned.Id, false));
        notifications.RefreshNotificationList.Skip(refreshCountBeforeDisable)
            .Where(x => x is RefreshDataType.PlexLibrary or RefreshDataType.PlexLibrarySyncStatus)
            .ShouldBe([RefreshDataType.PlexLibrary, RefreshDataType.PlexLibrarySyncStatus]);
        using var disabled = await dbFactory.CreateAsync();
        (await disabled.PlexComparisonScopes.Where(x => x.MediaType == PlexMediaType.MusicArtist).ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await disabled.PlexComparisonScopes.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.MediaType, x.RemotePlexLibraryId, x.OwnedPlexLibraryId, x.CompletedAt })
            .ToListAsync(CancellationToken)).ShouldBe(videoScopes);
        (await disabled.PlexMusicArtistComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await disabled.PlexMusicAlbumComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await disabled.PlexMusicTrackComparisons.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await disabled.PlexArtists.IgnoreQueryFilters().Where(x => x.PlexLibraryId == owned.Id).ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await disabled.PlexAlbums.IgnoreQueryFilters().Where(x => x.PlexLibraryId == owned.Id).ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await disabled.PlexTracks.IgnoreQueryFilters().Where(x => x.PlexLibraryId == owned.Id).ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await disabled.PlexTrackData.IgnoreQueryFilters().Where(x => x.PlexLibraryId == owned.Id).ToListAsync(CancellationToken)).ShouldBeEmpty();
        var disabledLibrary = await disabled.PlexLibraries.IgnoreIsEnabledFilter().SingleAsync(x => x.Id == owned.Id, CancellationToken);
        (disabledLibrary.SyncedAt, disabledLibrary.SyncedContentChangedAt, disabledLibrary.Outdated,
            disabledLibrary.MusicArtistCount, disabledLibrary.MusicAlbumCount, disabledLibrary.MusicTrackCount, disabledLibrary.MediaSize)
            .ShouldBe(((DateTime?)null, (long?)null, false, 0, 0, 0, 0L));
        (await disabled.PlexArtists.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Title, x.PlexApiRatingKey }).ToListAsync(CancellationToken))
            .ShouldBe(artists.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Title, x.PlexApiRatingKey }));
        (await disabled.PlexAlbums.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Title, x.PlexArtistId }).ToListAsync(CancellationToken))
            .ShouldBe(albums.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Title, x.PlexArtistId }));
        (await disabled.PlexTracks.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Title, x.PlexAlbumId }).ToListAsync(CancellationToken))
            .ShouldBe(tracks.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Title, x.PlexAlbumId }));
        (await disabled.PlexTrackData.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.PlexTrackId)
            .Select(x => x.PlexTrackId).ToListAsync(CancellationToken))
            .ShouldBe(tracks.Where(x => x.PlexLibraryId == remote.Id).OrderBy(x => x.Id).Select(x => x.Id));
        (await disabled.PlexMovies.OrderBy(x => x.Id).Select(x => new { x.Id, x.Title, x.PlexLibraryId, x.PlexApiRatingKey, x.MediaSize })
            .ToListAsync(CancellationToken)).ShouldBe(movieControl);
        (await disabled.PlexTvShows.OrderBy(x => x.Id).Select(x => new { x.Id, x.Title, x.PlexLibraryId, x.PlexApiRatingKey, x.ChildCount })
            .ToListAsync(CancellationToken)).ShouldBe(tvControl);
        (await disabled.PlexTvShowEpisodes.OrderBy(x => x.Id).Select(x => new { x.Id, x.Title, x.PlexLibraryId, x.TvShowSeasonId, x.MediaSize })
            .ToListAsync(CancellationToken)).ShouldBe(episodeControl);
        (await disabled.PlexMovieComparisons.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt, x.HitState })
            .ToListAsync(CancellationToken)).ShouldBe(movieHitControl);
        (await disabled.PlexTvShowComparisons.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt, x.HitState })
            .ToListAsync(CancellationToken)).ShouldBe(showHitControl);
        (await disabled.PlexSeasonComparisons.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt, x.HitState })
            .ToListAsync(CancellationToken)).ShouldBe(seasonHitControl);
        (await disabled.PlexEpisodeComparisons.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.RemotePlexMediaId, x.OwnedPlexMediaId, x.ComparedAt, x.HitState })
            .ToListAsync(CancellationToken)).ShouldBe(episodeHitControl);
        (await scheduler.GetJobKeys(JobTypes.LibraryComparisonJob, CancellationToken)).ShouldBeEmpty();
        var notCompared = await client.GETAsync<GetMediaComparisonDetailsEndpoint, GetMediaComparisonDetailsEndpointRequest, ResultDTO<PlexMediaComparisonDetailsDTO>>(
            new GetMediaComparisonDetailsEndpointRequest(currentArtistId, PlexMediaType.MusicArtist));
        notCompared.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        notCompared.Result.IsSuccess.ShouldBeTrue();
        notCompared.Result.Errors.Count.ShouldBe(0);
        notCompared.Result.Value.ShouldNotBeNull();
        notCompared.Result.Value.State.ShouldBe(PlexMediaComparisonState.NotCompared);
        notCompared.Result.Value.Rows.ShouldBeEmpty();
        // Only the resync adds external Music reads; HTTP browse/detail/disable never contact Plex.
        fixture.Requests.Skip(requestCountBeforeBrowse).Select(x => x.Path).Distinct().ShouldBe([fixture.RemoteLibraryPath]);
        fixture.Requests.Select(x => x.Method).Distinct().ShouldBe([HttpMethod.Get]);
        fixture.Requests.Select(x => x.TokenPresent).Distinct().ShouldBe([true]);
        fixture.Requests.Count.ShouldBe(18);
        fixture.Requests.GroupBy(x => (x.Path, x.Type)).Select(x => (x.Key.Path, x.Key.Type, Count: x.Count()))
            .OrderBy(x => x.Path).ThenBy(x => x.Type).ShouldBe(new[] { fixture.OwnedLibraryPath, fixture.RemoteLibraryPath }
                .SelectMany(path => new[] { "8", "9", "10" }.Select(type => (Path: path, Type: type,
                    Count: path == fixture.OwnedLibraryPath ? 2 : 4)))
                .OrderBy(x => x.Path).ThenBy(x => x.Type));
    }

    private sealed class ComparisonJobObserver(string name = nameof(ComparisonJobObserver)) : IJobListener
    {
        public string Name => name;
        public ConcurrentQueue<PlexLibraryComparisonJobPayload> Started { get; } = new();
        public ConcurrentQueue<(JobStatus Status, string? Error)> Completed { get; } = new();
        public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (context.MergedJobDataMap.GetPayload<PlexLibraryComparisonJobPayload>() is { } payload)
                Started.Enqueue(payload);
            return Task.CompletedTask;
        }
        public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
        {
            var result = context.Result as BackgroundJobResult;
            Completed.Enqueue((jobException is null ? result?.Status ?? JobStatus.Failed : JobStatus.Failed,
                jobException?.ToString() ?? result?.ErrorSummary));
            return Task.CompletedTask;
        }
    }

    private sealed class MusicResponseFixture
    {
        public string OwnedLibraryPath { get; set; } = string.Empty;
        public string RemoteLibraryPath { get; set; } = string.Empty;
        public Dictionary<string, int> ArtistIndexes { get; } = new();
        public Dictionary<string, int> AlbumIndexes { get; } = new();
        public Dictionary<(string Path, string Type), JsonNode> FullResponses { get; } = new();
        public ConcurrentQueue<(string Path, string Type, HttpMethod Method, bool TokenPresent)> Requests { get; } = new();
    }

    private sealed class MusicFixtureHandler(HttpMessageHandler inner, MusicResponseFixture fixture) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker _inner = new(inner, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await _inner.SendAsync(request, cancellationToken);
            var path = request.RequestUri!.AbsolutePath;
            if (path != fixture.OwnedLibraryPath && path != fixture.RemoteLibraryPath)
                return response;
            var query = request.ParseQueryToDictionary();
            var type = query["type"];
            fixture.Requests.Enqueue((path, type, request.Method,
                request.Headers.Contains("X-Plex-Token") || query.ContainsKey("X-Plex-Token")));
            var root = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))!;
            var start = int.Parse(query["X-Plex-Container-Start"]);
            var size = int.Parse(query["X-Plex-Container-Size"]);
            lock (fixture)
            {
                // The count probe contains the complete generated hierarchy. Slice AFTER dropping the absent file,
                // otherwise the production five-item request would hide the sixth generated track as well.
                if (size > 0)
                    root = fixture.FullResponses[(path, type)].DeepClone();
                var container = root["MediaContainer"]!;
                var metadata = container["Metadata"]!.AsArray();
                var ordered = metadata.OrderBy(x => int.Parse(x!["ratingKey"]!.GetValue<string>())).ToList();
                foreach (var item in ordered)
                {
                    item!["guid"] = $"plex://{item["type"]!.GetValue<string>()}/{item["ratingKey"]!.GetValue<string>()}";
                    item["Guid"] = new JsonArray();
                }
                if (type == "8")
                {
                    for (var i = 0; i < ordered.Count; i++)
                    {
                        var item = ordered[i]!;
                        fixture.ArtistIndexes[item["ratingKey"]!.GetValue<string>()] = i;
                        var title = i switch { 0 => "Alpha Complete", 1 => "B Partial", _ => path == fixture.OwnedLibraryPath ? "C Owned only" : "C Remote only" };
                        item["title"] = title;
                        item["titleSort"] = title;
                    }
                }
                else if (type == "9")
                {
                    foreach (var item in ordered)
                    {
                        fixture.AlbumIndexes[item!["ratingKey"]!.GetValue<string>()] = fixture.ArtistIndexes[item["parentRatingKey"]!.GetValue<string>()];
                        item["title"] = "Studio edition";
                        item["titleSort"] = "Studio edition";
                        item["year"] = 2001;
                        item["originallyAvailableAt"] = "2001-01-01";
                    }
                }
                else if (type == "10")
                {
                    foreach (var group in ordered.GroupBy(x => x!["parentRatingKey"]!.GetValue<string>()))
                    {
                        var index = 0;
                        foreach (var item in group)
                        {
                            index++;
                            item!["title"] = $"Track {index}";
                            item["titleSort"] = $"Track {index}";
                            item["index"] = index;
                            item["parentIndex"] = 1;
                            if (path == fixture.OwnedLibraryPath && fixture.AlbumIndexes[group.Key] == 1 && index == 2)
                                metadata.Remove(item);
                        }
                    }
                    if (path == fixture.OwnedLibraryPath)
                        container["totalSize"] = 5;
                }
                if (size == 0)
                    fixture.FullResponses[(path, type)] = root.DeepClone();
                else
                    container["Metadata"] = new JsonArray(metadata.Skip(start).Take(size).Select(x => x!.DeepClone()).ToArray());
                container["size"] = container["Metadata"]!.AsArray().Count;
                container["offset"] = start;
            }
            response.Content.Dispose();
            response.Content = new StringContent(root.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
            return response;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
