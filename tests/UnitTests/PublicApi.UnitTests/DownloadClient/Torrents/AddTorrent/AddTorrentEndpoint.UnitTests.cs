using System.Text.Json;
using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable RedundantAssignment

namespace Reaparr.PublicAPI.UnitTests;

public class AddTorrentEndpointUnitTests : BaseEndpointUnitTest<AddTorrentEndpoint, AddTorrentEndpointRequest>
{
    [Test]
    public async Task ShouldReturnOk_WhenValidTorrentFileIsUploaded()
    {
        // Arrange
        await SetupDatabase(1234, config =>
        {
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
            config.RadarrIntegrationCount = 1;
        });
        var dbContext = IDbContext;
        var integration = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var movieData = await dbContext.PlexMovieData.SingleAsync(CancellationToken);
        var movieFile = await dbContext.DownloadTaskMovieFile.AsTracking().SingleAsync(CancellationToken);
        movieFile.RadarrIntegrationId = integration.Id;
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiRatingKey), movieData.PlexApiRatingKey);
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiMediaId), movieData.PlexApiMediaId);
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiPartId), movieData.PlexApiPartId);
        await dbContext.SaveChangesAsync(CancellationToken);

        var validMetadata = CreateSourceMetadata(movieData, movieData.PlexMovieId);
        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(validMetadata, "test.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { Movies = 1 }));

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            request,
            integrationIdentity: integration.Id.ToRadarrIdentity()
        );

        endpointResult.StatusCode.ShouldBe(200);
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.ValidationErrors.Count.ShouldBe(0);
        endpointResult.Endpoint.HttpContext.Response.Body.Position = 0;
        using var responseReader = new StreamReader(endpointResult.Endpoint.HttpContext.Response.Body);
        (await responseReader.ReadToEndAsync(CancellationToken)).ShouldBe("Ok.");
        // Verify command was called with correct data
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x =>
                    x.Send(
                        It.Is<CreateDownloadTasksCommand>(cmd =>
                            cmd.Request.DownloadMedias.Count == 1
                            && cmd.Request.DownloadMedias[0].Type == validMetadata.Type
                            && cmd.Request.DownloadMedias[0].PlexServerId == validMetadata.ServerId
                            && cmd.Request.DownloadMedias[0].PlexLibraryId == validMetadata.LibraryId
                            && cmd.Request.DownloadMedias[0].MediaIds.Contains(validMetadata.MediaId)
                            && cmd.Request.DownloadMedias[0].Qualities.Count == 1
                            && cmd.Request.DownloadMedias[0].Qualities[0].MediaDataType == validMetadata.Type
                            && cmd.Request.DownloadMedias[0].Qualities[0].VideoQuality == validMetadata.VideoQuality
                            && cmd.Request.DownloadMedias[0].Qualities[0].MediaId == validMetadata.MediaId
                            && cmd.Request.DownloadMedias[0].Qualities[0].DataId == validMetadata.DataId
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            );

        // Verify IFormFile mock interactions
        torrentFileMock.Verify(f => f.OpenReadStream(), Times.Once);
        torrentFileMock.Verify(f => f.FileName, Times.AtLeastOnce);
    }

    [Test]
    public async Task ShouldRejectScalarValidTorrent_WhenDataAndPartIdsDiffer()
    {
        // Arrange
        var invalidMetadata = CreateValidTorrentMetadata() with { DataId = 2 };
        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(invalidMetadata, "mismatched.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.IsValid.ShouldBeFalse();
        endpointResult.ValidationErrors.Count.ShouldBe(1);
        endpointResult.StatusCode.ShouldBe(400);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        torrentFileMock.Verify(x => x.OpenReadStream(), Times.Once());
    }

    [Test]
    public async Task ShouldReturnValidationErrors_WhenTorrentMetadataIsInvalid()
    {
        // Arrange
        var invalidMetadata = new TorrentMetadataDTO
        {
            Type = PlexMediaType.Movie,
            MediaId = 0, // Invalid - should be > 0
            DataId = -1, // Invalid - should be > 0
            PartId = 0, // Invalid - should be > 0
            PlexApiPartId = 0, // Invalid - should be > 0
            VideoQuality = VideoQuality.HD,
            LibraryId = 0, // Invalid - should be > 0
            ServerId = 0, // Invalid - should be > 0
        };

        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(invalidMetadata, "invalid.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { Movies = 1 }));

        // Act
        var endpointResult = await TestEndpointHandleAsync(request);

        // Assert
        endpointResult.IsValid.ShouldBeFalse();
        endpointResult.ValidationErrors.ShouldNotBeEmpty();
        endpointResult.ValidationErrors.Count.ShouldBeGreaterThan(0);

        // Verify command was not called
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()), Times.Never);

        // Verify IFormFile mock interactions
        torrentFileMock.Verify(f => f.OpenReadStream(), Times.Once);
        torrentFileMock.Verify(f => f.FileName, Times.AtLeastOnce);
    }

    [Test]
    public async Task ShouldReturnRecognizedFailureAndPreserveHashes_WhenCreateDownloadTasksCommandFails()
    {
        // Arrange
        await SetupDatabase(1235, config =>
        {
            config.MovieCount = 1;
            config.RadarrIntegrationCount = 1;
            config.MovieDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var integration = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var movieData = await dbContext.PlexMovieData.SingleAsync(CancellationToken);
        var control = await dbContext.DownloadTaskMovieFile.AsTracking().SingleAsync(CancellationToken);
        control.RadarrIntegrationId = integration.Id;
        await dbContext.SaveChangesAsync(CancellationToken);
        var before = (control.Id, control.ParentId, control.HashId, control.DownloadStatus);
        var validMetadata = CreateSourceMetadata(movieData, movieData.PlexMovieId);
        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(validMetadata, "test.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };

        var failureResult = Result.Fail("Command execution failed");
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<CreateDownloadTasksCommand>(command =>
                        command.Request.Integration == integration.Id.ToRadarrIdentity()
                        && command.Request.DownloadMedias.Count == 1
                        && command.Request.DownloadMedias[0].MediaIds.SequenceEqual(new[] { validMetadata.MediaId })
                        && command.Request.DownloadMedias[0].PlexLibraryId == validMetadata.LibraryId
                        && command.Request.DownloadMedias[0].PlexServerId == validMetadata.ServerId
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(failureResult)
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            request,
            integrationIdentity: integration.Id.ToRadarrIdentity()
        );

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.ValidationErrors.Count.ShouldBe(0);
        endpointResult.Endpoint.HttpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(endpointResult.Endpoint.HttpContext.Response.Body);
        (await reader.ReadToEndAsync(CancellationToken)).ShouldBe("Fails.");
        var after = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        (after.Id, after.ParentId, after.HashId, after.DownloadStatus).ShouldBe(before);
        Mock.Mock<ICommandExecutor>().Verify();

        // Verify command was called once
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify IFormFile mock interactions
        torrentFileMock.Verify(f => f.OpenReadStream(), Times.Once);
        torrentFileMock.Verify(f => f.FileName, Times.AtLeastOnce);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldHandleTaskCreationCancellation_WhenCommandReturnsCancellation(bool cancelExecutionToken)
    {
        // Arrange
        await SetupDatabase(1235, config =>
        {
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
            config.RadarrIntegrationCount = 1;
        });
        var dbContext = IDbContext;
        var integration = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var movieData = await dbContext.PlexMovieData.SingleAsync(CancellationToken);
        var movieFile = await dbContext.DownloadTaskMovieFile.AsTracking().SingleAsync(CancellationToken);
        movieFile.RadarrIntegrationId = integration.Id;
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiRatingKey), movieData.PlexApiRatingKey);
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiMediaId), movieData.PlexApiMediaId);
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiPartId), movieData.PlexApiPartId);
        await dbContext.SaveChangesAsync(CancellationToken);
        var before = await dbContext
            .DownloadTaskMovieFile.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.HashId,
                x.DownloadStatus,
                x.RadarrIntegrationId,
            })
            .ToArrayAsync(CancellationToken);
        before.Length.ShouldBe(1);
        var metadata = CreateSourceMetadata(movieData, movieData.PlexMovieId);
        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(metadata, "task-creation-cancelled.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };
        await using var expectedHashStream = torrentFile.OpenReadStream();
        var expectedHashId = new BencodeParser().Parse<Torrent>(expectedHashStream).GetInfoHash();
        using var executionCancellation = new CancellationTokenSource();
        var executionCancellationToken = executionCancellation.Token;
        Action cancelExecution = executionCancellation.Cancel;
        var creationResult = Result.Fail<DownloadTaskCreationReport>(
            new ExceptionalError(new OperationCanceledException("Task creation was cancelled"))
        );
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<CreateDownloadTasksCommand>(command =>
                        command.Request.Integration == integration.Id.ToRadarrIdentity()
                        && command.Request.DownloadMedias.Count == 1
                        && command.Request.DownloadMedias[0].MediaIds.SequenceEqual(new[] { metadata.MediaId })
                        && command.Request.DownloadMedias[0].PlexLibraryId == metadata.LibraryId
                        && command.Request.DownloadMedias[0].PlexServerId == metadata.ServerId
                    ),
                    executionCancellationToken
                )
            )
            .Returns(() =>
            {
                if (cancelExecutionToken)
                    cancelExecution();

                return Task.FromResult(creationResult);
            })
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.CaptureErrors(
                    It.Is<ResultBase>(result =>
                        result.Errors.Count == 1
                        && result
                            .Errors.OfType<ExceptionalError>()
                            .Any(error => error.Exception is OperationCanceledException)
                    ),
                    TelemetryActivity.ExternalDownloadSubmission
                )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.ExternalDownloadSubmissionProcessed(
                    It.Is<IntegrationIdentity>(value =>
                        value.Id == integration.Id && value.Type == IntegrationType.Radarr
                    ),
                    ToTelemetryMetadata(metadata),
                    It.Is<string>(hash => hash == expectedHashId),
                    It.Is<Result<DownloadTaskCreationReport>>(result =>
                        result.IsFailed
                        && result.Errors.Count == 1
                        && result
                            .Errors.OfType<ExceptionalError>()
                            .Any(error => error.Exception is OperationCanceledException)
                    ),
                    null,
                    TelemetryStage.TaskCreation
                )
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            request,
            integrationIdentity: integration.Id.ToRadarrIdentity(),
            executionCancellationToken: executionCancellationToken
        );

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.ValidationErrors.Count.ShouldBe(0);
        if (cancelExecutionToken)
        {
            endpointResult.Endpoint.HttpContext.Response.Body.Length.ShouldBe(0);
        }
        else
        {
            endpointResult.Endpoint.HttpContext.Response.Body.Position = 0;
            using var reader = new StreamReader(endpointResult.Endpoint.HttpContext.Response.Body);
            (await reader.ReadToEndAsync(CancellationToken)).ShouldBe("Fails.");
        }

        var after = await dbContext
            .DownloadTaskMovieFile.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.HashId,
                x.DownloadStatus,
                x.RadarrIntegrationId,
            })
            .ToArrayAsync(CancellationToken);
        after.ShouldBe(before);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.ExternalDownloadSubmissionProcessed(
                        It.IsAny<IntegrationIdentity>(),
                        It.IsAny<TorrentMetadataTelemetryDTO>(),
                        It.IsAny<string>(),
                        It.IsAny<Result<DownloadTaskCreationReport>>(),
                        It.IsAny<Result<int>>(),
                        TelemetryStage.HashAssociation
                    ),
                Times.Never
            );
        torrentFileMock.Verify(x => x.OpenReadStream(), Times.Exactly(2));
    }

    [Test]
    public async Task ShouldReturnRecognizedFailure_WhenValidatedTorrentHasNoOwnedHashTarget()
    {
        // Arrange
        await SetupDatabase(1236, config =>
        {
            config.MovieCount = 1;
            config.RadarrIntegrationCount = 1;
        });
        var dbContext = IDbContext;
        var integration = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var movieData = await dbContext.PlexMovieData.SingleAsync(CancellationToken);
        var metadata = CreateSourceMetadata(movieData, movieData.PlexMovieId);
        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(metadata, "missing-target.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { Movies = 1 }))
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            request,
            integrationIdentity: integration.Id.ToRadarrIdentity()
        );

        // Assert
        endpointResult.StatusCode.ShouldBe(200);
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.ValidationErrors.Count.ShouldBe(0);
        endpointResult.Endpoint.HttpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(endpointResult.Endpoint.HttpContext.Response.Body);
        (await reader.ReadToEndAsync(CancellationToken)).ShouldBe("Fails.");
        (await dbContext.DownloadTaskMovieFile.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBeEmpty();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(
                x =>
                    x.ExternalDownloadSubmissionProcessed(
                        It.Is<IntegrationIdentity>(i => i.Id == integration.Id && i.Type == IntegrationType.Radarr),
                        ToTelemetryMetadata(metadata),
                        It.IsAny<string>(),
                        It.Is<Result<DownloadTaskCreationReport>>(result =>
                            result.IsSuccess && result.Value.Total == 1
                        ),
                        It.Is<Result<int>>(result => result.IsSuccess && result.Value == 0),
                        TelemetryStage.HashAssociation
                    ),
                Times.Once
            );
        torrentFileMock.Verify(x => x.OpenReadStream(), Times.Once());
    }

    [Test]
    public async Task ShouldReturnInternalServerErrorAndPreserveCommittedTask_WhenHashAssociationThrows()
    {
        // Arrange
        await SetupDatabase(1238, config =>
        {
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
            config.RadarrIntegrationCount = 1;
        });
        var dbContext = IDbContext;
        var integration = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var movieData = await dbContext.PlexMovieData.SingleAsync(CancellationToken);
        var movieFile = await dbContext.DownloadTaskMovieFile.AsTracking().SingleAsync(CancellationToken);
        movieFile.RadarrIntegrationId = integration.Id;
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiRatingKey), movieData.PlexApiRatingKey);
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiMediaId), movieData.PlexApiMediaId);
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiPartId), movieData.PlexApiPartId);
        await dbContext.SaveChangesAsync(CancellationToken);
        var before = await dbContext
            .DownloadTaskMovieFile.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.HashId,
                x.DownloadStatus,
                x.RadarrIntegrationId,
            })
            .ToArrayAsync(CancellationToken);
        before.Length.ShouldBe(1);
        var metadata = CreateSourceMetadata(movieData, movieData.PlexMovieId);
        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(metadata, "association-failure.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };
        await using var expectedHashStream = torrentFile.OpenReadStream();
        var expectedHashId = new BencodeParser().Parse<Torrent>(expectedHashStream).GetInfoHash();
        var endpointDbContext = IDbContext;
        var createResult = Result.Ok(new DownloadTaskCreationReport { Movies = 1 });
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<CreateDownloadTasksCommand>(command =>
                        command.Request.Integration == integration.Id.ToRadarrIdentity()
                        && command.Request.DownloadMedias.Count == 1
                        && command.Request.DownloadMedias[0].MediaIds.SequenceEqual(new[] { metadata.MediaId })
                        && command.Request.DownloadMedias[0].PlexLibraryId == metadata.LibraryId
                        && command.Request.DownloadMedias[0].PlexServerId == metadata.ServerId
                    ),
                    CancellationToken
                )
            )
            .Returns(() =>
            {
                endpointDbContext.Dispose();
                return Task.FromResult(createResult);
            })
            .Verifiable(Times.Once());
        Exception? reportedException = null;
        Exception? capturedException = null;
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.ExternalDownloadSubmissionProcessed(
                    It.Is<IntegrationIdentity>(value =>
                        value.Id == integration.Id && value.Type == IntegrationType.Radarr
                    ),
                    ToTelemetryMetadata(metadata),
                    It.Is<string>(hash => hash == expectedHashId),
                    It.Is<Result<DownloadTaskCreationReport>>(result =>
                        result.IsSuccess && result.Errors.Count == 0 && result.Value.Total == 1
                    ),
                    It.Is<Result<int>>(result =>
                        result.IsFailed
                        && result.Errors.Count == 1
                        && result.Errors.OfType<ExceptionalError>().Count() == 1
                    ),
                    TelemetryStage.HashAssociation
                )
            )
            .Callback<
                IntegrationIdentity,
                TorrentMetadataTelemetryDTO,
                string,
                Result<DownloadTaskCreationReport>?,
                Result<int>?,
                TelemetryStage
            >(
                (_, _, _, _, associationResult, _) =>
                {
                    reportedException = associationResult!.Errors.OfType<ExceptionalError>().Single().Exception;
                }
            )
            .Verifiable(Times.Once());
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.CaptureErrors(
                    It.Is<ResultBase>(result => result.IsFailed && result.Errors.Count == 1),
                    TelemetryActivity.ExternalDownloadSubmission
                )
            )
            .Callback<ResultBase, TelemetryActivity>(
                (result, _) =>
                {
                    capturedException = result.Errors.OfType<ExceptionalError>().Single().Exception;
                }
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            request,
            services => services.AddSingleton(endpointDbContext),
            integrationIdentity: integration.Id.ToRadarrIdentity()
        );

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.ValidationErrors.Count.ShouldBe(0);
        endpointResult.Endpoint.HttpContext.Response.Body.Position = 0;
        var response = (
            await JsonSerializer.DeserializeAsync<BaseResultDTO>(
                endpointResult.Endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        response.IsSuccess.ShouldBeFalse();
        response.Errors.Count.ShouldBe(1);
        reportedException.ShouldNotBeNull();
        reportedException.ShouldBeOfType<ObjectDisposedException>();
        capturedException.ShouldNotBeNull();
        ReferenceEquals(reportedException, capturedException).ShouldBeTrue();
        var after = await IDbContext
            .DownloadTaskMovieFile.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.HashId,
                x.DownloadStatus,
                x.RadarrIntegrationId,
            })
            .ToArrayAsync(CancellationToken);
        after.ShouldBe(before);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        torrentFileMock.Verify(x => x.OpenReadStream(), Times.Exactly(2));
    }

    [Test]
    public async Task ShouldReportCancellationAndPreserveCommittedTask_WhenHashAssociationIsCancelled()
    {
        // Arrange
        await SetupDatabase(1239, config =>
        {
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
            config.RadarrIntegrationCount = 1;
        });
        var dbContext = IDbContext;
        var integration = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var movieData = await dbContext.PlexMovieData.SingleAsync(CancellationToken);
        var movieFile = await dbContext.DownloadTaskMovieFile.AsTracking().SingleAsync(CancellationToken);
        movieFile.RadarrIntegrationId = integration.Id;
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiRatingKey), movieData.PlexApiRatingKey);
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiMediaId), movieData.PlexApiMediaId);
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiPartId), movieData.PlexApiPartId);
        await dbContext.SaveChangesAsync(CancellationToken);
        var before = await dbContext
            .DownloadTaskMovieFile.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.HashId,
                x.DownloadStatus,
                x.RadarrIntegrationId,
            })
            .ToArrayAsync(CancellationToken);
        before.Length.ShouldBe(1);
        var metadata = CreateSourceMetadata(movieData, movieData.PlexMovieId);
        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(metadata, "association-cancelled.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };
        await using var expectedHashStream = torrentFile.OpenReadStream();
        var expectedHashId = new BencodeParser().Parse<Torrent>(expectedHashStream).GetInfoHash();
        using var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        Action cancelAssociation = cancellation.Cancel;
        var endpointDbContext = IDbContext;
        var createResult = Result.Ok(new DownloadTaskCreationReport { Movies = 1 });
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<CreateDownloadTasksCommand>(command =>
                        command.Request.Integration == integration.Id.ToRadarrIdentity()
                        && command.Request.DownloadMedias.Count == 1
                        && command.Request.DownloadMedias[0].MediaIds.SequenceEqual(new[] { metadata.MediaId })
                        && command.Request.DownloadMedias[0].PlexLibraryId == metadata.LibraryId
                        && command.Request.DownloadMedias[0].PlexServerId == metadata.ServerId
                    ),
                    cancellationToken
                )
            )
            .Returns(() =>
            {
                cancelAssociation();
                return Task.FromResult(createResult);
            })
            .Verifiable(Times.Once());
        Exception? cancellationException = null;
        Mock.Mock<ITelemetry>()
            .Setup(x =>
                x.ExternalDownloadSubmissionProcessed(
                    It.Is<IntegrationIdentity>(value =>
                        value.Id == integration.Id && value.Type == IntegrationType.Radarr
                    ),
                    ToTelemetryMetadata(metadata),
                    It.Is<string>(hash => hash == expectedHashId),
                    It.Is<Result<DownloadTaskCreationReport>>(result =>
                        result.IsSuccess && result.Errors.Count == 0 && result.Value.Total == 1
                    ),
                    It.Is<Result<int>>(result =>
                        result.IsFailed
                        && result.Errors.Count == 1
                        && result.Errors.OfType<ExceptionalError>().Count() == 1
                        && result.Errors.OfType<ExceptionalError>().Single().Exception is OperationCanceledException
                    ),
                    TelemetryStage.HashAssociation
                )
            )
            .Callback<
                IntegrationIdentity,
                TorrentMetadataTelemetryDTO,
                string,
                Result<DownloadTaskCreationReport>?,
                Result<int>?,
                TelemetryStage
            >(
                (_, _, _, _, associationResult, _) =>
                {
                    cancellationException = associationResult!.Errors.OfType<ExceptionalError>().Single().Exception;
                }
            )
            .Verifiable(Times.Once());

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            request,
            services => services.AddSingleton(endpointDbContext),
            integration.Id.ToRadarrIdentity(),
            cancellationToken
        );

        // Assert
        endpointResult.StatusCode.ShouldBe(StatusCodes.Status200OK);
        endpointResult.IsValid.ShouldBeTrue();
        endpointResult.ValidationErrors.Count.ShouldBe(0);
        endpointResult.Endpoint.HttpContext.Response.Body.Length.ShouldBe(0);
        cancellationException.ShouldNotBeNull();
        (cancellationException is OperationCanceledException).ShouldBeTrue();
        var after = await IDbContext
            .DownloadTaskMovieFile.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.HashId,
                x.DownloadStatus,
                x.RadarrIntegrationId,
            })
            .ToArrayAsync(CancellationToken);
        after.ShouldBe(before);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ITelemetry>().Verify();
        Mock.Mock<ITelemetry>()
            .Verify(
                x => x.CaptureErrors(It.IsAny<ResultBase>(), TelemetryActivity.ExternalDownloadSubmission),
                Times.Never
            );
        torrentFileMock.Verify(x => x.OpenReadStream(), Times.Exactly(2));
    }

    [Test]
    public async Task ShouldRejectMixedMediaIdentityBeforeCreatingTasks_WhenTorrentIsUploaded()
    {
        // Arrange
        await SetupDatabase(1237, config =>
        {
            config.PlexMovieLibraryCount = 1;
            config.PlexTvShowLibraryCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 1;
            config.RadarrIntegrationCount = 1;
        });
        var dbContext = IDbContext;
        var integration = await dbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var movieData = await dbContext.PlexMovieData.SingleAsync(CancellationToken);
        var episodeData = await dbContext.PlexTvShowEpisodeData.SingleAsync(CancellationToken);
        var before = await dbContext
            .DownloadTaskMovieFile.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.HashId,
                x.RadarrIntegrationId,
            })
            .ToArrayAsync(CancellationToken);
        var forgedMetadata = CreateSourceMetadata(episodeData, movieData.PlexMovieId) with
        {
            VideoQuality = movieData.VideoQuality,
        };
        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(forgedMetadata, "mixed-identity.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            request,
            integrationIdentity: integration.Id.ToRadarrIdentity()
        );

        // Assert
        endpointResult.StatusCode.ShouldBe(400);
        endpointResult.IsValid.ShouldBeFalse();
        endpointResult.ValidationErrors.Count.ShouldBe(1);
        endpointResult.ValidationErrors.Single().PropertyName.ShouldBe(nameof(TorrentMetadataDTO.DataId));
        (
            await dbContext
                .DownloadTaskMovieFile.OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.ParentId,
                    x.HashId,
                    x.RadarrIntegrationId,
                })
                .ToArrayAsync(CancellationToken)
        ).ShouldBe(before);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        torrentFileMock.Verify(x => x.OpenReadStream(), Times.Once());
    }

    [Test]
    [Arguments(IntegrationType.Sonarr, PlexMediaType.MusicArtist)]
    [Arguments(IntegrationType.Radarr, PlexMediaType.MusicArtist)]
    [Arguments(IntegrationType.Sonarr, PlexMediaType.MusicAlbum)]
    [Arguments(IntegrationType.Radarr, PlexMediaType.MusicAlbum)]
    [Arguments(IntegrationType.Sonarr, PlexMediaType.MusicTrack)]
    [Arguments(IntegrationType.Radarr, PlexMediaType.MusicTrack)]
    [Arguments(IntegrationType.Sonarr, PlexMediaType.PhotoAlbum)]
    [Arguments(IntegrationType.Radarr, PlexMediaType.PhotoAlbum)]
    [Arguments(IntegrationType.Sonarr, PlexMediaType.PhotoImage)]
    [Arguments(IntegrationType.Radarr, PlexMediaType.PhotoImage)]
    [Arguments(IntegrationType.Sonarr, PlexMediaType.OtherVideos)]
    [Arguments(IntegrationType.Radarr, PlexMediaType.OtherVideos)]
    [Arguments(IntegrationType.Sonarr, PlexMediaType.Movie)]
    [Arguments(IntegrationType.Radarr, PlexMediaType.Episode)]
    public async Task ShouldRejectUnsupportedOrOppositeFamilyWithoutCreatingTasks_WhenIntegrationUploadsTorrent(
        IntegrationType integrationType,
        PlexMediaType mediaType
    )
    {
        // Arrange
        await SetupDatabase(88390, config =>
        {
            config.MovieDownloadTasksCount = 1;
            config.SonarrIntegrationCount = 1;
            config.RadarrIntegrationCount = 1;
        });
        var dbContext = IDbContext;
        var integrationId =
            integrationType == IntegrationType.Sonarr
                ? await dbContext.SonarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken)
                : await dbContext.RadarrIntegrations.Select(x => x.Id).SingleAsync(CancellationToken);
        var before = await dbContext
            .DownloadTaskMovieFile.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.HashId,
                x.DownloadStatus,
            })
            .ToArrayAsync(CancellationToken);
        before.Length.ShouldBe(1);
        var metadata = CreateValidTorrentMetadata(mediaType);
        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(metadata, "unsupported.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };

        // Act
        var response = await TestEndpointHandleAsync(
            request,
            integrationIdentity: new IntegrationIdentity(integrationType, integrationId)
        );

        // Assert
        if (mediaType is PlexMediaType.Movie or PlexMediaType.Episode)
            response.StatusCode.ShouldBe(403);
        else
        {
            response.IsValid.ShouldBeFalse();
            response.ValidationErrors.Count.ShouldBe(1);
            response.ValidationErrors.Single().PropertyName.ShouldBe("GeneralErrors");
            response.ValidationErrors.Single().ErrorMessage.ShouldBe("Type");
        }
        (
            await dbContext
                .DownloadTaskMovieFile.OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.ParentId,
                    x.HashId,
                    x.DownloadStatus,
                })
                .ToArrayAsync(CancellationToken)
        ).ShouldBe(before);
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()), Times.Never());
        torrentFileMock.Verify(x => x.OpenReadStream(), Times.Once());
    }

    [Test]
    public async Task ShouldSetHashIdOnMovieDownloadTask_WhenTorrentIsMovieType()
    {
        // Setup database with movie download tasks that match the metadata
        await SetupDatabase(1234, config =>
        {
            config.PlexServerCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
            config.RadarrIntegrationCount = 1;
        });
        var integration = await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken);

        using var movieContext = IDbContext;
        var movieData = await movieContext.PlexMovieData.SingleAsync(CancellationToken);
        var movieFile = await movieContext.DownloadTaskMovieFile.AsTracking().FirstAsync(CancellationToken);
        var movie = await movieContext.DownloadTaskMovie.AsTracking().FirstAsync(CancellationToken);
        movie.RadarrIntegrationId = integration.Id;
        movieFile.RadarrIntegrationId = integration.Id;
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiRatingKey), movieData.PlexApiRatingKey);
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiMediaId), movieData.PlexApiMediaId);
        movieFile.UpdateInitProperty(nameof(DownloadTaskMovieFile.PlexApiPartId), movieData.PlexApiPartId);
        await movieContext.SaveChangesAsync(CancellationToken);

        // Create metadata with the actual source row identity
        var actualMetadata = CreateSourceMetadata(movieData, movieData.PlexMovieId);

        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(actualMetadata, "movie.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };
        var movieMetadata = actualMetadata;

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { Movies = 1 }));

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            request,
            integrationIdentity: integration.Id.ToRadarrIdentity()
        );

        // Assert
        endpointResult.StatusCode.ShouldBe(200);

        // Verify command was called once
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify IFormFile mock interactions
        torrentFileMock.Verify(f => f.OpenReadStream(), Times.Once);
        torrentFileMock.Verify(f => f.FileName, Times.AtLeastOnce);

        // Verify that the HashId was set on the movie file
        var updatedMovieFile = await IDbContext.DownloadTaskMovieFile.FirstOrDefaultAsync(
            x =>
                x.PlexLibraryId == movieMetadata.LibraryId
                && x.PlexServerId == movieMetadata.ServerId
                && x.PlexApiPartId == movieMetadata.PlexApiPartId,
            CancellationToken
        );

        updatedMovieFile.ShouldNotBeNull();
        updatedMovieFile.HashId.ShouldNotBeNull();
        updatedMovieFile.HashId.ShouldNotBeEmpty();
    }

    [Test]
    public async Task ShouldSetHashIdOnEpisodeDownloadTask_WhenTorrentIsEpisodeType()
    {
        // Setup database with TV show download tasks that match the metadata
        await SetupDatabase(5678, config =>
        {
            config.TvShowCount = 1;
            config.TvShowSeasonCount = 1;
            config.TvShowEpisodeCount = 1;
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
            config.SonarrIntegrationCount = 1;
        });
        var integration = await IDbContext.SonarrIntegrations.SingleAsync(CancellationToken);

        using var episodeContext = IDbContext;
        var episodeData = await episodeContext.PlexTvShowEpisodeData.SingleAsync(CancellationToken);
        var episodeFile = await episodeContext.DownloadTaskTvShowEpisodeFile.AsTracking().FirstAsync(CancellationToken);
        var episode = await episodeContext.DownloadTaskTvShowEpisode.AsTracking().FirstAsync(CancellationToken);
        episode.SonarrIntegrationId = integration.Id;
        episodeFile.SonarrIntegrationId = integration.Id;
        episodeFile.UpdateInitProperty(
            nameof(DownloadTaskTvShowEpisodeFile.PlexApiRatingKey),
            episodeData.PlexApiRatingKey
        );
        episodeFile.UpdateInitProperty(
            nameof(DownloadTaskTvShowEpisodeFile.PlexApiMediaId),
            episodeData.PlexApiMediaId
        );
        episodeFile.UpdateInitProperty(nameof(DownloadTaskTvShowEpisodeFile.PlexApiPartId), episodeData.PlexApiPartId);
        await episodeContext.SaveChangesAsync(CancellationToken);

        // Create metadata with the actual source row identity
        var actualMetadata = CreateSourceMetadata(episodeData, episodeData.PlexTvShowEpisodeId, PlexMediaType.Episode);

        var (torrentFile, torrentFileMock) = CreateMockTorrentFile(actualMetadata, "episode.torrent");
        var request = new AddTorrentEndpointRequest { TorrentFile = torrentFile };
        var episodeMetadata = actualMetadata;

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { Episodes = 1 }));

        // Act
        var endpointResult = await TestEndpointHandleAsync(
            request,
            integrationIdentity: integration.Id.ToSonarrIdentity()
        );

        // Assert
        endpointResult.StatusCode.ShouldBe(200);

        // Verify command was called once
        Mock.Mock<ICommandExecutor>()
            .Verify(x => x.Send(It.IsAny<CreateDownloadTasksCommand>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify IFormFile mock interactions
        torrentFileMock.Verify(f => f.OpenReadStream(), Times.Once);
        torrentFileMock.Verify(f => f.FileName, Times.AtLeastOnce);

        // Verify that the HashId was set on the episode file
        var updatedEpisodeFile = await IDbContext.DownloadTaskTvShowEpisodeFile.FirstOrDefaultAsync(
            x =>
                x.PlexLibraryId == episodeMetadata.LibraryId
                && x.PlexServerId == episodeMetadata.ServerId
                && x.PlexApiPartId == episodeMetadata.PlexApiPartId,
            CancellationToken
        );

        updatedEpisodeFile.ShouldNotBeNull();
        updatedEpisodeFile.HashId.ShouldNotBeNull();
        updatedEpisodeFile.HashId.ShouldNotBeEmpty();
    }

    private static TorrentMetadataDTO CreateSourceMetadata(
        BasePlexMediaData source,
        int mediaId,
        PlexMediaType type = PlexMediaType.Movie
    ) =>
        new()
        {
            Type = type,
            MediaId = mediaId,
            DataId = source.Id,
            PartId = source.Id,
            PlexApiPartId = source.PlexApiPartId,
            VideoQuality = source.VideoQuality,
            LibraryId = source.PlexLibraryId,
            ServerId = source.PlexServerId,
        };

    private static TorrentMetadataTelemetryDTO ToTelemetryMetadata(TorrentMetadataDTO metadata) =>
        new()
        {
            Type = metadata.Type,
            MediaId = metadata.MediaId,
            DataId = metadata.DataId,
            PartId = metadata.PartId,
            PlexApiPartId = metadata.PlexApiPartId,
            VideoQuality = metadata.VideoQuality,
            LibraryId = metadata.LibraryId,
            ServerId = metadata.ServerId,
        };

    private static TorrentMetadataDTO CreateValidTorrentMetadata(PlexMediaType type = PlexMediaType.Movie) =>
        new()
        {
            Type = type,
            MediaId = 1,
            DataId = 1,
            PartId = 1,
            PlexApiPartId = 12345,
            VideoQuality = VideoQuality.HD,
            LibraryId = 1,
            ServerId = 1,
        };

    private static (IFormFile file, Mock<IFormFile> mock) CreateMockTorrentFile(
        TorrentMetadataDTO metadata,
        string fileName
    )
    {
        // Create a proper torrent using BencodeNET.Torrents.Torrent class like in DownloadTorrentEndpoint
        var extraFields = new BDictionary
        {
            ["created by"] = new BString("Reaparr"),
            ["comment"] = new BString("Test torrent for unit tests"),

            // Add metadata as extra fields
            [nameof(TorrentMetadataDTO.Type)] = new BString(metadata.Type.ToString()),
            [nameof(TorrentMetadataDTO.MediaId)] = new BString(metadata.MediaId.ToString()),
            [nameof(TorrentMetadataDTO.DataId)] = new BString(metadata.DataId.ToString()),
            [nameof(TorrentMetadataDTO.PartId)] = new BString(metadata.PartId.ToString()),
            [nameof(TorrentMetadataDTO.PlexApiPartId)] = new BString(metadata.PlexApiPartId.ToString()),
            [nameof(TorrentMetadataDTO.VideoQuality)] = new BString(metadata.VideoQuality.ToString()),
            [nameof(TorrentMetadataDTO.LibraryId)] = new BString(metadata.LibraryId.ToString()),
            [nameof(TorrentMetadataDTO.ServerId)] = new BString(metadata.ServerId.ToString()),
        };

        var torrent = new Torrent
        {
            CreationDate = DateTime.UtcNow,
            Pieces = new byte[20], // Single piece hash (20 bytes SHA1)
            PieceSize = 32768,
            Trackers =
            [
                ["http://tracker.example.com/announce"],
            ],
            File = new SingleFileInfo { FileName = "test.mkv", FileSize = 1000000 },
            ExtraFields = extraFields,
        };

        var torrentBytes = torrent.EncodeAsBytes();

        var formFileMock = new Mock<IFormFile>();
        formFileMock.Setup(f => f.FileName).Returns(fileName);
        formFileMock.Setup(f => f.Length).Returns(torrentBytes.Length);
        formFileMock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(torrentBytes));
        formFileMock.Setup(f => f.ContentType).Returns("application/x-bittorrent");

        return (formFileMock.Object, formFileMock);
    }
}
