namespace Reaparr.Application.UnitTests;

public class MoveFileWithResumeCommandHandlerUnitTests : BaseUnitTest<MoveFileWithResumeCommandHandler>
{
    private static byte[] CreateBytes(int length)
    {
        var data = new byte[length];
        for (var i = 0; i < length; i++)
            data[i] = (byte)('A' + (i % 26));
        return data;
    }

    [Test]
    public async Task ExecuteAsync_CopiesSmallFile_SuccessAndContentMatches()
    {
        var sourcePath = "/test/source.bin";
        var targetPath = "/test/target.bin";
        var content = CreateBytes(256 * 1024);

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(content));
            fs.AddFile(targetPath, new MockFileData([]));
        });

        MoveFileTransferProgressDTO? lastProgress = null;
        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = 0,
            DataTotal = content.LongLength,
            Progress = p => lastProgress = p,
        };

        var result = await Sut.ExecuteAsync(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var file = Mock.Container.Resolve<IFile>();
        await using var targetStream = file.Open(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var readBack = new byte[targetStream.Length];
        _ = await targetStream.ReadAsync(readBack, 0, readBack.Length, CancellationToken.None);

        readBack.ShouldBe(content);
        lastProgress.ShouldNotBeNull();
        lastProgress!.Transferred.ShouldBe(content.LongLength);
        lastProgress.DataTotal.ShouldBe(content.LongLength);
        lastProgress.FileTransferSpeed.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task ExecuteAsync_ReportsProgressMultipleTimes_ForLargeFile()
    {
        var sourcePath = "/test/source-large.bin";
        var targetPath = "/test/target-large.bin";
        var content = CreateBytes(2_500_000); // ~2.5 MB -> 3 iterations with 1MB buffer

        var progressCalls = 0;

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(content));
            fs.AddFile(targetPath, new MockFileData([]));
        });

        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = 0,
            DataTotal = content.LongLength,
            Progress = _ => progressCalls++,
        };

        var result = await Sut.ExecuteAsync(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        progressCalls.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task ExecuteAsync_ResumeFromOffset_WritesRemainderOnly()
    {
        var sourcePath = "/test/source-resume.bin";
        var targetPath = "/test/target-resume.bin";
        var content = CreateBytes(1_500_000); // 1.5 MB
        var offset = 1_000_000; // 1 MB

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(content));

            // Pre-populate target with first offset bytes
            fs.AddFile(targetPath, new MockFileData(content.Take(offset).ToArray()));
        });

        MoveFileTransferProgressDTO? last = null;
        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = offset,
            DataTotal = content.LongLength,
            Progress = p => last = p,
        };

        var result = await Sut.ExecuteAsync(command, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();

        var file = Mock.Container.Resolve<IFile>();
        await using var targetStream = file.Open(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var readBack = new byte[targetStream.Length];
        _ = await targetStream.ReadAsync(readBack, 0, readBack.Length, CancellationToken.None);
        readBack.ShouldBe(content);

        last.ShouldNotBeNull();
        last!.Transferred.ShouldBe(content.LongLength);
        last.DataTotal.ShouldBe(content.LongLength);
    }

    [Test]
    public async Task ExecuteAsync_CancellationStopsFurtherWrites_WhenCancelledBeforeStart()
    {
        var sourcePath = "/test/source-cancel.bin";
        var targetPath = "/test/target-cancel.bin";
        var content = CreateBytes(2_500_000); // multiple iterations

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(content));
            fs.AddFile(targetPath, new MockFileData([]));
        });

        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = 0,
            DataTotal = content.LongLength,
            Progress = _ => { },
        };

        var result = await Sut.ExecuteAsync(command, cts.Token);

        result.IsCancelled.ShouldBeTrue();

        var file = Mock.Container.Resolve<IFile>();
        await using var targetStream = file.Open(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Cancellation was requested before the first read, so no bytes should be written.
        targetStream.Length.ShouldBe(0);
    }

    [Test]
    public async Task ExecuteAsync_CancellationRequestedInsideProgress_StopsAfterNextCheck()
    {
        var sourcePath = "/test/source-cancel2.bin";
        var targetPath = "/test/target-cancel2.bin";
        var content = CreateBytes(2_500_000);

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(content));
            fs.AddFile(targetPath, new MockFileData([]));
        });

        var cts = new CancellationTokenSource();

        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = 0,
            DataTotal = content.LongLength,
            Progress = p =>
            {
                if (p.Transferred >= 1_048_576)
                    cts.Cancel();
            },
        };

        var result = await Sut.ExecuteAsync(command, cts.Token);

        result.IsCancelled.ShouldBeTrue();

        var file = Mock.Container.Resolve<IFile>();
        await using var targetStream = file.Open(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        targetStream.Length.ShouldBeLessThan(content.LongLength);
        targetStream.Length.ShouldBeGreaterThanOrEqualTo(1_048_576);
    }

    [Test]
    public async Task ExecuteAsync_OpenTargetFails_ReturnsFailed()
    {
        var sourcePath = "/test/source-openfail.bin";
        var targetPath = "/test/target-openfail.bin";
        var content = CreateBytes(1024);

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(content));
            fs.AddDirectory(targetPath);
        });

        MoveFileTransferProgressDTO? last = null;
        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = 0,
            DataTotal = content.LongLength,
            Progress = p => last = p,
        };

        var result = await Sut.ExecuteAsync(command, CancellationToken.None);
        result.IsFailed.ShouldBeTrue();
        last.ShouldBeNull();
    }

    [Test]
    public async Task ExecuteAsync_OpenSourceFails_ReturnsFailed()
    {
        var sourcePath = "/test/source-openfail2.bin";
        var targetPath = "/test/target-openfail2.bin";

        // Use MockFileSystem: ensure directory exists; do NOT create the source file.
        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(targetPath, new MockFileData([]));
        });

        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = 0,
            DataTotal = 100,
            Progress = _ => { },
        };

        var result = await Sut.ExecuteAsync(command, CancellationToken.None);
        result.IsFailed.ShouldBeTrue();
    }

    [Test]
    public async Task ExecuteAsync_CurrentOffsetEqualsTotal_NoWritesAndOk()
    {
        var sourcePath = "/test/source-offset-eq.bin";
        var targetPath = "/test/target-offset-eq.bin";
        var content = CreateBytes(128 * 1024);

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(content));
            fs.AddFile(targetPath, new MockFileData(content));
        });

        var progressCalled = false;
        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = content.LongLength,
            DataTotal = content.LongLength,
            Progress = _ => progressCalled = true,
        };

        var result = await Sut.ExecuteAsync(command, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        progressCalled.ShouldBeFalse();

        var file = Mock.Container.Resolve<IFile>();
        await using var targetStream = file.Open(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        targetStream.Length.ShouldBe(content.LongLength);
    }

    [Test]
    public async Task ExecuteAsync_CurrentOffsetGreaterThanTotal_ReturnsFailedAndKeepsSourceFile()
    {
        // Arrange
        var sourcePath = "/test/source-offset-gt.bin";
        var targetPath = "/test/target-offset-gt.bin";
        var content = CreateBytes(64 * 1024);

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(content));
            fs.AddFile(targetPath, new MockFileData(content));
        });

        var progressCalled = false;
        var expectedBytes = content.LongLength;
        var transferredBytes = content.LongLength + 10;
        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = transferredBytes,
            DataTotal = expectedBytes,
            Progress = _ => progressCalled = true,
        };

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        var resultMessage = result.ToString();
        resultMessage.ShouldContain("Move ended with a byte count mismatch");
        resultMessage.ShouldContain(expectedBytes.ToString());
        resultMessage.ShouldContain(transferredBytes.ToString());
        resultMessage.ShouldContain(sourcePath);
        resultMessage.ShouldContain(targetPath);
        progressCalled.ShouldBeFalse();

        var file = Mock.Container.Resolve<IFile>();
        file.Exists(sourcePath).ShouldBeTrue();
    }

    [Test]
    public async Task ExecuteAsync_ProgressSpeedIsNonNegative()
    {
        var sourcePath = "/test/source-speed.bin";
        var targetPath = "/test/target-speed.bin";
        var content = CreateBytes(600 * 1024);

        var speeds = new List<long>();

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(content));
            fs.AddFile(targetPath, new MockFileData([]));
        });

        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = 0,
            DataTotal = content.LongLength,
            Progress = p => speeds.Add(p.FileTransferSpeed),
        };

        var result = await Sut.ExecuteAsync(command, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        speeds.Count.ShouldBeGreaterThan(0);
        speeds.ShouldAllBe(x => x >= 0);
    }

    [Test]
    public async Task ExecuteAsync_ProgressDataTotalMatchesCommand()
    {
        var sourcePath = "/test/source-datatotal.bin";
        var targetPath = "/test/target-datatotal.bin";
        var content = CreateBytes(123_456);

        var seenTotals = new HashSet<long>();

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(content));
            fs.AddFile(targetPath, new MockFileData([]));
        });

        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = 0,
            DataTotal = content.LongLength,
            Progress = p => seenTotals.Add(p.DataTotal),
        };

        var result = await Sut.ExecuteAsync(command, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        seenTotals.ShouldContain(content.LongLength);
    }

    [Test]
    public async Task ExecuteAsync_Success_DeletesSourceFileAfterCompletion()
    {
        var sourcePath = "/test/source-delete-success.bin";
        var targetPath = "/test/target-delete-success.bin";
        var content = CreateBytes(256 * 1024);

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(content));
            fs.AddFile(targetPath, new MockFileData([]));
        });

        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = 0,
            DataTotal = content.LongLength,
            Progress = _ => { },
        };

        var result = await Sut.ExecuteAsync(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        var file = Mock.Container.Resolve<IFile>();
        file.Exists(sourcePath).ShouldBeFalse();
    }

    [Test]
    public async Task ExecuteAsync_Cancelled_DoesNotDeleteSourceFile()
    {
        var sourcePath = "/test/source-delete-cancel.bin";
        var targetPath = "/test/target-delete-cancel.bin";
        var content = CreateBytes(2_500_000);

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(content));
            fs.AddFile(targetPath, new MockFileData([]));
        });

        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = 0,
            DataTotal = content.LongLength,
            Progress = _ => { },
        };

        var result = await Sut.ExecuteAsync(command, cts.Token);

        result.IsCancelled.ShouldBeTrue();

        var file = Mock.Container.Resolve<IFile>();
        file.Exists(sourcePath).ShouldBeTrue();
    }

    [Test]
    public async Task ExecuteAsync_DeleteSourceFails_ReturnsFailed()
    {
        var sourcePath = "/test/source-delete-fail.bin";
        var targetPath = "/test/target-delete-fail.bin";

        var sourceContent = CreateBytes(1024);

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(sourceContent));
            fs.AddFile(targetPath, new MockFileData([]));
            fs.File.SetAttributes(sourcePath, FileAttributes.ReadOnly);
        });

        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = 0,
            DataTotal = sourceContent.LongLength,
            Progress = _ => { },
        };

        var result = await Sut.ExecuteAsync(command, CancellationToken.None);

        result.IsFailed.ShouldBeTrue();
    }

    [Test]
    public async Task ExecuteAsync_SourceShorterThanExpected_ReturnsFailedAndKeepsSourceFile()
    {
        // Arrange
        var sourcePath = "/test/source-incomplete.bin";
        var targetPath = "/test/target-incomplete.bin";
        var content = CreateBytes(256 * 1024);

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(content));
            fs.AddFile(targetPath, new MockFileData([]));
        });

        var expectedBytes = content.LongLength + 1;
        var transferredBytes = content.LongLength;
        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = 0,
            DataTotal = expectedBytes,
            Progress = _ => { },
        };

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        var resultMessage = result.ToString();
        resultMessage.ShouldContain("Move ended with a byte count mismatch");
        resultMessage.ShouldContain(expectedBytes.ToString());
        resultMessage.ShouldContain(transferredBytes.ToString());
        resultMessage.ShouldContain(sourcePath);
        resultMessage.ShouldContain(targetPath);

        var file = Mock.Container.Resolve<IFile>();
        file.Exists(sourcePath).ShouldBeTrue();
    }

    [Test]
    public async Task ExecuteAsync_SourceLongerThanExpected_ReturnsFailedAndKeepsSourceFile()
    {
        // Arrange
        var sourcePath = "/test/source-over-transfer.bin";
        var targetPath = "/test/target-over-transfer.bin";
        var content = CreateBytes(256 * 1024);

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(content));
            fs.AddFile(targetPath, new MockFileData([]));
        });

        var expectedBytes = content.LongLength - 1;
        var transferredBytes = content.LongLength;
        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = 0,
            DataTotal = expectedBytes,
            Progress = _ => { },
        };

        // Act
        var result = await Sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.ShouldNotBeEmpty();
        var resultMessage = result.ToString();
        resultMessage.ShouldContain("Move ended with a byte count mismatch");
        resultMessage.ShouldContain(expectedBytes.ToString());
        resultMessage.ShouldContain(transferredBytes.ToString());
        resultMessage.ShouldContain(sourcePath);
        resultMessage.ShouldContain(targetPath);

        var file = Mock.Container.Resolve<IFile>();
        file.Exists(sourcePath).ShouldBeTrue();
    }

    [Test]
    public async Task ShouldTruncateStaleDestinationContent_WhenFreshStart()
    {
        // Verifies that a restart (currentOffset = 0) with a pre-existing destination from a previous
        // run is fully replaced by the new source, leaving no stale bytes beyond the new content.
        var sourcePath = "/test/source-truncate.bin";
        var targetPath = "/test/target-truncate.bin";

        var newContent = CreateBytes(100 * 1024); // 100 KB new download
        var staleContent = CreateBytes(200 * 1024); // 200 KB stale destination from previous run

        SetupFileSystem(fs =>
        {
            fs.AddDirectory("/test");
            fs.AddFile(sourcePath, new MockFileData(newContent));
            fs.AddFile(targetPath, new MockFileData(staleContent));
        });

        var command = new MoveFileWithResumeCommand
        {
            SourcePath = sourcePath,
            TargetPath = targetPath,
            CurrentOffset = 0,
            DataTotal = newContent.LongLength,
            Progress = _ => { },
        };

        var result = await Sut.ExecuteAsync(command, CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();

        var file = Mock.Container.Resolve<IFile>();
        await using var targetStream = file.Open(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var readBack = new byte[targetStream.Length];
        _ = await targetStream.ReadAsync(readBack, 0, readBack.Length, CancellationToken.None);

        // Destination must be exactly the new content — no leftover stale bytes.
        readBack.Length.ShouldBe(newContent.Length);
        readBack.ShouldBe(newContent);
    }
}
