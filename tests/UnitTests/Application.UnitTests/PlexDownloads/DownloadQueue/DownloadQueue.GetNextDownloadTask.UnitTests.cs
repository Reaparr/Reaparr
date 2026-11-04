namespace Reaparr.Application.UnitTests;

public class DownloadQueueGetNextDownloadTaskUnitTests : BaseUnitTest<DownloadQueue>
{
    [Test]
    public async Task ShouldHaveNextDownloadTask_WhenAllAreQueued()
    {
        // Arrange
        await SetupDatabase(69402, config => config.TvShowDownloadTasksCount = 5);
        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeTrue();
        var nextDownloadTaskId = downloadTasks[0].Children[0].Children[0].Children[0].Id;
        nextDownloadTask.Value.Id.ShouldBe(nextDownloadTaskId);
    }

    [Test]
    public async Task ShouldHaveNextDownloadTask_WhenADownloadTaskHasBeenCompleted()
    {
        // Arrange
        await SetupDatabase(92673, config => config.TvShowDownloadTasksCount = 5);
        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );
        downloadTasks[0].SetDownloadStatus(DownloadStatus.Completed);
        await IDbContext.SaveChangesAsync(CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeTrue();
        var nextDownloadTaskId = downloadTasks[1].Children[0].Children[0].Children[0].Id;
        nextDownloadTask.Value.Id.ShouldBe(nextDownloadTaskId);
    }

    [Test]
    public async Task ShouldHaveNextQueuedDownloadTaskInDownloadingTask_WhenAParentDownloadTaskIsAlreadyDownloading()
    {
        // Arrange
        await SetupDatabase(48899, config => config.TvShowDownloadTasksCount = 5);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );
        downloadTasks[0].SetDownloadStatus(DownloadStatus.Downloading);
        foreach (var child in downloadTasks[0].Children)
            child.SetDownloadStatus(DownloadStatus.Queued);
        await IDbContext.SaveChangesAsync(CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeTrue();
        var nextDownloadTaskId = downloadTasks[0].Children[0].Children[0].Children[0].Id;
        nextDownloadTask.Value.Id.ShouldBe(nextDownloadTaskId);
    }

