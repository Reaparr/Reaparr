using System.ComponentModel;
using Downloader;
using DownloadProgressChangedEventArgs = Downloader.DownloadProgressChangedEventArgs;

namespace Reaparr.BaseTests;

/// <summary>
/// Test default for direct-download execution. It keeps endpoint/integration tests on the real
/// queue/job/status pipeline without relying on public sample media URLs staying online.
/// </summary>
public static class MockDownloadService
{
    private const double DOWNLOAD_FILE_SIZE_IN_MIB = 1;
    private const long DOWNLOAD_FILE_SIZE_IN_BYTES = 1024 * 1024;

    public static Func<IFile, Func<DownloadConfiguration, IDownloadService>> SuccessFactory { get; } =
        file => _ => CreateSuccess(file);

    public static Func<IFile, Func<DownloadConfiguration, IDownloadService>> PausableFactory { get; } =
        file => _ => CreatePausable(file);

    private static IDownloadService CreateSuccess(IFile file)
    {
        // Simulates a completed direct download: write the file expected by verification,
        // then raise the same progress/completed events the real downloader raises.
        var mock = CreateBaseMock();
        mock.Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((_, targetPath, _) => Complete(mock, file, targetPath));
        mock.Setup(x => x.DownloadFileTaskAsync(It.IsAny<DownloadPackage>(), It.IsAny<CancellationToken>()))
            .Returns<DownloadPackage, CancellationToken>(
                async (package, _) =>
                {
                    await Complete(mock, file, package.FileName);
                    return Stream.Null;
                }
            );

        return mock.Object;
    }

    private static IDownloadService CreatePausable(IFile file)
    {
        // Simulates an in-progress download: emit partial progress, wait until StopAsync calls CancelTaskAsync,
        // then raise a cancelled completion event so production code transitions to Paused.
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mock = CreateBaseMock();
        mock.Setup(x => x.CancelTaskAsync())
            .Returns(() =>
            {
                stopped.TrySetResult();
                return Task.CompletedTask;
            });
        mock.Setup(x => x.DownloadFileTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(
                async (_, targetPath, cancellationToken) =>
                {
                    // DirectPlexDownloadClient verifies the final, non-.reaptemp path after completion.
                    var finalPath = targetPath.RemoveReapTempSuffix();
                    await file.WriteAllBytesAsync(
                        finalPath,
                        FakeData.GetDownloadFile(DOWNLOAD_FILE_SIZE_IN_MIB),
                        cancellationToken
                    );
                    // ReSharper disable once MethodHasAsyncOverload
                    //DownloadProgressChanged and DownloadFileCompleted are void event handlers (AsyncCompletedEventArgs/DownloadProgressChangedEventArgs events), so Moq's RaiseAsync is not appropriate here and can fail to raise the events. Match the working Complete path and use mock.Raise(...), dropping the await.
                    mock.Raise(
                        x => x.DownloadProgressChanged += null,
                        mock.Object,
                        new DownloadProgressChangedEventArgs("Main")
                        {
                            TotalBytesToReceive = mock.Object.Package.TotalFileSize,
                            ReceivedBytesSize = mock.Object.Package.TotalFileSize / 2,
                            BytesPerSecondSpeed = 1024,
                        }
                    );

                    await stopped.Task.WaitAsync(cancellationToken);
                    // DownloadProgressChanged and DownloadFileCompleted are void event handlers (AsyncCompletedEventArgs/DownloadProgressChangedEventArgs events), so Moq's RaiseAsync is not appropriate here and can fail to raise the events. Match the working Complete path and use mock.Raise(...), dropping the await.
                    // ReSharper disable once MethodHasAsyncOverload
                    mock.Raise(
                        x => x.DownloadFileCompleted += null,
                        mock.Object,
                        new AsyncCompletedEventArgs(null, true, mock.Object.Package)
                    );
                }
            );

        return mock.Object;
    }

    private static Mock<IDownloadService> CreateBaseMock()
    {
        var package = new DownloadPackage
        {
            FileName = "mock-download.mp4",
            TotalFileSize = DOWNLOAD_FILE_SIZE_IN_BYTES,
            Urls = ["http://mock/mock-download.mp4"],
        };

        var mock = new Mock<IDownloadService>();
        mock.Setup(x => x.Clear()).Returns(Task.CompletedTask);
        mock.Setup(x => x.CancelTaskAsync()).Returns(Task.CompletedTask);
        mock.Setup(x => x.Package).Returns(package);
        return mock;
    }

    private static async Task Complete(Mock<IDownloadService> mock, IFile file, string targetPath)
    {
        // DirectPlexDownloadClient verifies the final, non-.reaptemp path after completion.
        var finalPath = targetPath.RemoveReapTempSuffix();
        await file.WriteAllBytesAsync(finalPath, FakeData.GetDownloadFile(DOWNLOAD_FILE_SIZE_IN_MIB));
        mock.Raise(
            x => x.DownloadProgressChanged += null,
            mock.Object,
            new DownloadProgressChangedEventArgs("Main")
            {
                TotalBytesToReceive = mock.Object.Package.TotalFileSize,
                ReceivedBytesSize = mock.Object.Package.TotalFileSize,
                BytesPerSecondSpeed = 1024,
            }
        );
        mock.Raise(
            x => x.DownloadFileCompleted += null,
            mock.Object,
            new AsyncCompletedEventArgs(null, false, mock.Object.Package)
        );
    }
}
