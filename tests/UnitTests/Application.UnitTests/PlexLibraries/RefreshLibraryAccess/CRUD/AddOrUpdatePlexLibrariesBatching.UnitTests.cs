using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Reaparr.Data;

namespace Reaparr.Application.UnitTests;

public class AddOrUpdatePlexLibrariesBatchingUnitTests : BaseUnitTest<AddOrUpdatePlexLibrariesCommandHandler>
{
    [Test]
    [Arguments(1, 1)]
    [Arguments(3, 4)]
    public async Task ShouldReadLibrariesAndOwnershipOnceAndPreserveScopedUpdates_WhenBatchSizeGrows(
        int serverCount,
        int librariesPerServer
    )
    {
        // Arrange
        var scenario = await CreateScenario(serverCount, librariesPerServer);
        using var dbContext = IDbContext;
        scenario.Libraries.Count.ShouldBe((serverCount + 1) * (librariesPerServer + 1));
        scenario
            .Libraries.Where(x => !x.IsEnabled)
            .Select(x => x.PlexServerId)
            .Order()
            .ShouldBe(scenario.Libraries.Select(x => x.PlexServerId).Distinct().Order());
        var beforeAccess = await dbContext
            .PlexAccountLibraries.Select(x => new AccessRow(
                x.PlexAccountId,
                x.PlexServerId,
                x.PlexLibraryId,
                x.IsLibraryOwned
            ))
            .ToListAsync(CancellationToken);
        beforeAccess
            .Where(x => x.AccountId == scenario.AccountId && scenario.ServerIds.Contains(x.ServerId))
            .ShouldBeEmpty();
        beforeAccess.Count.ShouldBe((serverCount + 2) * (librariesPerServer + 1));
        var targetLibraries = scenario.TargetLibraries;
        var targetIds = targetLibraries.Select(x => x.Id).Order().ToArray();
        var expectedAccess = beforeAccess
            .Concat(
                targetLibraries.Select(x => new AccessRow(
                    scenario.AccountId,
                    x.PlexServerId,
                    x.Id,
                    x.PlexServerId == scenario.ServerIds[0]
                ))
            )
            .OrderBy(x => x.AccountId)
            .ThenBy(x => x.ServerId)
            .ThenBy(x => x.LibraryId)
            .ToArray();
        var reads = new LibraryReadCounter();
        await using var measuredContext = CreateMeasuredContext(dbContext.DatabaseName, reads);
        SetupDependencies(builder =>
            builder.RegisterInstance(measuredContext).As<IReaparrDbContext>().ExternallyOwned()
        );
        var pipeline = new ValidationPipeline<AddOrUpdatePlexLibrariesCommand, Result<List<PlexLibraryAccessRapport>>>([
            new AddOrUpdatePlexLibrariesValidator(),
        ]);
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<InvalidateLibraryComparisonJobsCommand>(c =>
                        c.PlexLibraryIds.Order().SequenceEqual(targetIds)
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        foreach (var libraryId in targetIds)
            Mock.Mock<ICommandExecutor>()
                .Setup(x =>
                    x.Send(
                        It.Is<ScheduleAffectedLibraryComparisonJobsCommand>(c => c.PlexLibraryId == libraryId),
                        CancellationToken
                    )
                )
                .ReturnsAsync(Result.Ok())
                .Verifiable(Times.Once());

        // Act
        var result = await pipeline.ExecuteAsync(
            scenario.Command,
            () => Sut.ExecuteAsync(scenario.Command, CancellationToken),
            CancellationToken
        );

        // Assert: count only the handler's physical reads, not fixture or post-state queries.
        reads.LibraryReads.ShouldBe(1);
        reads.OwnershipReads.ShouldBe(1);
        measuredContext.ChangeTracker.Entries<PlexLibrary>().Select(x => x.Entity.Id).Order().ShouldBe(targetIds);
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Select(x => x.PlexServerId).Order().ShouldBe(scenario.ServerIds.Order());
        result
            .Value.SelectMany(x => x.Data)
            .OrderBy(x => x.PlexLibraryId)
            .ShouldBe(
                targetLibraries
                    .OrderBy(x => x.Id)
                    .Select(x => new PlexLibraryAccessRow(
                        PlexAccessState.Granted,
                        x.PlexServerId,
                        x.Id,
                        $"Refreshed {x.Id}"
                    ))
            );
        var libraries = await dbContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .OrderBy(x => x.Id)
            .Select(x => new LibraryRow(x.Id, x.PlexServerId, x.Uuid, x.Title, x.IsEnabled, x.UpdatedAt))
            .ToListAsync(CancellationToken);
        libraries.ShouldBe(scenario.ExpectedLibraries);
        var access = await dbContext
            .PlexAccountLibraries.OrderBy(x => x.PlexAccountId)
            .ThenBy(x => x.PlexServerId)
            .ThenBy(x => x.PlexLibraryId)
            .Select(x => new AccessRow(x.PlexAccountId, x.PlexServerId, x.PlexLibraryId, x.IsLibraryOwned))
            .ToListAsync(CancellationToken);
        access.ShouldBe(expectedAccess);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<QueueLibrarySyncJobCommand>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    private async Task<Scenario> CreateScenario(int serverCount, int librariesPerServer)
    {
        await SetupDatabase(
            5001,
            config =>
            {
                config.PlexServerCount = serverCount + 1;
                config.PlexMovieLibraryCount = librariesPerServer + 1;
                config.PlexAccountCount = 2;
            }
        );
        using var dbContext = IDbContext;
        var accountId = await dbContext.PlexAccounts.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var serverIds = await dbContext
            .PlexServers.OrderBy(x => x.Id)
            .Select(x => x.Id)
            .Take(serverCount)
            .ToArrayAsync(CancellationToken);
        var libraries = await dbContext.PlexLibraries.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        PrepareLibraries(libraries);
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == accountId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.IsServerOwned, x => x.PlexServerId == serverIds[0]),
                CancellationToken
            );
        await dbContext
            .PlexAccountLibraries.Where(x => x.PlexAccountId == accountId && serverIds.Contains(x.PlexServerId))
            .ExecuteDeleteAsync(CancellationToken);
        var updatedAt = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        var targetLibraries = libraries
            .Where(x => serverIds.Contains(x.PlexServerId))
            .GroupBy(x => x.PlexServerId)
            .SelectMany(x => x.Take(librariesPerServer))
            .ToList();
        var targetIds = targetLibraries.Select(x => x.Id).ToHashSet();
        var incoming = targetLibraries.ToApiLibraries(updatedAt);
        for (var index = 0; index < incoming.Count; index++)
            incoming[index].Title = $"Refreshed {targetLibraries[index].Id}";
        var expectedLibraries = libraries
            .Select(x => new LibraryRow(
                x.Id,
                x.PlexServerId,
                x.Uuid,
                targetIds.Contains(x.Id) ? $"Refreshed {x.Id}" : x.Title,
                x.IsEnabled,
                targetIds.Contains(x.Id) ? updatedAt : x.UpdatedAt
            ))
            .ToArray();
        return new Scenario(
            accountId,
            serverIds,
            libraries,
            targetLibraries,
            expectedLibraries,
            new AddOrUpdatePlexLibrariesCommand { PlexAccountId = accountId, PlexLibraries = incoming }
        );
    }

