using LukeHagar.PlexAPI.SDK.Models.Components;
using LukeHagar.PlexAPI.SDK.Models.Requests;

namespace Reaparr.BaseTests;

public sealed class MakeDecisionMediaContainerConfig
{
    public long GeneralDecisionCode { get; set; } = 1001;
    public string GeneralDecisionText { get; set; } = "Direct play not available; Conversion OK.";
    public long TranscodeDecisionCode { get; set; } = 1001;
    public string TranscodeDecisionText { get; set; } = "Direct play not available; Conversion OK.";
    public long DirectPlayDecisionCode { get; set; } = 3000;
    public string DirectPlayDecisionText { get; set; } = "App cannot direct play this item. Direct play is disabled.";
    public string? MediaVideoResolution { get; set; } = "1080p";
    public int? MediaWidth { get; set; } = 1920;
    public int? MediaHeight { get; set; } = 1080;
    public bool IncludeAudioStream { get; set; } = true;
    public string PartDecision { get; set; } = "transcode";
    public MediaContainerWithDecisionDecision? AudioDecision { get; set; } = MediaContainerWithDecisionDecision.Copy;
    public List<MakeDecisionVideoStreamConfig> VideoStreams { get; } =
    [
        new()
        {
            DisplayTitle = "1080p",
            ExtendedDisplayTitle = "1080p (H.264)",
            Width = 1920,
            Height = 1080,
            Decision = MediaContainerWithDecisionDecision.Copy,
        },
    ];
}

public sealed class MakeDecisionVideoStreamConfig
{
    public string? DisplayTitle { get; set; }
    public string? ExtendedDisplayTitle { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public MediaContainerWithDecisionDecision? Decision { get; set; } = MediaContainerWithDecisionDecision.Copy;
}

public partial class FakePlexApiData
{
    public static MakeDecisionResponse GetMakeDecisionResponse(
        HttpStatusCode statusCode,
        Seed seed,
        MediaContainerWithDecision? responseBody = null,
        HttpRequestMessage? request = null
    )
    {
        _ = seed;

        var body = responseBody ?? GetMakeDecisionMediaContainer();

        return new MakeDecisionResponse
        {
            StatusCode = (int)statusCode,
            ContentType = ContentType.ApplicationJson,
            MediaContainerWithDecision = body,
            RawResponse = GetHttpResponseMessage(statusCode, body, request),
        };
    }

