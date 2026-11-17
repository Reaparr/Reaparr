using Flurl;
using LukeHagar.PlexAPI.SDK.Models.Components;

namespace Reaparr.PlexApi;

public class GetDashTranscodeDecisionCommandValidator : AbstractValidator<GetDashTranscodeDecisionCommand>
{
    public GetDashTranscodeDecisionCommandValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
        RuleFor(x => x.DecisionRequest).NotNull();
        RuleFor(x => x.DecisionRequest.TranscodeSessionId).NotEmpty();
        RuleFor(x => x.DecisionRequest.PlexSessionId).NotEmpty();
        RuleFor(x => x.DecisionRequest.ClientIdentifier).NotEmpty();
    }
}

public class GetDashTranscodeDecisionCommandHandler
    : ICommandHandler<GetDashTranscodeDecisionCommand, Result<GetDashTranscodeDecisionResult>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IPlexApiClientFactory _plexApiClientFactory;

    public GetDashTranscodeDecisionCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        IPlexApiClientFactory plexApiClientFactory
    )
    {
        _log = log.ForContext<GetDashTranscodeDecisionCommandHandler>();
        _dbContext = dbContext;
        _plexApiClientFactory = plexApiClientFactory;
    }

    public async Task<Result<GetDashTranscodeDecisionResult>> ExecuteAsync(
        GetDashTranscodeDecisionCommand command,
        CancellationToken cancellationToken
    )
    {
        var tokenResult = await _dbContext.GetPlexServerTokenAsync(command.PlexServerId, cancellationToken);
        if (tokenResult.IsFailed)
            return tokenResult.ToResult();

        var connectionResult = await _dbContext.ChoosePlexServerConnection(command.PlexServerId, cancellationToken);
        if (connectionResult.IsFailed)
            return connectionResult.ToResult();

        var options = new PlexApiClientOptions
        {
            ConnectionUrl = connectionResult.Value.Url,
            Timeout = 30,
            RetryCount = 0,
        };

        var decisionRequest = command.DecisionRequest;

        var debugDecisionUrl = new Url(connectionResult.Value.Url)
            .AppendPathSegment("video/:/transcode/universal/decision")
            .ApplyDashTranscodeQueryParams(decisionRequest.ToMakeDecisionRequest(), tokenResult.Value)
            .ToString();

        _log.Here().Information("Requesting Plex transcode decision URL: {Url}", debugDecisionUrl);

        var client = _plexApiClientFactory.CreateClient(tokenResult.Value, options, cancellationToken);
        var decisionResponse = await client.Transcoder
            .MakeDecisionAsync(decisionRequest.ToMakeDecisionRequest())
            .ToResponse(cancellationToken);

        if (decisionResponse.IsCancelledOrFailed)
            return decisionResponse.ToResult();

        var mediaContainer = decisionResponse.Value.MediaContainerWithDecision?.MediaContainer;
        if (mediaContainer is null)
            return Result.Fail("Invalid decision response: missing MediaContainer").LogError();

        var summary = new GetDashTranscodeDecisionResult
        {
            GeneralDecisionCode = mediaContainer.GeneralDecisionCode?.ToString() ?? "unknown",
            GeneralDecisionText = mediaContainer.GeneralDecisionText ?? "unknown",
            TranscodeDecisionCode = mediaContainer.TranscodeDecisionCode?.ToString() ?? "unknown",
            TranscodeDecisionText = mediaContainer.TranscodeDecisionText ?? "unknown",
            VideoDecision = "unknown",
            AudioDecision = "unknown",
            TranscodedQuality = VideoQuality.None,
            PartDecision = "unknown",
            SuggestedClientType = PlexDownloadClientType.Dash,
        };
        var partDecisions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var metadata in mediaContainer.Metadata ?? [])
        {
            foreach (var media in metadata.Media ?? [])
            {
                if (summary.TranscodedQuality is VideoQuality.None)
                {
                    summary = summary with { TranscodedQuality = ResolveMediaQuality(media) };
                }
                else
                {
                    summary = summary with
                    {
                        TranscodedQuality = GetHighestVideoQuality(
                            summary.TranscodedQuality,
                            ResolveMediaQuality(media)
                        ),
                    };
                }

                foreach (var part in media.Part ?? [])
                {
                    partDecisions.Add(part.Decision?.ToString().ToLowerInvariant() ?? "unknown");

                    foreach (var stream in part.Stream ?? [])
                    {
                        var decision = stream.Decision?.ToString() ?? "unknown";
                        if (IsVideoStream(stream.StreamType))
                        {
                            summary = summary with { VideoDecision = decision };
                            if (summary.TranscodedQuality is VideoQuality.None)
                            {
                                summary = summary with { TranscodedQuality = ResolveStreamQuality(stream, media) };
                            }
                            else
                            {
                                summary = summary with
                                {
                                    TranscodedQuality = GetHighestVideoQuality(
                                        summary.TranscodedQuality,
                                        ResolveStreamQuality(stream, media)
                                    ),
                                };
                            }
                        }
                        else if (IsAudioStream(stream.StreamType))
                        {
                            summary = summary with { AudioDecision = decision };
                        }
                    }
                }
            }
        }

        summary = summary with
        {
            PartDecision = partDecisions.Count switch
            {
                0 => "unknown",
                1 => partDecisions.First(),
                _ => "conflict",
            },
        };

        _log.Here()
            .Information(
                "Decision returned: generalCode={GeneralCode}, generalText={GeneralText}, "
                    + "transcodeCode={TranscodeCode}, transcodeText={TranscodeText}, "
                    + "session={Session}, sessionId={SessionId}",
                summary.GeneralDecisionCode,
                summary.GeneralDecisionText,
                summary.TranscodeDecisionCode,
                summary.TranscodeDecisionText,
                decisionRequest.TranscodeSessionId,
                decisionRequest.PlexSessionId
            );

        _log.Here()
            .Information(
                "Stream decisions - Video: {VideoDecision}, Audio: {AudioDecision}, Part: {PartDecision}, TranscodeQuality: {TranscodeQuality}",
                summary.VideoDecision,
                summary.AudioDecision,
                summary.PartDecision,
                summary.TranscodedQuality
            );

        if (string.Equals(summary.PartDecision, "directplay", StringComparison.OrdinalIgnoreCase))
        {
            _log.Here()
                .Information(
                    "Plex decided direct play for this item. Suggesting direct download client instead of DASH"
                );

            summary = summary with { SuggestedClientType = PlexDownloadClientType.Direct };
        }

        if (!string.Equals(summary.VideoDecision, "copy", StringComparison.OrdinalIgnoreCase))
        {
            _log.Here()
                .Warning(
                    "Video is being transcoded instead of direct streamed. "
                        + "Quality may be degraded. Decision: {Decision}",
                    summary.VideoDecision
                );
        }

        if (!string.Equals(summary.AudioDecision, "copy", StringComparison.OrdinalIgnoreCase))
        {
            _log.Here()
                .Warning(
                    "Audio is being transcoded instead of direct streamed. "
                        + "Quality may be degraded. Decision: {Decision}",
                    summary.AudioDecision
                );
        }

        return Result.Ok(summary);
    }

    private static bool IsVideoStream(MediaContainerWithDecisionStreamType streamType) =>
        streamType.ToString() is "Video" or "1";

    private static bool IsAudioStream(MediaContainerWithDecisionStreamType streamType) =>
        streamType.ToString() is "Audio" or "2";

    private static VideoQuality ResolveStreamQuality(
        MediaContainerWithDecisionStream stream,
        MediaContainerWithDecisionMedia media
    )
    {
        if (stream.Width is { } streamWidth)
            return ToVideoQualityByWidth(Convert.ToInt32(streamWidth));

        if (stream.Height is { } streamHeight)
            return ToVideoQuality(Convert.ToInt32(streamHeight));

        var mediaResolutionQuality = media.VideoResolution?.ToVideoQuality() ?? VideoQuality.Unknown;
        if (mediaResolutionQuality is not VideoQuality.Unknown)
            return mediaResolutionQuality;

        return VideoQuality.None;
    }

    private static VideoQuality ResolveMediaQuality(MediaContainerWithDecisionMedia media)
    {
        var mediaResolutionQuality = media.VideoResolution?.ToVideoQuality() ?? VideoQuality.Unknown;
        if (mediaResolutionQuality is not VideoQuality.Unknown)
            return mediaResolutionQuality;

        if (media.Width is { } mediaWidth)
            return ToVideoQualityByWidth(Convert.ToInt32(mediaWidth));

        if (media.Height is { } mediaHeight)
            return ToVideoQuality(Convert.ToInt32(mediaHeight));

        return VideoQuality.None;
    }

    private static VideoQuality ToVideoQualityByWidth(int width)
    {
        if (width >= 7600)
            return VideoQuality.UHD_8K;
        if (width >= 3800)
            return VideoQuality.UHD_4K;
        if (width >= 2500)
            return VideoQuality.QHD;
        if (width >= 1700)
            return VideoQuality.FullHD;
        if (width >= 1200)
            return VideoQuality.HD;
        if (width >= 950)
            return VideoQuality.DVD;
        if (width >= 700)
            return VideoQuality.SD;
        if (width >= 450)
            return VideoQuality.nHD;
        if (width >= 300)
            return VideoQuality.SubSD_CIF;
        return VideoQuality.SubSD_144p;
    }

    private static VideoQuality ToVideoQuality(int height)
    {
        if (height >= 3800)
            return VideoQuality.UHD_8K;
        if (height >= 2000)
            return VideoQuality.UHD_4K;
        if (height >= 1300)
            return VideoQuality.QHD;
        if (height >= 1000)
            return VideoQuality.FullHD;
        if (height >= 700)
            return VideoQuality.HD;
        if (height >= 560)
            return VideoQuality.DVD;
        if (height >= 470)
            return VideoQuality.SD;
        if (height >= 350)
            return VideoQuality.nHD;
        if (height >= 200)
            return VideoQuality.SubSD_CIF;
        return VideoQuality.SubSD_144p;
    }

    private static VideoQuality GetHighestVideoQuality(VideoQuality current, VideoQuality candidate) =>
        (VideoQuality)Math.Max((int)current, (int)candidate);
}
