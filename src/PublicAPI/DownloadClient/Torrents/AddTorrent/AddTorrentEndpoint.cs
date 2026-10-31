using BencodeNET.Exceptions;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using Reaparr.Application.Contracts;

namespace Reaparr.PublicAPI;

public record AddTorrentEndpointRequest
{
    [FormField, BindFrom("urls")]
    public string? Urls { get; init; }

    [FormField, BindFrom("category")]
    public string? Category { get; init; }

    [FormField, BindFrom("paused")]
    public bool? Paused { get; init; }

    /// <summary>
    /// Single torrent file (binary upload).
    /// </summary>
    [FormField, BindFrom("torrents")]
    public IFormFile? TorrentFile { get; init; }
}

public class AddTorrentEndpointRequestValidator : Validator<AddTorrentEndpointRequest>
{
    public AddTorrentEndpointRequestValidator()
    {
        RuleFor(x => x.TorrentFile).NotNull().NotEmpty().WithMessage("A Reaparr torrent file must be provided.");
    }
}

public class TorrentMetadataDTOValidator : Validator<TorrentMetadataDTO>
{
    public TorrentMetadataDTOValidator()
    {
        RuleFor(x => x.LibraryId).GreaterThan(0).WithMessage("LibraryId must be greater than 0.");

        RuleFor(x => x.ServerId).GreaterThan(0).WithMessage("ServerId must be greater than 0.");

        RuleFor(x => x.MediaId).GreaterThan(0).WithMessage("MediaId must be greater than 0.");

        RuleFor(x => x.DataId).GreaterThan(0).WithMessage("DataId must be greater than 0.");

        RuleFor(x => x.PartId).GreaterThan(0).WithMessage("PartId must be greater than 0.");

        RuleFor(x => x.PlexApiPartId)
            .GreaterThan(0)
            .WithMessage($"{nameof(TorrentMetadataDTO.PlexApiPartId)} must be greater than 0.");

        RuleFor(x => x.Type)
            .Must(type => type is PlexMediaType.Episode or PlexMediaType.Movie)
            .WithMessage("Type must be Episode or Movie.");

        RuleFor(x => x.VideoQuality).IsInEnum().WithMessage("Quality must be a valid VideoQuality value.");
        RuleFor(x => x)
            .Must(r => r.DataId == r.PartId)
            .WithMessage($"{nameof(TorrentMetadataDTO.DataId)} and {nameof(TorrentMetadataDTO.PartId)} must match.");
    }
}

