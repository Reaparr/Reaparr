namespace Reaparr.Domain.UnitTests;

public class DownloadTaskActionsConvertUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(DownloadStatus.AuthError)]
    [Arguments(DownloadStatus.StorageError)]
    [Arguments(DownloadStatus.MoveError)]
    public void ShouldOfferResumeAndRestart_WhenErrorIsRecoverable(DownloadStatus status)
    {
        // Arrange
        DownloadActions[] expected =
        [
            DownloadActions.Details,
            DownloadActions.Start,
            DownloadActions.Restart,
            DownloadActions.Delete,
        ];

        // Act
        var actions = DownloadTaskActions.Convert(status);

        // Assert
        actions.ShouldBe(expected);
    }
}
