using System.IO.Abstractions;
using Autofac;
using Reaparr.Data;
using Reaparr.Data.Contracts;
using Reaparr.Environment;
using Reaparr.Identity.Contracts;
using Reaparr.Settings.Contracts;

namespace Reaparr.Domain.UnitTests;

public class ReaparrDbContextManagerUnitTests : BaseUnitTest<ReaparrDbContextManager>
{
    private string DatabasePath => Mock.Container.Resolve<IPathProvider>().DatabasePath;

    [Test]
    public async Task ShouldConnectToDatabaseAndCheckToMigrate_WhenDatabaseAlreadyExists()
    {
        // Arrange

        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(true);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.GetPendingMigrations()).Returns([]);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.CanConnect()).Returns(true);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(false);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.GetPendingMigrations()).Returns([]);

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.CanConnect(), Times.Once);
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.GetPendingMigrations(), Times.Once);
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.Migrate(), Times.Never);
        Mock.Mock<IAuthDbContextDatabase>().Verify(x => x.CanConnect(), Times.Never);
        Mock.Mock<IAuthDbContextDatabase>().Verify(x => x.GetPendingMigrations(), Times.Once);
        Mock.Mock<IAuthDbContextDatabase>().Verify(x => x.Migrate(), Times.Never);
    }

    [Test]
    public async Task ShouldCreateDatabase_WhenDatabaseDoesNotExist()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(false).Verifiable(Times.Once);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok()).Verifiable(Times.Once);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok()).Verifiable(Times.Once);
        Mock.Mock<IReaparrDbContextDatabase>()
            .Setup(x => x.Optimize(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(0));

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.Migrate(), Times.Once); // Database creation involves migration
    }

    [Test]
    public async Task ShouldFailToCreateDatabase_WhenExceptionIsThrown()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>()
            .Setup(x => x.Migrate())
            .Throws(new Exception("Test Exception"))
            .Verifiable(Times.Once);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok()).Verifiable(Times.Never);

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsFailed.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldBackUpAndResetDatabase_WhenDatabaseCannotConnect()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        Mock.Mock<IFile>().Setup(x => x.Copy(It.IsAny<string>(), It.IsAny<string>())).Verifiable(Times.Exactly(3));
        Mock.Mock<IDirectory>()
            .Setup(x => x.CreateDirectory(It.IsAny<string>()))
            .Returns(new Mock<IDirectoryInfo>().Object);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok()).Verifiable(Times.Once);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok()).Verifiable(Times.Once);
        Mock.Mock<IReaparrDbContextDatabase>()
            .Setup(x => x.Optimize(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(0));
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.EnsureDeleted()).Returns(Result.Ok(true));
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CloseConnection());
        Mock.Mock<IGeneralSettings>().SetupSet(x => x.FirstTimeSetup = true).Verifiable(Times.Once);

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var mockDbContext = Mock.Mock<IReaparrDbContextDatabase>();
        mockDbContext.Verify(x => x.CanConnect(), Times.Once);
        mockDbContext.Verify(x => x.EnsureDeleted(), Times.Once); // Database is reset
        mockDbContext.Verify(x => x.Migrate(), Times.Once); // Database is recreated after reset
    }

    [Test]
    public async Task ShouldMigrateReaparrDatabase_WhenPendingMigrationsExist()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(true);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>()
            .Setup(x => x.GetPendingMigrations())
            .Returns(["Migration1", "Migration2"]);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok()).Verifiable(Times.Once);
        Mock.Mock<IReaparrDbContextDatabase>()
            .Setup(x => x.Optimize(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(0));
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(false);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.GetPendingMigrations()).Returns([]);

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.Migrate(), Times.Once);
        Mock.Mock<IAuthDbContextDatabase>().Verify(x => x.Migrate(), Times.Never);
    }

    [Test]
    public async Task ShouldMigrateAuthDatabase_WhenPendingMigrationsExist()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(true);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.GetPendingMigrations()).Returns([]);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(false);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.GetPendingMigrations()).Returns(["AuthMigration1"]);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok()).Verifiable(Times.Once);

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.Migrate(), Times.Never);
        Mock.Mock<IAuthDbContextDatabase>().Verify(x => x.Migrate(), Times.Once);
    }

    [Test]
    public async Task ShouldSkipMigration_WhenDatabaseIsInMemory()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(true);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(true);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.GetPendingMigrations()).Returns(["Migration1"]);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(true);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.GetPendingMigrations()).Returns(["AuthMigration1"]);

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.Migrate(), Times.Never);
        Mock.Mock<IAuthDbContextDatabase>().Verify(x => x.Migrate(), Times.Never);
    }

    [Test]
    public async Task ShouldResetDatabase_WhenReaparrMigrationFails()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        Mock.Mock<IFile>().Setup(x => x.Copy(It.IsAny<string>(), It.IsAny<string>()));
        Mock.Mock<IDirectory>()
            .Setup(x => x.CreateDirectory(It.IsAny<string>()))
            .Returns(new Mock<IDirectoryInfo>().Object);

        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(true);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.GetPendingMigrations()).Returns(["Migration1"]);
        Mock.Mock<IReaparrDbContextDatabase>()
            .SetupSequence(x => x.Migrate())
            .Returns(Result.Fail("Migration failed"))
            .Returns(Result.Ok()); // Second call during database reset
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CloseConnection());
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.EnsureDeleted()).Returns(Result.Ok(true));
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok());
        Mock.Mock<IReaparrDbContextDatabase>()
            .Setup(x => x.Optimize(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(0));
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(false);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.GetPendingMigrations()).Returns([]);
        Mock.Mock<IGeneralSettings>().SetupSet(x => x.FirstTimeSetup = true).Verifiable(Times.AtLeastOnce);

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.Migrate(), Times.Exactly(2)); // Once for a migration attempt and once for a reset
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.EnsureDeleted(), Times.Once);
    }

    [Test]
    public async Task ShouldResetDatabase_WhenAuthDatabaseMigrationFails()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        Mock.Mock<IFile>().Setup(x => x.Copy(It.IsAny<string>(), It.IsAny<string>()));
        Mock.Mock<IDirectory>()
            .Setup(x => x.CreateDirectory(It.IsAny<string>()))
            .Returns(new Mock<IDirectoryInfo>().Object);

        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(true);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.GetPendingMigrations()).Returns([]);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok());
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CloseConnection());
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.EnsureDeleted()).Returns(Result.Ok(true));

        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(false);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.GetPendingMigrations()).Returns(["AuthMigration1"]);
        Mock.Mock<IAuthDbContextDatabase>()
            .SetupSequence(x => x.Migrate())
            .Returns(Result.Fail("Auth migration failed"))
            .Returns(Result.Ok()); // Second call during database reset
        Mock.Mock<IReaparrDbContextDatabase>()
            .Setup(x => x.Optimize(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(0));
        Mock.Mock<IGeneralSettings>().SetupSet(x => x.FirstTimeSetup = true).Verifiable(Times.AtLeastOnce);

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IAuthDbContextDatabase>().Verify(x => x.Migrate(), Times.Exactly(2)); // Once for migration attempt, once for reset
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.EnsureDeleted(), Times.Once);
    }

    [Test]
    public async Task ShouldResetDatabase_WhenMigrationThrowsException()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        Mock.Mock<IFile>().Setup(x => x.Copy(It.IsAny<string>(), It.IsAny<string>()));
        Mock.Mock<IDirectory>()
            .Setup(x => x.CreateDirectory(It.IsAny<string>()))
            .Returns(new Mock<IDirectoryInfo>().Object);

        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(true);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.GetPendingMigrations()).Returns(["Migration1"]);
        Mock.Mock<IReaparrDbContextDatabase>()
            .SetupSequence(x => x.Migrate())
            .Throws(new Exception("Migration exception"))
            .Returns(Result.Ok()); // Second call during database reset
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CloseConnection());
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.EnsureDeleted()).Returns(Result.Ok(true));
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok());
        Mock.Mock<IReaparrDbContextDatabase>()
            .Setup(x => x.Optimize(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(0));
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.IsInMemory()).Returns(false);
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.GetPendingMigrations()).Returns([]);
        Mock.Mock<IGeneralSettings>().SetupSet(x => x.FirstTimeSetup = true).Verifiable(Times.Once);

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.EnsureDeleted(), Times.Once);
    }

    [Test]
    public async Task ShouldFailBackup_WhenBackupDirectoryCannotBeCreated()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        Mock.Mock<IDirectory>()
            .Setup(x => x.CreateDirectory(It.IsAny<string>()))
            .Throws(new Exception("Directory creation failed"));
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(false);

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsFailed.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldHandlePartialBackup_WhenSomeDatabaseFilesAreMissing()
    {
        // Arrange
        List<string> dbFiles = [DatabasePath, DatabasePath + "-shm", DatabasePath + "-wal"];
        Mock.Mock<IFile>().Setup(x => x.Exists(dbFiles[0])).Returns(true);
        Mock.Mock<IFile>().Setup(x => x.Exists(dbFiles[1])).Returns(false);
        Mock.Mock<IFile>().Setup(x => x.Exists(dbFiles[2])).Returns(true);
        Mock.Mock<IFile>().Setup(x => x.Copy(It.IsAny<string>(), It.IsAny<string>()));
        Mock.Mock<IDirectory>()
            .Setup(x => x.CreateDirectory(It.IsAny<string>()))
            .Returns(new Mock<IDirectoryInfo>().Object);

        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CloseConnection());
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.EnsureDeleted()).Returns(Result.Ok(true));
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok());
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok());
        Mock.Mock<IReaparrDbContextDatabase>()
            .Setup(x => x.Optimize(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(0));
        Mock.Mock<IGeneralSettings>().SetupSet(x => x.FirstTimeSetup = true).Verifiable(Times.Once);

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IFile>().Verify(x => x.Copy(dbFiles[0], It.IsAny<string>()), Times.Once);
        Mock.Mock<IFile>().Verify(x => x.Copy(dbFiles[2], It.IsAny<string>()), Times.Once);
        Mock.Mock<IFile>().Verify(x => x.Copy(dbFiles[1], It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ShouldFailReset_WhenDatabaseDeletionFails()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        Mock.Mock<IFile>().Setup(x => x.Copy(It.IsAny<string>(), It.IsAny<string>()));
        Mock.Mock<IDirectory>()
            .Setup(x => x.CreateDirectory(It.IsAny<string>()))
            .Returns(new Mock<IDirectoryInfo>().Object);

        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CloseConnection());
        Mock.Mock<IReaparrDbContextDatabase>()
            .Setup(x => x.EnsureDeleted())
            .Returns(Result.Fail("Database deletion failed"));

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.EnsureDeleted(), Times.Once);
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.Migrate(), Times.Never); // Should not attempt to recreate if deletion failed
    }

    [Test]
    public async Task ShouldFailReset_WhenDatabaseCreationFailsAfterReset()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        Mock.Mock<IFile>().Setup(x => x.Copy(It.IsAny<string>(), It.IsAny<string>()));
        Mock.Mock<IDirectory>()
            .Setup(x => x.CreateDirectory(It.IsAny<string>()))
            .Returns(new Mock<IDirectoryInfo>().Object);

        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CloseConnection());
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.EnsureDeleted()).Returns(Result.Ok(true));
        Mock.Mock<IReaparrDbContextDatabase>()
            .Setup(x => x.Migrate())
            .Throws(new Exception("Database creation failed"));

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.EnsureDeleted(), Times.Once);
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.Migrate(), Times.Once);
    }

    [Test]
    public async Task ShouldHandleExceptionInResetDatabase_AndReturnFailure()
    {
        // Arrange
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>()
            .Setup(x => x.CloseConnection())
            .Throws(new Exception("Connection close failed"));
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(true);

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsFailed.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldSetFirstTimeSetupToTrue_WhenDatabaseIsReset()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        Mock.Mock<IFile>().Setup(x => x.Copy(It.IsAny<string>(), It.IsAny<string>()));
        Mock.Mock<IDirectory>()
            .Setup(x => x.CreateDirectory(It.IsAny<string>()))
            .Returns(new Mock<IDirectoryInfo>().Object);

        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CloseConnection());
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.EnsureDeleted()).Returns(Result.Ok(true));
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok());
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok());
        Mock.Mock<IReaparrDbContextDatabase>()
            .Setup(x => x.Optimize(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(0));

        var generalSettingsMock = Mock.Mock<IGeneralSettings>();
        generalSettingsMock.SetupProperty(x => x.FirstTimeSetup);

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        generalSettingsMock.VerifySet(x => x.FirstTimeSetup = true, Times.Once);
    }

    [Test]
    public async Task ShouldSkipBackup_WhenDatabaseDoesNotExistDuringReset()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(DatabasePath)).Returns(false); // Database doesn't exist for backup
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CanConnect()).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.CloseConnection());
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.EnsureDeleted()).Returns(Result.Ok(true));
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok());
        Mock.Mock<IAuthDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok());
        Mock.Mock<IReaparrDbContextDatabase>()
            .Setup(x => x.Optimize(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(0));

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Mock.Mock<IDirectory>().Verify(x => x.CreateDirectory(It.IsAny<string>()), Times.Never);
        Mock.Mock<IFile>().Verify(x => x.Copy(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task ShouldFailAuthDatabaseCreation_WhenExceptionIsThrown()
    {
        // Arrange
        Mock.Mock<IFile>().Setup(x => x.Exists(It.IsAny<string>())).Returns(false);
        Mock.Mock<IReaparrDbContextDatabase>().Setup(x => x.Migrate()).Returns(Result.Ok());
        Mock.Mock<IAuthDbContextDatabase>()
            .Setup(x => x.Migrate())
            .Throws(new Exception("Auth database creation failed"));

        // Act
        var result = await Sut.SetupAsync();

        // Assert
        result.IsFailed.ShouldBeTrue();
        Mock.Mock<IReaparrDbContextDatabase>().Verify(x => x.Migrate(), Times.Once);
        Mock.Mock<IAuthDbContextDatabase>().Verify(x => x.Migrate(), Times.Once);
    }
}
