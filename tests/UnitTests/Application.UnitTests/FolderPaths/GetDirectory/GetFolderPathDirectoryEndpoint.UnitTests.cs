using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Autofac;

namespace Reaparr.Application.UnitTests;

public class GetFolderPathDirectoryEndpointUnitTests
    : BaseEndpointUnitTest<GetFolderPathDirectoryEndpoint, GetFolderPathDirectoryRequest, FileSystemDTO>
{
    [Test]
    public async Task ShouldReturnRequestedDirectoryWithoutProbingMounts_WhenPathExists()
    {
        // Arrange
        // Current-directory mapping uses native DirectoryInfo, so isolate that boundary in a real temporary directory.
        var directory = Directory.CreateTempSubdirectory(nameof(GetFolderPathDirectoryEndpointUnitTests));
        var path = directory.FullName;
        var childPath = Path.Combine(path, "child") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(childPath);
        var modified = Directory.GetLastWriteTimeUtc(path);
        var childModified = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var parent = Directory.GetParent(path)!.FullName;
        SetupFileSystem(system => system.AddDirectory(path));
        var disk = Mock.Mock<IDiskProvider>();
        disk.Setup(x => x.GetAllMounts()).Returns([]).Verifiable(Times.Never());
        disk.Setup(x => x.GetVolumeName(It.IsAny<DriveInfo>())).Verifiable(Times.Never());
        disk.Setup(x => x.GetFiles(It.IsAny<string>())).Verifiable(Times.Never());
        disk.Setup(x => x.GetParent(path)).Returns(parent).Verifiable(Times.Once());
        disk.Setup(x => x.GetDirectories(path))
            .Returns(Result.Ok(new List<FileSystemModel>
            {
                new()
                {
                    Type = FileSystemEntityType.Folder,
                    Name = "child",
                    Path = childPath,
                    LastModified = childModified,
                    Extension = "",
                    Size = 0,
                    HasReadPermission = true,
                    HasWritePermission = false,
                },
            }))
            .Verifiable(Times.Once());

        // Act
        var outcome = await TestEndpointHandleAsync(new GetFolderPathDirectoryRequest(path), RegisterDirectoryServices);

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var result = (await JsonSerializer.DeserializeAsync<ResultDTO<FileSystemDTO>>(
            outcome.Endpoint.HttpContext.Response.Body, DefaultJsonSerializerOptions.ConfigStandard, CancellationToken
        )).ShouldNotBeNull();
        (result.IsSuccess, result.Errors.Count).ShouldBe((true, 0));
        var value = result.Value.ShouldNotBeNull();
        value.Parent.ShouldBe(parent);
        value.Files.ShouldBeEmpty();
        var current = value.Current.ShouldNotBeNull();
        (current.Type, current.Name, current.Path, current.LastModified, current.Extension, current.Size,
            current.HasReadPermission, current.HasWritePermission).ShouldBe(
            (FileSystemEntityType.Folder, directory.Name, path + Path.DirectorySeparatorChar, (DateTime?)modified, "", 0L, true, true));
        value.Directories.Select(x => (x.Type, x.Name, x.Path, x.LastModified, x.Extension, x.Size,
            x.HasReadPermission, x.HasWritePermission)).ShouldBe([
            (FileSystemEntityType.Folder, "child", childPath, (DateTime?)childModified, "", 0L, true, false),
        ]);
        disk.Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldReturnRootListingOnce_WhenPathIsEmptyOrMissing(bool missing)
    {
        // Arrange
        var path = missing ? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) : "";
        SetupFileSystem();
        var disk = Mock.Mock<IDiskProvider>();
        disk.Setup(x => x.GetAllMounts()).Returns([]).Verifiable(Times.Once());
        disk.Setup(x => x.GetVolumeName(It.IsAny<DriveInfo>())).Verifiable(Times.Never());
        disk.Setup(x => x.GetDirectories(It.IsAny<string>())).Verifiable(Times.Never());
        disk.Setup(x => x.GetFiles(It.IsAny<string>())).Verifiable(Times.Never());
        disk.Setup(x => x.GetParent(It.IsAny<string>())).Verifiable(Times.Never());

        // Act
        var outcome = await TestEndpointHandleAsync(new GetFolderPathDirectoryRequest(path), RegisterDirectoryServices);

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var result = (await JsonSerializer.DeserializeAsync<ResultDTO<FileSystemDTO>>(
            outcome.Endpoint.HttpContext.Response.Body, DefaultJsonSerializerOptions.ConfigStandard, CancellationToken
        )).ShouldNotBeNull();
        (result.IsSuccess, result.Errors.Count).ShouldBe((true, 0));
        var value = result.Value.ShouldNotBeNull();
        value.Parent.ShouldBe("");
        value.Current.ShouldBeNull();
        value.Directories.ShouldBeEmpty();
        value.Files.ShouldBeEmpty();
        disk.Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldPropagateDirectoryFailureWithoutProbingMounts_WhenListingFails(bool throws)
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        SetupFileSystem(system => system.AddDirectory(path));
        var disk = Mock.Mock<IDiskProvider>();
        disk.Setup(x => x.GetAllMounts()).Returns([]).Verifiable(Times.Never());
        disk.Setup(x => x.GetVolumeName(It.IsAny<DriveInfo>())).Verifiable(Times.Never());
        disk.Setup(x => x.GetFiles(It.IsAny<string>())).Verifiable(Times.Never());
        disk.Setup(x => x.GetParent(It.IsAny<string>())).Verifiable(Times.Never());
        if (throws)
            disk.Setup(x => x.GetDirectories(path)).Throws(new IOException("listing-unavailable")).Verifiable(Times.Once());
        else
            disk.Setup(x => x.GetDirectories(path))
                .Returns(Result.Fail<List<FileSystemModel>>("listing-unavailable")).Verifiable(Times.Once());

        // Act
        var outcome = await TestEndpointHandleAsync(new GetFolderPathDirectoryRequest(path), RegisterDirectoryServices);

        // Assert
        outcome.IsValid.ShouldBeTrue();
        outcome.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        var result = (await JsonSerializer.DeserializeAsync<ResultDTO<FileSystemDTO>>(
            outcome.Endpoint.HttpContext.Response.Body, DefaultJsonSerializerOptions.ConfigStandard, CancellationToken
        )).ShouldNotBeNull();
        (result.IsSuccess, result.Errors.Count).ShouldBe((false, 1));
        result.Value.ShouldBeNull();
        disk.Verify();
    }

    [Test]
    public async Task ShouldRejectNullPathBeforeDirectoryLookup_WhenValidationFails()
    {
        // Arrange
        SetupFileSystem();
        var disk = Mock.Mock<IDiskProvider>();
        disk.Setup(x => x.GetAllMounts()).Returns([]).Verifiable(Times.Never());
        disk.Setup(x => x.GetDirectories(It.IsAny<string>())).Verifiable(Times.Never());
        disk.Setup(x => x.GetFiles(It.IsAny<string>())).Verifiable(Times.Never());
        disk.Setup(x => x.GetParent(It.IsAny<string>())).Verifiable(Times.Never());
        disk.Setup(x => x.GetVolumeName(It.IsAny<DriveInfo>())).Verifiable(Times.Never());

        // Act
        var outcome = await TestEndpointHandleAsync(new GetFolderPathDirectoryRequest(null!), RegisterDirectoryServices);

        // Assert
        outcome.HasValidator.ShouldBeTrue();
        outcome.IsValid.ShouldBeFalse();
        outcome.ValidationErrors.Select(x => x.PropertyName).ShouldBe([nameof(GetFolderPathDirectoryRequest.Path)]);
        outcome.Endpoint.HttpContext.Response.Body.Length.ShouldBe(0);
        disk.Verify();
    }

    private void RegisterDirectoryServices(IServiceCollection services)
    {
        var fileSystem = Mock.Container.Resolve<IFileSystem>();
        services.AddSingleton(fileSystem.Directory);
        services.AddSingleton(fileSystem.Path);
        services.AddSingleton(Mock.Mock<IDiskProvider>().Object);
    }
}
