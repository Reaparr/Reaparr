using Quartz.Impl.Matchers;

namespace Reaparr.IntegrationTests;

public class GetAllMediaByTypeEndpointIntegrationTests : BaseIntegrationTests
{
    [Test]
    [Arguments(false, "The LÉON: Professional Professional", new[] { 0 })]
    [Arguments(true, "The LÉON: Professional Professional", new[] { 0 })]
    [Arguments(false, "the and !", new[] { 0, 1, 2, 3 })]
    [Arguments(true, "the and !", new[] { 0, 1, 2, 3 })]
    public async Task ShouldNormalizeSearchWithoutLosingLibraryScope_WhenUsingEitherOverviewPath(
        bool useSnapshot,
        string search,
        int[] expectedIndexes
    )
    {
        // Arrange
        var seed = new Seed(29104);
        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.HttpClientOptions = (handler, _) => handler.SetupIdentityRequest(seed);
                config.DatabaseOptions = data =>
                {
                    data.PlexAccountCount = 1;
                    data.PlexServerCount = 1;
                    data.PlexMovieLibraryCount = 2;
                    data.MovieCount = 4;
                };
            }
        );
        using var context = container.DbContext;
        var libraryIds = await context.PlexLibraries
            .Where(x => x.Type == PlexMediaType.Movie)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToArrayAsync(CancellationToken);
        libraryIds.Length.ShouldBe(2);
        var movies = await context.PlexMovies.AsNoTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        string[] titles = ["The Léon: Professional", "Leon Amateur", "Professional Practice", "Arrival"];
        string[] normalizedTitles = ["leon professional", "leon amateur", "professional practice", "arrival"];
        foreach (var libraryId in libraryIds)
        {
            var libraryMovies = movies.Where(x => x.PlexLibraryId == libraryId).ToArray();
            libraryMovies.Length.ShouldBe(titles.Length);
            for (var index = 0; index < libraryMovies.Length; index++)
            {
                var movieId = libraryMovies[index].Id;
                var title = titles[index];
                var searchTitle = title.ToSearchTitle();
                await context.PlexMovies.Where(x => x.Id == movieId).ExecuteUpdateAsync(
                    update => update.SetProperty(x => x.Title, title).SetProperty(x => x.SearchTitle, searchTitle),
                    CancellationToken
                );
            }
        }
        movies = await context.PlexMovies.AsNoTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        foreach (var libraryId in libraryIds)
            movies.Where(x => x.PlexLibraryId == libraryId).Select(x => x.SearchTitle).ShouldBe(normalizedTitles);
        var before = movies.Select(x => new
        {
            x.Id,
            x.Title,
            x.SearchTitle,
            x.PlexLibraryId,
            x.PlexServerId,
        }).ToArray();
        var libraryMoviesToSearch = movies.Where(x => x.PlexLibraryId == libraryIds[0]).ToArray();
        var expectedMovies = expectedIndexes.Select(index => libraryMoviesToSearch[index]).OrderBy(x => x.Id).ToArray();
        if (useSnapshot)
        {
            var rebuilt = await container.Resolve<ICommandExecutor>().Send(
                new RebuildMediaOverviewCommand([PlexMediaType.Movie]),
                CancellationToken
            );
            rebuilt.IsSuccess.ShouldBeTrue();
            rebuilt.Errors.Count.ShouldBe(0);
        }
        int[] expectedSnapshotIds = useSnapshot ? movies.Select(x => x.Id).ToArray() : [];
        (await context.MediaOverviewMovieSnapshots.OrderBy(x => x.PlexMovieId)
            .Select(x => x.PlexMovieId).ToArrayAsync(CancellationToken))
            .ShouldBe(expectedSnapshotIds);
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        var response = await client.GETAsync<
            GetAllMediaByTypeEndpoint,
            GetAllMediaByTypeRequest,
            ResultDTO<PlexMediaStatisticsDTO>
        >(
            new GetAllMediaByTypeRequest
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = libraryIds[0],
                Search = search,
                Sort = "title:asc",
                Page = 1,
                PageSize = 10,
            }
        );

        // Assert
        response.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Errors.Count.ShouldBe(0);
        var value = response.Result.Value.ShouldNotBeNull();
        (value.Page, value.PageSize, value.TotalCount, value.MediaSize)
            .ShouldBe((1, 10, expectedMovies.Length, expectedMovies.Sum(x => x.MediaSize)));
        value.MediaList.OrderBy(x => x.Id)
            .Select(x => (x.Id, x.Title, x.SearchTitle, x.PlexLibraryId, x.PlexServerId, x.Type))
            .ShouldBe(expectedMovies.Select(x =>
                (x.Id, x.Title, x.SearchTitle, x.PlexLibraryId, x.PlexServerId, PlexMediaType.Movie)));
        using var afterContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        (await afterContext.PlexMovies.OrderBy(x => x.Id).Select(x => new
        {
            x.Id,
            x.Title,
            x.SearchTitle,
            x.PlexLibraryId,
            x.PlexServerId,
        }).ToArrayAsync(CancellationToken)).ShouldBe(before);
        (await afterContext.MediaOverviewMovieSnapshots.OrderBy(x => x.PlexMovieId)
            .Select(x => x.PlexMovieId).ToArrayAsync(CancellationToken))
            .ShouldBe(expectedSnapshotIds);
    }

    [Test]
    public async Task ShouldHideDisabledParentServerLibraryWithoutChangingEnabledControl_WhenUsingOptimizedAndCanonicalOverviewPaths()
    {
        // Arrange
        var seed = new Seed(29105);
        const int movieCount = 4;
        const string search = "The LÉON: Professional Professional";
        const string optimizedSort = "title:asc";
        const string canonicalSort = "title:asc,addedAt:asc";
        string[] titles = ["The Léon: Professional", "Leon Amateur", "Professional Practice", "Arrival"];
        string[] normalizedTitles = ["leon professional", "leon amateur", "professional practice", "arrival"];

        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.HttpClientOptions = (handler, _) => handler.SetupIdentityRequest(seed);
                config.DatabaseOptions = data =>
                {
                    data.PlexAccountCount = 1;
                    data.PlexServerCount = 2;
                    data.PlexMovieLibraryCount = 1;
                    data.MovieCount = movieCount;
                };
            }
        );
        using var context = container.DbContext;

        var servers = await context
            .PlexServers.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.IsEnabled })
            .ToArrayAsync(CancellationToken);
        servers.Length.ShouldBe(2);
        servers.Select(x => x.IsEnabled).ShouldBe([true, true]);
        var targetServerId = servers[0].Id;

        var libraries = await context
            .PlexLibraries.IgnoreIsEnabledFilter()
            .Where(x => x.Type == PlexMediaType.Movie)
            .OrderBy(x => x.PlexServerId)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexServerId,
                x.IsEnabled,
                x.Type,
            })
            .ToArrayAsync(CancellationToken);
        libraries.Length.ShouldBe(2);
        libraries.Select(x => x.PlexServerId).ShouldBe(servers.Select(x => x.Id).ToArray());
        libraries.Select(x => x.IsEnabled).ShouldBe([true, true]);
        var targetLibraryId = libraries[0].Id;
        var enabledControlLibraryId = libraries[1].Id;

        var movies = await context.PlexMovies.AsNoTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        movies.Count.ShouldBe(movieCount * libraries.Length);
        foreach (var library in libraries)
        {
            var libraryMovies = movies.Where(x => x.PlexLibraryId == library.Id).ToArray();
            libraryMovies.Length.ShouldBe(titles.Length);
            for (var index = 0; index < libraryMovies.Length; index++)
            {
                var movieId = libraryMovies[index].Id;
                var title = titles[index];
                await context.PlexMovies.Where(x => x.Id == movieId).ExecuteUpdateAsync(
                    update => update.SetProperty(x => x.Title, title).SetProperty(x => x.SearchTitle, title.ToSearchTitle()),
                    CancellationToken
                );
            }
        }

        movies = await context.PlexMovies.AsNoTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        foreach (var library in libraries)
            movies.Where(x => x.PlexLibraryId == library.Id).Select(x => x.SearchTitle).ShouldBe(normalizedTitles);

        var movieState = movies
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.SearchTitle,
                x.AddedAt,
                x.MediaSize,
                x.PlexLibraryId,
                x.PlexServerId,
            })
            .ToArray();
        var targetMovies = movieState.Where(x => x.PlexLibraryId == targetLibraryId).ToArray();
        var enabledControlMovies = movieState.Where(x => x.PlexLibraryId == enabledControlLibraryId).ToArray();
        var targetSearchMovies = targetMovies
            .Where(x => x.SearchTitle.Contains("leon") && x.SearchTitle.Contains("professional"))
            .ToArray();
        var enabledControlSearchMovies = enabledControlMovies
            .Where(x => x.SearchTitle.Contains("leon") && x.SearchTitle.Contains("professional"))
            .ToArray();
        targetSearchMovies.Length.ShouldBe(1);
        enabledControlSearchMovies.Length.ShouldBe(1);
        targetSearchMovies.Select(x => x.Title).ShouldBe(["The Léon: Professional"]);
        enabledControlSearchMovies.Select(x => x.Title).ShouldBe(["The Léon: Professional"]);
        var expectedTargetSearch = targetSearchMovies
            .Select(x => (x.Id, x.Title, x.SearchTitle, x.PlexLibraryId, x.PlexServerId, Type: PlexMediaType.Movie))
            .ToArray();
        var expectedEnabledControlSearch = enabledControlSearchMovies
            .Select(x => (x.Id, x.Title, x.SearchTitle, x.PlexLibraryId, x.PlexServerId, Type: PlexMediaType.Movie))
            .ToArray();

        var libraryState = libraries
            .Select(x => new
            {
                x.Id,
                x.PlexServerId,
                x.IsEnabled,
                x.Type,
            })
            .ToArray();
        var allMovieState = movieState;
        var accountId = await context.PlexAccounts.Select(x => x.Id).SingleAsync(CancellationToken);
        var accountServerState = await context
            .PlexAccountServers.OrderBy(x => x.PlexServerId)
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.IsServerOwned,
            })
            .ToArrayAsync(CancellationToken);
        accountServerState.ShouldBe(
            servers
                .Select(x => new
                {
                    PlexAccountId = accountId,
                    PlexServerId = x.Id,
                    IsServerOwned = true,
                })
                .ToArray()
        );
        var accountLibraryState = await context
            .PlexAccountLibraries.OrderBy(x => x.PlexLibraryId)
            .Select(x => new
            {
                x.PlexAccountId,
                x.PlexServerId,
                x.PlexLibraryId,
                x.IsLibraryOwned,
            })
            .ToArrayAsync(CancellationToken);
        accountLibraryState.ShouldBe(
            libraries
                .Select(x => new
                {
                    PlexAccountId = accountId,
                    PlexServerId = x.PlexServerId,
                    PlexLibraryId = x.Id,
                    IsLibraryOwned = true,
                })
                .OrderBy(x => x.PlexLibraryId)
                .ToArray()
        );
        var serverStatusState = await context
            .PlexServerStatuses.OrderBy(x => x.Id)
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

        var rebuilt = await container.Resolve<ICommandExecutor>().Send(
            new RebuildMediaOverviewCommand([PlexMediaType.Movie]),
            CancellationToken
        );
        rebuilt.IsSuccess.ShouldBeTrue();
        rebuilt.Errors.Count.ShouldBe(0);
        var expectedSnapshotIds = allMovieState.Select(x => x.Id).OrderBy(x => x).ToArray();
        using (var snapshotContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync())
        {
            (
                await snapshotContext.MediaOverviewMovieSnapshots
                    .OrderBy(x => x.PlexMovieId)
                    .Select(x => x.PlexMovieId)
                    .ToArrayAsync(CancellationToken)
            ).ShouldBe(expectedSnapshotIds);
        }

        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        var targetBeforeDisable = await client.GETAsync<
            GetAllMediaByTypeEndpoint,
            GetAllMediaByTypeRequest,
            ResultDTO<PlexMediaStatisticsDTO>
        >(
            new GetAllMediaByTypeRequest
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = targetLibraryId,
                Search = search,
                Sort = optimizedSort,
                Page = 1,
                PageSize = 10,
                FilterOwnedMedia = false,
                FilterOfflineMedia = false,
            }
        );
        var targetBeforeValue = targetBeforeDisable.Result.Value.ShouldNotBeNull();
        targetBeforeDisable.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        targetBeforeDisable.Result.IsSuccess.ShouldBeTrue();
        targetBeforeDisable.Result.Errors.Count.ShouldBe(0);
        (targetBeforeValue.Page, targetBeforeValue.PageSize, targetBeforeValue.TotalCount, targetBeforeValue.MediaSize).ShouldBe(
            (1, 10, expectedTargetSearch.Length, targetSearchMovies.Sum(x => x.MediaSize))
        );
        targetBeforeValue.MediaList.OrderBy(x => x.Id)
            .Select(x => (x.Id, x.Title, x.SearchTitle, x.PlexLibraryId, x.PlexServerId, x.Type))
            .ShouldBe(expectedTargetSearch);

        var enabledControlBeforeDisable = await client.GETAsync<
            GetAllMediaByTypeEndpoint,
            GetAllMediaByTypeRequest,
            ResultDTO<PlexMediaStatisticsDTO>
        >(
            new GetAllMediaByTypeRequest
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = enabledControlLibraryId,
                Search = search,
                Sort = optimizedSort,
                Page = 1,
                PageSize = 10,
                FilterOwnedMedia = false,
                FilterOfflineMedia = false,
            }
        );
        var enabledControlBeforeValue = enabledControlBeforeDisable.Result.Value.ShouldNotBeNull();
        enabledControlBeforeDisable.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        enabledControlBeforeDisable.Result.IsSuccess.ShouldBeTrue();
        enabledControlBeforeDisable.Result.Errors.Count.ShouldBe(0);
        (
            enabledControlBeforeValue.Page,
            enabledControlBeforeValue.PageSize,
            enabledControlBeforeValue.TotalCount,
            enabledControlBeforeValue.MediaSize
        ).ShouldBe(
            (
                1,
                10,
                expectedEnabledControlSearch.Length,
                enabledControlSearchMovies.Sum(x => x.MediaSize)
            )
        );
        enabledControlBeforeValue.MediaList.OrderBy(x => x.Id)
            .Select(x => (x.Id, x.Title, x.SearchTitle, x.PlexLibraryId, x.PlexServerId, x.Type))
            .ShouldBe(expectedEnabledControlSearch);

        var scheduler = container.BackgroundJobScheduler;
        var snapshotJobKey = MediaOverviewSnapshotJob.GetJobKey();
        await scheduler.AddJob(
            JobBuilder.Create<MediaOverviewSnapshotJob>().WithIdentity(snapshotJobKey).StoreDurably().Build(),
            true,
            CancellationToken
        );
        var snapshotObserver = new MediaOverviewSnapshotJobObserver();
        scheduler.ListenerManager.AddJobListener(snapshotObserver, KeyMatcher<JobKey>.KeyEquals(snapshotJobKey));

        try
        {
            var disable = await client.PUTAsync<
                SetServerEnabledEndpoint,
                SetServerEnabledRequest,
                ResultDTO<PlexServerDTO>
            >(new SetServerEnabledRequest { PlexServerId = targetServerId, IsEnabled = false });

            // Assert
            disable.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
            disable.Result.IsSuccess.ShouldBeTrue();
            disable.Result.Errors.Count.ShouldBe(0);
            var disabledServerValue = disable.Result.Value.ShouldNotBeNull();
            (disabledServerValue.Id, disabledServerValue.IsEnabled).ShouldBe((targetServerId, false));

            var snapshotOutcome = await snapshotObserver.Completion.WaitAsync(CancellationToken);
            snapshotOutcome.Status.ShouldBe(JobStatus.Completed);
            snapshotOutcome.ErrorSummary.ShouldBeNull();
        }
        finally
        {
            scheduler.ListenerManager.RemoveJobListener(snapshotObserver.Name);
        }

        using var afterContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        (
            await afterContext
                .PlexServers.IgnoreIsEnabledFilter()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.IsEnabled })
                .ToArrayAsync(CancellationToken)
        ).ShouldBe(
            servers
                .Select(x => new
                {
                    x.Id,
                    IsEnabled = x.Id == targetServerId ? false : x.IsEnabled,
                })
                .ToArray()
        );
        (
            await afterContext
                .PlexLibraries.IgnoreIsEnabledFilter()
                .Where(x => x.Type == PlexMediaType.Movie)
                .OrderBy(x => x.PlexServerId)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.PlexServerId,
                    x.IsEnabled,
                    x.Type,
                })
                .ToArrayAsync(CancellationToken)
        ).ShouldBe(libraryState);
        (
            await afterContext
                .PlexAccountServers.OrderBy(x => x.PlexServerId)
                .Select(x => new
                {
                    x.PlexAccountId,
                    x.PlexServerId,
                    x.IsServerOwned,
                })
                .ToArrayAsync(CancellationToken)
        ).ShouldBe(accountServerState);
        (
            await afterContext
                .PlexAccountLibraries.OrderBy(x => x.PlexLibraryId)
                .Select(x => new
                {
                    x.PlexAccountId,
                    x.PlexServerId,
                    x.PlexLibraryId,
                    x.IsLibraryOwned,
                })
                .ToArrayAsync(CancellationToken)
        ).ShouldBe(accountLibraryState);
        (
            await afterContext
                .PlexServerStatuses.OrderBy(x => x.Id)
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
                .ToArrayAsync(CancellationToken)
        ).ShouldBe(serverStatusState);
        (
            await afterContext
                .PlexMovies.OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.SearchTitle,
                    x.AddedAt,
                    x.MediaSize,
                    x.PlexLibraryId,
                    x.PlexServerId,
                })
                .ToArrayAsync(CancellationToken)
        ).ShouldBe(allMovieState);
        (
            await afterContext.MediaOverviewMovieSnapshots
                .OrderBy(x => x.PlexMovieId)
                .Select(x => x.PlexMovieId)
                .ToArrayAsync(CancellationToken)
        ).ShouldBe(expectedSnapshotIds);

        var disabledOptimized = await client.GETAsync<
            GetAllMediaByTypeEndpoint,
            GetAllMediaByTypeRequest,
            ResultDTO<PlexMediaStatisticsDTO>
        >(
            new GetAllMediaByTypeRequest
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = targetLibraryId,
                Search = search,
                Sort = optimizedSort,
                Page = 1,
                PageSize = 10,
                FilterOwnedMedia = false,
                FilterOfflineMedia = false,
            }
        );
        disabledOptimized.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        disabledOptimized.Result.IsSuccess.ShouldBeTrue();
        disabledOptimized.Result.Errors.Count.ShouldBe(0);
        var disabledOptimizedValue = disabledOptimized.Result.Value.ShouldNotBeNull();
        (disabledOptimizedValue.Page, disabledOptimizedValue.PageSize).ShouldBe((1, 10));
        (
            disabledOptimizedValue.TotalCount,
            disabledOptimizedValue.MediaCount,
            disabledOptimizedValue.MovieCount,
            disabledOptimizedValue.TvShowCount,
            disabledOptimizedValue.SeasonCount,
            disabledOptimizedValue.EpisodeCount,
            disabledOptimizedValue.TotalMovieCount,
            disabledOptimizedValue.TotalTvShowCount,
            disabledOptimizedValue.TotalSeasonCount,
            disabledOptimizedValue.TotalEpisodeCount,
            disabledOptimizedValue.MediaSize,
            disabledOptimizedValue.TotalMediaSize
        ).ShouldBe((0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0L, 0L));
        disabledOptimizedValue.MediaList.ShouldBeEmpty();
        disabledOptimizedValue.NavigationIndexes.ShouldBeEmpty();
        disabledOptimizedValue.Roles.ShouldBeEmpty();
        disabledOptimizedValue.Countries.ShouldBeEmpty();
        disabledOptimizedValue.Genres.ShouldBeEmpty();
        disabledOptimizedValue.Qualities.ShouldBeEmpty();

        var enabledControlOptimized = await client.GETAsync<
            GetAllMediaByTypeEndpoint,
            GetAllMediaByTypeRequest,
            ResultDTO<PlexMediaStatisticsDTO>
        >(
            new GetAllMediaByTypeRequest
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = enabledControlLibraryId,
                Search = search,
                Sort = optimizedSort,
                Page = 1,
                PageSize = 10,
                FilterOwnedMedia = false,
                FilterOfflineMedia = false,
            }
        );
        enabledControlOptimized.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        enabledControlOptimized.Result.IsSuccess.ShouldBeTrue();
        enabledControlOptimized.Result.Errors.Count.ShouldBe(0);
        var enabledControlOptimizedValue = enabledControlOptimized.Result.Value.ShouldNotBeNull();
        (
            enabledControlOptimizedValue.Page,
            enabledControlOptimizedValue.PageSize,
            enabledControlOptimizedValue.TotalCount,
            enabledControlOptimizedValue.MediaSize
        ).ShouldBe(
            (
                1,
                10,
                expectedEnabledControlSearch.Length,
                enabledControlSearchMovies.Sum(x => x.MediaSize)
            )
        );
        enabledControlOptimizedValue.MediaList.OrderBy(x => x.Id)
            .Select(x => (x.Id, x.Title, x.SearchTitle, x.PlexLibraryId, x.PlexServerId, x.Type))
            .ShouldBe(expectedEnabledControlSearch);

        var disabledCanonical = await client.GETAsync<
            GetAllMediaByTypeEndpoint,
            GetAllMediaByTypeRequest,
            ResultDTO<PlexMediaStatisticsDTO>
        >(
            new GetAllMediaByTypeRequest
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = targetLibraryId,
                Search = search,
                Sort = canonicalSort,
                Page = 1,
                PageSize = 10,
                FilterOwnedMedia = false,
                FilterOfflineMedia = false,
            }
        );
        disabledCanonical.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        disabledCanonical.Result.IsSuccess.ShouldBeTrue();
        disabledCanonical.Result.Errors.Count.ShouldBe(0);
        var disabledCanonicalValue = disabledCanonical.Result.Value.ShouldNotBeNull();
        (disabledCanonicalValue.Page, disabledCanonicalValue.PageSize).ShouldBe((1, 10));
        (
            disabledCanonicalValue.TotalCount,
            disabledCanonicalValue.MediaCount,
            disabledCanonicalValue.MovieCount,
            disabledCanonicalValue.TvShowCount,
            disabledCanonicalValue.SeasonCount,
            disabledCanonicalValue.EpisodeCount,
            disabledCanonicalValue.TotalMovieCount,
            disabledCanonicalValue.TotalTvShowCount,
            disabledCanonicalValue.TotalSeasonCount,
            disabledCanonicalValue.TotalEpisodeCount,
            disabledCanonicalValue.MediaSize,
            disabledCanonicalValue.TotalMediaSize
        ).ShouldBe((0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0L, 0L));
        disabledCanonicalValue.MediaList.ShouldBeEmpty();
        disabledCanonicalValue.NavigationIndexes.ShouldBeEmpty();
        disabledCanonicalValue.Roles.ShouldBeEmpty();
        disabledCanonicalValue.Countries.ShouldBeEmpty();
        disabledCanonicalValue.Genres.ShouldBeEmpty();
        disabledCanonicalValue.Qualities.ShouldBeEmpty();

        var enabledControlCanonical = await client.GETAsync<
            GetAllMediaByTypeEndpoint,
            GetAllMediaByTypeRequest,
            ResultDTO<PlexMediaStatisticsDTO>
        >(
            new GetAllMediaByTypeRequest
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = enabledControlLibraryId,
                Search = search,
                Sort = canonicalSort,
                Page = 1,
                PageSize = 10,
                FilterOwnedMedia = false,
                FilterOfflineMedia = false,
            }
        );
        enabledControlCanonical.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        enabledControlCanonical.Result.IsSuccess.ShouldBeTrue();
        enabledControlCanonical.Result.Errors.Count.ShouldBe(0);
        var enabledControlCanonicalValue = enabledControlCanonical.Result.Value.ShouldNotBeNull();
        (
            enabledControlCanonicalValue.Page,
            enabledControlCanonicalValue.PageSize,
            enabledControlCanonicalValue.TotalCount,
            enabledControlCanonicalValue.MediaSize
        ).ShouldBe(
            (
                1,
                10,
                expectedEnabledControlSearch.Length,
                enabledControlSearchMovies.Sum(x => x.MediaSize)
            )
        );
        enabledControlCanonicalValue.MediaList.OrderBy(x => x.Id)
            .Select(x => (x.Id, x.Title, x.SearchTitle, x.PlexLibraryId, x.PlexServerId, x.Type))
            .ShouldBe(expectedEnabledControlSearch);
    }

    private sealed class MediaOverviewSnapshotJobObserver : IJobListener
    {
        private readonly TaskCompletionSource<BackgroundJobTerminalOutcome> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public string Name => nameof(MediaOverviewSnapshotJobObserver);

        public Task<BackgroundJobTerminalOutcome> Completion => _completion.Task;

        public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            _completion.TrySetResult(new BackgroundJobTerminalOutcome(JobStatus.Failed, "Job execution was vetoed."));
            return Task.CompletedTask;
        }

        public Task JobWasExecuted(
            IJobExecutionContext context,
            JobExecutionException? jobException,
            CancellationToken cancellationToken = default
        )
        {
            _completion.TrySetResult(BackgroundJobTerminalOutcome.From(context, jobException));
            return Task.CompletedTask;
        }
    }
}
