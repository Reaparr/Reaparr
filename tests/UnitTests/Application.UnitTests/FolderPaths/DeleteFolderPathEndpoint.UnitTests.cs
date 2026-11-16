namespace Reaparr.Application.UnitTests;

public class DeleteFolderPathEndpointUnitTests
    : BaseEndpointUnitTest<DeleteFolderPathEndpoint, DeleteFolderPathEndpointRequest, BaseResultDTO>
{
    [Test]
    public async Task ShouldReportDeletedRecordWithoutDeletingFiles_WhenFolderPathIsDeleted()
    {
        // Arrange
        await SetupDatabase(44209);
        var db = IDbContext;
        db.FolderPaths.Add(
            new FolderPath
            {
                Id = 11,
                FolderType = FolderType.MovieFolder,
                MediaType = PlexMediaType.Movie,
                DisplayName = "Delete target",
                DirectoryPath = "/tmp/delete-target",
            }
        );
        await db.SaveChangesAsync(CancellationToken);
        var control = await db.FolderPaths.SingleAsync(x => x.Id == 2, CancellationToken);
        Mock.Mock<ITelemetry>().Setup(x => x.FolderPathDeleted(11, 1)).Verifiable(Times.Once());

        // Act
        var outcome = await TestEndpointHandleAsync(new DeleteFolderPathEndpointRequest(11));

        // Assert
        outcome.IsValid.ShouldBeTrue();
        var response = outcome.Response.ShouldNotBeNull();
        (response.IsSuccess, response.Errors.Count).ShouldBe((true, 0));
        (await db.FolderPaths.AnyAsync(x => x.Id == 11, CancellationToken)).ShouldBeFalse();
        var retained = await db.FolderPaths.SingleAsync(x => x.Id == control.Id, CancellationToken);
        (retained.Id, retained.DisplayName, retained.DirectoryPath).ShouldBe(
            (control.Id, control.DisplayName, control.DirectoryPath)
        );
        Mock.Mock<ITelemetry>().Verify();
    }

    [Test]
    public async Task ShouldReportNoOp_WhenFolderPathDoesNotExist()
    {
        // Arrange
        await SetupDatabase(44210);
        Mock.Mock<ITelemetry>().Setup(x => x.FolderPathDeleted(999, 0)).Verifiable(Times.Once());

        // Act
        var outcome = await TestEndpointHandleAsync(new DeleteFolderPathEndpointRequest(999));

        // Assert
        outcome.IsValid.ShouldBeTrue();
        var response = outcome.Response.ShouldNotBeNull();
        (response.IsSuccess, response.Errors.Count).ShouldBe((true, 0));
        Mock.Mock<ITelemetry>().Verify();
    }
}
