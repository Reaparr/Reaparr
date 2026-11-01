using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Reaparr.Application.UnitTests;

public class GetAllPlexServerConnectionsEndpointUnitTests
    : BaseEndpointWithoutRequestUnitTest<
        GetAllPlexServerConnectionsEndpoint,
        List<PlexServerConnectionDTO>
    >
{
    [Test]
    [Arguments(1)]
    [Arguments(20)]
    public async Task ShouldChooseEachPreferredConnectionWithOneQuery_WhenServerCountChanges(int serverCount)
    {
        // Arrange
        var fixture = await ArrangePreferredConnectionsAsync(serverCount);
        var commands = new ConcurrentQueue<string>();
        using var commandSubscriptions = new System.Reactive.Disposables.CompositeDisposable();
        using var listeners = DiagnosticListener.AllListeners.Subscribe(listener =>
        {
            if (listener.Name != "Microsoft.EntityFrameworkCore")
                return;

            commandSubscriptions.Add(
                listener.Subscribe(entry =>
                {
                    if (
                        entry.Value is CommandExecutedEventData command
                        && command.Context?.Database.GetConnectionString() == fixture.ConnectionString
                    )
                        commands.Enqueue(command.Command.CommandText);
                })
            );
        });

        // Act
        var result = await TestEndpointHandleAsync();

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Endpoint.HttpContext.Response.StatusCode.ShouldBe(200);
        result.Endpoint.HttpContext.Response.Body.Position = 0;
        var response = await JsonSerializer.DeserializeAsync<ResultDTO<List<PlexServerConnectionDTO>>>(
            result.Endpoint.HttpContext.Response.Body,
            DefaultJsonSerializerOptions.ConfigStandard,
            CancellationToken
        );
        response.ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var value = response.Value.ShouldNotBeNull();
        var actual = value.OrderBy(x => x.Id).Select(Snapshot).ToList();
        actual.ShouldBe(fixture.Expected.OrderBy(x => x.Id));
        actual.Where(x => x.ChosenConnection).Select(x => x.Id).OrderBy(x => x).ShouldBe(
            fixture.ChosenIds.OrderBy(x => x)
        );
        commands.Count.ShouldBe(1);
    }

    [Test]
    public async Task ShouldKeepPolicyFailureRowsVisible_WhenConnectionsCannotBeChosen()
    {
        // Arrange
        var fixture = await ArrangePolicyFailureGraphAsync();

        // Act
        var result = await TestEndpointHandleAsync();

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Endpoint.HttpContext.Response.StatusCode.ShouldBe(200);
        result.Endpoint.HttpContext.Response.Body.Position = 0;
        var response = await JsonSerializer.DeserializeAsync<ResultDTO<List<PlexServerConnectionDTO>>>(
            result.Endpoint.HttpContext.Response.Body,
            DefaultJsonSerializerOptions.ConfigStandard,
            CancellationToken
        );
        response.ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var value = response.Value.ShouldNotBeNull();
        var actual = value.OrderBy(x => x.Id).Select(Snapshot).ToList();
        actual.ShouldBe(fixture.Expected.OrderBy(x => x.Id));
        actual.Select(x => x.Id).Intersect(fixture.DisabledConnectionIds).ShouldBeEmpty();
    }

    private async Task<PreferredFixture> ArrangePreferredConnectionsAsync(int serverCount)
    {
        await SetupDatabase(
            72000 + serverCount,
            config =>
            {
                config.PlexServerCount = serverCount;
                config.PlexServerConnectionPerServerCount = 3;
            }
        );

        var dbContext = IDbContext;
        var servers = await dbContext.PlexServers.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var connections = await dbContext.PlexServerConnections.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var chosenIds = new HashSet<int>();
        foreach (var server in servers)
        {
            var preferred = connections.Where(x => x.PlexServerId == server.Id).OrderBy(x => x.Id).Skip(1).First();
            chosenIds.Add(preferred.Id);
            await dbContext.PlexServers
                .IgnoreIsEnabledFilter()
                .Where(x => x.Id == server.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.PreferredConnectionId, preferred.Id), CancellationToken);
        }

        connections = await dbContext
            .PlexServerConnections.Include(x => x.LatestConnectionStatus)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        return new(
            connections.Select(x => Snapshot(x, chosenIds.Contains(x.Id))).ToList(),
            chosenIds,
            ((DbContext)dbContext).Database.GetConnectionString()!
        );
    }

    private async Task<PolicyFixture> ArrangePolicyFailureGraphAsync()
    {
        await SetupDatabase(
            72005,
            config =>
            {
                config.PlexServerCount = 5;
                config.PlexServerConnectionPerServerCount = 3;
            }
        );

        var dbContext = IDbContext;
        var servers = await dbContext.PlexServers.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var connections = await dbContext.PlexServerConnections.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var preferred = connections.Where(x => x.PlexServerId == servers[0].Id).OrderBy(x => x.Id).Skip(1).First();
        await dbContext.PlexServers
            .IgnoreIsEnabledFilter()
            .Where(x => x.Id == servers[0].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.PreferredConnectionId, preferred.Id), CancellationToken);
        await dbContext.PlexServerStatuses
            .Where(x => x.PlexServerId == servers[1].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsSuccessful, false), CancellationToken);
        await dbContext.PlexServerStatuses
            .Where(x => x.PlexServerId == servers[2].Id || x.PlexServerId == servers[4].Id)
            .ExecuteDeleteAsync(CancellationToken);
        await dbContext.PlexServerConnections
            .Where(x => x.PlexServerId == servers[4].Id)
            .ExecuteDeleteAsync(CancellationToken);
        await dbContext.PlexServers
            .IgnoreIsEnabledFilter()
            .Where(x => x.Id == servers[3].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);

        var returnedConnections = await dbContext
            .PlexServerConnections.Include(x => x.LatestConnectionStatus)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var disabledConnectionIds = connections
            .Where(x => x.PlexServerId == servers[3].Id)
            .Select(x => x.Id)
            .ToHashSet();
        var expected = returnedConnections
            .Where(x => x.PlexServerId != servers[3].Id)
            .Select(x => Snapshot(x, x.Id == preferred.Id))
            .ToList();
        return new(
            expected,
            disabledConnectionIds
        );
    }

    private static ConnectionSnapshot Snapshot(PlexServerConnection source, bool chosen) =>
        new(
            source.Id,
            source.PlexServerId,
            chosen,
            source.LatestConnectionStatus?.Id,
            source.LatestConnectionStatus?.IsSuccessful,
            source.LatestConnectionStatus?.StatusCode,
            source.LatestConnectionStatus?.StatusMessage,
            source.LatestConnectionStatus?.LastChecked,
            source.LatestConnectionStatus?.PlexServerId,
            source.LatestConnectionStatus?.PlexServerConnectionId
        );

    private static ConnectionSnapshot Snapshot(PlexServerConnectionDTO source) =>
        new(
            source.Id,
            source.PlexServerId,
            source.ChosenConnection,
            source.LatestConnectionStatus?.Id,
            source.LatestConnectionStatus?.IsSuccessful,
            source.LatestConnectionStatus?.StatusCode,
            source.LatestConnectionStatus?.StatusMessage,
            source.LatestConnectionStatus?.LastChecked,
            source.LatestConnectionStatus?.PlexServerId,
            source.LatestConnectionStatus?.PlexServerConnectionId
        );

    private sealed record PreferredFixture(
        List<ConnectionSnapshot> Expected,
        HashSet<int> ChosenIds,
        string ConnectionString
    );

    private sealed record PolicyFixture(
        List<ConnectionSnapshot> Expected,
        HashSet<int> DisabledConnectionIds
    );

    private sealed record ConnectionSnapshot(
        int Id,
        int PlexServerId,
        bool ChosenConnection,
        int? StatusId,
        bool? IsSuccessful,
        int? StatusCode,
        string? StatusMessage,
        DateTime? LastChecked,
        int? StatusPlexServerId,
        int? StatusPlexServerConnectionId
    );
}
