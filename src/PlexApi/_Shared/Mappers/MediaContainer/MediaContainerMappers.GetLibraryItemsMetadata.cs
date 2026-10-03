using LukeHagar.PlexAPI.SDK.Models.Components;

namespace Reaparr.PlexApi;

public static class MediaContainerMappers
{
    public static LibraryMediaItemDTO ToMediaItemDTO(this Metadata data)
    {
        return new LibraryMediaItemDTO
        {
            RatingKey = int.TryParse(data.RatingKey!, out var ratingKey) ? ratingKey : -1,
            Key = data.Key,
            Type = data.Type == "clip" ? PlexMediaType.OtherVideos : data.Type.ToPlexMediaType(),
            Title = data.Title,
            Summary = data.Summary ?? string.Empty,
            Year = data.Year ?? 0,
            SortIndex = 0, // Set later on
            SortTitle = string.IsNullOrWhiteSpace(data.TitleSort) ? data.Title.ToSortTitle() : data.TitleSort,
            SearchTitle = data.Title.ToSearchTitle(),
            OriginalTitle = data.OriginalTitle ?? string.Empty,
            ChildCount = data.ChildCount ?? 0,
            Media = data.Media?.Select(x => x.ToItemMediaDTO(
                data.Type is "movie" or "episode" && data.Subtype != "photo"
            )).ToList() ?? [],
            Genre = data.Genre?.Select(x => x.ToGenreDTO()).ToList() ?? [],
            Country = data.Country?.Select(x => x.ToCountryDTO()).ToList() ?? [],
            Role = data.Role?.Select(x => x.ToRoleDTO()).ToList() ?? [],
            Studio = data.Studio ?? string.Empty,
            ContentRating = data.ContentRating ?? string.Empty,
            Index = data.Index ?? -1,
            ParentIndex = data.ParentIndex ?? -1,
            // Duration is in milliseconds and we want seconds
            Duration = data.Duration is { } milliseconds ? milliseconds / 1000 : 0,
            Thumb = data.Thumb!,
            Art = data.Art!,
            Theme = data.Theme!,
            Guid = data.Guid ?? string.Empty,
            AddedAt = DateTimeExtensions.FromUnixTime(data.AddedAt),
            UpdatedAt = data.UpdatedAt is { } updatedAt ? DateTimeExtensions.FromUnixTime(updatedAt) : null,
            OriginallyAvailableAt = data.OriginallyAvailableAt?.ToString() ?? string.Empty,
            Ratings = [],
            Guids = data.Guids?.Select(x => new MetaDataGuidsDTO(x.Id)).ToList() ?? [],
            GrandparentTitle = data.GrandparentTitle ?? string.Empty,
            ParentTitle = data.ParentTitle ?? string.Empty,
            ParentGuid = data.ParentGuid ?? string.Empty,
            ParentRatingKey = data.ParentRatingKey ?? string.Empty,
            AudienceRating = data.AudienceRating,
            Rating = data.Rating ?? 0f,
        };
    }

    public static LibraryMediaItemGenreDTO ToGenreDTO(this Tag x) =>
        new()
        {
            Name = x.TagValue,
            PlexId = -1,
            Filter = string.Empty,
            Key = x.TagValue.ToMd5Hash(),
        };

    public static LibraryMediaItemCountryDTO ToCountryDTO(this Tag x) =>
        new()
        {
            Name = x.TagValue,
            PlexId = -1,
            Filter = string.Empty,
            Key = x.TagValue.ToMd5Hash(),
        };

    public static LibraryMediaItemRoleDTO ToRoleDTO(this Tag x) =>
        new()
        {
            Name = x.TagValue,
            PlexId = -1,
            Role = null,
            Filter = null,
            Thumb = null,
            Key = x.TagValue.ToMd5Hash(),
        };