    [Test]
    public async Task ShouldHaveNoDownloadTask_WhenADownloadTaskIsAlreadyDownloading()
    {
        // Arrange
        await SetupDatabase(69598, config => config.TvShowDownloadTasksCount = 5);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );
        downloadTasks[0].SetDownloadStatus(DownloadStatus.Downloading);
        await IDbContext.SaveChangesAsync(CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldSelectQueuedLeaf_WhenOnlyParentIsServerUnreachable()
    {
        // Arrange
        await SetupDatabase(69598, config => config.TvShowDownloadTasksCount = 5);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );
        downloadTasks[0].SetDownloadStatus(DownloadStatus.ServerUnreachable);
        await IDbContext.SaveChangesAsync(CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeTrue();
        var nextDownloadTaskId = downloadTasks[0].Children[0].Children[0].Children[0].Id;
        nextDownloadTask.Value.Id.ShouldBe(nextDownloadTaskId);
    }

    [Test]
    public async Task ShouldPrioritizeServerUnreachable_WhenQueuedAndServerUnreachableExist()
    {
        // Arrange
        await SetupDatabase(71452, config => config.MovieDownloadTasksCount = 3);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );

        var queuedTask = downloadTasks[0].Children[0];
        queuedTask.SetDownloadStatus(DownloadStatus.Queued);

        var serverUnreachableTask = downloadTasks[1].Children[0];
        serverUnreachableTask.SetDownloadStatus(DownloadStatus.ServerUnreachable);

        await IDbContext.SaveChangesAsync(CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeTrue();
        nextDownloadTask.Value.Id.ShouldBe(serverUnreachableTask.Id);
    }

    [Test]
    public async Task ShouldSelectServerUnreachableTask_WhenQueuedTaskExists()
    {
        // Arrange
        await SetupDatabase(51422, config => config.MovieDownloadTasksCount = 2);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );

        var serverUnreachableTask = downloadTasks[0].Children[0];
        serverUnreachableTask.SetDownloadStatus(DownloadStatus.ServerUnreachable);
        downloadTasks[0].SetDownloadStatus(DownloadStatus.ServerUnreachable);

        var queuedTask = downloadTasks[1].Children[0];
        queuedTask.SetDownloadStatus(DownloadStatus.Queued);
        downloadTasks[1].SetDownloadStatus(DownloadStatus.Queued);

        await IDbContext.SaveChangesAsync(CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeTrue();
        nextDownloadTask.Value.Id.ShouldBe(serverUnreachableTask.Id);
    }

    [Test]
    public async Task ShouldSelectServerUnreachableTask_WhenRetryMetadataAllows()
    {
        // Arrange
        await SetupDatabase(51423, config => config.MovieDownloadTasksCount = 2);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );

        var serverUnreachableTask = downloadTasks[0].Children[0];
        serverUnreachableTask.SetDownloadStatus(DownloadStatus.ServerUnreachable);
        downloadTasks[0].SetDownloadStatus(DownloadStatus.ServerUnreachable);

        var queuedTask = downloadTasks[1].Children[0];
        queuedTask.SetDownloadStatus(DownloadStatus.Queued);
        downloadTasks[1].SetDownloadStatus(DownloadStatus.Queued);

        await IDbContext.SaveChangesAsync(CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeTrue();
        nextDownloadTask.Value.Id.ShouldBe(serverUnreachableTask.Id);
    }

    [Test]
    public async Task ShouldHaveNoNextDownloadTask_WhenAllArePaused()
    {
        // Arrange
        await SetupDatabase(61827, config => config.MovieDownloadTasksCount = 2);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );

        foreach (var downloadTask in downloadTasks)
        {
            downloadTask.SetDownloadStatus(DownloadStatus.Paused);
            foreach (var child in downloadTask.Children)
                child.SetDownloadStatus(DownloadStatus.Paused);
        }

        await IDbContext.SaveChangesAsync(CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldHaveNoNextDownloadTask_WhenMovingAndDownloadFinished()
    {
        // Arrange
        await SetupDatabase(61612, config => config.MovieDownloadTasksCount = 5);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );
        downloadTasks[0].SetDownloadStatus(DownloadStatus.Moving);
        downloadTasks[1].SetDownloadStatus(DownloadStatus.DownloadFinished);
        downloadTasks[2].SetDownloadStatus(DownloadStatus.DownloadFinished);
        downloadTasks[3].SetDownloadStatus(DownloadStatus.DownloadFinished);
        downloadTasks[4].SetDownloadStatus(DownloadStatus.DownloadFinished);
        await IDbContext.SaveChangesAsync(CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeFalse();
    }

    [Test]
    public async Task ShouldHaveLastQueuedDownloadTask_WhenMovingQueuedAndDownloadFinished()
    {
        // Arrange
        await SetupDatabase(13297, config => config.MovieDownloadTasksCount = 5);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );
        downloadTasks[0].SetDownloadStatus(DownloadStatus.Moving);
        downloadTasks[1].SetDownloadStatus(DownloadStatus.DownloadFinished);
        downloadTasks[2].SetDownloadStatus(DownloadStatus.DownloadFinished);
        downloadTasks[3].SetDownloadStatus(DownloadStatus.DownloadFinished);
        downloadTasks[4].SetDownloadStatus(DownloadStatus.Queued);
        await IDbContext.SaveChangesAsync(CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeTrue();
        var nextDownloadTaskId = downloadTasks[4].Children[0].Id;
        nextDownloadTask.Value.Id.ShouldBe(nextDownloadTaskId);
    }

    [Test]
    public async Task ShouldPrioritizeDownloadClientError_OverQueued()
    {
        // Arrange
        await SetupDatabase(81901, config => config.MovieDownloadTasksCount = 3);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );

        var queuedTask = downloadTasks[0].Children[0];
        queuedTask.SetDownloadStatus(DownloadStatus.Queued);
        downloadTasks[0].SetDownloadStatus(DownloadStatus.Queued);

        var downloadClientErrorTask = downloadTasks[1].Children[0];
        downloadClientErrorTask.SetDownloadStatus(DownloadStatus.DownloadClientError);
        downloadTasks[1].SetDownloadStatus(DownloadStatus.DownloadClientError);

        await IDbContext.SaveChangesAsync(CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeTrue();
        nextDownloadTask.Value.Id.ShouldBe(downloadClientErrorTask.Id);
    }

    [Test]
    public async Task ShouldPrioritizeError_OverQueued()
    {
        // Arrange
        await SetupDatabase(81902, config => config.MovieDownloadTasksCount = 3);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );

        var queuedTask = downloadTasks[0].Children[0];
        queuedTask.SetDownloadStatus(DownloadStatus.Queued);
        downloadTasks[0].SetDownloadStatus(DownloadStatus.Queued);

        var errorTask = downloadTasks[1].Children[0];
        errorTask.SetDownloadStatus(DownloadStatus.Error);
        downloadTasks[1].SetDownloadStatus(DownloadStatus.Error);

        await IDbContext.SaveChangesAsync(CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeTrue();
        nextDownloadTask.Value.Id.ShouldBe(errorTask.Id);
    }

    [Test]
    public async Task ShouldPrioritizeDownloadClientError_OverError()
    {
        // Arrange
        await SetupDatabase(81903, config => config.MovieDownloadTasksCount = 3);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );

        var errorTask = downloadTasks[0].Children[0];
        errorTask.SetDownloadStatus(DownloadStatus.Error);
        downloadTasks[0].SetDownloadStatus(DownloadStatus.Error);

        var downloadClientErrorTask = downloadTasks[1].Children[0];
        downloadClientErrorTask.SetDownloadStatus(DownloadStatus.DownloadClientError);
        downloadTasks[1].SetDownloadStatus(DownloadStatus.DownloadClientError);

        await IDbContext.SaveChangesAsync(CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeTrue();
        nextDownloadTask.Value.Id.ShouldBe(downloadClientErrorTask.Id);
    }

    [Test]
    public async Task ShouldHonorFullPriorityChain_ServerUnreachableOverDownloadClientErrorOverErrorOverQueued()
    {
        // Arrange
        await SetupDatabase(81904, config => config.MovieDownloadTasksCount = 4);

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );

        var queuedTask = downloadTasks[0].Children[0];
        queuedTask.SetDownloadStatus(DownloadStatus.Queued);
        downloadTasks[0].SetDownloadStatus(DownloadStatus.Queued);

        var errorTask = downloadTasks[1].Children[0];
        errorTask.SetDownloadStatus(DownloadStatus.Error);
        downloadTasks[1].SetDownloadStatus(DownloadStatus.Error);

        var downloadClientErrorTask = downloadTasks[2].Children[0];
        downloadClientErrorTask.SetDownloadStatus(DownloadStatus.DownloadClientError);
        downloadTasks[2].SetDownloadStatus(DownloadStatus.DownloadClientError);

        var serverUnreachableTask = downloadTasks[3].Children[0];
        serverUnreachableTask.SetDownloadStatus(DownloadStatus.ServerUnreachable);
        downloadTasks[3].SetDownloadStatus(DownloadStatus.ServerUnreachable);

        await IDbContext.SaveChangesAsync(CancellationToken);

        // Act
        var nextDownloadTask = Sut.GetNextDownloadTask(downloadTasks, out _);

        // Assert
        nextDownloadTask.IsSuccess.ShouldBeTrue();
        nextDownloadTask.Value.Id.ShouldBe(serverUnreachableTask.Id);
    }
}