public class AddTorrentEndpoint : Endpoint<AddTorrentEndpointRequest>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILogger _log;

    public override void Configure()
    {
        Post(PublicApiRoutes.DownloadClient + "/torrents/add");
        Description(x => x.IsDownloadClient());
        AllowFileUploads();
        AllowAnonymous();
        PreProcessor<DownloadClientAuthenticationPreProcessor<AddTorrentEndpointRequest>>();
    }

    public AddTorrentEndpoint(ILogger logger, IReaparrDbContext dbContext, ICommandExecutor commandExecutor)
    {
        _log = logger.ForContext<AddTorrentEndpoint>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public override async Task HandleAsync(AddTorrentEndpointRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);

        var parser = new BencodeParser();
        Torrent torrent;
        TorrentMetadataDTO metadata;
        string hashId;
        try
        {
            torrent = parser.Parse<Torrent>(req.TorrentFile!.OpenReadStream());
            metadata = torrent.ExtraFields.ToTorrentMetadataDTO();
            hashId = torrent.GetInfoHash();
        }
        catch (FormatException ex)
        {
            _log.Here()
                .Warning(
                    "[Torrent/Add] Invalid torrent file format for {TorrentFileName}: {Error}",
                    req.TorrentFile?.FileName,
                    ex.Message
                );
            AddError("torrents", "Invalid torrent file.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }
        catch (BencodeException ex)
        {
            _log.Here()
                .Warning(
                    "[Torrent/Add] Invalid torrent file bencode for {TorrentFileName}: {Error}",
                    req.TorrentFile?.FileName,
                    ex.Message
                );
            AddError("torrents", "Invalid torrent file.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }
        catch (Exception ex)
        {
            _log.Here()
                .Warning(
                    "[Torrent/Add] Failed to parse torrent file for {TorrentFileName}: {Error}",
                    req.TorrentFile?.FileName,
                    ex.Message
                );
            AddError("torrents", "Invalid torrent file.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        // Ensure this is a valid Reaparr torrent file
        var validationResult = await new TorrentMetadataDTOValidator().ValidateAsync(metadata, ct);
        if (!validationResult.IsValid)
        {
            _log.Here()
                .Error(
                    "[Torrent/Add] Invalid torrent metadata for file {TorrentFileName}, Errors: {Errors}",
                    req.TorrentFile.FileName,
                    validationResult.Errors
                );

            foreach (var error in validationResult.Errors)
                AddError(error.PropertyName, error.ErrorMessage);

            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var identity = HttpContext.GetIntegrationIdentity();
        if (!identity.Supports(metadata.Type))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var source = await GetValidatedMediaSourceAsync(metadata, ct);
        if (source is null)
        {
            await RespondToMissingSourceAsync(metadata, ct);
            return;
        }

        _log.Here()
            .Debug(
                "[Torrent/Add] Uploaded torrent file: {TorrentFileName}, {Size} bytes, MetaData={MetaData}",
                req.TorrentFile.FileName,
                torrent.File.FileSize,
                metadata
            );

        var torrentDetails = (Metadata: metadata, HashId: hashId, FileName: req.TorrentFile.FileName);
        await CreateAndTrackDownloadAsync(torrentDetails, source, identity, ct);
    }

    private async Task RespondToMissingSourceAsync(TorrentMetadataDTO metadata, CancellationToken ct)
    {
        if (await HasStoredMediaIdentityAsync(metadata))
        {
            _log.Here()
                .Warning(
                    "[Torrent/Add] Torrent metadata does not match a stored {MediaType} source: {MetaData}",
                    metadata.Type,
                    metadata
                );
            AddError(
                new FluentValidation.Results.ValidationFailure(
                    nameof(TorrentMetadataDTO.DataId),
                    "DataId, PartId, MediaId, PlexApiPartId, LibraryId, and ServerId must identify the same media source."
                )
            );
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        _log.Here().Warning("[Torrent/Add] Torrent source no longer exists: {MetaData}", metadata);
        await Send.StringAsync("Fails.", cancellation: ct);
    }

    private async Task CreateAndTrackDownloadAsync(
        (TorrentMetadataDTO Metadata, string HashId, string FileName) torrent,
        MediaSourceIdentity source,
        IntegrationIdentity identity,
        CancellationToken ct
    )
    {
        var metadata = torrent.Metadata;
        List<DownloadMediaDTO> list =
        [
            new()
            {
                Qualities =
                [
                    new PlexMediaQualityDTO
                    {
                        MediaDataType = metadata.Type,
                        MediaId = metadata.MediaId,
                        DataId = metadata.DataId,
                        VideoQuality = metadata.VideoQuality,
                    },
                ],
                MediaIds = [metadata.MediaId],
                Type = metadata.Type,
                SelectionKind = null,
                PlexServerId = metadata.ServerId,
                PlexLibraryId = metadata.LibraryId,
                KeepCompletedInDownloadFolder = true,
            },
        ];

        var createResult = await _commandExecutor.Send(
            new CreateDownloadTasksCommand(new CreateDownloadTasksRequest(list, integration: identity)),
            ct
        );
        if (createResult.IsFailed)
        {
            _log.Here()
                .Error(
                    "[Torrent/Add] Failed to create download tasks for torrent {TorrentFileName}, Error: {Error}",
                    torrent.FileName,
                    createResult.Errors
                );
            await Send.StringAsync("Fails.", cancellation: ct);
            return;
        }

        // Set the hashId on the created download tasks so Sonarr/Radarr can keep track
        if (!await SetHashIdOnDownloadTask((Type: metadata.Type, Source: source), torrent.HashId, identity, ct))
        {
            await Send.StringAsync("Fails.", cancellation: ct);
            return;
        }

        await Send.StringAsync("Ok.", cancellation: ct);
    }

    private Task<MediaSourceIdentity?> GetValidatedMediaSourceAsync(
        TorrentMetadataDTO metadata,
        CancellationToken ct
    ) =>
        metadata.Type switch
        {
            PlexMediaType.Episode => GetValidatedMediaSourceAsync(
                _dbContext.PlexTvShowEpisodeData.Where(p =>
                    p.PlexTvShowEpisodeId == metadata.MediaId
                    && p.PlexTvShowEpisode!.PlexLibraryId == metadata.LibraryId
                    && p.PlexTvShowEpisode.PlexServerId == metadata.ServerId
                ),
                metadata,
                ct
            ),
            PlexMediaType.Movie => GetValidatedMediaSourceAsync(
                _dbContext.PlexMovieData.Where(p =>
                    p.PlexMovieId == metadata.MediaId
                    && p.PlexMovie!.PlexLibraryId == metadata.LibraryId
                    && p.PlexMovie.PlexServerId == metadata.ServerId
                ),
                metadata,
                ct
            ),
            _ => Task.FromResult<MediaSourceIdentity?>(null),
        };

    private async Task<bool> HasStoredMediaIdentityAsync(TorrentMetadataDTO metadata)
    {
        if (metadata.Type == PlexMediaType.Movie)
        {
            return await _dbContext.PlexMovieData.AnyAsync(p => p.Id == metadata.DataId)
                || await _dbContext.PlexMovies.AnyAsync(p => p.Id == metadata.MediaId);
        }

        return await _dbContext.PlexTvShowEpisodeData.AnyAsync(p => p.Id == metadata.DataId)
            || await _dbContext.PlexTvShowEpisodes.AnyAsync(p => p.Id == metadata.MediaId);
    }

    private static Task<MediaSourceIdentity?> GetValidatedMediaSourceAsync<T>(
        IQueryable<T> parts,
        TorrentMetadataDTO metadata,
        CancellationToken ct
    )
        where T : BasePlexMediaData =>
        parts
            .Where(p =>
                p.Id == metadata.DataId
                && p.PlexApiPartId == metadata.PlexApiPartId
                && p.PlexLibraryId == metadata.LibraryId
                && p.PlexServerId == metadata.ServerId
            )
            .Select(p => new MediaSourceIdentity
            {
                PlexApiRatingKey = p.PlexApiRatingKey,
                PlexApiMediaId = p.PlexApiMediaId,
                PlexApiPartId = p.PlexApiPartId,
                PlexLibraryId = p.PlexLibraryId,
                PlexServerId = p.PlexServerId,
            })
            .SingleOrDefaultAsync(ct);

    private async Task<bool> SetHashIdOnDownloadTask(
        (PlexMediaType Type, MediaSourceIdentity Source) validatedSource,
        string hashId,
        IntegrationIdentity identity,
        CancellationToken ct
    )
    {
        var count = validatedSource.Type switch
        {
            PlexMediaType.Episode => await SetHashIdAsync(
                _dbContext.DownloadTaskTvShowEpisodeFile.WhereIntegrationOwnershipMatches(identity),
                validatedSource.Source,
                hashId,
                ct
            ),
            PlexMediaType.Movie => await SetHashIdAsync(
                _dbContext.DownloadTaskMovieFile.WhereIntegrationOwnershipMatches(identity),
                validatedSource.Source,
                hashId,
                ct
            ),
            _ => 0,
        };

        if (count == 0)
        {
            _log.Here()
                .Warning(
                    "Could not find any DownloadTask to set HashId for torrent source: {Source}",
                    validatedSource.Source
                );
            return false;
        }

        _log.Here()
            .Debug("Set HashId on {Count} DownloadTasks for torrent source: {Source}", count, validatedSource.Source);
        return true;
    }

    private static Task<int> SetHashIdAsync<T>(
        IQueryable<T> files,
        MediaSourceIdentity source,
        string hashId,
        CancellationToken ct
    )
        where T : DownloadTaskFileBase =>
        files
            .Where(x =>
                x.PlexLibraryId == source.PlexLibraryId
                && x.PlexServerId == source.PlexServerId
                && x.PlexApiRatingKey == source.PlexApiRatingKey
                && x.PlexApiMediaId == source.PlexApiMediaId
                && x.PlexApiPartId == source.PlexApiPartId
            )
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.HashId, hashId), ct);

    private record MediaSourceIdentity
    {
        public required int PlexApiRatingKey { get; init; }

        public required int PlexApiMediaId { get; init; }

        public required int PlexApiPartId { get; init; }

        public required int PlexLibraryId { get; init; }

        public required int PlexServerId { get; init; }
    }
}
