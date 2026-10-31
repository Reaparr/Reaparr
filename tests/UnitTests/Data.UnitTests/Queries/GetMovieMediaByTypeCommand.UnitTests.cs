using Autofac;
using FlexQuery.NET.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Reaparr.Application.Contracts;
using Reaparr.Environment;

namespace Reaparr.Data.UnitTests;

public class GetMovieMediaByTypeCommandUnitTests : BaseCommandUnitTest<GetMovieMediaByTypeCommand>
{
    public GetMovieMediaByTypeCommandUnitTests()
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ICommand<Result>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
    }

    [Test]
    public async Task ShouldReturnOnlyMoviesFromSpecificLibrary_WhenPlexLibraryIdIsSet()
    {
        // Arrange
        await SetupDatabase(
            70001,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 5;
                cfg.MovieCount = 6;
            }
        );

        var dbContext = IDbContext;
        var targetLibraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = targetLibraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        var projectedLibraryIds = new List<int?>();
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<Result>, CancellationToken>(
                (x, _) => projectedLibraryIds.Add(((ApplyComparisonStateCommand)x).PlexLibraryId)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == targetLibraryId);
        projectedLibraryIds.ShouldBe([targetLibraryId]);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldFilterMoviesByProjectedComparisonState_WhenComparisonStateFilterIsSet()
    {
        // Arrange
        await SetupDatabase(
            70020,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 4;
            }
        );

        var dbContext = IDbContext;
        var targetLibraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);
        var movieIds = await dbContext
            .PlexMovies.Where(x => x.PlexLibraryId == targetLibraryId)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Take(4)
            .ToListAsync(CancellationToken);

        for (var i = 0; i < movieIds.Count; i++)
        {
            var year = 2000 + i;
            var index = i;
            await dbContext
                .PlexMovies.Where(x => x.Id == movieIds[index])
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.Year, year), CancellationToken);
        }

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = targetLibraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                ComparisonState = PlexMediaComparisonState.Missing,
                Parameters = new FlexQueryParameters
                {
                    Page = 1,
                    PageSize = 1,
                    Sort = "Year:asc",
                    Filter = null,
                },
            }
        );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<Result>, CancellationToken>(
                (projectionCommand, _) =>
                {
                    var applyCommand = (ApplyComparisonStateCommand)projectionCommand;
                    for (var i = 0; i < applyCommand.Items.Count; i++)
                    {
                        applyCommand
                            .Items[i]
                            .SetComparisonState(
                                i is 0 or 2 ? PlexMediaComparisonState.Missing : PlexMediaComparisonState.Owned
                            );
                    }
                }
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(2);
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items.ShouldAllBe(x => x.ComparisonId == PlexMediaComparisonState.Missing.ToComparisonId());
        result.Value.Items.Select(x => (x.Id, x.Year)).ShouldBe([(movieIds[0], 2000)]);
        result
            .Value.NavigationIndexes.Select(x => new { x.Label, x.Index })
            .ShouldBe([new { Label = "2000", Index = 0 }, new { Label = "2002", Index = 1 }]);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldFilterMoviesByProjectedComparisonStateAcrossLibraries_WhenAllMediaModeComparisonStateFilterIsSet()
    {
        // Arrange
        await SetupDatabase(
            70022,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 2;
                cfg.MovieCount = 2;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                ComparisonState = PlexMediaComparisonState.Missing,
                Parameters = new FlexQueryParameters
                {
                    Page = 1,
                    PageSize = 1,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        var observedPlexLibraryIds = new List<int?>();
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<Result>, CancellationToken>(
                (projectionCommand, _) =>
                {
                    var applyCommand = (ApplyComparisonStateCommand)projectionCommand;
                    observedPlexLibraryIds.Add(applyCommand.PlexLibraryId);

                    for (var i = 0; i < applyCommand.Items.Count; i++)
                    {
                        applyCommand
                            .Items[i]
                            .SetComparisonState(
                                i is 0 or 2 ? PlexMediaComparisonState.Missing : PlexMediaComparisonState.Owned
                            );
                    }
                }
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        observedPlexLibraryIds.ShouldBe([null]);
        result.Value.TotalCount.ShouldBe(2);
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items.ShouldAllBe(x => x.ComparisonId == PlexMediaComparisonState.Missing.ToComparisonId());
        result.Value.NavigationIndexes.Max(x => x.Index).ShouldBe(1);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReturnMoviesFromMultipleLibraries_WhenPlexLibraryIdIsZero()
    {
        // Arrange
        await SetupDatabase(
            70003,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 2;
                cfg.MovieCount = 4;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.Select(x => x.PlexLibraryId).Distinct().Count().ShouldBe(2);
    }

    [Test]
    public async Task ShouldProjectMoviesInOneCommand_WhenPlexLibraryIdIsZero()
    {
        // Arrange
        await SetupDatabase(
            70038,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 3;
                cfg.MovieCount = 2;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        var projectedLibraryIds = new List<int?>();
        var projectedItemLibraryIds = new List<int>();
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<Result>, CancellationToken>(
                (x, _) =>
                {
                    var applyCommand = (ApplyComparisonStateCommand)x;
                    projectedLibraryIds.Add(applyCommand.PlexLibraryId);
                    projectedItemLibraryIds.AddRange(applyCommand.Items.Select(item => item.PlexLibraryId));
                    applyCommand.Items.ShouldAllBe(item => item.PlexLibraryId > 0);
                }
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(x => x.PlexLibraryId).Distinct().Count().ShouldBe(3);
        projectedLibraryIds.ShouldBe([null]);
        projectedItemLibraryIds.Distinct().Count().ShouldBe(3);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReturnProjectionFailure_WhenAllLibraryComparisonProjectionFails()
    {
        // Arrange
        await SetupDatabase(
            70039,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 2;
                cfg.MovieCount = 2;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                ComparisonState = PlexMediaComparisonState.Missing,
                Parameters = new FlexQueryParameters
                {
                    Page = 1,
                    PageSize = 1,
                    Sort = "comparisonState:asc",
                    Filter = null,
                },
            }
        );

        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ApplyComparisonStateCommand>(projection =>
                        projection.PlexLibraryId == null
                        && projection.Items.Count > 0
                        && projection.Items.All(item => item.PlexLibraryId > 0)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result.Fail("Projection failed"))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);

        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldPropagateProjectionCancellationFromDispatchedMovieHandler()
    {
        // Arrange
        await SetupDatabase(
            70040,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 2;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                ComparisonState = PlexMediaComparisonState.Missing,
                Parameters = new FlexQueryParameters
                {
                    Page = 1,
                    PageSize = 1,
                    Sort = "comparisonState:asc",
                },
            }
        );
        var cancelled = ResultExtensions.TaskIsCancelled("comparison projection");
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ApplyComparisonStateCommand>(projection =>
                        projection.PlexLibraryId == null
                        && projection.MediaType == PlexMediaType.Movie
                        && projection.Items.Count == 2
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(cancelled)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ShouldBeSameAs(cancelled.Errors.Single());

        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldExcludeOwnedLibraries_WhenFilterOwnedMediaIsTrueForAllLibraries()
    {
        // Arrange
        await SetupDatabase(
            70004,
            cfg =>
            {
                cfg.PlexServerCount = 2;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 4;
                cfg.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraryToKeep = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexServerId })
            .FirstAsync(CancellationToken);

        await dbContext
            .PlexServers.Where(x => x.Id == libraryToKeep.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnedOverride, false), CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = true,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == libraryToKeep.Id);
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenAllLibrariesAreOwnedAndFilterOwnedMediaIsTrueForAllLibraries()
    {
        // Arrange
        await SetupDatabase(
            70005,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 2;
                cfg.MovieCount = 4;
                cfg.PlexAccountCount = 1;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = true,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(0);
        result.Value.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldExcludeOfflineServerLibraries_WhenFilterOfflineMediaIsTrueForAllLibraries()
    {
        // Arrange
        await SetupDatabase(
            70006,
            cfg =>
            {
                cfg.PlexServerCount = 2;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 3;
            }
        );

        var dbContext = IDbContext;
        var serverIds = await dbContext.PlexServers.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);
        var offlineServerId = serverIds.Last();

        await dbContext
            .PlexServerStatuses.Where(x => x.PlexServerId == offlineServerId)
            .ExecuteDeleteAsync(CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = true,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.ShouldAllBe(x => x.PlexServerId != offlineServerId);
    }

    [Test]
    public async Task ShouldExcludeServerLibraries_WhenServerHasNoPlexAccountAccessForAllLibraries()
    {
        // Arrange
        await SetupDatabase(
            70033,
            cfg =>
            {
                cfg.PlexServerCount = 2;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 3;
                cfg.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var inaccessibleServerId = await dbContext
            .PlexServers.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .LastAsync(CancellationToken);

        await dbContext
            .PlexAccountServers.Where(x => x.PlexServerId == inaccessibleServerId)
            .ExecuteDeleteAsync(CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.ShouldAllBe(x => x.PlexServerId != inaccessibleServerId);
    }

    [Test]
    public async Task ShouldIgnoreServerAccessFilter_WhenSpecificLibraryIdIsSet()
    {
        // Arrange
        await SetupDatabase(
            70034,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 3;
                cfg.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var targetLibrary = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .Select(x => new { x.Id, x.PlexServerId })
            .FirstAsync(CancellationToken);

        await dbContext
            .PlexAccountServers.Where(x => x.PlexServerId == targetLibrary.PlexServerId)
            .ExecuteDeleteAsync(CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = targetLibrary.Id,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == targetLibrary.Id);
    }

    [Test]
    public async Task ShouldExcludeLibrariesWithoutPlexAccountAccess_WhenServerHasPartialLibraryAccessForAllLibraries()
    {
        // Arrange
        await SetupDatabase(
            70035,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 2;
                cfg.MovieCount = 4;
                cfg.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var libraryIds = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        var inaccessibleLibraryId = libraryIds.Last();

        await dbContext
            .PlexAccountLibraries.Where(x => x.PlexLibraryId == inaccessibleLibraryId)
            .ExecuteDeleteAsync(CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId != inaccessibleLibraryId);
    }

    [Test]
    public async Task ShouldIgnoreLibraryAccessFilter_WhenSpecificLibraryIdIsSet()
    {
        // Arrange
        await SetupDatabase(
            70036,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 2;
                cfg.MovieCount = 4;
                cfg.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var inaccessibleLibraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .LastAsync(CancellationToken);

        await dbContext
            .PlexAccountLibraries.Where(x => x.PlexLibraryId == inaccessibleLibraryId)
            .ExecuteDeleteAsync(CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = inaccessibleLibraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == inaccessibleLibraryId);
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenAllServersAreOfflineAndFilterOfflineMediaIsTrueForAllLibraries()
    {
        // Arrange
        await SetupDatabase(
            70007,
            cfg =>
            {
                cfg.PlexServerCount = 2;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 3;
            }
        );

        var dbContext = IDbContext;
        await dbContext.PlexServerStatuses.ExecuteDeleteAsync(CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = true,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(0);
        result.Value.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldIgnoreOwnedFilter_WhenSpecificPlexLibraryIdIsSet()
    {
        // Arrange
        await SetupDatabase(
            70008,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 2;
                cfg.MovieCount = 4;
                cfg.PlexAccountCount = 1;
            }
        );

        var dbContext = IDbContext;
        var targetLibraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = targetLibraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = true,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == targetLibraryId);
    }

    [Test]
    public async Task ShouldIgnoreOfflineFilter_WhenSpecificPlexLibraryIdIsSet()
    {
        // Arrange
        await SetupDatabase(
            70009,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 4;
            }
        );

        var dbContext = IDbContext;
        await dbContext.PlexServerStatuses.ExecuteDeleteAsync(CancellationToken);

        var targetLibraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = targetLibraryId,
                FilterOfflineMedia = true,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == targetLibraryId);
    }

    [Test]
    public async Task ShouldRejectNonMovieMediaType_BeforeComparisonProjection()
    {
        // Arrange
        await SetupDatabase(
            70010,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 2;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Season,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        // Validation results contain the bad-request envelope and the field-specific failure.
        result.Errors.Count.ShouldBe(2);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<ApplyComparisonStateCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenNoAllowedLibrariesExistInAllLibraryMode()
    {
        // Arrange
        await SetupDatabase(70011);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(0);
        result.Value.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldAssignSortIndexesSequentiallyForMovies_WhenResultsReturned()
    {
        // Arrange
        await SetupDatabase(
            70012,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 6;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(6);
        result.Value.MediaCount.ShouldBe(6);
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.Select(x => x.SortIndex).ShouldBe(Enumerable.Range(1, result.Value.Items.Count));
    }

    [Test]
    public async Task ShouldUseDefaultPageSize_WhenPageSizeIsNotProvided()
    {
        // Arrange
        await SetupDatabase(
            70106,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 150;
            }
        );

        var expectedMovieCount = await IDbContext.PlexMovies.CountAsync(CancellationToken);
        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 2,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(expectedMovieCount - MediaQueryFilter.MaximumPageSize);
        result.Value.TotalCount.ShouldBe(expectedMovieCount);
        result.Value.Page.ShouldBe(2);
        result.Value.PageSize.ShouldBe(MediaQueryFilter.MaximumPageSize);
    }

    [Test]
    public async Task ShouldRespectPaging_WhenPageAndPageSizeAreProvided()
    {
        // Arrange
        await SetupDatabase(
            70014,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 8;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 1,
                    PageSize = 3,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(3);
    }

    [Test]
    public async Task ShouldReturnPageAndPageSize_WhenPageAndPageSizeAreProvided()
    {
        // Arrange
        await SetupDatabase(
            70101,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 8;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 2,
                    PageSize = 3,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Page.ShouldBe(2);
        result.Value.PageSize.ShouldBe(3);
    }

    [Test]
    public async Task ShouldReturnTotalMatchingCount_WhenPagingIsApplied()
    {
        // Arrange
        await SetupDatabase(
            70102,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 8;
            }
        );

        var expectedMovieCount = await IDbContext.PlexMovies.CountAsync(CancellationToken);
        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 1,
                    PageSize = 3,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(3);
        result.Value.TotalCount.ShouldBe(expectedMovieCount);
        result.Value.MediaCount.ShouldBe(expectedMovieCount);
    }

    [Test]
    public async Task ShouldCountOnlyRequestedMediaType_WhenAllLibraryModeHasMultipleServersAndMixedLibraries()
    {
        // Arrange
        await SetupDatabase(
            70103,
            cfg =>
            {
                cfg.PlexServerCount = 2;
                cfg.PlexMovieLibraryCount = 2;
                cfg.PlexTvShowLibraryCount = 2;
                cfg.MovieCount = 3;
                cfg.TvShowCount = 4;
            }
        );

        var expectedMovieCount = await IDbContext.PlexMovies.CountAsync(CancellationToken);
        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 1,
                    PageSize = 2,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
        result.Value.Items.ShouldAllBe(x => x.Type == PlexMediaType.Movie);
        result.Value.TotalCount.ShouldBe(expectedMovieCount);
        result.Value.MediaCount.ShouldBe(expectedMovieCount);
        result.Value.MovieCount.ShouldBe(expectedMovieCount);
        result.Value.TvShowCount.ShouldBe(0);
    }

    [Test]
    public async Task ShouldAssignGlobalSortIndexes_WhenPageChanges()
    {
        // Arrange
        await SetupDatabase(
            70104,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 10;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 2,
                    PageSize = 3,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(x => x.SortIndex).ShouldBe([4, 5, 6]);
    }

    [Test]
    public async Task ShouldBuildNavigationIndexesFromFullSortedResult_WhenPageIsPartial()
    {
        // Arrange
        await SetupDatabase(
            70105,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 10;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 1,
                    PageSize = 2,
                    Sort = "Year:asc",
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
        result.Value.NavigationIndexes.ShouldNotBeEmpty();
        result.Value.NavigationIndexes.Max(x => x.Index).ShouldBeLessThan(result.Value.TotalCount);
    }

    [Test]
    public async Task ShouldReturnDifferentPageData_WhenPageChanges()
    {
        // Arrange
        await SetupDatabase(
            70015,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 10;
            }
        );

        var page1 = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 1,
                    PageSize = 3,
                    Sort = null,
                    Filter = null,
                },
            }
        );
        var page2 = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 2,
                    PageSize = 3,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var resultPage1 = await TestHandlerExecuteAsync<PagedMediaQueryResult>(page1);
        var page1Ids = resultPage1.Value.Items.Select(x => x.Id).ToList();

        var resultPage2 = await TestHandlerExecuteAsync<PagedMediaQueryResult>(page2);
        var page2Ids = resultPage2.Value.Items.Select(x => x.Id).ToList();

        // Assert
        resultPage1.IsSuccess.ShouldBeTrue();
        resultPage2.IsSuccess.ShouldBeTrue();
        page1Ids.ShouldNotBe(page2Ids);
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenSpecificLibraryIdDoesNotExist()
    {
        // Arrange
        await SetupDatabase(
            70016,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 4;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 999999,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(0);
        result.Value.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnOnlyMovies_WhenMovieTypeRequestedAndBothMediaTypesExist()
    {
        // Arrange
        await SetupDatabase(
            70017,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.PlexTvShowLibraryCount = 1;
                cfg.MovieCount = 5;
                cfg.TvShowCount = 5;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.ShouldAllBe(x => x.Type == PlexMediaType.Movie);
    }

    [Test]
    public async Task ShouldNotExcludeOwnedLibraries_WhenFilterOwnedMediaIsFalse()
    {
        // Arrange
        await SetupDatabase(
            70019,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 2;
                cfg.MovieCount = 4;
                cfg.PlexAccountCount = 1;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.Select(x => x.PlexLibraryId).Distinct().Count().ShouldBe(2);
    }

    [Test]
    public async Task ShouldNotExcludeOfflineLibraries_WhenFilterOfflineMediaIsFalse()
    {
        // Arrange
        await SetupDatabase(
            70020,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 4;
            }
        );

        var dbContext = IDbContext;
        await dbContext.PlexServerStatuses.ExecuteDeleteAsync(CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenSpecificLibraryExistsButForDifferentMediaType()
    {
        // Arrange
        await SetupDatabase(
            70021,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.PlexTvShowLibraryCount = 1;
                cfg.MovieCount = 4;
                cfg.TvShowCount = 4;
            }
        );

        var dbContext = IDbContext;
        var tvShowLibraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.TvShow)
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = tvShowLibraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnEmpty_WhenRequestedPageIsBeyondAvailableRange()
    {
        // Arrange
        await SetupDatabase(
            70022,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 3;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 5,
                    PageSize = 10,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.TotalCount.ShouldBe(3);
        result.Value.MediaCount.ShouldBe(3);
        result.Value.MovieCount.ShouldBe(3);
    }

    [Test]
    public async Task ShouldTreatServerAsOffline_WhenOnlyUnsuccessfulStatusesExistAndFilterOfflineMediaIsTrue()
    {
        // Arrange
        await SetupDatabase(
            70023,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 5;
            }
        );

        var dbContext = IDbContext;
        await dbContext.PlexServerStatuses.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.IsSuccessful, false),
            CancellationToken
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = true,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldApplyProvidedSort_WhenSortExpressionIsSet()
    {
        // Arrange
        await SetupDatabase(
            70024,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 10;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = 10,
                    Sort = "Year:desc",
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
        result.Value.Items.Zip(result.Value.Items.Skip(1)).ShouldAllBe(x => x.First.Year >= x.Second.Year);
    }

    [Test]
    [Arguments("Title:asc")]
    [Arguments("sortIndex:asc")]
    [Arguments("SORTINDEX:asc")]
    [Arguments("SoRtInDeX:asc")]
    public async Task ShouldSortAllLibraryMoviesByTitle_WhenCanonicalOrLegacyTitleSortIsRequested(string sort)
    {
        // Arrange
        await SetupDatabase(
            70233,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 2;
                cfg.MovieCount = 4;
            }
        );

        var dbContext = IDbContext;
        var movieIds = await dbContext.PlexMovies.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);

        await SetAllLibrarySortIndexRegressionDataAsync(dbContext, movieIds);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = 10,
                    Sort = sort,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result
            .Value.Items.Select(x => x.Title)
            .ShouldBe(["Alpha", "Bravo", "Charlie", "Delta", "Whiskey", "Xray", "Yankee", "Zulu"]);
        result.Value.NavigationIndexes.Select(x => x.Label).ShouldBe(["A", "B", "C", "D", "W", "X", "Y", "Z"]);
    }

    [Test]
    public async Task ShouldSortMoviesByVideoQualityAscending_WhenVideoQualitySortFieldIsUsed()
    {
        // Arrange
        await SetupDatabase(
            70030,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 4;
            }
        );

        var dbContext = IDbContext;
        var movieIds = await dbContext.PlexMovies.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);

        await SetMovieQualitySortTestDataAsync(dbContext, movieIds);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = 10,
                    Sort = "videoQuality:asc,Year:desc",
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result
            .Value.Items.Select(x => x.Title)
            .ShouldBe(["SD Movie (2021)", "SD Movie (2000)", "HD Movie", "4K Movie"]);
        result
            .Value.NavigationIndexes.Select(x => new { x.Label, x.Index })
            .ShouldBe([
                new { Label = "480", Index = 0 },
                new { Label = "1080", Index = 2 },
                new { Label = "2160", Index = 3 },
            ]);
    }

    [Test]
    public async Task ShouldSortMoviesByVideoQualityDescending_WhenVideoQualitySortFieldIsUsed()
    {
        // Arrange
        await SetupDatabase(
            70031,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 4;
            }
        );

        var dbContext = IDbContext;
        var movieIds = await dbContext.PlexMovies.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync(CancellationToken);

        await SetMovieQualitySortTestDataAsync(dbContext, movieIds);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = 10,
                    Sort = "videoQuality:desc,Year:asc",
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result
            .Value.Items.Select(x => x.Title)
            .ShouldBe(["4K Movie", "HD Movie", "SD Movie (2000)", "SD Movie (2021)"]);
        result
            .Value.NavigationIndexes.Select(x => new { x.Label, x.Index })
            .ShouldBe([
                new { Label = "2160", Index = 0 },
                new { Label = "1080", Index = 1 },
                new { Label = "480", Index = 2 },
            ]);
    }

    [Test]
    public async Task ShouldReturnOnlyRequestedLibrary_WhenSpecificLibraryIdAndSortProvided()
    {
        // Arrange
        await SetupDatabase(
            70025,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 2;
                cfg.MovieCount = 5;
            }
        );

        var dbContext = IDbContext;
        var targetLibraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = targetLibraryId,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = 10,
                    Sort = "Year:asc",
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldNotBeEmpty();
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == targetLibraryId);
    }

    [Test]
    public async Task ShouldUseLibraryCounts_WhenNoFilterOrSearchAndAllLibrariesRequested()
    {
        // Arrange
        await SetupDatabase(
            70026,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 2;
                cfg.MovieCount = 6;
            }
        );

        var dbContext = IDbContext;
        var expectedMovieCount = await dbContext.PlexMovies.CountAsync(CancellationToken);
        var expectedMediaSize = await dbContext.PlexMovies.SumAsync(x => x.MediaSize, CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(expectedMovieCount);
        result.Value.MediaCount.ShouldBe(expectedMovieCount);
        result.Value.MovieCount.ShouldBe(expectedMovieCount);
        result.Value.TvShowCount.ShouldBe(0);
        result.Value.SeasonCount.ShouldBe(0);
        result.Value.EpisodeCount.ShouldBe(0);
        result.Value.MediaSize.ShouldBe(expectedMediaSize);
    }

    [Test]
    public async Task ShouldUseRequestedLibraryCounts_WhenNoFilterOrSearchAndSpecificLibraryRequested()
    {
        // Arrange
        await SetupDatabase(
            70027,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 2;
                cfg.MovieCount = 6;
            }
        );

        var dbContext = IDbContext;
        var targetLibrary = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id })
            .FirstAsync(CancellationToken);
        var expectedMovieCount = await dbContext
            .PlexMovies.Where(x => x.PlexLibraryId == targetLibrary.Id)
            .CountAsync(CancellationToken);
        var expectedMediaSize = await dbContext
            .PlexMovies.Where(x => x.PlexLibraryId == targetLibrary.Id)
            .SumAsync(x => x.MediaSize, CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = targetLibrary.Id,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == targetLibrary.Id);
        result.Value.TotalCount.ShouldBe(expectedMovieCount);
        result.Value.MediaCount.ShouldBe(expectedMovieCount);
        result.Value.MovieCount.ShouldBe(expectedMovieCount);
        result.Value.TvShowCount.ShouldBe(0);
        result.Value.SeasonCount.ShouldBe(0);
        result.Value.EpisodeCount.ShouldBe(0);
        result.Value.MediaSize.ShouldBe(expectedMediaSize);
    }

    [Test]
    public async Task ShouldCalculateMediaSizeFromMovieRows_WhenLibraryMediaSizeSnapshotIsStale()
    {
        // Arrange
        await SetupDatabase(
            70232,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 6;
            }
        );

        var dbContext = IDbContext;
        var targetLibrary = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        var expectedMediaSize = await dbContext
            .PlexMovies.Where(x => x.PlexLibraryId == targetLibrary.Id)
            .SumAsync(x => x.MediaSize, CancellationToken);
        await dbContext
            .PlexLibraries.Where(x => x.Id == targetLibrary.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.MediaSize, 0), CancellationToken);

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = targetLibrary.Id,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = null,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == targetLibrary.Id);
        result.Value.MediaSize.ShouldBe(expectedMediaSize);
        result.Value.TotalMediaSize.ShouldBe(expectedMediaSize);
        result.Value.MediaSize.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task ShouldFilterMoviesByCountryId_WhenMetadataCountryFilterIsApplied()
    {
        // Arrange
        await SetupDatabase(
            70028,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 6;
            }
        );

        var dbContext = IDbContext;
        var expectedMovie = await ConfigureExactMetadataMatchAsync(dbContext, VideoQuality.FullHD);

        var filter = $"Countries:any:Id:eq:{expectedMovie.CountryId}";
        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = filter,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsFailed.ShouldBeFalse();
        AssertReturnedExactMovies(result.Value, [expectedMovie.MovieId, expectedMovie.CountryOnlyMovieId]);
    }

    [Test]
    public async Task ShouldFilterMoviesByRoleId_WhenMetadataRoleFilterIsApplied()
    {
        // Arrange
        await SetupDatabase(
            70029,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 6;
            }
        );

        var dbContext = IDbContext;
        var expectedMovie = await ConfigureExactMetadataMatchAsync(dbContext, VideoQuality.FullHD);

        var filter = $"Actors:any:Id:eq:{expectedMovie.ActorId}";
        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = filter,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsFailed.ShouldBeFalse();
        AssertReturnedExactMovies(result.Value, [expectedMovie.MovieId, expectedMovie.ActorOnlyMovieId]);
    }

    [Test]
    public async Task ShouldFilterMoviesByGenreId_WhenMetadataGenreFilterIsApplied()
    {
        // Arrange
        await SetupDatabase(
            70230,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 6;
            }
        );

        var dbContext = IDbContext;
        var expectedMovie = await ConfigureExactMetadataMatchAsync(dbContext, VideoQuality.FullHD);

        var filter = $"Genres:any:Id:eq:{expectedMovie.GenreId}";
        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = null,
                    Sort = null,
                    Filter = filter,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsFailed.ShouldBeFalse();
        AssertReturnedExactMovies(result.Value, [expectedMovie.MovieId, expectedMovie.GenreOnlyMovieId]);
    }

    [Test]
    public async Task ShouldFilterMoviesByQualityGenreCountryAndRole_WhenAllMetadataFiltersAreApplied()
    {
        // Arrange
        await SetupDatabase(
            70231,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 6;
            }
        );

        var dbContext = IDbContext;
        var expectedMovie = await ConfigureExactMetadataMatchAsync(dbContext, VideoQuality.FullHD);
        var otherQualityMovie = await dbContext
            .PlexMovies.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.PlexLibraryId })
            .Skip(4)
            .FirstAsync(CancellationToken);
        dbContext.PlexMovieGenres.Add(
            new PlexMovieGenres(expectedMovie.GenreId, otherQualityMovie.PlexLibraryId, otherQualityMovie.Id)
        );
        dbContext.PlexMovieCountries.Add(
            new PlexMovieCountries(expectedMovie.CountryId, otherQualityMovie.PlexLibraryId, otherQualityMovie.Id)
        );
        dbContext.PlexMovieActors.Add(
            new PlexMovieActors(expectedMovie.ActorId, otherQualityMovie.PlexLibraryId, otherQualityMovie.Id)
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        var filter = string.Join(
            "&",
            $"MediaDataList:any:VideoQuality:eq:{VideoQuality.FullHD}",
            $"Genres:any:Id:eq:{expectedMovie.GenreId}",
            $"Countries:any:Id:eq:{expectedMovie.CountryId}",
            $"Actors:any:Id:eq:{expectedMovie.ActorId}"
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = null,
                    PageSize = 20,
                    Sort = null,
                    Filter = filter,
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsFailed.ShouldBeFalse();
        AssertReturnedExactMovies(result.Value, [expectedMovie.MovieId]);
        result.Value.Items.ShouldAllBe(x => x.Qualities.Any(y => y.VideoQuality == VideoQuality.FullHD));

        var returnedMovieId = result.Value.Items.Single().Id;
        var hasMatchingGenre = await dbContext.PlexMovieGenres.AnyAsync(
            x => x.PlexMovieId == returnedMovieId && x.GenresId == expectedMovie.GenreId,
            CancellationToken
        );
        var hasMatchingCountry = await dbContext.PlexMovieCountries.AnyAsync(
            x => x.PlexMovieId == returnedMovieId && x.CountryId == expectedMovie.CountryId,
            CancellationToken
        );
        var hasMatchingActor = await dbContext.PlexMovieActors.AnyAsync(
            x => x.PlexMovieId == returnedMovieId && x.PlexActorId == expectedMovie.ActorId,
            CancellationToken
        );

        hasMatchingGenre.ShouldBeTrue();
        hasMatchingCountry.ShouldBeTrue();
        hasMatchingActor.ShouldBeTrue();
        result.Value.Genres.ShouldBe([expectedMovie.GenreId]);
        result.Value.Countries.ShouldBe([expectedMovie.CountryId]);
        result.Value.Roles.ShouldBe([expectedMovie.ActorId]);
        result.Value.Qualities.ShouldBe([7]);
    }

    [Test]
    public async Task ShouldLeaveRawFilterUnchanged_WhenTheSameCommandExecutesRepeatedly()
    {
        // Arrange
        await SetupDatabase(
            70232,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 4;
            }
        );
        var dbContext = IDbContext;
        var libraryId = await dbContext.PlexLibraries.Select(x => x.Id).SingleAsync(CancellationToken);
        var expectedIds = await dbContext
            .PlexMovies.OrderBy(x => x.SortIndex)
            .Select(x => x.Id)
            .Take(2)
            .ToListAsync(CancellationToken);
        var filter = new MediaQueryFilter
        {
            MediaType = PlexMediaType.Movie,
            PlexLibraryId = libraryId,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters
            {
                Page = 1,
                PageSize = 2,
                Sort = "Title:asc",
            },
        };
        var command = new GetMovieMediaByTypeCommand(filter);
        var originalSort = filter.Parameters.Sort;
        var originalHash = filter.QueryHash;
        var materializations = new EntityMaterializationCounter();
        var options = new DbContextOptionsBuilder<ReaparrDbContext>();
        options.ConfigureSqlite(dbContext.DatabaseName, SqliteOpenMode.Memory);
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).AddInterceptors(materializations);
        await using var measuredContext = new ReaparrDbContext(
            options.Options,
            Mock.Container.Resolve<ILogger>(),
            Mock.Container.Resolve<IPathProvider>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            dbContext.DatabaseName
        );
        SetupDependencies(builder =>
            builder.RegisterInstance(measuredContext).As<IReaparrDbContext>().ExternallyOwned()
        );
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<ApplyComparisonStateCommand>(c =>
                        c.MediaType == PlexMediaType.Movie
                        && c.PlexLibraryId == libraryId
                        && c.Items.Select(item => item.Id).SequenceEqual(expectedIds)
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(2));

        // Act
        var first = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);
        var second = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        first.IsSuccess.ShouldBeTrue();
        first.Errors.Count.ShouldBe(0);
        second.IsSuccess.ShouldBeTrue();
        second.Errors.Count.ShouldBe(0);
        filter.Parameters.Sort.ShouldBe(originalSort);
        filter.QueryHash.ShouldBe(originalHash);
        first.Value.QueryHash.ShouldBe(originalHash);
        second.Value.QueryHash.ShouldBe(originalHash);
        first.Value.Items.Select(x => x.Id).ShouldBe(expectedIds);
        first.Value.Items.Select(x => x.SortIndex).ShouldBe([1, 2]);
        first.Value.TotalCount.ShouldBe(4);
        materializations.Types.ShouldBeEmpty();
        second.Value.Items.Select(x => x.Id).ShouldBe(first.Value.Items.Select(x => x.Id));
        second.Value.Items.Select(x => x.SortIndex).ShouldBe(first.Value.Items.Select(x => x.SortIndex));
        second.Value.TotalCount.ShouldBe(first.Value.TotalCount);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    private sealed class EntityMaterializationCounter : IMaterializationInterceptor
    {
        public List<Type> Types { get; } = [];

        public object InitializedInstance(MaterializationInterceptionData materializationData, object instance)
        {
            Types.Add(instance.GetType());
            return instance;
        }
    }

    private async Task<ExpectedMovieMetadata> ConfigureExactMetadataMatchAsync(
        IReaparrDbContext dbContext,
        VideoQuality targetQuality
    )
    {
        var movies = await dbContext
            .PlexMovies.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlexLibraryId,
                x.PlexServerId,
            })
            .ToListAsync(CancellationToken);

        movies.Count.ShouldBeGreaterThanOrEqualTo(4);

        var expectedMovie = movies[0];
        var genreOnlyMovie = movies[1];
        var countryOnlyMovie = movies[2];
        var actorOnlyMovie = movies[3];

        var genre = new PlexGenreVideo
        {
            Name = "Regression Genre",
            Key = "regression-genre",
            Type = PlexGenreType.Unknown,
        };
        var country = new PlexCountry { Name = "Regression Country", Key = "regression-country" };
        var actor = new PlexActor { Name = "Regression Actor", Key = "regression-actor" };

        dbContext.PlexGenresVideo.Add(genre);
        dbContext.PlexCountries.Add(country);
        dbContext.PlexActors.Add(actor);
        await dbContext.SaveChangesAsync(CancellationToken);

        dbContext.PlexMovieGenres.AddRange(
            new PlexMovieGenres(genre.Id, expectedMovie.PlexLibraryId, expectedMovie.Id),
            new PlexMovieGenres(genre.Id, genreOnlyMovie.PlexLibraryId, genreOnlyMovie.Id)
        );
        dbContext.PlexMovieCountries.AddRange(
            new PlexMovieCountries(country.Id, expectedMovie.PlexLibraryId, expectedMovie.Id),
            new PlexMovieCountries(country.Id, countryOnlyMovie.PlexLibraryId, countryOnlyMovie.Id)
        );
        dbContext.PlexMovieActors.AddRange(
            new PlexMovieActors(actor.Id, expectedMovie.PlexLibraryId, expectedMovie.Id),
            new PlexMovieActors(actor.Id, actorOnlyMovie.PlexLibraryId, actorOnlyMovie.Id)
        );

        await dbContext.SaveChangesAsync(CancellationToken);

        await dbContext
            .PlexMovieData.Where(x => x.PlexMovieId == expectedMovie.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.VideoQuality, targetQuality), CancellationToken);

        await dbContext
            .PlexMovieData.Where(x => x.PlexMovieId != expectedMovie.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.VideoQuality, VideoQuality.HD), CancellationToken);

        return new ExpectedMovieMetadata(
            expectedMovie.Id,
            genreOnlyMovie.Id,
            countryOnlyMovie.Id,
            actorOnlyMovie.Id,
            genre.Id,
            country.Id,
            actor.Id
        );
    }

    private async Task SetAllLibrarySortIndexRegressionDataAsync(
        IReaparrDbContext dbContext,
        IReadOnlyList<int> movieIds
    )
    {
        movieIds.Count.ShouldBeGreaterThanOrEqualTo(8);

        var titlesById = new[]
        {
            (movieIds[0], "Zulu", 1),
            (movieIds[1], "Alpha", 1),
            (movieIds[2], "Charlie", 2),
            (movieIds[3], "Bravo", 2),
            (movieIds[4], "Yankee", 3),
            (movieIds[5], "Delta", 3),
            (movieIds[6], "Xray", 4),
            (movieIds[7], "Whiskey", 4),
        };

        foreach (var (movieId, title, sortIndex) in titlesById)
        {
            await dbContext
                .PlexMovies.Where(x => x.Id == movieId)
                .ExecuteUpdateAsync(
                    x =>
                        x.SetProperty(y => y.Title, title)
                            .SetProperty(y => y.SearchTitle, title.ToLowerInvariant())
                            .SetProperty(y => y.SortIndex, sortIndex),
                    CancellationToken
                );
        }
    }

    private async Task SetMovieQualitySortTestDataAsync(IReaparrDbContext dbContext, IReadOnlyList<int> movieIds)
    {
        await dbContext
            .PlexMovies.Where(x => x.Id == movieIds[0])
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.Title, "SD Movie (2000)")
                        .SetProperty(y => y.Year, 2000)
                        .SetProperty(y => y.VideoQuality, VideoQuality.SD),
                CancellationToken
            );

        await dbContext
            .PlexMovies.Where(x => x.Id == movieIds[1])
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.Title, "4K Movie")
                        .SetProperty(y => y.Year, 2020)
                        .SetProperty(y => y.VideoQuality, VideoQuality.UHD_4K),
                CancellationToken
            );

        await dbContext
            .PlexMovies.Where(x => x.Id == movieIds[2])
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.Title, "HD Movie")
                        .SetProperty(y => y.Year, 2010)
                        .SetProperty(y => y.VideoQuality, VideoQuality.FullHD),
                CancellationToken
            );

        await dbContext
            .PlexMovies.Where(x => x.Id == movieIds[3])
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.Title, "SD Movie (2021)")
                        .SetProperty(y => y.Year, 2021)
                        .SetProperty(y => y.VideoQuality, VideoQuality.SD),
                CancellationToken
            );
    }

    [Test]
    [Arguments("Id:asc", false, PlexMediaType.Movie)]
    [Arguments("sortTitle:asc", false, PlexMediaType.Movie)]
    [Arguments("quality:asc", false, PlexMediaType.Movie)]
    [Arguments("1:asc", false, PlexMediaType.Movie)]
    [Arguments("comparisonState:asc", true, PlexMediaType.Movie)]
    [Arguments("Title:asc,Id:asc", false, PlexMediaType.Movie)]
    [Arguments("Title:asc", true, PlexMediaType.Movie)]
    [Arguments("SortIndex:desc", true, PlexMediaType.Movie)]
    [Arguments("SearchTitle:asc", true, PlexMediaType.Movie)]
    public void ShouldValidateCanonicalSortFields_AndRejectRemovedOrRawIdSorts(
        string sort,
        bool valid,
        PlexMediaType mediaType
    )
    {
        // Arrange
        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = mediaType,
                PlexLibraryId = 1,
                FilterOfflineMedia = false,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters { Sort = sort },
            }
        );

        // Act
        var result = new GetMovieMediaByTypeCommandValidator().Validate(command);

        // Assert
        result.IsValid.ShouldBe(valid);
        if (!valid)
            result.Errors.Select(x => x.PropertyName).ShouldContain("Filter.Parameters.Sort");
    }

    [Test]
    public async Task ShouldSetRequestHashFromFilter_WhenQueryReturnsResults()
    {
        // Arrange
        await SetupDatabase(
            70231,
            cfg =>
            {
                cfg.PlexServerCount = 1;
                cfg.PlexMovieLibraryCount = 1;
                cfg.MovieCount = 4;
            }
        );

        var command = new GetMovieMediaByTypeCommand(
            new MediaQueryFilter
            {
                MediaType = PlexMediaType.Movie,
                PlexLibraryId = 0,
                FilterOfflineMedia = true,
                FilterOwnedMedia = false,
                Parameters = new FlexQueryParameters
                {
                    Page = 2,
                    PageSize = 3,
                    Sort = "Year:desc",
                    Filter = "Year:gte:2000",
                },
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PagedMediaQueryResult>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.QueryHash.ShouldBe(command.Filter.QueryHash);
    }

    private static void AssertReturnedExactMovies(
        PagedMediaQueryResult result,
        IReadOnlyCollection<int> expectedMovieIds
    )
    {
        expectedMovieIds.ShouldNotBeEmpty();
        result.Items.ShouldNotBeEmpty();
        result.Items.Select(x => x.Id).OrderBy(x => x).ShouldBe(expectedMovieIds.OrderBy(x => x));
        result.TotalCount.ShouldBe(expectedMovieIds.Count);
        result.MediaCount.ShouldBe(expectedMovieIds.Count);
        result.MovieCount.ShouldBe(expectedMovieIds.Count);
        result.TvShowCount.ShouldBe(0);
        result.SeasonCount.ShouldBe(0);
        result.EpisodeCount.ShouldBe(0);
    }

    private sealed record ExpectedMovieMetadata(
        int MovieId,
        int GenreOnlyMovieId,
        int CountryOnlyMovieId,
        int ActorOnlyMovieId,
        int GenreId,
        int CountryId,
        int ActorId
    );
}
