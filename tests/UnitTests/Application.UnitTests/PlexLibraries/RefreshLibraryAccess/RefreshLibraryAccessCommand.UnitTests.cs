namespace Reaparr.Application.UnitTests;

public class RefreshLibraryAccessCommandUnitTests : BaseUnitTest<RefreshLibraryAccessHandler>
{
    [Test]
    public async Task ShouldReturnFailedResult_WhenPlexAccountIdIsInvalid()
    {
        // Arrange
        await SetupDatabase(1, config =>
        {
            config.PlexAccountCount = 1;
            config.PlexServerCount = 0;
        });
        var request = new RefreshLibraryAccessCommand(0);

        // Act
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
    }

    [Test]
    public async Task ShouldReturnEmptyResult_WhenNoPlexServersAccessible()
    {
        // Arrange
        await SetupDatabase(1, config =>
        {
            config.PlexAccountCount = 1;
            config.PlexServerCount = 0;
        });

        // Act
        var request = new RefreshLibraryAccessCommand(1);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Reports.ShouldBeEmpty();
        result.Value.OfflineServers.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldRetrieveLibrariesFromSinglePlexServer_WhenPlexAccountHasAccessToOneServer()
    {
        // Arrange
        var seed = await SetupDatabase(1, config =>
        {
            config.PlexAccountCount = 1;
            config.PlexServerCount = 1;
        });
        var plexServer = IDbContext.PlexServers.FirstOrDefault();
        plexServer.ShouldNotBeNull();
        var updatedTime = DateTime.UtcNow - TimeSpan.FromHours(9);
        var plexLibraries = FakeData.GetPlexLibrary(seed).Generate(5).ToApiLibraries(updatedTime);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetLibrarySectionsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(plexLibraries))
            .Verifiable(Times.Once);

        var rapport = new PlexLibraryAccessRapport("Piet", 1, plexServer.Name);

        rapport.AddGranted(1, plexLibraries.Find(x => x.Id == 1)?.Name ?? string.Empty);
        rapport.AddGranted(2, plexLibraries.Find(x => x.Id == 2)?.Name ?? string.Empty);
        rapport.AddGranted(3, plexLibraries.Find(x => x.Id == 3)?.Name ?? string.Empty);
        rapport.AddGranted(4, plexLibraries.Find(x => x.Id == 4)?.Name ?? string.Empty);
        rapport.AddGranted(5, plexLibraries.Find(x => x.Id == 5)?.Name ?? string.Empty);

        Mock.SetupCommand(It.IsAny<AddOrUpdatePlexLibrariesCommand>)
            .ReturnsAsync(Result.Ok(new List<PlexLibraryAccessRapport> { rapport }))
            .Verifiable(Times.Once);

        // Act
        var request = new RefreshLibraryAccessCommand(1, 1);
        var result = await Sut.ExecuteAsync(request, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var reports = result.Value.Reports;
        reports.ShouldNotBeEmpty();
        reports.First().GetGranted.Count.ShouldBe(5);
    }

    [Test]
    public async Task ShouldReturnFailedResult_WhenSpecificServerIsDisabled_EvenWhenRequestedDirectly()
    {
        // Arrange
        await SetupDatabase(2, config =>
        {
            config.PlexAccountCount = 1;
            config.PlexServerCount = 1;
        });

        var dbContext = IDbContext;
        var plexServer = await dbContext.PlexServers.IgnoreIsEnabledFilter().FirstAsync(CancellationToken);

        await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == plexServer.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GetLibrarySectionsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new List<PlexLibrary>()))
            .Verifiable(Times.Never());

        // Act
        var result = await Sut.ExecuteAsync(new RefreshLibraryAccessCommand(1, plexServer.Id), CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<ICommandExecutor>().Verify();
    }
}