    public static LibraryMediaItemMediaDTO ToItemMediaDTO(this Media media, bool inferVideoQuality = true) =>
        new()
        {
            Id = Convert.ToInt32(media.Id),
            Duration = media.Duration ?? 0,
            Bitrate = media.Bitrate ?? 0,
            Width = media.Width ?? 0,
            Height = media.Height ?? 0,
            AspectRatio = media.AspectRatio ?? 0,
            AudioChannels = media.AudioChannels ?? 0,
            AudioCodec = media.AudioCodec ?? string.Empty,
            VideoCodec = media.VideoCodec ?? string.Empty,
            VideoResolution = inferVideoQuality
                ? (media.VideoResolution ?? media.Part?.FirstOrDefault()?.File.ParseQualityFromFileName()
                    ?? string.Empty).ToVideoQuality()
                : VideoQuality.Unknown,
            Container = media.Container ?? string.Empty,
            VideoFrameRate = media.VideoFrameRate ?? string.Empty,
            VideoProfile = media.VideoProfile ?? string.Empty,
            AudioProfile = media.AudioProfile ?? string.Empty,
            Parts = media.Part?.Select(x => x.ToItemPartDTO()).ToList() ?? [],
            OptimizedForStreaming = media.OptimizedForStreaming ?? false,
        };

    public static LibraryMediaItemPartDTO ToItemPartDTO(this Part part) =>
        new()
        {
            Id = Convert.ToInt32(part.Id),
            Key = part.Key,
            Duration = part.Duration ?? -1,
            File = part.File ?? string.Empty,
            Size = part.Size ?? -1,
            Container = part.Container ?? string.Empty,
            Stream = part.Stream?.Select(x => x.ToItemStreamDTO()).ToList() ?? [],
        };

    public static LibraryMediaItemStreamDTO ToItemStreamDTO(
        this LukeHagar.PlexAPI.SDK.Models.Components.Stream source
    ) =>
        new()
        {
            Id = source.Id,
            StreamType = source.StreamType switch
            {
                LukeHagar.PlexAPI.SDK.Models.Components.StreamType.Video => Domain.StreamType.Video,
                LukeHagar.PlexAPI.SDK.Models.Components.StreamType.Audio => Domain.StreamType.Audio,
                LukeHagar.PlexAPI.SDK.Models.Components.StreamType.Subtitle => Domain.StreamType.Subtitle,
                _ => Domain.StreamType.Unknown,
            },
            Default = source.Default,
            Codec = source.Codec,
            Index = source.Index,
            Bitrate = source.Bitrate ?? 0,
            Language = source.Language ?? string.Empty,
            LanguageTag = source.LanguageTag ?? string.Empty,
            LanguageCode = source.LanguageCode ?? string.Empty,
            DOVIBLCompatID = source.DOVIBLCompatID,
            DOVIBLPresent = source.DOVIBLPresent,
            DOVIELPresent = source.DOVIELPresent,
            DOVILevel = source.DOVILevel,
            DOVIPresent = source.DOVIPresent,
            DOVIProfile = source.DOVIProfile,
            DOVIRPUPresent = source.DOVIRPUPresent,
            DOVIVersion = source.DOVIVersion,
            BitDepth = source.BitDepth,
            ChromaLocation = source.ChromaLocation,
            ChromaSubsampling = source.ChromaSubsampling,
            CodedHeight = source.CodedHeight,
            CodedWidth = source.CodedWidth,
            ColorPrimaries = source.ColorPrimaries,
            ColorRange = source.ColorRange,
            ColorSpace = source.ColorSpace,
            ColorTrc = source.ColorTrc,
            FrameRate = source.FrameRate,
            Height = source.Height,
            Level = source.Level,
            Original = source.Original,
            HasScalingMatrix = source.HasScalingMatrix,
            Profile = source.Profile,
            ScanType = source.ScanType,
            RefFrames = source.RefFrames,
            Width = source.Width,
            DisplayTitle = source.DisplayTitle,
            ExtendedDisplayTitle = source.ExtendedDisplayTitle!,
            Selected = source.Selected,
            Forced = source.Forced,
            Channels = source.Channels,
            AudioChannelLayout = source.AudioChannelLayout,
            SamplingRate = source.SamplingRate,
            CanAutoSync = source.CanAutoSync?.Boolean ?? false,
            HearingImpaired = source.HearingImpaired,
            Dub = source.Dub,
            Title = source.Title,
        };
}
