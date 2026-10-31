namespace Reaparr.Application.UnitTests;

public class RefreshLibraryAccessAggregationUnitTests : BaseUnitTest<RefreshLibraryAccessHandler>
{
    [Test]
    public async Task ShouldCollectEveryTimedOutServer_WhenParallelRefreshesCompleteWithMixedResults()
    {
        // Arrange
        var seed = await SetupDatabase(
            4601,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 4;
            }
        );
        var servers = await IDbContext.PlexServers.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        servers.Count.ShouldBe(4);
        var libraries = FakeData
            .GetPlexLibrary(seed)
            .RuleFor(x => x.PlexServerId, servers[1].Id)
            .Generate(1)
            .ToApiLibraries(new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        var gates = MockPendingRefreshes(servers);
        var firstTimeout = ResultExtensions
            .Create504GatewayTimeoutResult("First timeout")
            .ToResult<List<PlexLibrary>>();
        var lastTimeout = ResultExtensions.Create504GatewayTimeoutResult("Last timeout").ToResult<List<PlexLibrary>>();
        Mock.SetupCommand(It.IsAny<AddOrUpdatePlexLibrariesCommand>)
            .ReturnsAsync(Result.Ok(new List<PlexLibraryAccessRapport>()));

        // Act: all refreshes must start before any of them is released.
        var execution = Sut.ExecuteAsync(new RefreshLibraryAccessCommand(1), CancellationToken);
        await Task.WhenAll(gates.Values.Select(x => x.Started.Task)).WaitAsync(CancellationToken);
        gates[servers[1].Id].Completion.SetResult(Result.Ok(libraries));
        gates[servers[3].Id].Completion.SetResult(Result.Fail<List<PlexLibrary>>("Not a timeout"));
        await Task.WhenAll(
            Task.Run(() => gates[servers[2].Id].Completion.SetResult(lastTimeout), CancellationToken),
            Task.Run(() => gates[servers[0].Id].Completion.SetResult(firstTimeout), CancellationToken)
        );
        var result = await execution;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
        result.Value.OfflineServers.Order().ShouldBe(new[] { servers[0].Id, servers[2].Id }.Order());
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldPreserveFailureAndCancellationWithoutUpdatingLibraries_WhenNoRefreshSucceeds(bool cancelled)
    {
        // Arrange
        await SetupDatabase(
            4602,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 2;
            }
        );
        var servers = await IDbContext.PlexServers.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        servers.Count.ShouldBe(2);
        var first = ResultExtensions.Create504GatewayTimeoutResult("Timed out").ToResult<List<PlexLibrary>>();
        var second = cancelled
            ? ResultExtensions.TaskIsCancelled(nameof(GetLibrarySectionsCommand)).ToResult<List<PlexLibrary>>()
            : Result.Fail<List<PlexLibrary>>("Different failure");
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetLibrarySectionsCommand>(), It.IsAny<CancellationToken>()))
            .Returns(
                (ICommand<Result<List<PlexLibrary>>> command, CancellationToken _) =>
                    Task.FromResult(((GetLibrarySectionsCommand)command).PlexServerId == servers[0].Id ? first : second)
            );

        // Act
        var result = await Sut.ExecuteAsync(new RefreshLibraryAccessCommand(1), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.IsCancelled.ShouldBe(cancelled);
        var expectedErrors = cancelled ? second.Errors.AsEnumerable() : first.Errors.Concat(second.Errors);
        result.Errors.ShouldBe(expectedErrors, ignoreOrder: true);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<AddOrUpdatePlexLibrariesCommand>(), It.IsAny<CancellationToken>()),
                Times.Never
            );
    }

    private Dictionary<
        int,
        (TaskCompletionSource Started, TaskCompletionSource<Result<List<PlexLibrary>>> Completion)
    > MockPendingRefreshes(IEnumerable<PlexServer> servers)
    {
        var gates = servers.ToDictionary(
            x => x.Id,
            _ =>
                (
                    Started: new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
                    Completion: new TaskCompletionSource<Result<List<PlexLibrary>>>(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    )
                )
        );
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetLibrarySectionsCommand>(), It.IsAny<CancellationToken>()))
            .Returns(
                (ICommand<Result<List<PlexLibrary>>> command, CancellationToken _) =>
                {
                    var gate = gates[((GetLibrarySectionsCommand)command).PlexServerId];
                    gate.Started.SetResult();
                    return gate.Completion.Task;
                }
            );
        return gates;
    }
}
