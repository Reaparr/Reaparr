namespace Reaparr.Application.UnitTests;

public class RefreshPlexOtherVideoLibraryCommandUnitTests : BaseCommandUnitTest<RefreshPlexOtherVideoLibraryCommand>
{
    [Test]
    public async Task ShouldPublishStructuredErrorAndSkipOptimization_WhenSyncFails()
    {
        // Arrange
        await SetupDatabase(625623, x =>
        {
            x.PlexServerCount = 1;
            x.PlexMovieLibraryCount = 1;
        });
        var dbContext = IDbContext;
        await dbContext.PlexLibraries.ExecuteUpdateAsync(p => p.SetProperty(x => x.Type, PlexMediaType.OtherVideos), CancellationToken);
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var response = new InsertMediaMetaDataCommandResponse(library);
        var failure = Result.Fail("catalog retrieval incomplete");

        Mock.Mock<ILibrarySyncProgressStore>()
            .Setup(x => x.UpdateErrorAsync(library.Id,
                It.Is<Result>(r => r.IsFailed && r.Errors.Count == 1), CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<SyncPlexOtherVideosCommand>(c =>
                c.LibraryMetadata == response && !c.ForceMediaRefresh), CancellationToken))
            .ReturnsAsync(failure).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<PlexLibrary>(new RefreshPlexOtherVideoLibraryCommand(response));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors.ShouldBe(failure.Errors);
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(
            It.IsAny<ScheduleOptimizeDatabaseJobCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify(x => x.Send(
            It.IsAny<QueueMediaOverviewRebuildCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ILibrarySyncProgressStore>().Verify(x => x.UpdateItemAsync(
            It.IsAny<int>(), It.IsAny<LibraryProgressItem>(), It.IsAny<CancellationToken>()), Times.Never());
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ILibrarySyncProgressStore>().Verify();
    }
}
