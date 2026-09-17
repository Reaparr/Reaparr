using Reaparr.Identity;

namespace Reaparr.Data.UnitTests;

public class DbContextOptimizeUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldOptimizeReaparrDatabase_WithoutTreatingCancellationTokenAsSqlParameter()
    {
        // Arrange
        await SetupDatabase(91237);
        await using var dbContext = (ReaparrDbContext)IDbContext;

        // Act
        var result = await dbContext.Optimize(CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
    }

    [Test]
    public async Task ShouldReturnCancelledResult_WhenReaparrDatabaseOptimizationIsCancelled()
    {
        // Arrange
        await SetupDatabase(91239);
        await using var dbContext = (ReaparrDbContext)IDbContext;
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var result = await dbContext.Optimize(cancellationTokenSource.Token);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.IsCancelled.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldOptimizeAuthDatabase_WithoutTreatingCancellationTokenAsSqlParameter()
    {
        // Arrange
        await SetupDatabase(91238);
        await using var dbContext = (AuthDbContext)IAuthDbContext;

        // Act
        var result = await dbContext.Optimize(CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
    }
}