    private static void PrepareLibraries(List<PlexLibrary> libraries)
    {
        foreach (var group in libraries.GroupBy(x => x.PlexServerId))
        {
            var index = 0;
            foreach (var library in group)
            {
                library.Uuid = index switch
                {
                    2 => "CASE-SENSITIVE",
                    3 => "case-sensitive",
                    _ => $"shared-{index}",
                };
                library.IsEnabled = index++ != 0;
                library.SyncedContentChangedAt = library.ContentChangedAt;
                library.Outdated = false;
            }
        }
    }

    private ReaparrDbContext CreateMeasuredContext(string databaseName, LibraryReadCounter reads)
    {
        var options = new DbContextOptionsBuilder<ReaparrDbContext>();
        options.ConfigureSqlite(databaseName, SqliteOpenMode.Memory);
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).AddInterceptors(reads);
        return new ReaparrDbContext(
            options.Options,
            Mock.Container.Resolve<ILogger>(),
            Mock.Container.Resolve<IPathProvider>(),
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            databaseName
        );
    }

    private sealed record Scenario(
        int AccountId,
        int[] ServerIds,
        List<PlexLibrary> Libraries,
        List<PlexLibrary> TargetLibraries,
        LibraryRow[] ExpectedLibraries,
        AddOrUpdatePlexLibrariesCommand Command
    );

    private sealed record LibraryRow(
        int Id,
        int ServerId,
        string Uuid,
        string Title,
        bool IsEnabled,
        DateTime? UpdatedAt
    );

    private sealed record AccessRow(int AccountId, int ServerId, int LibraryId, bool IsOwned);

    private sealed class LibraryReadCounter : DbCommandInterceptor
    {
        public int LibraryReads { get; private set; }
        public int OwnershipReads { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result
        )
        {
            CountRead(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            CountRead(command);
            return ValueTask.FromResult(result);
        }

        private void CountRead(DbCommand command)
        {
            if (!command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                return;
            if (command.CommandText.Contains("FROM \"PlexLibraries\"", StringComparison.Ordinal))
                LibraryReads++;
            if (command.CommandText.Contains("FROM \"PlexAccountServers\"", StringComparison.Ordinal))
                OwnershipReads++;
        }
    }
}
