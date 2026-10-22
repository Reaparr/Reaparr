namespace Reaparr.Application.UnitTests;

public class ClearCompletedDownloadTasksByServerIdEndpointUnitTests
    : BaseEndpointUnitTest<ClearCompletedDownloadTasksByServerIdEndpoint, ClearCompletedDownloadTasksByServerIdEndpointRequest, ResultDTO<CountResponseDTO>>
{
    [Test]
    public async Task ShouldNotifyAfterRemovingOnlyCompletedTargetTasks_WhenClearingServer()
    {
        // Arrange
        await SetupDatabase(71195, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.MovieDownloadTasksCount = 3;
        });
        var dbContext = IDbContext;
        var tasks = await dbContext.DownloadTaskMovie.AsTracking().Include(x => x.Children)
            .OrderBy(x => x.PlexServerId).ThenBy(x => x.Id).ToListAsync(CancellationToken);
        var targetServerId = tasks[0].PlexServerId;
        var removed = tasks[0];
        var active = tasks.First(x => x.PlexServerId == targetServerId && x.Id != removed.Id);
        var foreign = tasks[2];
        var foreignLibrary = await dbContext.PlexLibraries
            .Where(x => x.Type == PlexMediaType.Movie && x.PlexServerId != targetServerId).SingleAsync(CancellationToken);
        new[] { foreign }.SetRelationshipIds(foreignLibrary.PlexServerId, foreignLibrary.Id);
        tasks.SetDownloadStatus(DownloadStatus.Completed);
        new[] { active }.SetDownloadStatus(DownloadStatus.Queued);
        await dbContext.SaveChangesAsync(CancellationToken);
        var expectedRoots = tasks.Where(x => x.Id != removed.Id).Select(x => x.Id).Order().ToArray();
        var expectedFiles = tasks.Where(x => x.Id != removed.Id).SelectMany(x => x.Children).Select(x => x.Id).Order().ToArray();
        var beforeRoots = await dbContext.DownloadTaskMovie.Select(x => x.Id).OrderBy(x => x).ToArrayAsync(CancellationToken);
        beforeRoots.ShouldBe(tasks.Select(x => x.Id).Order().ToArray());
        (await dbContext.DownloadTaskMovie.Select(x => x.PlexServerId).Distinct().OrderBy(x => x).ToArrayAsync(CancellationToken))
            .ShouldBe(new[] { targetServerId, foreignLibrary.PlexServerId }.Order().ToArray());
        Guid[]? rootsAtNotification = null;
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<ClearCompletedDownloadTasksByServerIdCommand>(c => c.PlexServerId == targetServerId), It.IsAny<CancellationToken>()))
            .Returns((ClearCompletedDownloadTasksByServerIdCommand command, CancellationToken ct) =>
                new ClearCompletedDownloadTasksByServerIdCommandHandler(dbContext).ExecuteAsync(command, ct))
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(RefreshDataType.DownloadTasks))
            .Returns(async () =>
            {
                rootsAtNotification = await dbContext.DownloadTaskMovie.Select(x => x.Id).OrderBy(x => x).ToArrayAsync(CancellationToken);
            })
            .Verifiable(Times.Once());

        // Act
        var endpoint = await TestEndpointHandleAsync(new ClearCompletedDownloadTasksByServerIdEndpointRequest { PlexServerId = targetServerId });

        // Assert
        endpoint.IsValid.ShouldBeTrue();
        endpoint.StatusCode.ShouldBe(200);
        var result = endpoint.Response.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldNotBeNull().Count.ShouldBe(1);
        rootsAtNotification.ShouldBe(expectedRoots);
        (await dbContext.DownloadTaskMovie.Select(x => x.Id).OrderBy(x => x).ToArrayAsync(CancellationToken)).ShouldBe(expectedRoots);
        (await dbContext.DownloadTaskMovieFile.Select(x => x.Id).OrderBy(x => x).ToArrayAsync(CancellationToken)).ShouldBe(expectedFiles);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldKeepTasksAndNotNotify_WhenClearFailsOrIsCancelled(bool cancelled)
    {
        // Arrange
        await SetupDatabase(71295, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
            config.MovieDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var before = await dbContext.DownloadTaskMovie.Select(x => new { x.Id, x.PlexServerId, x.DownloadStatus }).ToArrayAsync(CancellationToken);
        var targetServerId = before.Single().PlexServerId;
        var failure = cancelled
            ? Result.Fail<int>(new ExceptionalError(new OperationCanceledException()))
            : Result.Fail<int>("Clear unavailable");
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<ClearCompletedDownloadTasksByServerIdCommand>(c => c.PlexServerId == targetServerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure).Verifiable(Times.Once());

        // Act
        var endpoint = await TestEndpointHandleAsync(new ClearCompletedDownloadTasksByServerIdEndpointRequest { PlexServerId = targetServerId });

        // Assert
        endpoint.IsValid.ShouldBeTrue();
        var result = endpoint.Response.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        (await dbContext.DownloadTaskMovie.Select(x => new { x.Id, x.PlexServerId, x.DownloadStatus }).ToArrayAsync(CancellationToken)).ShouldBe(before);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<INotificationHubService>().Verify(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()), Times.Never());
        Mock.Mock<INotificationHubService>().Verify(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()), Times.Never());
    }
}
