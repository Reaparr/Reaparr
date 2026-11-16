namespace Reaparr.Application.UnitTests;

public class CreateFolderPathEndpointUnitTests
    : BaseEndpointUnitTest<CreateFolderPathEndpoint, CreateFolderPathEndpointRequest, ResultDTO<FolderPathDTO>>
{
    [Test]
    public async Task ShouldReportPersistedFolder_WhenFolderPathIsCreated()
    {
        // Arrange
        await SetupDatabase(44208);
        var db = IDbContext;
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.FolderPathChanged(
                    It.Is<FolderPath>(folder =>
                        folder.Id > 0
                        && folder.FolderType == FolderType.MovieFolder
                        && folder.MediaType == PlexMediaType.Movie
                        && folder.DisplayName == "Created folder"
                        && folder.DirectoryPath == "/tmp/created-folder"
                    ),
                    TelemetryAction.Create
                )
            )
            .Verifiable(Times.Once());
        var request = new CreateFolderPathEndpointRequest
        {
            FolderPathDto = new FolderPathDTO
            {
                Id = 0,
                FolderType = FolderType.MovieFolder,
                MediaType = PlexMediaType.Movie,
                DisplayName = "Created folder",
                Directory = "/tmp/created-folder",
                IsValid = false,
                IsDefault = false,
            },
        };

        // Act
        var outcome = await TestEndpointHandleAsync(request);

        // Assert
        outcome.IsValid.ShouldBeTrue();
        var response = outcome.Response.ShouldNotBeNull();
        response.IsSuccess.ShouldBeTrue();
        response.Errors.Count.ShouldBe(0);
        var created = await db.FolderPaths.SingleAsync(x => x.DisplayName == "Created folder", CancellationToken);
        (created.FolderType, created.MediaType, created.DirectoryPath).ShouldBe(
            (FolderType.MovieFolder, PlexMediaType.Movie, "/tmp/created-folder")
        );
        Mock.Mock<ITelemetry>().Verify();
    }
}
