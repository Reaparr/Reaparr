namespace Reaparr.Application.UnitTests;

public class RefreshPlexServerAccountsAccessEndpointUnitTests
    : BaseEndpointUnitTest<
        RefreshPlexServerAccountsAccessEndpoint,
        RefreshPlexServerAccountsAccessEndpointRequest,
        ResultDTO<List<RefreshPlexAccountAccessRapportDTO>>
    >
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ShouldReportPartialFailureAndPreserveCancellation_WhenRefreshingServerAccounts(
        bool failed,
        bool cancelled
    )
    {
        // Arrange
        await SetupDatabase(
            77605,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = cancelled ? 1 : 2;
            }
        );
        var dbContext = IDbContext;
        var serverId = await dbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var accounts = await dbContext.PlexAccounts.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var targetId = accounts[0].Id;
        var before = await dbContext
            .PlexAccountServers.OrderBy(x => x.PlexAccountId)
            .Select(x => new { x.PlexAccountId, x.PlexServerId })
            .ToListAsync(CancellationToken);
        before.Select(x => x.PlexAccountId).ShouldBe(accounts.Select(x => x.Id));
        var reports = accounts
            .Select(a => new RefreshPlexAccountAccessRapportDTO(a.Id, a.DisplayName) { Access = [] })
            .ToList();
        foreach (var report in reports)
        {
            var outcome =
                failed && report.PlexAccountId == targetId
                    ? (
                        cancelled
                            ? ResultExtensions
                                .TaskIsCancelled("server-account-refresh")
                                .ToResult<List<RefreshPlexAccountAccessRapportDTO>>()
                            : Result.Fail<List<RefreshPlexAccountAccessRapportDTO>>("Account refresh failed")
                    )
                    : Result.Ok(new List<RefreshPlexAccountAccessRapportDTO> { report });
            Mock.Mock<ICommandExecutor>()
                .Setup(x =>
                    x.Send(
                        It.Is<RefreshPlexAccountAccessCommand>(c => c.PlexAccountId == report.PlexAccountId),
                        CancellationToken
                    )
                )
                .ReturnsAsync(outcome)
                .Verifiable(Times.Once());
        }

        // Act
        var response = await TestEndpointHandleAsync(
            new RefreshPlexServerAccountsAccessEndpointRequest { PlexServerId = serverId }
        );

        // Assert
        response.ValidationResult!.IsValid.ShouldBeTrue();
        response.Response.ShouldNotBeNull();
        response.Response.IsSuccess.ShouldBe(!failed);
        response.Response.Errors.Count.ShouldBe(failed ? 1 : 0);
        if (!failed)
            response.Response.Value.ShouldBeEquivalentTo(reports);
        else if (!cancelled)
        {
            response.Response.Errors.Single().Message.ShouldBe("Account refresh failed");
            response.Response.Value.ShouldBeEquivalentTo(reports.Where(x => x.PlexAccountId != targetId).ToList());
        }
        (
            await dbContext
                .PlexAccountServers.OrderBy(x => x.PlexAccountId)
                .Select(x => new { x.PlexAccountId, x.PlexServerId })
                .ToListAsync(CancellationToken)
        ).ShouldBe(before);
        Mock.Mock<ICommandExecutor>().Verify();
    }
}
