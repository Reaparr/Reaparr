using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Reaparr.Data.UnitTests;

public class DbContextExtensionsStreamLanguagesUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldInsertOrIgnoreExactNormalizedPairsAcrossBatches_WhenLanguagesRepeat()
    {
        // Arrange
        await SetupDatabase(59301);
        using var dbContext = IDbContext;
        var existing = new StreamLanguage { LanguageCode = "eng", LanguageTag = "eng" };
        dbContext.StreamLanguages.Add(existing);
        await dbContext.SaveChangesAsync(CancellationToken);
        var existingId = existing.Id;
        var languages = Enumerable
            .Range(0, 405)
            .Select(x => new StreamLanguage { LanguageCode = "test", LanguageTag = $"tag-{x:D3}" })
            .Concat([
                new StreamLanguage { LanguageCode = "ENG", LanguageTag = "ENG" },
                new StreamLanguage { LanguageCode = "eng", LanguageTag = "en" },
                new StreamLanguage { LanguageCode = "", LanguageTag = "" },
                new StreamLanguage { LanguageCode = "", LanguageTag = "" },
                new StreamLanguage { LanguageCode = "x' OR 1=1 --", LanguageTag = "literal" },
            ])
            .ToArray();
        (
            await dbContext
                .StreamLanguages.Select(x => new
                {
                    x.Id,
                    x.LanguageCode,
                    x.LanguageTag,
                })
                .SingleAsync(CancellationToken)
        ).ShouldBe(
            new
            {
                Id = existingId,
                LanguageCode = "eng",
                LanguageTag = "eng",
            }
        );

        // Act
        var first = await dbContext.InsertOrIgnoreStreamLanguagesAsync(languages, CancellationToken);
        var second = await dbContext.InsertOrIgnoreStreamLanguagesAsync(languages.Reverse(), CancellationToken);

        // Assert
        first[("eng", "eng")].ShouldBe(existingId);
        first[("ENG", "eNg")].ShouldBe(existingId);
        second[("ENG", "EN")].ShouldBe(first[("eng", "en")]);
        first[("eng", "en")].ShouldNotBe(existingId);
        first.OrderBy(x => x.Key).ShouldBe(second.OrderBy(x => x.Key));
        var actual = await dbContext
            .StreamLanguages.OrderBy(x => x.LanguageCode)
            .ThenBy(x => x.LanguageTag)
            .Select(x => new
            {
                x.Id,
                x.LanguageCode,
                x.LanguageTag,
            })
            .ToListAsync(CancellationToken);
        var expectedPairs = Enumerable
            .Range(0, 405)
            .Select(x => ("test", $"tag-{x:D3}"))
            .Concat([("eng", "eng"), ("eng", "en"), ("", ""), ("x' or 1=1 --", "literal")])
            .OrderBy(x => x.Item1, StringComparer.Ordinal)
            .ThenBy(x => x.Item2, StringComparer.Ordinal)
            .ToArray();
        actual.Select(x => (x.LanguageCode, x.LanguageTag)).ShouldBe(expectedPairs);
        actual.Select(x => x.Id).Distinct().Count().ShouldBe(expectedPairs.Length);
        foreach (var row in actual)
            first[(row.LanguageCode, row.LanguageTag)].ShouldBe(row.Id);
    }

    [Test]
    public async Task ShouldShareOneLookupIdentity_WhenTransactionsInsertSamePairConcurrently()
    {
        // Arrange
        await SetupDatabase(59302);
        using var firstContext = IDbContext;
        using var secondContext = IDbContext;
        (await firstContext.StreamLanguages.ToListAsync(CancellationToken)).ShouldBeEmpty();
        var firstId = 0;
        var secondId = 0;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var first = Task.Run(
            async () =>
            {
                await start.Task;
                return await firstContext.ExecuteTransactionAsync(
                    async (context, ct) =>
                    {
                        var ids = await context.InsertOrIgnoreStreamLanguagesAsync(
                            [new StreamLanguage { LanguageCode = "ENG", LanguageTag = "eng" }],
                            ct
                        );
                        firstId = ids[("eng", "eng")];
                    },
                    CancellationToken
                );
            },
            CancellationToken
        );
        var second = Task.Run(
            async () =>
            {
                await start.Task;
                return await secondContext.ExecuteTransactionAsync(
                    async (context, ct) =>
                    {
                        var ids = await context.InsertOrIgnoreStreamLanguagesAsync(
                            [new StreamLanguage { LanguageCode = "eng", LanguageTag = "ENG" }],
                            ct
                        );
                        secondId = ids[("eng", "eng")];
                    },
                    CancellationToken
                );
            },
            CancellationToken
        );
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        // Assert
        foreach (var result in results)
        {
            result.IsSuccess.ShouldBeTrue();
            result.Errors.Count.ShouldBe(0);
        }
        firstId.ShouldBeGreaterThan(0);
        secondId.ShouldBe(firstId);
        using var assertContext = IDbContext;
        (
            await assertContext
                .StreamLanguages.Select(x => new
                {
                    x.Id,
                    x.LanguageCode,
                    x.LanguageTag,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe([
            new
            {
                Id = firstId,
                LanguageCode = "eng",
                LanguageTag = "eng",
            },
        ]);
    }

    [Test]
    public async Task ShouldPreserveExistingOwnerAndPersistStreams_WhenUpgradingFromPreviousMigration()
    {
        // Arrange
        await SetupDatabase(
            59304,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
            }
        );
        using var dbContext = IDbContext;
        var ownerBefore = await dbContext
            .PlexMovieData.Select(x => new
            {
                x.Id,
                x.PlexMovieId,
                x.PlexApiRatingKey,
                x.PlexApiMediaId,
                x.PlexApiPartId,
            })
            .SingleAsync(CancellationToken);
        var database = ((DbContext)dbContext).Database;
        var migrations = database.GetMigrations().ToList();
        var migrationAssembly = database.GetService<IMigrationsAssembly>();
        var streamMigrationIndex = migrations.FindIndex(migration =>
            migrationAssembly
                .CreateMigration(migrationAssembly.Migrations[migration], database.ProviderName!)
                .UpOperations.OfType<CreateTableOperation>()
                .Any(operation => operation.Name == nameof(IReaparrDbContext.PlexMovieDataStreams))
        );
        streamMigrationIndex.ShouldBeGreaterThan(0);
        var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[streamMigrationIndex - 1], CancellationToken);

        // Act
        await migrator.MigrateAsync(migrations[streamMigrationIndex], CancellationToken);
        var stream = new PlexMovieDataStream
        {
            Id = 0,
            MediaDataId = ownerBefore.Id,
            PlexApiStreamId = 59304,
            StreamType = StreamType.Subtitle,
            LanguageId = 0,
            IsExternal = null,
            SidecarFormat = "srt",
            Language = new StreamLanguage { LanguageCode = "eng", LanguageTag = "en-us" },
        };
        dbContext.PlexMovieDataStreams.Add(stream);
        await dbContext.SaveChangesAsync(CancellationToken);
        dbContext.ClearChangeTracker();

        // Assert
        (
            await dbContext
                .PlexMovieData.Select(x => new
                {
                    x.Id,
                    x.PlexMovieId,
                    x.PlexApiRatingKey,
                    x.PlexApiMediaId,
                    x.PlexApiPartId,
                })
                .SingleAsync(CancellationToken)
        ).ShouldBe(ownerBefore);
        (
            await dbContext
                .PlexMovieDataStreams.Select(x => new
                {
                    x.Id,
                    x.MediaDataId,
                    x.PlexApiStreamId,
                    x.StreamType,
                    x.IsExternal,
                    x.SidecarFormat,
                    x.Language.LanguageCode,
                    x.Language.LanguageTag,
                })
                .SingleAsync(CancellationToken)
        ).ShouldBe(
            new
            {
                stream.Id,
                MediaDataId = ownerBefore.Id,
                PlexApiStreamId = 59304,
                StreamType = StreamType.Subtitle,
                IsExternal = (bool?)null,
                SidecarFormat = (string?)"srt",
                LanguageCode = "eng",
                LanguageTag = "en-us",
            }
        );
    }
}
