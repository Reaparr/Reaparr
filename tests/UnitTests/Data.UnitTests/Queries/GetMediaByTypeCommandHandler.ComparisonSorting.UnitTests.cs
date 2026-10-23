using FlexQuery.NET.Models;
using Reaparr.Application.Contracts;

namespace Reaparr.Data.UnitTests;

public class GetMediaByTypeCommandHandlerComparisonSortingUnitTests : BaseUnitTest<GetMediaByTypeCommandHandler>
{
    public GetMediaByTypeCommandHandlerComparisonSortingUnitTests()
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ICommand<Result>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        SetupMediaByTypeCommandDispatch();
    }

    [Test]
    public async Task ShouldSortComparisonStateBeforePaging_WithDeterministicTitleAndIdTies()
    {
        // Arrange
        var fixture = await ArrangeMovieFixtureAsync();
        var tiedIds = new[] { fixture.Ids[1], fixture.Ids[3] };
        var tiedMovies = await IDbContext.PlexMovies
            .Where(x => tiedIds.Contains(x.Id))
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.SearchTitle, x.SortIndex })
            .ToListAsync(CancellationToken);
        tiedMovies.Select(x => (x.Id, x.SearchTitle, x.SortIndex))
            .ShouldBe([(fixture.Ids[1], "alpha", 1), (fixture.Ids[3], "alpha", 1)]);
        ConfigureComparisonProjection(
            PlexMediaType.Movie,
            fixture.LibraryId,
            fixture.Ids,
            new Dictionary<int, PlexMediaComparisonState>
            {
                [fixture.Ids[0]] = PlexMediaComparisonState.Missing,
                [fixture.Ids[1]] = PlexMediaComparisonState.Owned,
                [fixture.Ids[2]] = PlexMediaComparisonState.Missing,
                [fixture.Ids[3]] = PlexMediaComparisonState.Owned,
            }
        );

        // Act
        var ascendingPage1 = await Sut.ExecuteAsync(CreateCommand(PlexMediaType.Movie, fixture.LibraryId, 1, "comparisonState:asc,Title:asc"), CancellationToken);
        var ascendingPage2 = await Sut.ExecuteAsync(CreateCommand(PlexMediaType.Movie, fixture.LibraryId, 2, "comparisonState:asc,Title:asc"), CancellationToken);
        var descendingPage1 = await Sut.ExecuteAsync(CreateCommand(PlexMediaType.Movie, fixture.LibraryId, 1, "comparisonState:desc,Title:asc"), CancellationToken);
        var descendingPage2 = await Sut.ExecuteAsync(CreateCommand(PlexMediaType.Movie, fixture.LibraryId, 2, "comparisonState:desc,Title:asc"), CancellationToken);

        // Assert
        foreach (var result in new[] { ascendingPage1, ascendingPage2, descendingPage1, descendingPage2 })
        {
            result.IsSuccess.ShouldBeTrue();
            result.Errors.Count.ShouldBe(0);
            result.Value.TotalCount.ShouldBe(4);
            result.Value.MediaCount.ShouldBe(4);
            result.Value.Items.Select(x => x.SortIndex).ShouldBe(result.Value.Page == 1 ? new[] { 1, 2 } : new[] { 3, 4 });
            (result.Value.MediaSize, result.Value.TotalMediaSize).ShouldBe((1000L, 1000L));
        }

        ascendingPage1.Value.Items.Select(x => x.Id).ShouldBe([fixture.Ids[1], fixture.Ids[3]]);
        ascendingPage2.Value.Items.Select(x => x.Id).ShouldBe([fixture.Ids[2], fixture.Ids[0]]);
        descendingPage1.Value.Items.Select(x => x.Id).ShouldBe([fixture.Ids[2], fixture.Ids[0]]);
        descendingPage2.Value.Items.Select(x => x.Id).ShouldBe([fixture.Ids[1], fixture.Ids[3]]);
        ascendingPage1.Value.Items.Select(x => x.ComparisonId).ShouldBe([1, 1]);
        descendingPage1.Value.Items.Select(x => x.ComparisonId).ShouldBe([3, 3]);
        ascendingPage1.Value.Items.Select(x => (x.Id, x.SearchTitle, x.SortIndex)).ShouldBe(
            [(fixture.Ids[1], "alpha", 1), (fixture.Ids[3], "alpha", 2)]
        );
        ascendingPage1.Value.NavigationIndexes.Select(x => (x.Label, x.Index)).ShouldBe([("A", 0), ("B", 2), ("Z", 3)]);
        descendingPage1.Value.NavigationIndexes.Select(x => (x.Label, x.Index)).ShouldBe([("B", 0), ("Z", 1), ("A", 2)]);
        Mock.Mock<ICommandExecutor>().Verify();
        MediaByTypeDispatchedCommands.Count.ShouldBe(4);
        MediaByTypeDispatchedCommands.ShouldAllBe(command => command is GetMovieMediaByTypeCommand);
    }

    [Test]
    public async Task ShouldSortTvShowComparisonStateBeforePaging_AndPreserveSecondarySortPrecedence()
    {
        // Arrange
        var fixture = await ArrangeTvShowFixtureAsync();
        ConfigureComparisonProjection(
            PlexMediaType.TvShow,
            fixture.LibraryId,
            fixture.Ids,
            new Dictionary<int, PlexMediaComparisonState>
            {
                [fixture.Ids[0]] = PlexMediaComparisonState.Missing,
                [fixture.Ids[1]] = PlexMediaComparisonState.Owned,
                [fixture.Ids[2]] = PlexMediaComparisonState.Missing,
                [fixture.Ids[3]] = PlexMediaComparisonState.Owned,
            }
        );

        // Act
        var ascendingPage1 = await Sut.ExecuteAsync(CreateCommand(PlexMediaType.TvShow, fixture.LibraryId, 1, "comparisonState:asc,Year:desc,Title:asc"), CancellationToken);
        var ascendingPage2 = await Sut.ExecuteAsync(CreateCommand(PlexMediaType.TvShow, fixture.LibraryId, 2, "comparisonState:asc,Year:desc,Title:asc"), CancellationToken);
        var descendingPage1 = await Sut.ExecuteAsync(CreateCommand(PlexMediaType.TvShow, fixture.LibraryId, 1, "comparisonState:desc,Year:desc,Title:asc"), CancellationToken);
        var descendingPage2 = await Sut.ExecuteAsync(CreateCommand(PlexMediaType.TvShow, fixture.LibraryId, 2, "comparisonState:desc,Year:desc,Title:asc"), CancellationToken);

        // Assert
        foreach (var result in new[] { ascendingPage1, ascendingPage2, descendingPage1, descendingPage2 })
        {
            result.IsSuccess.ShouldBeTrue();
            result.Errors.Count.ShouldBe(0);
            result.Value.TotalCount.ShouldBe(4);
            result.Value.MediaCount.ShouldBe(4);
            result.Value.Items.Select(x => x.SortIndex).ShouldBe(result.Value.Page == 1 ? new[] { 1, 2 } : new[] { 3, 4 });
            (result.Value.MediaSize, result.Value.TotalMediaSize).ShouldBe((1000L, 1000L));
        }

        ascendingPage1.Value.Items.Select(x => x.Id).ShouldBe([fixture.Ids[1], fixture.Ids[3]]);
        ascendingPage2.Value.Items.Select(x => x.Id).ShouldBe([fixture.Ids[0], fixture.Ids[2]]);
        descendingPage1.Value.Items.Select(x => x.Id).ShouldBe([fixture.Ids[0], fixture.Ids[2]]);
        descendingPage2.Value.Items.Select(x => x.Id).ShouldBe([fixture.Ids[1], fixture.Ids[3]]);
        ascendingPage1.Value.NavigationIndexes.Select(x => (x.Label, x.Index)).ShouldBe(
            [("A", 0), ("C", 1), ("Z", 2), ("B", 3)]
        );
        descendingPage1.Value.NavigationIndexes.Select(x => (x.Label, x.Index)).ShouldBe(
            [("Z", 0), ("B", 1), ("A", 2), ("C", 3)]
        );
        Mock.Mock<ICommandExecutor>().Verify();
        MediaByTypeDispatchedCommands.Count.ShouldBe(4);
        MediaByTypeDispatchedCommands.ShouldAllBe(command => command is GetTvShowMediaByTypeCommand);
    }

    [Test]
    public async Task ShouldSortMusicComparisonStateBeforePaging_WithDistinctStatesAndStableTies()
    {
        // Arrange
        var fixture = await ArrangeMusicArtistFixtureAsync();
        ConfigureComparisonProjection(
            PlexMediaType.MusicArtist,
            fixture.LibraryId,
            fixture.Ids,
            new Dictionary<int, PlexMediaComparisonState>
            {
                [fixture.Ids[0]] = PlexMediaComparisonState.Missing,
                [fixture.Ids[1]] = PlexMediaComparisonState.Owned,
                [fixture.Ids[2]] = PlexMediaComparisonState.Missing,
                [fixture.Ids[3]] = PlexMediaComparisonState.Owned,
            }
        );

        // Act
        var ascendingPage1 = await Sut.ExecuteAsync(CreateCommand(PlexMediaType.MusicArtist, fixture.LibraryId, 1, "comparisonState:asc,Title:asc"), CancellationToken);
        var ascendingPage2 = await Sut.ExecuteAsync(CreateCommand(PlexMediaType.MusicArtist, fixture.LibraryId, 2, "comparisonState:asc,Title:asc"), CancellationToken);
        var descendingPage1 = await Sut.ExecuteAsync(CreateCommand(PlexMediaType.MusicArtist, fixture.LibraryId, 1, "comparisonState:desc,Title:asc"), CancellationToken);
        var descendingPage2 = await Sut.ExecuteAsync(CreateCommand(PlexMediaType.MusicArtist, fixture.LibraryId, 2, "comparisonState:desc,Title:asc"), CancellationToken);

        // Assert
        foreach (var result in new[] { ascendingPage1, ascendingPage2, descendingPage1, descendingPage2 })
        {
            result.IsSuccess.ShouldBeTrue();
            result.Errors.Count.ShouldBe(0);
            result.Value.TotalCount.ShouldBe(4);
            result.Value.MediaCount.ShouldBe(4);
            result.Value.Items.Select(x => x.SortIndex).ShouldBe(result.Value.Page == 1 ? new[] { 1, 2 } : new[] { 3, 4 });
            (result.Value.MediaSize, result.Value.TotalMediaSize).ShouldBe((1000L, 1000L));
        }

        ascendingPage1.Value.Items.Select(x => x.Id).ShouldBe([fixture.Ids[1], fixture.Ids[3]]);
        ascendingPage2.Value.Items.Select(x => x.Id).ShouldBe([fixture.Ids[2], fixture.Ids[0]]);
        descendingPage1.Value.Items.Select(x => x.Id).ShouldBe([fixture.Ids[2], fixture.Ids[0]]);
        descendingPage2.Value.Items.Select(x => x.Id).ShouldBe([fixture.Ids[1], fixture.Ids[3]]);
        ascendingPage1.Value.Items.Select(x => x.ComparisonId).ShouldBe([1, 1]);
        descendingPage1.Value.Items.Select(x => x.ComparisonId).ShouldBe([3, 3]);
        ascendingPage1.Value.NavigationIndexes.Select(x => (x.Label, x.Index)).ShouldBe(
            [("A", 0), ("C", 1), ("B", 2), ("Z", 3)]
        );
        descendingPage1.Value.NavigationIndexes.Select(x => (x.Label, x.Index)).ShouldBe(
            [("B", 0), ("Z", 1), ("A", 2), ("C", 3)]
        );
        Mock.Mock<ICommandExecutor>().Verify();
        MediaByTypeDispatchedCommands.Count.ShouldBe(4);
        MediaByTypeDispatchedCommands.ShouldAllBe(command => command is GetMusicArtistMediaByTypeCommand);
    }

    private async Task<(int LibraryId, List<int> Ids)> ArrangeMovieFixtureAsync()
    {
        await SetupDatabase(
            70240,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 4;
            }
        );

        var dbContext = IDbContext;
        var libraryId = await dbContext.PlexLibraries.Select(x => x.Id).SingleAsync(CancellationToken);
        var movieIds = await dbContext.PlexMovies.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        var titleData = new[] { (Title: "Zulu", SortIndex: 4), (Title: "Alpha", SortIndex: 1), (Title: "Bravo", SortIndex: 2), (Title: "Alpha", SortIndex: 1) };
        for (var index = 0; index < movieIds.Count; index++)
        {
            var data = titleData[index];
            await dbContext.PlexMovies.Where(x => x.Id == movieIds[index]).ExecuteUpdateAsync(
                x => x.SetProperty(y => y.Title, data.Title)
                    .SetProperty(y => y.SearchTitle, data.Title.ToLowerInvariant())
                    .SetProperty(y => y.SortIndex, data.SortIndex)
                    .SetProperty(y => y.MediaSize, (index + 1) * 100L),
                CancellationToken
            );
        }

        return (libraryId, movieIds);
    }

    private async Task<(int LibraryId, List<int> Ids)> ArrangeTvShowFixtureAsync()
    {
        await SetupDatabase(
            70241,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexTvShowLibraryCount = 1;
                cfg.TvShowCount = 4;
            }
        );

        var dbContext = IDbContext;
        var libraryId = await dbContext.PlexLibraries.Select(x => x.Id).SingleAsync(CancellationToken);
        var tvShowIds = await dbContext.PlexTvShows.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        var titleData = new[]
        {
            (Title: "Zulu", SortIndex: 4, Year: 2020),
            (Title: "Alpha", SortIndex: 1, Year: 2021),
            (Title: "Bravo", SortIndex: 2, Year: 2010),
            (Title: "Charlie", SortIndex: 3, Year: 2010),
        };
        for (var index = 0; index < tvShowIds.Count; index++)
        {
            var data = titleData[index];
            await dbContext.PlexTvShows.Where(x => x.Id == tvShowIds[index]).ExecuteUpdateAsync(
                x => x.SetProperty(y => y.Title, data.Title)
                    .SetProperty(y => y.SearchTitle, data.Title.ToLowerInvariant())
                    .SetProperty(y => y.SortIndex, data.SortIndex)
                    .SetProperty(y => y.Year, data.Year)
                    .SetProperty(y => y.MediaSize, (index + 1) * 100L),
                CancellationToken
            );
        }

        return (libraryId, tvShowIds);
    }

    private async Task<(int LibraryId, List<int> Ids)> ArrangeMusicArtistFixtureAsync()
    {
        await SetupDatabase(
            87502,
            cfg =>
            {
                cfg.PlexMusicLibraryCount = 1;
                cfg.MusicArtistCount = 4;
                cfg.MusicAlbumCount = 0;
            }
        );

        var dbContext = IDbContext;
        var libraryId = await dbContext.PlexLibraries.Select(x => x.Id).SingleAsync(CancellationToken);
        var artists = await dbContext.PlexMusicArtists.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var titleData = new[] { (Title: "Zulu", SortIndex: 4), (Title: "Alpha", SortIndex: 1), (Title: "Bravo", SortIndex: 2), (Title: "Charlie", SortIndex: 3) };
        for (var index = 0; index < artists.Count; index++)
        {
            var data = titleData[index];
            artists[index].Title = data.Title;
            artists[index].MediaSize = (index + 1) * 100L;
            dbContext.Entry(artists[index]).Property(nameof(BasePlexMedia.SearchTitle)).CurrentValue = data.Title.ToLowerInvariant();
            dbContext.Entry(artists[index]).Property(nameof(BasePlexMedia.SortIndex)).CurrentValue = data.SortIndex;
        }

        await dbContext.SaveChangesAsync(CancellationToken);
        var ids = artists.Select(x => x.Id).ToList();
        dbContext.ClearChangeTracker();
        return (libraryId, ids);
    }

    private void ConfigureComparisonProjection(
        PlexMediaType mediaType,
        int libraryId,
        IReadOnlyCollection<int> ids,
        IReadOnlyDictionary<int, PlexMediaComparisonState> states
    )
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(
                It.Is<ApplyComparisonStateCommand>(command =>
                    command.MediaType == mediaType
                    && command.PlexLibraryId == libraryId
                    && command.Items.Count == ids.Count
                    && command.Items.Select(item => item.Id).ToHashSet().SetEquals(ids)
                    && command.Items.All(item => item.PlexLibraryId == libraryId)
                ),
                CancellationToken
            ))
            .Callback<ICommand<Result>, CancellationToken>((command, _) =>
            {
                foreach (var item in ((ApplyComparisonStateCommand)command).Items)
                    item.SetComparisonState(states[item.Id]);
            })
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(4));
    }

    private static GetMediaByTypeCommand CreateCommand(PlexMediaType mediaType, int libraryId, int page, string sort) =>
        new()
        {
            Filter = new MediaQueryFilter
            {
                MediaType = mediaType,
                PlexLibraryId = libraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters { Page = page, PageSize = 2, Sort = sort },
            },
        };
}
