using System.IO.Abstractions;
using Autofac;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Reaparr.Environment;
using Reaparr.Identity.Contracts;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;

namespace Reaparr.Data.UnitTests;

public class ReaparrDbContextManagerMigrationProgressUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(0)]
    [Arguments(2)]
    public async Task ShouldReportOrderedMigrationSteps_WhenApplyingPendingMigrations(int pendingCount)
    {
        // Arrange
        await SetupDatabase(
            59307,
            options: null,
            targetMigration: pendingCount == 0 ? null : "20261031153443_AddMusicPhotosAndOtherVideos"
        );
        var dbContext = (ReaparrDbContext)IDbContext;
        var database = dbContext.Database;
        var migrations = database.GetMigrations().ToArray();
        if (pendingCount > 0)
            await database.GetService<IMigrator>().MigrateAsync(migrations[^(pendingCount + 1)], CancellationToken);
        var pending = (await database.GetPendingMigrationsAsync(CancellationToken)).ToArray();
        pending.ShouldBe(migrations.TakeLast(pendingCount).ToArray());
        var appliedBefore = (await database.GetAppliedMigrationsAsync(CancellationToken)).ToArray();
        var databasePath = Mock.Container.Resolve<IPathProvider>().DatabasePath;
        SetAppRuntimeInfo(x => x.IsIntegrationTestMode = false);
        Mock.Mock<IFile>().Setup(x => x.Exists(databasePath)).Returns(true).Verifiable(Times.Once());
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(false).Verifiable(Times.Once());
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.GetPendingMigrations()).Returns([]).Verifiable(Times.Once());
        var manager = Mock.Create<ReaparrDbContextManager>(
            new TypedParameter(typeof(IReaparrDbContextDatabase), dbContext)
        );
        using var context = TestCorrelator.CreateContext();

        // Act
        var result = await manager.SetupAsync(CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await database.GetPendingMigrationsAsync(CancellationToken)).ShouldBeEmpty();
        (await database.GetAppliedMigrationsAsync(CancellationToken)).ShouldBe(
            appliedBefore.Concat(pending).OrderBy(x => x).ToArray()
        );
        var events = TestCorrelator.GetLogEventsFromContextId(context.Id).ToArray();
        var steps = events
            .Where(x => x.Properties.ContainsKey("MigrationStep"))
            .Select(x => new
            {
                Step = (int)((ScalarValue)x.Properties["MigrationStep"]).Value!,
                Count = (int)((ScalarValue)x.Properties["MigrationCount"]).Value!,
                Name = (string)((ScalarValue)x.Properties["MigrationName"]).Value!,
                Starting = x.MessageTemplate.Text.Contains("Starting migration"),
            })
            .ToArray();
        steps.ShouldBe(
            pending
                .SelectMany(
                    (name, index) =>
                        new[]
                        {
                            new
                            {
                                Step = index + 1,
                                Count = pendingCount,
                                Name = name,
                                Starting = true,
                            },
                            new
                            {
                                Step = index + 1,
                                Count = pendingCount,
                                Name = name,
                                Starting = false,
                            },
                        }
                )
                .ToArray()
        );
        var completions = events.Where(x => x.MessageTemplate.Text.StartsWith("All {MigrationCount}")).ToArray();
        if (pendingCount == 0)
            completions.ShouldBeEmpty();
        else
        {
            ((ScalarValue)completions.Single().Properties["MigrationCount"]).Value.ShouldBe(pendingCount);
            Array
                .IndexOf(events, completions.Single())
                .ShouldBeGreaterThan(
                    Array.IndexOf(events, events.Last(x => x.Properties.ContainsKey("MigrationStep")))
                );
        }
        Mock.Mock<IFile>().Verify();
        Mock.Mock<IAuthDbContextDatabase>().Verify();
        Mock.Mock<IAuthDbContextDatabase>().Verify(x => x.Migrate(), Times.Never());
    }
}
