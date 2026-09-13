namespace Reaparr.Application.UnitTests;

public class DeletePlexAccountByIdEndpointUnitTests
    : BaseEndpointUnitTest<DeletePlexAccountByIdEndpoint, DeletePlexAccountByIdRequest, BaseResultDTO>
{
    [Test]
    public async Task ShouldDeleteAllRelatedDataWhenAccountIsDeleted()
    {
        // Arrange
        await SetupDatabase(
            1223,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexServerCount = 5;
                config.PlexMovieLibraryCount = 3;
                config.MovieCount = 10;
                config.TvShowCount = 10;
            }
        );

        var testAccount = IDbContext.PlexAccounts.IncludeServerAccess().IncludeLibraryAccess().FirstOrDefault();
        testAccount.ShouldNotBeNull();
        testAccount.PlexServers.ShouldNotBeEmpty();
        testAccount.PlexLibraries.ShouldNotBeEmpty();
        IDbContext.PlexMovies.ShouldNotBeEmpty();
        IDbContext.PlexTvShows.ShouldNotBeEmpty();

        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand()).ReturnsAsync(Result.Ok());
        Mock.SendRefreshNotification();
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestEndpointHandleAsync(new DeletePlexAccountByIdRequest(testAccount.Id));

        // Assert
        result.ShouldNotBeNull();
        result.Response.ShouldNotBeNull();
        result.Response.IsSuccess.ShouldBeTrue();
        IDbContext.PlexAccounts.ToList().ShouldBeEmpty();
        IDbContext.PlexServers.ToList().ShouldBeEmpty();
        IDbContext.PlexLibraries.ToList().ShouldBeEmpty();
        IDbContext.PlexMovies.ToList().ShouldBeEmpty();
        IDbContext.PlexTvShows.ToList().ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldDeleteOnlyUnreferencedServersAndLibraries_AfterRemovingDeletedAccountAccess()
    {
        // Arrange
        await SetupDatabase(
            91102,
            config =>
            {
                config.PlexAccountCount = 2;
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 2;
            }
        );

        var dbContext = IDbContext;
        var accounts = await dbContext.PlexAccounts.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var servers = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);

        var deleteAccountId = accounts[0].Id;
        var keepAccountId = accounts[1].Id;

        var deleteOnlyServerId = servers[0].Id;
        var sharedServerId = servers[1].Id;

        var deleteOnlyLibraryId = libraries.First(x => x.PlexServerId == deleteOnlyServerId).Id;
        var sharedLibraryId = libraries.First(x => x.PlexServerId == sharedServerId).Id;

        await dbContext.PlexAccountServers.ExecuteDeleteAsync(CancellationToken);
        await dbContext.PlexAccountLibraries.ExecuteDeleteAsync(CancellationToken);

        await dbContext.PlexAccountServers.AddRangeAsync(
            [
                new PlexAccountServer
                {
                    PlexAccountId = deleteAccountId,
                    PlexServerId = deleteOnlyServerId,
                    AuthToken = "token-delete-only-server",
                    AuthTokenCreationDate = DateTime.UtcNow,
                    IsServerOwned = true,
                },
                new PlexAccountServer
                {
                    PlexAccountId = deleteAccountId,
                    PlexServerId = sharedServerId,
                    AuthToken = "token-delete-shared-server",
                    AuthTokenCreationDate = DateTime.UtcNow,
                    IsServerOwned = false,
                },
                new PlexAccountServer
                {
                    PlexAccountId = keepAccountId,
                    PlexServerId = sharedServerId,
                    AuthToken = "token-keep-shared-server",
                    AuthTokenCreationDate = DateTime.UtcNow,
                    IsServerOwned = false,
                },
            ],
            CancellationToken
        );

        await dbContext.PlexAccountLibraries.AddRangeAsync(
            [
                new PlexAccountLibrary
                {
                    PlexAccountId = deleteAccountId,
                    PlexLibraryId = deleteOnlyLibraryId,
                    PlexServerId = deleteOnlyServerId,
                    IsLibraryOwned = true,
                },
                new PlexAccountLibrary
                {
                    PlexAccountId = deleteAccountId,
                    PlexLibraryId = sharedLibraryId,
                    PlexServerId = sharedServerId,
                    IsLibraryOwned = false,
                },
                new PlexAccountLibrary
                {
                    PlexAccountId = keepAccountId,
                    PlexLibraryId = sharedLibraryId,
                    PlexServerId = sharedServerId,
                    IsLibraryOwned = false,
                },
            ],
            CancellationToken
        );

        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.SendRefreshNotification();
        Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var endpoint = await TestEndpointHandleAsync(new DeletePlexAccountByIdRequest(deleteAccountId));

        // Assert
        endpoint.ShouldNotBeNull();
        endpoint.Response.ShouldNotBeNull();
        endpoint.Response.IsSuccess.ShouldBeTrue();

        var remainingServers = await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        remainingServers.ShouldContain(sharedServerId);
        remainingServers.ShouldNotContain(deleteOnlyServerId);

        var remainingLibraries = await dbContext.PlexLibraries.Select(x => x.Id).ToListAsync(CancellationToken);
        remainingLibraries.ShouldContain(sharedLibraryId);
        remainingLibraries.ShouldNotContain(deleteOnlyLibraryId);

        var remainingAccountServerLinks = await dbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == keepAccountId)
            .ToListAsync(CancellationToken);
        remainingAccountServerLinks.Count.ShouldBe(1);
        remainingAccountServerLinks[0].PlexServerId.ShouldBe(sharedServerId);

        var deletedAccountLinks = await dbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == deleteAccountId)
            .ToListAsync(CancellationToken);
        deletedAccountLinks.ShouldBeEmpty();
    }
}