    public static string GetMakeDecisionMediaContainerJsonText(Action<MakeDecisionMediaContainerConfig>? options = null)
    {
        var config = new MakeDecisionMediaContainerConfig();
        options?.Invoke(config);

        if (config.VideoStreams.Count == 0)
            throw new InvalidOperationException("At least one video stream is required");

        var streamId = 302467;
        var streams = config
            .VideoStreams.Select(x =>
            {
                var currentId = streamId++;
                return new
                {
                    codec = "h264",
                    displayTitle = x.DisplayTitle,
                    extendedDisplayTitle = x.ExtendedDisplayTitle,
                    id = currentId,
                    key = $"/library/streams/{currentId}",
                    streamType = 1,
                    decision = x.Decision?.ToString().ToLowerInvariant(),
                    width = x.Width,
                    height = x.Height,
                };
            })
            .ToList<object>();

        if (config.IncludeAudioStream)
        {
            var currentId = streamId++;
            streams.Add(
                new
                {
                    codec = "aac",
                    displayTitle = "Nederlands (AAC Stereo)",
                    extendedDisplayTitle = "Nederlands (AAC Stereo)",
                    id = currentId,
                    key = $"/library/streams/{currentId}",
                    streamType = 2,
                    decision = config.AudioDecision?.ToString().ToLowerInvariant(),
                }
            );
        }

        var data = new
        {
            MediaContainer = new
            {
                size = 1,
                directPlayDecisionCode = config.DirectPlayDecisionCode,
                directPlayDecisionText = config.DirectPlayDecisionText,
                generalDecisionCode = config.GeneralDecisionCode,
                generalDecisionText = config.GeneralDecisionText,
                transcodeDecisionCode = config.TranscodeDecisionCode,
                transcodeDecisionText = config.TranscodeDecisionText,
                Metadata = new[]
                {
                    new
                    {
                        title = "Kees van der Spek Ontmaskert: Oekraine",
                        type = "episode",
                        addedAt = 1737305627,
                        Media = new[]
                        {
                            new
                            {
                                id = 60880,
                                videoResolution = config.MediaVideoResolution,
                                width = config.MediaWidth,
                                height = config.MediaHeight,
                                Part = new[]
                                {
                                    new
                                    {
                                        id = 91921,
                                        key = "/library/parts/91921",
                                        decision = config.PartDecision,
                                        Stream = streams,
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

        return JsonSerializer.Serialize(data, DefaultJsonSerializerOptions.PlexApiSerialization);
    }

    public static MediaContainerWithDecision GetMakeDecisionMediaContainer(
        Action<MakeDecisionMediaContainerConfig>? options = null
    )
    {
        var json = GetMakeDecisionMediaContainerJsonText(options);

        return JsonSerializer.Deserialize<MediaContainerWithDecision>(
            json,
            DefaultJsonSerializerOptions.PlexApiSerialization
        ) ?? throw new InvalidOperationException("Failed to deserialize transcode decision media container");
    }

    public static MediaContainerWithDecision GetMakeDecisionMediaContainerWithMismatchedDisplayTitle() =>
        GetMakeDecisionMediaContainer(config =>
        {
            config.DirectPlayDecisionCode = 3000;
            config.DirectPlayDecisionText =
                "This app cannot play this item. The reason is: video.bitDepth limitation applies: 10 > 8.";
            config.GeneralDecisionCode = 1001;
            config.GeneralDecisionText = "Direct play not available; Conversion OK.";
            config.TranscodeDecisionCode = 1001;
            config.TranscodeDecisionText = "Direct play not available; Conversion OK.";
            config.MediaVideoResolution = "SD";
            config.MediaWidth = 720;
            config.MediaHeight = 404;
            config.AudioDecision = MediaContainerWithDecisionDecision.Transcode;
            config.VideoStreams.Clear();
            config.VideoStreams.Add(
                new MakeDecisionVideoStreamConfig
                {
                    DisplayTitle = "1080p",
                    ExtendedDisplayTitle = "1080p (PRORES)",
                    Width = 720,
                    Height = 404,
                    Decision = MediaContainerWithDecisionDecision.Transcode,
                }
            );
        });

    public static MediaContainerWithDecision GetMakeDecisionDirectPlayMediaContainer() =>
        GetMakeDecisionMediaContainer(config =>
        {
            config.GeneralDecisionCode = 1000;
            config.GeneralDecisionText = "Direct play OK.";
            config.DirectPlayDecisionCode = 1000;
            config.DirectPlayDecisionText = "Direct play OK.";
            config.TranscodeDecisionCode = 1000;
            config.TranscodeDecisionText = "Direct play OK.";
            config.PartDecision = "directplay";
            config.MediaVideoResolution = "sd";
            config.MediaWidth = 720;
            config.MediaHeight = 384;
            config.AudioDecision = null;
            config.VideoStreams.Clear();
            config.VideoStreams.Add(
                new MakeDecisionVideoStreamConfig
                {
                    DisplayTitle = "SD",
                    ExtendedDisplayTitle = "SD (MPEG4)",
                    Width = 720,
                    Height = 384,
                    Decision = null,
                }
            );
        });

    public static MediaContainerWithDecision GetMakeDecisionMediaContainerWithWidthFallback() =>
        GetMakeDecisionMediaContainer(config =>
        {
            config.MediaVideoResolution = null;
            config.MediaWidth = 3840;
            config.MediaHeight = 2160;
            config.VideoStreams.Clear();
            config.VideoStreams.Add(
                new MakeDecisionVideoStreamConfig
                {
                    DisplayTitle = "Video Stream",
                    ExtendedDisplayTitle = "Video Stream",
                    Width = 3840,
                    Height = 2160,
                    Decision = MediaContainerWithDecisionDecision.Copy,
                }
            );
        });
}
