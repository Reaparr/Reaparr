namespace Reaparr.Application.UnitTests;

public class RecalculatePlexGenreTypesCommandUnitTests : BaseCommandUnitTest<RecalculatePlexGenreTypesCommand>
{
    [Test]
    public async Task ShouldLeaveGenreTypesUnchanged_WhenCalculatedTypesAlreadyMatch()
    {
        // Arrange
        await SetupDatabase(7101);
        await SeedGenres(
            new PlexGenre
            {
                Name = "Sport",
                Key = "sport",
                Type = PlexGenreType.Sport,
            },
            new PlexGenre
            {
                Name = "Documentaire",
                Key = "documentaire",
                Type = PlexGenreType.Documentary,
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PlexGenreTypeRecalculationResult>(
            new RecalculatePlexGenreTypesCommand()
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ScannedCount.ShouldBe(2);
        result.Value.UpdatedCount.ShouldBe(0);
        var genreTypes = await GetGenreTypes();
        genreTypes["sport"].ShouldBe(PlexGenreType.Sport);
        genreTypes["documentaire"].ShouldBe(PlexGenreType.Documentary);
    }

    [Test]
    public async Task ShouldUpdateChangedGenreTypes_WhenExistingTypesAreStale()
    {
        // Arrange
        await SetupDatabase(7102);
        await SeedGenres(
            new PlexGenre
            {
                Name = "Sport",
                Key = "sport",
                Type = PlexGenreType.Unknown,
            },
            new PlexGenre
            {
                Name = "Documentaire",
                Key = "documentaire",
                Type = PlexGenreType.Unknown,
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PlexGenreTypeRecalculationResult>(
            new RecalculatePlexGenreTypesCommand()
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ScannedCount.ShouldBe(2);
        result.Value.UpdatedCount.ShouldBe(2);
        var genreTypes = await GetGenreTypes();
        genreTypes["sport"].ShouldBe(PlexGenreType.Sport);
        genreTypes["documentaire"].ShouldBe(PlexGenreType.Documentary);
    }

    [Test]
    public async Task ShouldScanAndUpdateAllGenres_WhenRowsAreMixed()
    {
        // Arrange
        await SetupDatabase(7103);
        await SeedGenres(
            new PlexGenre
            {
                Name = "Sport",
                Key = "sport",
                Type = PlexGenreType.Unknown,
            },
            new PlexGenre
            {
                Name = "Comedy",
                Key = "comedy",
                Type = PlexGenreType.Comedy,
            },
            new PlexGenre
            {
                Name = "Sport / Documentary",
                Key = "sport-documentary",
                Type = PlexGenreType.Unknown,
            },
            new PlexGenre
            {
                Name = "Unclassified",
                Key = "unclassified",
                Type = PlexGenreType.Unknown,
            },
            new PlexGenre
            {
                Name = "Asia",
                Key = "asia",
                Type = PlexGenreType.Unknown,
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<PlexGenreTypeRecalculationResult>(
            new RecalculatePlexGenreTypesCommand()
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ScannedCount.ShouldBe(5);
        result.Value.UpdatedCount.ShouldBe(3);
        var genreTypes = await GetGenreTypes();
        genreTypes["sport"].ShouldBe(PlexGenreType.Sport);
        genreTypes["comedy"].ShouldBe(PlexGenreType.Comedy);
        genreTypes["sport-documentary"].ShouldBe(PlexGenreType.Group);
        genreTypes["unclassified"].ShouldBe(PlexGenreType.Unknown);
        genreTypes["asia"].ShouldBe(PlexGenreType.Foreign);
    }

    [Test]
    public async Task ShouldReturnZeroCounts_WhenGenreTableIsEmpty()
    {
        // Arrange
        await SetupDatabase(7104);

        // Act
        var result = await TestHandlerExecuteAsync<PlexGenreTypeRecalculationResult>(
            new RecalculatePlexGenreTypesCommand()
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ScannedCount.ShouldBe(0);
        result.Value.UpdatedCount.ShouldBe(0);
    }

    private async Task SeedGenres(params PlexGenre[] genres)
    {
        using var dbContext = IDbContext;
        dbContext.PlexGenres.AddRange(genres);
        await dbContext.SaveChangesAsync(CancellationToken);
    }

    private async Task<Dictionary<string, PlexGenreType>> GetGenreTypes()
    {
        using var dbContext = IDbContext;
        return await dbContext.PlexGenres.ToDictionaryAsync(x => x.Key, x => x.Type, CancellationToken);
    }
}
