using System.IO.Abstractions;
using Autofac;
using Reaparr.Environment;
using Reaparr.FileSystem.Contracts;

namespace Reaparr.FileSystem.UnitTests;

public class FileSystemExtensionsUnitTests : BaseUnitTest
{
    [Test]
    public void ShouldReturnCorrectAvailableSpace_WhenUsingTheRootPath()
    {
        // Arrange
        var path = "/";

        SetupFileSystem();

        // Act
        var sut = Mock.Container.Resolve<IPath>();
        var result = sut.GetAvailableSpaceByDirectory(path);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(DefaultAvailableSpace);
    }

    [Test]
    public void ShouldReturnCorrectAvailableSpace_WhenUsingTheMoviesPath()
    {
        // Arrange
        var path = Mock.Create<IPathProvider>().DefaultMovieDestinationFolder;

        SetupFileSystem(system =>
        {
            system.AddDirectory(path);
        });

        // Act
        var sut = Mock.Container.Resolve<IPath>();
        var result = sut.GetAvailableSpaceByDirectory(path);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(DefaultAvailableSpace);
    }

    [Test]
    public void ShouldReturnCorrectAvailableSpace_WhenUsingACustomFolder()
    {
        // Arrange
        var path = "/SomeCustomFolder";

        SetupFileSystem(system =>
        {
            system.AddDirectory(path);
        });

        // Act
        var sut = Mock.Container.Resolve<IPath>();
        var result = sut.GetAvailableSpaceByDirectory(path);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(DefaultAvailableSpace);
    }

    [Test]
    public void ShouldReturnFailedResult_WhenUsingAFolderThatDoesNotExist()
    {
        // Arrange
        var path = @"C:\FolderDoesNotExist";

        SetupFileSystem();

        // Act
        var sut = Mock.Container.Resolve<IPath>();
        var result = sut.GetAvailableSpaceByDirectory(path);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
    }
}
