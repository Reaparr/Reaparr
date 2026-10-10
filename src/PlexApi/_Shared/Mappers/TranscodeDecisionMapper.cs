using LukeHagar.PlexAPI.SDK.Models.Components;
using LukeHagar.PlexAPI.SDK.Models.Requests;
using Protocol = LukeHagar.PlexAPI.SDK.Models.Requests.QueryParamProtocol;

namespace Reaparr.PlexApi;

public static class TranscodeDecisionMapper
{
    public static MakeDecisionRequest ToMakeDecisionRequest(this TranscodeDecisionRequest source) =>
        new()
        {
            Accepts = Accepts.ApplicationJson,
            ClientIdentifier = source.ClientIdentifier,
            Product = "Plex Web",
            Version = "4.158.0",
            Platform = "Chrome",
            PlatformVersion = "130.0",
            Device = "Linux",
            Model = "standalone",
            DeviceName = "Chrome",
            TranscodeType = TranscodeType.Video,
            HasMDE = BoolInt.True,
            Path = source.MetaDataPath,
            MediaIndex = 0,
            PartIndex = 0,
            Protocol = Protocol.Dash,
            DirectPlay = BoolInt.True,
            DirectStream = BoolInt.True,
            DirectStreamAudio = BoolInt.True,
            SubtitleSize = 100,
            AudioBoost = 100,
            Location = LukeHagar.PlexAPI.SDK.Models.Requests.Location.Lan,
            AutoAdjustQuality = BoolInt.False,
            AutoAdjustSubtitle = BoolInt.True,
            PeakBitrate = 2000000,
            MediaBufferSize = 102400,
            Subtitles = LukeHagar.PlexAPI.SDK.Models.Requests.Subtitles.None,
            VideoResolution = "3840x2160",
            VideoQuality = 100,
            XPlexClientProfileExtra = ClientProfileExtra(Protocol.Dash),
            XPlexSessionIdentifier = source.PlexSessionId,
            TranscodeSessionId = source.TranscodeSessionId,
        };

    private static string ClientProfileExtra(Protocol protocol) =>
        "add-direct-play-profile(type=videoProfile&videoCodec=*&audioCodec=*&container=*)"
        + "+append-transcode-target-codec(type=videoProfile&context=streaming"
        + "&videoCodec=h264,hevc,vp9,av1,mpeg2video,mpeg4,vc1"
        + "&audioCodec=aac,ac3,eac3,dts,dca,mp3,flac,opus,vorbis,truehd"
        + $"&protocol={protocol.ToEnumMemberValue()})"
        + "+append-transcode-target-codec(type=videoProfile&context=streaming"
        + "&videoCodec=h264,hevc,vp9,av1,mpeg2video,mpeg4,vc1"
        + "&audioCodec=aac,ac3,eac3,dts,dca,mp3,flac,opus,vorbis,truehd"
        + "&protocol=http)"
        + "+append-transcode-target-codec(type=videoProfile&context=streaming"
        + "&videoCodec=h264,hevc,vp9,av1,mpeg2video,mpeg4,vc1"
        + "&audioCodec=aac,ac3,eac3,dts,dca,mp3,flac,opus,vorbis,truehd"
        + "&protocol=hls)"
        + "+add-limitation(scope=videoCodec&scopeName=*&type=upperBound&name=video.bitDepth&value=12&isRequired=false)"
        + "+add-limitation(scope=videoCodec&scopeName=*&type=upperBound&name=video.width&value=3840&isRequired=false)"
        + "+add-limitation(scope=videoCodec&scopeName=*&type=upperBound&name=video.height&value=2160&isRequired=false)"
        + "+add-limitation(scope=videoCodec&scopeName=*&type=upperBound&name=video.bitrate&value=2000000&isRequired=false)";
}
