namespace Reaparr.Application.UnitTests;

public class ApplyComparisonStateCommandHandlerUnitTests : BaseCommandUnitTest<ApplyComparisonStateCommand>
{
    [Test]
    public async Task ShouldReturnSuccessWithoutDispatch_WhenItemsAreEmpty()
    {
        // Arrange
        await SetupDatabase(
            70,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var command = new ApplyComparisonStateCommand([], PlexMediaType.Movie, 1);

        Mock.SetupCommand<Result>(x => x is ApplyOwnedMovieComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result>(x => x is ApplyRemoteMovieComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldFailValidation_WhenAllLibraryItemHasNoLibraryId()
    {
        // Arrange
        var items = new List<PlexMediaSlimDTO> { CreateItem(881, PlexMediaType.Movie) };
        var command = new ApplyComparisonStateCommand(items, PlexMediaType.Movie, null);

        Mock.SetupCommand<Result>(x => x is ApplyOwnedMovieComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result>(x => x is ApplyRemoteMovieComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldFailValidation_WhenExplicitLibraryIdIsZero()
    {
        // Arrange
        var command = new ApplyComparisonStateCommand(
            [CreateItem(901, PlexMediaType.Movie, 1)],
            PlexMediaType.Movie,
            0
        );

        Mock.SetupCommand<Result>(x => x is ApplyOwnedMovieComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result>(x => x is ApplyRemoteMovieComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldDispatchMovieProjectionPerLibrary_WhenAllLibraryScopeIsRequested()
    {
        // Arrange
        await SetupDatabase(
            76,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 2;
                config.PlexAccountCount = 1;
            }
        );
        var libraries = await IDbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, true);

        var items = new List<PlexMediaSlimDTO>
        {
            CreateItem(887, PlexMediaType.Movie, libraries[0].Id),
            CreateItem(888, PlexMediaType.Movie, libraries[1].Id),
        };
        var observed = new List<(int LibraryId, List<int> ItemIds)>();
        var command = new ApplyComparisonStateCommand(items, PlexMediaType.Movie, null);

        Mock.SetupCommand<Result>(x =>
                (x as ApplyOwnedMovieComparisonStateCommand) != null
                && libraries.Select(library => library.Id).Contains(((ApplyOwnedMovieComparisonStateCommand)x).OwnedLibraryId)
            )
            .Callback<ICommand<Result>, CancellationToken>(
                (sentCommand, _) =>
                {
                    var projection = (ApplyOwnedMovieComparisonStateCommand)sentCommand;
                    observed.Add(
                        (
                            projection.OwnedLibraryId,
                            projection.Items.Select(item => item.Id).OrderBy(id => id).ToList()
                        )
                    );
                }
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(libraries.Count));
        Mock.SetupCommand<Result>(x => x is ApplyRemoteMovieComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        observed.Select(x => x.LibraryId).OrderBy(x => x).ShouldBe(libraries.Select(x => x.Id).OrderBy(x => x));
        observed.Single(x => x.LibraryId == libraries[0].Id).ItemIds.ShouldBe([887]);
        observed.Single(x => x.LibraryId == libraries[1].Id).ItemIds.ShouldBe([888]);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldDispatchTvShowProjectionPerLibrary_WhenAllLibraryScopeIsRequested()
    {
        // Arrange
        await SetupDatabase(
            77,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 2;
                config.PlexAccountCount = 1;
            }
        );
        var libraries = await IDbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, true);

        var items = new List<PlexMediaSlimDTO>
        {
            CreateItem(889, PlexMediaType.TvShow, libraries[0].Id),
            CreateItem(890, PlexMediaType.TvShow, libraries[1].Id),
        };
        var observed = new List<(int LibraryId, List<int> ItemIds)>();
        var command = new ApplyComparisonStateCommand(items, PlexMediaType.TvShow, null);

        Mock.SetupCommand<Result>(x =>
                (x as ApplyOwnedTvShowComparisonStateCommand) != null
                && libraries.Select(library => library.Id).Contains(((ApplyOwnedTvShowComparisonStateCommand)x).OwnedLibraryId)
            )
            .Callback<ICommand<Result>, CancellationToken>(
                (sentCommand, _) =>
                {
                    var projection = (ApplyOwnedTvShowComparisonStateCommand)sentCommand;
                    observed.Add(
                        (
                            projection.OwnedLibraryId,
                            projection.Items.Select(item => item.Id).OrderBy(id => id).ToList()
                        )
                    );
                }
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Exactly(libraries.Count));
        Mock.SetupCommand<Result>(x => x is ApplyRemoteTvShowComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        observed.Select(x => x.LibraryId).OrderBy(x => x).ShouldBe(libraries.Select(x => x.Id).OrderBy(x => x));
        observed.Single(x => x.LibraryId == libraries[0].Id).ItemIds.ShouldBe([889]);
        observed.Single(x => x.LibraryId == libraries[1].Id).ItemIds.ShouldBe([890]);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldDispatchOwnedAndRemoteMovieProjection_WhenAllLibraryScopeContainsBothOwnershipTypes()
    {
        // Arrange
        await SetupDatabase(
            78,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var libraries = await IDbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, true);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, false);

        var items = new List<PlexMediaSlimDTO>
        {
            CreateItem(891, PlexMediaType.Movie, libraries[0].Id),
            CreateItem(892, PlexMediaType.Movie, libraries[0].Id),
            CreateItem(893, PlexMediaType.Movie, libraries[1].Id),
        };
        var command = new ApplyComparisonStateCommand(items, PlexMediaType.Movie, null);

        Mock.SetupCommand<Result>(x =>
                (x as ApplyOwnedMovieComparisonStateCommand) != null
                && ((ApplyOwnedMovieComparisonStateCommand)x).OwnedLibraryId == libraries[0].Id
            )
            .Callback<ICommand<Result>, CancellationToken>(
                (sentCommand, _) =>
                {
                    var projection = (ApplyOwnedMovieComparisonStateCommand)sentCommand;
                    projection.Items.Select(item => item.Id).OrderBy(id => id).ShouldBe([891, 892]);
                }
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);
        Mock.SetupCommand<Result>(x =>
                (x as ApplyRemoteMovieComparisonStateCommand) != null
                && ((ApplyRemoteMovieComparisonStateCommand)x).RemoteLibraryId == libraries[1].Id
            )
            .Callback<ICommand<Result>, CancellationToken>(
                (sentCommand, _) =>
                {
                    var projection = (ApplyRemoteMovieComparisonStateCommand)sentCommand;
                    projection.Items.Select(item => item.Id).ShouldBe([893]);
                }
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);
        Mock.SetupCommand<Result>(x =>
                x is ApplyOwnedMovieComparisonStateCommand
                && ((ApplyOwnedMovieComparisonStateCommand)x).OwnedLibraryId == libraries[1].Id
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result>(x =>
                x is ApplyRemoteMovieComparisonStateCommand
                && ((ApplyRemoteMovieComparisonStateCommand)x).RemoteLibraryId == libraries[0].Id
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldDispatchOwnedAndRemoteTvShowProjection_WhenAllLibraryScopeContainsBothOwnershipTypes()
    {
        // Arrange
        await SetupDatabase(
            79,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexTvShowLibraryCount = 1;
            }
        );
        var libraries = await IDbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, true);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, false);

        var items = new List<PlexMediaSlimDTO>
        {
            CreateItem(894, PlexMediaType.TvShow, libraries[0].Id),
            CreateItem(895, PlexMediaType.TvShow, libraries[1].Id),
            CreateItem(896, PlexMediaType.TvShow, libraries[1].Id),
        };
        var command = new ApplyComparisonStateCommand(items, PlexMediaType.TvShow, null);

        Mock.SetupCommand<Result>(x =>
                (x as ApplyOwnedTvShowComparisonStateCommand) != null
                && ((ApplyOwnedTvShowComparisonStateCommand)x).OwnedLibraryId == libraries[0].Id
            )
            .Callback<ICommand<Result>, CancellationToken>(
                (sentCommand, _) =>
                {
                    var projection = (ApplyOwnedTvShowComparisonStateCommand)sentCommand;
                    projection.Items.Select(item => item.Id).ShouldBe([894]);
                }
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);
        Mock.SetupCommand<Result>(x =>
                (x as ApplyRemoteTvShowComparisonStateCommand) != null
                && ((ApplyRemoteTvShowComparisonStateCommand)x).RemoteLibraryId == libraries[1].Id
            )
            .Callback<ICommand<Result>, CancellationToken>(
                (sentCommand, _) =>
                {
                    var projection = (ApplyRemoteTvShowComparisonStateCommand)sentCommand;
                    projection.Items.Select(item => item.Id).OrderBy(id => id).ShouldBe([895, 896]);
                }
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);
        Mock.SetupCommand<Result>(x =>
                x is ApplyOwnedTvShowComparisonStateCommand
                && ((ApplyOwnedTvShowComparisonStateCommand)x).OwnedLibraryId == libraries[1].Id
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result>(x =>
                x is ApplyRemoteTvShowComparisonStateCommand
                && ((ApplyRemoteTvShowComparisonStateCommand)x).RemoteLibraryId == libraries[0].Id
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldDispatchExplicitLibraryProjectionAsOneBatch_WhenItemsHaveDifferentLibraryIds()
    {
        // Arrange
        await SetupDatabase(
            80,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var libraries = await IDbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, true);

        var items = new List<PlexMediaSlimDTO>
        {
            CreateItem(897, PlexMediaType.Movie, libraries[0].Id),
            CreateItem(898, PlexMediaType.Movie, libraries[1].Id),
        };
        var command = new ApplyComparisonStateCommand(items, PlexMediaType.Movie, libraries[0].Id);

        Mock.SetupCommand<Result>(x =>
                (x as ApplyOwnedMovieComparisonStateCommand) != null
                && ((ApplyOwnedMovieComparisonStateCommand)x).OwnedLibraryId == libraries[0].Id
                && ReferenceEquals(((ApplyOwnedMovieComparisonStateCommand)x).Items, items)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);
        Mock.SetupCommand<Result>(x => x is ApplyRemoteMovieComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldStopDispatchingRemainingLibraries_WhenProjectionFails()
    {
        // Arrange
        await SetupDatabase(
            81,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
            }
        );
        var libraries = await IDbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        await SetOwnedOverrideAsync(libraries[0].PlexServerId, true);
        await SetOwnedOverrideAsync(libraries[1].PlexServerId, false);

        var command = new ApplyComparisonStateCommand(
            [
                CreateItem(899, PlexMediaType.Movie, libraries[0].Id),
                CreateItem(900, PlexMediaType.Movie, libraries[1].Id),
            ],
            PlexMediaType.Movie,
            null
        );

        Mock.SetupCommand<Result>(x =>
                (x as ApplyOwnedMovieComparisonStateCommand) != null
                && ((ApplyOwnedMovieComparisonStateCommand)x).OwnedLibraryId == libraries[0].Id
            )
            .ReturnsAsync(Result.Fail("Projection failed"))
            .Verifiable(Times.Once);
        Mock.SetupCommand<Result>(x => x is ApplyRemoteMovieComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result>(x =>
                x is ApplyOwnedMovieComparisonStateCommand
                && ((ApplyOwnedMovieComparisonStateCommand)x).OwnedLibraryId == libraries[1].Id
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldDispatchOwnedMovieProjection_WhenMovieLibraryIsOwned()
    {
        // Arrange
        await SetupDatabase(
            71,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var library = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        await SetOwnedOverrideAsync(library.PlexServerId, true);
        var items = new List<PlexMediaSlimDTO> { CreateItem(882, PlexMediaType.Movie) };
        var command = new ApplyComparisonStateCommand(items, PlexMediaType.Movie, library.Id);

        Mock.SetupCommand<Result>(x =>
                (x as ApplyOwnedMovieComparisonStateCommand) != null
                && ((ApplyOwnedMovieComparisonStateCommand)x).OwnedLibraryId == library.Id
                && ReferenceEquals(((ApplyOwnedMovieComparisonStateCommand)x).Items, items)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);
        Mock.SetupCommand<Result>(x => x is ApplyRemoteMovieComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldDispatchRemoteMovieProjection_WhenMovieLibraryIsNotOwned()
    {
        // Arrange
        await SetupDatabase(
            72,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var library = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        await SetOwnedOverrideAsync(library.PlexServerId, false);
        var items = new List<PlexMediaSlimDTO> { CreateItem(883, PlexMediaType.Movie) };
        var command = new ApplyComparisonStateCommand(items, PlexMediaType.Movie, library.Id);

        Mock.SetupCommand<Result>(x => x is ApplyOwnedMovieComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result>(x =>
                (x as ApplyRemoteMovieComparisonStateCommand) != null
                && ((ApplyRemoteMovieComparisonStateCommand)x).RemoteLibraryId == library.Id
                && ReferenceEquals(((ApplyRemoteMovieComparisonStateCommand)x).Items, items)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldDispatchOwnedTvShowProjection_WhenTvShowLibraryIsOwned()
    {
        // Arrange
        await SetupDatabase(
            73,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var library = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        await SetOwnedOverrideAsync(library.PlexServerId, true);
        var items = new List<PlexMediaSlimDTO> { CreateItem(884, PlexMediaType.TvShow) };
        var command = new ApplyComparisonStateCommand(items, PlexMediaType.TvShow, library.Id);

        Mock.SetupCommand<Result>(x =>
                (x as ApplyOwnedTvShowComparisonStateCommand) != null
                && ((ApplyOwnedTvShowComparisonStateCommand)x).OwnedLibraryId == library.Id
                && ReferenceEquals(((ApplyOwnedTvShowComparisonStateCommand)x).Items, items)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);
        Mock.SetupCommand<Result>(x => x is ApplyRemoteTvShowComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldDispatchRemoteTvShowProjection_WhenTvShowLibraryIsNotOwned()
    {
        // Arrange
        await SetupDatabase(
            74,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var library = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        await SetOwnedOverrideAsync(library.PlexServerId, false);
        var items = new List<PlexMediaSlimDTO> { CreateItem(885, PlexMediaType.TvShow) };
        var command = new ApplyComparisonStateCommand(items, PlexMediaType.TvShow, library.Id);

        Mock.SetupCommand<Result>(x => x is ApplyOwnedTvShowComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result>(x =>
                (x as ApplyRemoteTvShowComparisonStateCommand) != null
                && ((ApplyRemoteTvShowComparisonStateCommand)x).RemoteLibraryId == library.Id
                && ReferenceEquals(((ApplyRemoteTvShowComparisonStateCommand)x).Items, items)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReturnFailureWithoutDispatch_WhenMediaTypeIsUnsupported()
    {
        // Arrange
        await SetupDatabase(
            75,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.PlexAccountCount = 1;
            }
        );

        var library = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        var command = new ApplyComparisonStateCommand([CreateItem(886, PlexMediaType.Episode)], PlexMediaType.Episode, library.Id);

        Mock.SetupCommand<Result>(x => x is ApplyOwnedMovieComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result>(x => x is ApplyRemoteMovieComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result>(x => x is ApplyOwnedTvShowComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);
        Mock.SetupCommand<Result>(x => x is ApplyRemoteTvShowComparisonStateCommand)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        // Act
        var result = await TestHandlerExecuteAsync(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
    }

    private static PlexMediaSlimDTO CreateItem(int id, PlexMediaType mediaType, int plexLibraryId = 0) =>
        new()
        {
            Id = id,
            PlexApiRatingKey = id,
            PlexApiMetaDataKey = id,
            Title = $"Media {id}",
            SearchTitle = $"media {id}",
            SortIndex = id,
            Year = 2026,
            Duration = 120,
            MediaSize = 1024,
            ChildCount = 0,
            GrandChildCount = 0,
            AddedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            PlexLibraryId = plexLibraryId,
            PlexServerId = 0,
            Type = mediaType,
            HasThumb = false,
            Qualities = [],
        };
}
