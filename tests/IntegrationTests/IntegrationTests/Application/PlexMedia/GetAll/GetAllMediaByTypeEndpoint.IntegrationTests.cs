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
}
