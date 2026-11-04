namespace Reaparr.IntegrationTests;

public class MediaOverviewServerVisibilityIntegrationTests : BaseIntegrationTests
{
    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.TvShow)]
    [Arguments(PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldExcludeExplicitLibrary_WhenParentServerIsDisabled(PlexMediaType mediaType)
    {
        // Arrange
        var seed = new Seed(11042 + (int)mediaType);
        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.HttpClientOptions = (handler, _) => handler.SetupIdentityRequest(seed);
                config.DatabaseOptions = database =>
                {
                    database.PlexAccountCount = 1;
                    database.PlexServerCount = 2;
                    database.PlexServerConnectionPerServerCount = 1;
                    database.PlexMovieLibraryCount = 1;
                    database.PlexTvShowLibraryCount = 1;
                    database.PlexMusicLibraryCount = 1;
                    database.PlexPhotoLibraryCount = 1;
                    database.PlexOtherVideoLibraryCount = 1;
                    database.MovieCount = 2;
                    database.TvShowCount = 2;
                    database.TvShowSeasonCount = 1;
                    database.TvShowEpisodeCount = 1;
                    database.MusicArtistCount = 2;
                    database.MusicAlbumCount = 1;
                    database.MusicTrackCount = 1;
                    database.PhotoAlbumCount = 2;
                    database.PhotoCount = 1;
                    database.OtherVideoCount = 2;
                };
            }
        );
        var dbFactory = container.Resolve<IReaparrDbContextFactory>();
        using var beforeContext = await dbFactory.CreateAsync();
        var libraries = await beforeContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .Where(x => x.Type == mediaType)
            .OrderBy(x => x.PlexServerId)
            .ThenBy(x => x.Id)
            .Select(x =>
                new
                {
                    x.Id,
                    x.PlexServerId,
                    x.Type,
                    x.IsEnabled,
                    x.MediaSize,
                    x.MovieCount,
                    x.TvShowCount,
                    x.SeasonCount,
                    x.EpisodeCount,
                    x.MusicArtistCount,
                    x.MusicAlbumCount,
                    x.MusicTrackCount,
                    x.PhotoAlbumCount,
                    x.PhotoImageCount,
                    x.PhotoClipCount,
                    x.OtherVideoCount,
                }
            )
            .ToArrayAsync(CancellationToken);
        libraries.Length.ShouldBe(2);
        libraries.ShouldAllBe(x => x.IsEnabled);
        libraries.Select(x => x.PlexServerId).Distinct().Count().ShouldBe(2);

        var disabledLibrary = libraries[0];
        var enabledLibrary = libraries[1];
        var libraryIds = libraries.Select(x => x.Id).ToArray();
        var serverIds = libraries.Select(x => x.PlexServerId).ToArray();
        var expectedLibraries = libraries
            .Select(x =>
                (
                    Id: x.Id,
                    PlexServerId: x.PlexServerId,
                    Type: x.Type,
                    IsEnabled: x.IsEnabled,
                    MediaSize: x.MediaSize,
                    MovieCount: x.MovieCount,
                    TvShowCount: x.TvShowCount,
                    SeasonCount: x.SeasonCount,
                    EpisodeCount: x.EpisodeCount,
                    MusicArtistCount: x.MusicArtistCount,
                    MusicAlbumCount: x.MusicAlbumCount,
                    MusicTrackCount: x.MusicTrackCount,
                    PhotoAlbumCount: x.PhotoAlbumCount,
                    PhotoImageCount: x.PhotoImageCount,
                    PhotoClipCount: x.PhotoClipCount,
                    OtherVideoCount: x.OtherVideoCount
                )
            )
            .ToArray();
        var expectedServers = (
            await beforeContext
                .PlexServers.IgnoreIsEnabledFilter()
                .Where(x => serverIds.Contains(x.Id))
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.IsEnabled })
                .ToArrayAsync(CancellationToken)
        )
            .Select(x => (Id: x.Id, IsEnabled: x.IsEnabled))
            .ToArray();
        expectedServers.Length.ShouldBe(2);
        expectedServers.ShouldAllBe(x => x.IsEnabled);
        var expectedServerStatuses = await beforeContext
            .PlexServerStatuses.Where(x => serverIds.Contains(x.PlexServerId))
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexServerId,
                x.PlexServerConnectionId,
                x.IsSuccessful,
                x.StatusCode,
                x.StatusMessage,
                x.LastChecked,
            })
            .ToArrayAsync(CancellationToken);
        expectedServerStatuses.Length.ShouldBe(2);
        expectedServerStatuses.ShouldAllBe(x => x.IsSuccessful);
        var media = mediaType switch
        {
            PlexMediaType.Movie => (await beforeContext
                    .PlexMovies.OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.PlexLibraryId, x.PlexServerId, x.MediaSize })
                    .ToArrayAsync(CancellationToken))
                .Select(x => (Id: x.Id, LibraryId: x.PlexLibraryId, ServerId: x.PlexServerId, MediaSize: x.MediaSize))
                .ToArray(),
            PlexMediaType.TvShow => (await beforeContext
                    .PlexTvShows.OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.PlexLibraryId, x.PlexServerId, x.MediaSize })
                    .ToArrayAsync(CancellationToken))
                .Select(x => (Id: x.Id, LibraryId: x.PlexLibraryId, ServerId: x.PlexServerId, MediaSize: x.MediaSize))
                .ToArray(),
            PlexMediaType.MusicArtist => (await beforeContext
                    .PlexMusicArtists.OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.PlexLibraryId, x.PlexServerId, x.MediaSize })
                    .ToArrayAsync(CancellationToken))
                .Select(x => (Id: x.Id, LibraryId: x.PlexLibraryId, ServerId: x.PlexServerId, MediaSize: x.MediaSize))
                .ToArray(),
            PlexMediaType.PhotoAlbum => (await beforeContext
                    .PlexPhotoAlbums.OrderBy(x => x.Id)
                    .Select(
                        x =>
                            new
                            {
                                x.Id,
                                x.PlexLibraryId,
                                x.PlexServerId,
                                MediaSize = x.Memberships.Sum(membership => membership.PlexPhotoMediaData!.Size),
                            }
                    )
                    .ToArrayAsync(CancellationToken))
                .Select(x => (Id: x.Id, LibraryId: x.PlexLibraryId, ServerId: x.PlexServerId, MediaSize: x.MediaSize))
                .ToArray(),
            PlexMediaType.OtherVideos => (await beforeContext
                    .PlexOtherVideos.OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.PlexLibraryId, x.PlexServerId, x.MediaSize })
                    .ToArrayAsync(CancellationToken))
                .Select(x => (Id: x.Id, LibraryId: x.PlexLibraryId, ServerId: x.PlexServerId, MediaSize: x.MediaSize))
                .ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(mediaType), mediaType, null),
        };
        media.Length.ShouldBe(4);
        var disabledMedia = media.Where(x => x.LibraryId == disabledLibrary.Id).ToArray();
        var enabledMedia = media.Where(x => x.LibraryId == enabledLibrary.Id).ToArray();
        disabledMedia.Length.ShouldBe(2);
        enabledMedia.Length.ShouldBe(2);
        disabledMedia.ShouldAllBe(x => x.ServerId == disabledLibrary.PlexServerId);
        enabledMedia.ShouldAllBe(x => x.ServerId == enabledLibrary.PlexServerId);
        var expectedMedia = media.Select(x => (x.Id, x.LibraryId, x.ServerId, x.MediaSize)).ToArray();
        var expectedAccountLibraries = (
            await beforeContext
                .PlexAccountLibraries.Where(x => libraryIds.Contains(x.PlexLibraryId))
                .OrderBy(x => x.PlexLibraryId)
                .Select(x => new { x.PlexAccountId, x.PlexLibraryId, x.PlexServerId, x.IsLibraryOwned })
                .ToArrayAsync(CancellationToken)
        )
            .Select(x =>
                (
                    x.PlexAccountId,
                    x.PlexLibraryId,
                    x.PlexServerId,
                    x.IsLibraryOwned
                )
            )
            .ToArray();
        expectedAccountLibraries.Length.ShouldBe(2);
        expectedAccountLibraries.ShouldAllBe(x => x.IsLibraryOwned);
        var expectedAccountServers = (
            await beforeContext
                .PlexAccountServers.Where(x => serverIds.Contains(x.PlexServerId))
                .OrderBy(x => x.PlexServerId)
                .Select(x => new { x.PlexAccountId, x.PlexServerId, x.IsServerOwned })
                .ToArrayAsync(CancellationToken)
        )
            .Select(x => (x.PlexAccountId, x.PlexServerId, x.IsServerOwned))
            .ToArray();
        expectedAccountServers.Length.ShouldBe(2);
        expectedAccountServers.ShouldAllBe(x => x.IsServerOwned);

        var rebuild = await container.Resolve<ICommandExecutor>().Send(
            new RebuildMediaOverviewCommand([mediaType]),
            CancellationToken
        );
        rebuild.IsSuccess.ShouldBeTrue();
        rebuild.Errors.Count.ShouldBe(0);

        var expectedSnapshotRows = media
            .Select(x => (LibraryId: x.LibraryId, MediaId: x.Id))
            .OrderBy(x => x.LibraryId)
            .ThenBy(x => x.MediaId)
            .ToArray();
        using (var snapshotContext = await dbFactory.CreateAsync())
        {
            var snapshotRows = mediaType switch
            {
                PlexMediaType.Movie => (await snapshotContext.MediaOverviewMovieSnapshots
                        .OrderBy(x => x.PlexLibraryId)
                        .ThenBy(x => x.PlexMovieId)
                        .Select(x => new { x.PlexLibraryId, MediaId = x.PlexMovieId })
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (LibraryId: x.PlexLibraryId, x.MediaId))
                    .ToArray(),
                PlexMediaType.TvShow => (await snapshotContext.MediaOverviewTvShowSnapshots
                        .OrderBy(x => x.PlexLibraryId)
                        .ThenBy(x => x.PlexTvShowId)
                        .Select(x => new { x.PlexLibraryId, MediaId = x.PlexTvShowId })
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (LibraryId: x.PlexLibraryId, x.MediaId))
                    .ToArray(),
                PlexMediaType.MusicArtist => (await snapshotContext.MediaOverviewMusicArtistSnapshots
                        .OrderBy(x => x.PlexLibraryId)
                        .ThenBy(x => x.PlexArtistId)
                        .Select(x => new { x.PlexLibraryId, MediaId = x.PlexArtistId })
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (LibraryId: x.PlexLibraryId, x.MediaId))
                    .ToArray(),
                PlexMediaType.PhotoAlbum => (await snapshotContext.MediaOverviewPhotoAlbumSnapshots
                        .OrderBy(x => x.PlexLibraryId)
                        .ThenBy(x => x.PlexPhotoAlbumId)
                        .Select(x => new { x.PlexLibraryId, MediaId = x.PlexPhotoAlbumId })
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (LibraryId: x.PlexLibraryId, x.MediaId))
                    .ToArray(),
                PlexMediaType.OtherVideos => (await snapshotContext.MediaOverviewOtherVideoSnapshots
                        .OrderBy(x => x.PlexLibraryId)
                        .ThenBy(x => x.PlexOtherVideoId)
                        .Select(x => new { x.PlexLibraryId, MediaId = x.PlexOtherVideoId })
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (LibraryId: x.PlexLibraryId, x.MediaId))
                    .ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(mediaType), mediaType, null),
            };
            snapshotRows.ShouldBe(expectedSnapshotRows);
        }

        using var client = container.GetApiClient();
        await client.SignIn();
        var expectedEnabledMediaSize = enabledMedia.Sum(x => x.MediaSize);
        var expectedEnabledStats =
            (
                TotalCount: enabledMedia.Length,
                MediaCount: enabledMedia.Length,
                MovieCount: mediaType == PlexMediaType.Movie ? enabledMedia.Length : 0,
                TvShowCount: mediaType == PlexMediaType.TvShow ? enabledMedia.Length : 0,
                SeasonCount: mediaType == PlexMediaType.TvShow ? enabledLibrary.SeasonCount : 0,
                EpisodeCount: mediaType == PlexMediaType.TvShow ? enabledLibrary.EpisodeCount : 0,
                TotalMovieCount: mediaType == PlexMediaType.Movie ? enabledMedia.Length : enabledLibrary.MovieCount,
                TotalTvShowCount: mediaType == PlexMediaType.TvShow ? enabledMedia.Length : enabledLibrary.TvShowCount,
                TotalSeasonCount: enabledLibrary.SeasonCount,
                TotalEpisodeCount: enabledLibrary.EpisodeCount,
                MediaSize: expectedEnabledMediaSize,
                TotalMediaSize: expectedEnabledMediaSize
            );

        // Act and assert both overview paths before the parent is disabled.
        foreach (var sort in new[] { "title:asc", "title:asc,addedAt:asc" })
        {
            foreach (var library in libraries)
            {
                var response = await client.GETAsync<
                    GetAllMediaByTypeEndpoint,
                    GetAllMediaByTypeRequest,
                    ResultDTO<PlexMediaStatisticsDTO>
                >(
                    new GetAllMediaByTypeRequest
                    {
                        MediaType = mediaType,
                        PlexLibraryId = library.Id,
                        Page = 1,
                        PageSize = 10,
                        Sort = sort,
                        FilterOwnedMedia = false,
                        FilterOfflineMedia = false,
                    }
                );
                response.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
                response.Result.IsSuccess.ShouldBeTrue();
                response.Result.Errors.Count.ShouldBe(0);
                var value = response.Result.Value.ShouldNotBeNull();
                var expected = library.Id == disabledLibrary.Id ? disabledMedia : enabledMedia;
                var expectedMediaSize = expected.Sum(x => x.MediaSize);
                (
                    value.Page,
                    value.PageSize,
                    value.TotalCount,
                    value.MediaCount,
                    value.MediaSize,
                    value.TotalMediaSize
                ).ShouldBe((1, 10, expected.Length, expected.Length, expectedMediaSize, expectedMediaSize));
                value.MediaList
                    .OrderBy(x => x.Id)
                    .Select(x => (x.Id, x.PlexLibraryId, x.PlexServerId, x.Type))
                    .ShouldBe(
                        expected
                            .OrderBy(x => x.Id)
                            .Select(x => (x.Id, x.LibraryId, x.ServerId, mediaType))
                    );
            }
        }

        // Disable only the parent server, leaving both libraries, their media and access links intact.
        using (var updateContext = await dbFactory.CreateAsync())
        {
            var updated = await updateContext
                .PlexServers.IgnoreIsEnabledFilter()
                .Where(x => x.Id == disabledLibrary.PlexServerId)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);
            updated.ShouldBe(1);
        }

        // Assert
        using (var afterDisableContext = await dbFactory.CreateAsync())
        {
            var afterServers = (
                await afterDisableContext
                    .PlexServers.IgnoreIsEnabledFilter()
                    .Where(x => serverIds.Contains(x.Id))
                    .OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.IsEnabled })
                    .ToArrayAsync(CancellationToken)
            )
                .Select(x => (Id: x.Id, IsEnabled: x.IsEnabled))
                .ToArray();
            afterServers.ShouldBe(
                expectedServers
                    .Select(x => (x.Id, IsEnabled: x.Id == disabledLibrary.PlexServerId ? false : x.IsEnabled))
                    .ToArray()
            );

            var afterLibraries = (
                await afterDisableContext
                    .PlexLibraries.IgnoreIsEnabledFilter()
                    .Where(x => libraryIds.Contains(x.Id))
                    .OrderBy(x => x.PlexServerId)
                    .ThenBy(x => x.Id)
                    .Select(
                        x =>
                            new
                            {
                                x.Id,
                                x.PlexServerId,
                                x.Type,
                                x.IsEnabled,
                                x.MediaSize,
                                x.MovieCount,
                                x.TvShowCount,
                                x.SeasonCount,
                                x.EpisodeCount,
                                x.MusicArtistCount,
                                x.MusicAlbumCount,
                                x.MusicTrackCount,
                                x.PhotoAlbumCount,
                                x.PhotoImageCount,
                                x.PhotoClipCount,
                                x.OtherVideoCount,
                            }
                    )
                    .ToArrayAsync(CancellationToken)
            )
                .Select(x =>
                    (
                        x.Id,
                        x.PlexServerId,
                        x.Type,
                        x.IsEnabled,
                        x.MediaSize,
                        x.MovieCount,
                        x.TvShowCount,
                        x.SeasonCount,
                        x.EpisodeCount,
                        x.MusicArtistCount,
                        x.MusicAlbumCount,
                        x.MusicTrackCount,
                        x.PhotoAlbumCount,
                        x.PhotoImageCount,
                        x.PhotoClipCount,
                        x.OtherVideoCount
                    )
                )
                .ToArray();
            afterLibraries.ShouldBe(expectedLibraries);
            afterLibraries.ShouldAllBe(x => x.IsEnabled);

            var afterMedia = mediaType switch
            {
                PlexMediaType.Movie => (await afterDisableContext
                        .PlexMovies.OrderBy(x => x.Id)
                        .Select(x => new { x.Id, x.PlexLibraryId, x.PlexServerId, x.MediaSize })
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (x.Id, x.PlexLibraryId, x.PlexServerId, x.MediaSize))
                    .ToArray(),
                PlexMediaType.TvShow => (await afterDisableContext
                        .PlexTvShows.OrderBy(x => x.Id)
                        .Select(x => new { x.Id, x.PlexLibraryId, x.PlexServerId, x.MediaSize })
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (x.Id, x.PlexLibraryId, x.PlexServerId, x.MediaSize))
                    .ToArray(),
                PlexMediaType.MusicArtist => (await afterDisableContext
                        .PlexMusicArtists.OrderBy(x => x.Id)
                        .Select(x => new { x.Id, x.PlexLibraryId, x.PlexServerId, x.MediaSize })
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (x.Id, x.PlexLibraryId, x.PlexServerId, x.MediaSize))
                    .ToArray(),
                PlexMediaType.PhotoAlbum => (await afterDisableContext
                        .PlexPhotoAlbums.OrderBy(x => x.Id)
                        .Select(
                            x =>
                                new
                                {
                                    x.Id,
                                    x.PlexLibraryId,
                                    x.PlexServerId,
                                    MediaSize = x.Memberships.Sum(membership => membership.PlexPhotoMediaData!.Size),
                                }
                        )
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (x.Id, x.PlexLibraryId, x.PlexServerId, x.MediaSize))
                    .ToArray(),
                PlexMediaType.OtherVideos => (await afterDisableContext
                        .PlexOtherVideos.OrderBy(x => x.Id)
                        .Select(x => new { x.Id, x.PlexLibraryId, x.PlexServerId, x.MediaSize })
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (x.Id, x.PlexLibraryId, x.PlexServerId, x.MediaSize))
                    .ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(mediaType), mediaType, null),
            };
            afterMedia.ShouldBe(expectedMedia);

            var afterAccountLibraries = (
                await afterDisableContext
                    .PlexAccountLibraries.Where(x => libraryIds.Contains(x.PlexLibraryId))
                    .OrderBy(x => x.PlexLibraryId)
                    .Select(x => new { x.PlexAccountId, x.PlexLibraryId, x.PlexServerId, x.IsLibraryOwned })
                    .ToArrayAsync(CancellationToken)
            )
                .Select(x => (x.PlexAccountId, x.PlexLibraryId, x.PlexServerId, x.IsLibraryOwned))
                .ToArray();
            afterAccountLibraries.ShouldBe(expectedAccountLibraries);
            var afterAccountServers = (
                await afterDisableContext
                    .PlexAccountServers.Where(x => serverIds.Contains(x.PlexServerId))
                    .OrderBy(x => x.PlexServerId)
                    .Select(x => new { x.PlexAccountId, x.PlexServerId, x.IsServerOwned })
                    .ToArrayAsync(CancellationToken)
            )
                .Select(x => (x.PlexAccountId, x.PlexServerId, x.IsServerOwned))
                .ToArray();
            afterAccountServers.ShouldBe(expectedAccountServers);
            var afterServerStatuses = await afterDisableContext
                .PlexServerStatuses.Where(x => serverIds.Contains(x.PlexServerId))
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PlexServerId,
                    x.PlexServerConnectionId,
                    x.IsSuccessful,
                    x.StatusCode,
                    x.StatusMessage,
                    x.LastChecked,
                })
                .ToArrayAsync(CancellationToken);
            afterServerStatuses.ShouldBe(expectedServerStatuses);
        }

        // Rebuild the real snapshots after disabling the parent and repeat both query paths.
        var rebuiltAfterDisable = await container.Resolve<ICommandExecutor>().Send(
            new RebuildMediaOverviewCommand([mediaType]),
            CancellationToken
        );
        rebuiltAfterDisable.IsSuccess.ShouldBeTrue();
        rebuiltAfterDisable.Errors.Count.ShouldBe(0);

        using (var rebuiltContext = await dbFactory.CreateAsync())
        {
            var snapshotRows = mediaType switch
            {
                PlexMediaType.Movie => (await rebuiltContext.MediaOverviewMovieSnapshots
                        .OrderBy(x => x.PlexLibraryId)
                        .ThenBy(x => x.PlexMovieId)
                        .Select(x => new { x.PlexLibraryId, MediaId = x.PlexMovieId })
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (LibraryId: x.PlexLibraryId, x.MediaId))
                    .ToArray(),
                PlexMediaType.TvShow => (await rebuiltContext.MediaOverviewTvShowSnapshots
                        .OrderBy(x => x.PlexLibraryId)
                        .ThenBy(x => x.PlexTvShowId)
                        .Select(x => new { x.PlexLibraryId, MediaId = x.PlexTvShowId })
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (LibraryId: x.PlexLibraryId, x.MediaId))
                    .ToArray(),
                PlexMediaType.MusicArtist => (await rebuiltContext.MediaOverviewMusicArtistSnapshots
                        .OrderBy(x => x.PlexLibraryId)
                        .ThenBy(x => x.PlexArtistId)
                        .Select(x => new { x.PlexLibraryId, MediaId = x.PlexArtistId })
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (LibraryId: x.PlexLibraryId, x.MediaId))
                    .ToArray(),
                PlexMediaType.PhotoAlbum => (await rebuiltContext.MediaOverviewPhotoAlbumSnapshots
                        .OrderBy(x => x.PlexLibraryId)
                        .ThenBy(x => x.PlexPhotoAlbumId)
                        .Select(x => new { x.PlexLibraryId, MediaId = x.PlexPhotoAlbumId })
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (LibraryId: x.PlexLibraryId, x.MediaId))
                    .ToArray(),
                PlexMediaType.OtherVideos => (await rebuiltContext.MediaOverviewOtherVideoSnapshots
                        .OrderBy(x => x.PlexLibraryId)
                        .ThenBy(x => x.PlexOtherVideoId)
                        .Select(x => new { x.PlexLibraryId, MediaId = x.PlexOtherVideoId })
                        .ToArrayAsync(CancellationToken))
                    .Select(x => (LibraryId: x.PlexLibraryId, x.MediaId))
                    .ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(mediaType), mediaType, null),
            };
            snapshotRows.ShouldBe(expectedSnapshotRows);
        }

        // Assert the disabled explicit page is empty and the enabled control retains exact rows after rebuild.
        foreach (var sort in new[] { "title:asc", "title:asc,addedAt:asc" })
        {
            foreach (var library in libraries)
            {
                var response = await client.GETAsync<
                    GetAllMediaByTypeEndpoint,
                    GetAllMediaByTypeRequest,
                    ResultDTO<PlexMediaStatisticsDTO>
                >(
                    new GetAllMediaByTypeRequest
                    {
                        MediaType = mediaType,
                        PlexLibraryId = library.Id,
                        Page = 1,
                        PageSize = 10,
                        Sort = sort,
                        FilterOwnedMedia = false,
                        FilterOfflineMedia = false,
                    }
                );
                response.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
                response.Result.IsSuccess.ShouldBeTrue();
                response.Result.Errors.Count.ShouldBe(0);
                var value = response.Result.Value.ShouldNotBeNull();
                var isDisabled = library.Id == disabledLibrary.Id;
                if (isDisabled)
                {
                    (
                        value.Page,
                        value.PageSize,
                        value.TotalCount,
                        value.MediaCount,
                        value.MovieCount,
                        value.TvShowCount,
                        value.SeasonCount,
                        value.EpisodeCount,
                        value.TotalMovieCount,
                        value.TotalTvShowCount,
                        value.TotalSeasonCount,
                        value.TotalEpisodeCount,
                        value.MediaSize,
                        value.TotalMediaSize
                    ).ShouldBe((1, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0L, 0L));
                    value.MediaList.ShouldBeEmpty();
                    value.NavigationIndexes.ShouldBeEmpty();
                    value.Roles.ShouldBeEmpty();
                    value.Countries.ShouldBeEmpty();
                    value.Genres.ShouldBeEmpty();
                    value.Qualities.ShouldBeEmpty();
                }
                else
                {
                    (
                        value.Page,
                        value.PageSize,
                        value.TotalCount,
                        value.MediaCount,
                        value.MovieCount,
                        value.TvShowCount,
                        value.SeasonCount,
                        value.EpisodeCount,
                        value.TotalMovieCount,
                        value.TotalTvShowCount,
                        value.TotalSeasonCount,
                        value.TotalEpisodeCount,
                        value.MediaSize,
                        value.TotalMediaSize
                    ).ShouldBe(
                        (
                            1,
                            10,
                            expectedEnabledStats.TotalCount,
                            expectedEnabledStats.MediaCount,
                            expectedEnabledStats.MovieCount,
                            expectedEnabledStats.TvShowCount,
                            expectedEnabledStats.SeasonCount,
                            expectedEnabledStats.EpisodeCount,
                            expectedEnabledStats.TotalMovieCount,
                            expectedEnabledStats.TotalTvShowCount,
                            expectedEnabledStats.TotalSeasonCount,
                            expectedEnabledStats.TotalEpisodeCount,
                            expectedEnabledStats.MediaSize,
                            expectedEnabledStats.TotalMediaSize
                        )
                    );
                    value.MediaList
                        .OrderBy(x => x.Id)
                        .Select(x => (x.Id, x.PlexLibraryId, x.PlexServerId, x.Type))
                        .ShouldBe(
                            enabledMedia
                                .OrderBy(x => x.Id)
                                .Select(x => (x.Id, x.LibraryId, x.ServerId, mediaType))
                        );
                }
            }
        }
    }
}
