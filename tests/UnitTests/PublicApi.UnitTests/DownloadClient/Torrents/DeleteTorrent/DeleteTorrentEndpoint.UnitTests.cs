using Microsoft.Extensions.DependencyInjection;

namespace Reaparr.PublicAPI.UnitTests;

public class DeleteTorrentEndpointUnitTests : BaseEndpointUnitTest<DeleteTorrentEndpoint, DeleteTorrentRequest>
{
    public DeleteTorrentEndpointUnitTests()
    {
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.IsAny<ClearCompletedDownloadTasksByDownloadTaskKeyCommand>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Result.Ok());
    }

    [Test]
    public async Task ShouldDeleteDownloadTask_WhenHashIdMatches()
    {
        // Arrange
        await SetupDatabase(1234, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var hashId = "abc123";
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(p => p.HashId, hashId).SetProperty(p => p.DownloadStatus, DownloadStatus.Completed),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var request = new DeleteTorrentRequest { Hashes = [hashId] };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTasksByKeyCommand>(cmd =>
                            cmd.Keys.Count == 1 && cmd.Keys.Single().Id == movieFile.Id
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.IsAny<ClearCompletedDownloadTasksByDownloadTaskKeyCommand>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    private new Task<Seed> SetupDatabase(int seed, Action<FakeDataConfig>? configure = null) =>
        base.SetupDatabase(seed, config =>
        {
            configure?.Invoke(config);
            config.RadarrIntegrationCount = 1;
            config.AssignUnownedDownloadTasksToRadarrIntegration = true;
        });

    private async Task<EndpointUnitTestResult<DeleteTorrentEndpoint, object>> TestEndpointHandleAsync(
        DeleteTorrentRequest request,
        Action<IServiceCollection>? extraServices = null
    )
    {
        var identity = (await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken)).Id.ToRadarrIdentity();
        return await base.TestEndpointHandleAsync(request, extraServices, identity);
    }

    [Test]
    public async Task ShouldStopDownloadTask_WhenHashIdMatchesAndDownloading()
    {
        // Arrange
        await SetupDatabase(5678, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var hashId = "def456";
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, hashId).SetProperty(p => p.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var request = new DeleteTorrentRequest { Hashes = [hashId] };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(cmd => cmd.DownloadTaskGuid == movieFile.Id),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.IsAny<ClearCompletedDownloadTasksByDownloadTaskKeyCommand>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTasksByKeyCommand>(cmd =>
                            cmd.Keys.Count == 1 && cmd.Keys.Single().Id == movieFile.Id
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldStopDownloadTask_WhenHashIdMatchesHashesRawAndDownloading()
    {
        // Arrange
        await SetupDatabase(9012, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var hashId = "d42bd234e00c24b9e10f20f61e1a32d10c7efde9";
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, hashId).SetProperty(p => p.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var request = new DeleteTorrentRequest { Hashes = null, HashesRaw = hashId };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(cmd => cmd.DownloadTaskGuid == movieFile.Id),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.IsAny<ClearCompletedDownloadTasksByDownloadTaskKeyCommand>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTasksByKeyCommand>(cmd =>
                            cmd.Keys.Count == 1 && cmd.Keys.Single().Id == movieFile.Id
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldStopDownloadTask_WhenHashIdCasingDiffersInHashesRaw()
    {
        // Arrange
        await SetupDatabase(3456, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var storedHashId = "D42BD234E00C24B9E10F20F61E1A32D10C7EFDE9";
        var requestHashId = "d42bd234e00c24b9e10f20f61e1a32d10c7efde9";
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, storedHashId)
                        .SetProperty(p => p.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var request = new DeleteTorrentRequest { HashesRaw = requestHashId };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(cmd => cmd.DownloadTaskGuid == movieFile.Id),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTasksByKeyCommand>(cmd =>
                            cmd.Keys.Count == 1 && cmd.Keys.Single().Id == movieFile.Id
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldStopDownloadingAndDeleteMatchedTasks_WhenHashesRawIsTrimmedAll()
    {
        // Arrange
        await SetupDatabase(7890, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 2;
        });

        var dbContext = IDbContext;
        var movieFiles = await dbContext
            .DownloadTaskMovieFile.OrderBy(x => x.Id)
            .Take(2)
            .ToListAsync(CancellationToken);

        movieFiles.Count.ShouldBe(2);

        var downloadingTask = movieFiles[0];
        var completedTask = movieFiles[1];

        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == downloadingTask.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, "hash-downloading")
                        .SetProperty(p => p.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == completedTask.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, "hash-completed")
                        .SetProperty(p => p.DownloadStatus, DownloadStatus.Completed),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var request = new DeleteTorrentRequest { HashesRaw = " all\n" };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(cmd => cmd.DownloadTaskGuid == downloadingTask.Id),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(cmd => cmd.DownloadTaskGuid == completedTask.Id),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTaskFilesCommand>(cmd => cmd.Keys.Any(k => k.Id == completedTask.Id)),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.IsAny<ClearCompletedDownloadTasksByDownloadTaskKeyCommand>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldNotStopOrDelete_WhenHashesRawIsAllAndNoHashIdsExist()
    {
        // Arrange
        await SetupDatabase(2468, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, (string?)null)
                        .SetProperty(p => p.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        var request = new DeleteTorrentRequest { HashesRaw = "all" };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never);

        var existingMovieFile = await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .SingleOrDefaultAsync(CancellationToken);
        existingMovieFile.ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldStopQueuedTask_WhenHashesRawIsAll()
    {
        // Arrange
        await SetupDatabase(1357, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, "hash-queued")
                        .SetProperty(p => p.DownloadStatus, DownloadStatus.Queued),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var request = new DeleteTorrentRequest { HashesRaw = "all" };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(cmd => cmd.DownloadTaskGuid == movieFile.Id),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldPassDeleteFilesFalseToStop_WhenHashesRawIsAll()
    {
        // Arrange
        await SetupDatabase(9753, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, "hash-delete-files-false")
                        .SetProperty(p => p.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var request = new DeleteTorrentRequest { HashesRaw = "all", DeleteFiles = false };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(cmd =>
                            cmd.DownloadTaskGuid == movieFile.Id && cmd.DeleteFiles == false
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTasksByKeyCommand>(cmd =>
                            cmd.Keys.Count == 1 && cmd.Keys.Single().Id == movieFile.Id
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldCallDeleteDownloadTaskFilesCommand_WhenDeleteFilesTrueAndTaskIsCompleted()
    {
        // Arrange — Sonarr/Radarr scenario: task is Completed, deleteFiles=true.
        await SetupDatabase(9801, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var hashId = "completed-hash-abc";
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(p => p.HashId, hashId).SetProperty(p => p.DownloadStatus, DownloadStatus.Completed),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var request = new DeleteTorrentRequest { Hashes = [hashId], DeleteFiles = true };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTaskFilesCommand>(cmd => cmd.Keys.Any(k => k.Id == movieFile.Id)),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        // StopDownloadTaskCommand must NOT be called for a completed task.
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ShouldNotCallDeleteDownloadTaskFilesCommand_WhenDeleteFilesFalseAndTaskIsCompleted()
    {
        // Arrange — deleteFiles=false: files must not be touched even for completed tasks.
        await SetupDatabase(9802, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var hashId = "completed-hash-no-delete";
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x => x.SetProperty(p => p.HashId, hashId).SetProperty(p => p.DownloadStatus, DownloadStatus.Completed),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var request = new DeleteTorrentRequest { Hashes = [hashId], DeleteFiles = false };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
    }

    [Test]
    public async Task ShouldNotCallDeleteDownloadTaskFilesCommand_WhenNoCompletedTasksMatchHash()
    {
        // Arrange — only a downloading task matched; DeleteDownloadTaskFilesCommand must not be called.
        await SetupDatabase(9803, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var hashId = "downloading-only-hash";
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, hashId).SetProperty(p => p.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var request = new DeleteTorrentRequest { Hashes = [hashId], DeleteFiles = true };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
    }

    [Test]
    public async Task ShouldDeleteOnlyKeysWhosePrerequisitesSucceeded_WhenStoppingAndDeletingFiles()
    {
        // Arrange
        await SetupDatabase(9804, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 2;
        });

        var dbContext = IDbContext;
        var movieFiles = await dbContext
            .DownloadTaskMovieFile.OrderBy(x => x.Id)
            .Take(2)
            .ToListAsync(CancellationToken);
        movieFiles.Count.ShouldBe(2);

        var downloadingTask = movieFiles[0];
        var completedTask = movieFiles[1];

        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == downloadingTask.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, "hash-downloading-success")
                        .SetProperty(p => p.DownloadStatus, DownloadStatus.Downloading),
                CancellationToken
            );

        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == completedTask.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, "hash-completed-delete-fails")
                        .SetProperty(p => p.DownloadStatus, DownloadStatus.Completed),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("delete files failed"))
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        var request = new DeleteTorrentRequest { HashesRaw = "all" };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTasksByKeyCommand>(cmd =>
                            cmd.Keys.Count == 1 && cmd.Keys.Single().Id == downloadingTask.Id
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once()
            );
    }

    [Test]
    public async Task ShouldDeleteFilesForMatchedTasksInNonActiveStatuses_WhenDeleteFilesTrue()
    {
        // Arrange
        await SetupDatabase(9805, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);

        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, "hash-stopped")
                        .SetProperty(p => p.DownloadStatus, DownloadStatus.Stopped),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        var request = new DeleteTorrentRequest { Hashes = ["hash-stopped"] };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTaskFilesCommand>(cmd =>
                            cmd.Keys.Count == 1 && cmd.Keys.Single().Id == movieFile.Id
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTasksByKeyCommand>(cmd =>
                            cmd.Keys.Count == 1 && cmd.Keys.Single().Id == movieFile.Id
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldDeleteFilesForMoveFinishedTasks_WhenDeleteFilesTrue()
    {
        // Arrange
        await SetupDatabase(9806, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);

        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, "hash-move-finished")
                        .SetProperty(p => p.DownloadStatus, DownloadStatus.MoveFinished),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        var request = new DeleteTorrentRequest { Hashes = ["hash-move-finished"] };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTaskFilesCommand>(cmd =>
                            cmd.Keys.Count == 1 && cmd.Keys.Single().Id == movieFile.Id
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<DeleteDownloadTasksByKeyCommand>(cmd =>
                            cmd.Keys.Count == 1 && cmd.Keys.Single().Id == movieFile.Id
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
    }

    [Test]
    public async Task ShouldStopMovingTasks_WhenDeleteFilesTrue()
    {
        // Arrange
        await SetupDatabase(9807, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });

        var dbContext = IDbContext;
        var movieFile = await dbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);

        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(p => p.HashId, "hash-moving")
                        .SetProperty(p => p.DownloadStatus, DownloadStatus.Moving),
                CancellationToken
            );

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<StopDownloadTaskCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        var request = new DeleteTorrentRequest { Hashes = ["hash-moving"] };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<StopDownloadTaskCommand>(cmd => cmd.DownloadTaskGuid == movieFile.Id && cmd.DeleteFiles),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTaskFilesCommand>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
    }

    [Test]
    public async Task ShouldTrackNoOp_WhenNoDownloadTaskMatches()
    {
        // Arrange
        await SetupDatabase(9810);

        // Act
        var endpointResult = await TestEndpointHandleAsync(new DeleteTorrentRequest { Hashes = ["missing"] });

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpointResult.Endpoint.HttpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(endpointResult.Endpoint.HttpContext.Response.Body);
        (await reader.ReadToEndAsync(CancellationToken)).ShouldBe("Ok.");
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.ExternalDownloadRemovalProcessed(
                        It.IsAny<IntegrationIdentity>(),
                        It.Is<ExternalDownloadRemovalReport>(report =>
                            report.Result == null
                            && report.Stage == TelemetryStage.Lookup
                            && report.RequestedHashCount == 1
                            && !report.AllRequested
                            && report.MatchedCount == 0
                            && report.ActiveCount == 0
                            && report.NonActiveCount == 0
                            && report.EligibleCount == 0
                            && report.DeletedCount == 0
                            && report.DeleteFiles
                            && report.StopFailureCount == 0
                            && report.StopCancellationCount == 0
                            && report.FileFailureCount == 0
                            && report.FileCancellationCount == 0
                            && report.CleanupFailureCount == 0
                        )
                    ),
                Times.Once
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<DeleteDownloadTasksByKeyCommand>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
    }
}
