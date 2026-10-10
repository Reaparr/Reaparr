// ReSharper disable InconsistentNaming

namespace Reaparr.Domain;

public record LibraryMediaItemDTO
{
    public required int RatingKey { get; init; }

    public required string Key { get; init; }

    public required PlexMediaType Type { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required int Year { get; init; }

    public required int ParentIndex { get; init; }

    public required int Index { get; init; }

    public required string Studio { get; init; }

    public required string ContentRating { get; init; }

    public required string SortTitle { get; init; }

    public required string SearchTitle { get; init; }

    public required int SortIndex { get; set; }

    public required string OriginalTitle { get; init; }

    public required int ChildCount { get; init; }

    public required int Duration { get; init; }

    public required float Rating { get; init; }

    public required string Thumb { get; init; }

    public required string Art { get; init; }

    public required string Theme { get; init; }

    public required string Guid { get; init; }

    public required string GrandparentTitle { get; init; }

    public required string ParentTitle { get; init; }

    public required string ParentGuid { get; init; }

    public required string ParentRatingKey { get; init; }

    public required double? AudienceRating { get; init; }

    public required DateTime AddedAt { get; init; }

    public required DateTime? UpdatedAt { get; init; }

    public required string OriginallyAvailableAt { get; init; }

    public required List<MetaDataRatingsDTO> Ratings { get; init; } = [];

    public required List<MetaDataGuidsDTO> Guids { get; init; } = [];

    public required List<LibraryMediaItemMediaDTO> Media { get; init; } = [];

    public required List<LibraryMediaItemGenreDTO> Genre { get; init; } = [];

    public required List<LibraryMediaItemCountryDTO> Country { get; init; } = [];

    public required List<LibraryMediaItemRoleDTO> Role { get; init; } = [];
}

public record MetaDataRatingsDTO
{
    /// <summary>
    /// The image or reference for the rating.
    /// </summary>
    public required string Image { get; init; }

    /// <summary>
    /// The rating value.
    /// </summary>
    public required float Value { get; init; }

    /// <summary>
    /// The type of rating (e.g., audience, critic).
    /// </summary>
    public required string Type { get; init; }
}

public record MetaDataGuidsDTO
{
    [SetsRequiredMembers]
    public MetaDataGuidsDTO(string id)
    {
        Id = id;
    }

    /// <summary>
    /// The GUID value.
    /// </summary>
    public required string Id { get; init; }
}

public record LibraryMediaItemCountryDTO
{
    public required int PlexId { get; init; }

    public required string Name { get; init; }

    public required string Filter { get; init; }

    /// <summary>
    /// A MD5 hash of the name, used as a unique key.
    /// </summary>
    public required string Key { get; init; }
}

public record LibraryMediaItemGenreDTO
{
    public required int PlexId { get; init; }

    public required string Name { get; init; }

    public required string Filter { get; init; }

    /// <summary>
    /// A MD5 hash of the name, used as a unique key.
    /// </summary>
    public required string Key { get; init; }
}

public record LibraryMediaItemRoleDTO
{
    public required int PlexId { get; init; }

    public required string Name { get; init; }

    public required string? Role { get; init; }

    public required string? Filter { get; init; }

    public required string? Thumb { get; init; }

    /// <summary>
    /// A MD5 hash of the name, used as a unique key.
    /// </summary>
    public required string Key { get; init; }
}

public record LibraryMediaItemMediaDTO
{
    /// <summary>
    /// Unique media identifier.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Duration of the media in milliseconds.
    /// </summary>
    public required int Duration { get; init; }

    /// <summary>
    /// Bitrate in bits per second.
    /// </summary>
    public required int Bitrate { get; init; }

    /// <summary>
    /// Video width in pixels.
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// Video height in pixels.
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// Aspect ratio of the video.
    /// </summary>
    public required float AspectRatio { get; init; }

    /// <summary>
    /// Number of audio channels.
    /// </summary>
    public required int AudioChannels { get; init; }

    /// <summary>
    /// Audio codec used.
    /// </summary>
    public required string AudioCodec { get; init; }

    /// <summary>
    /// Video codec used.
    /// </summary>
    public required string VideoCodec { get; init; }

    /// <summary>
    /// Video resolution derived from Plex metadata.
    /// </summary>
    public required VideoQuality VideoResolution { get; init; }

    /// <summary>
    /// File container type.
    /// </summary>
    public required string Container { get; init; }

    /// <summary>
    /// Frame rate of the video (e.g., 24p).
    /// </summary>
    public required string VideoFrameRate { get; init; }

    /// <summary>
    /// Video profile (e.g., main 10).
    /// </summary>
    public required string VideoProfile { get; init; }

    /// <summary>
    /// Video profile (e.g., main 10).
    /// </summary>
    public required string AudioProfile { get; init; }

    /// <summary>
    /// Indicates whether this media file is optimized for direct streaming playback,
    /// meaning it is encoded and packaged to minimize buffering and avoid transcoding
    /// (for example, using streaming-friendly codecs, bitrate, and container settings).
    /// </summary>
    public required bool OptimizedForStreaming { get; init; }

    /// <summary>
    /// An array of parts for this media item.
    /// </summary>
    public required List<LibraryMediaItemPartDTO> Parts { get; init; }
}

public record LibraryMediaItemPartDTO
{
    /// <summary>
    /// Unique part identifier.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Key to access this part.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Duration of the part in milliseconds.
    /// </summary>
    public required int Duration { get; init; }

    /// <summary>
    /// File path for the part.
    /// </summary>
    public required string File { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// Container format of the part.
    /// </summary>
    public required string Container { get; init; }

    /// <summary>
    /// An array of streams for this part.
    /// </summary>
    public required List<LibraryMediaItemStreamDTO> Stream { get; init; }
}

public record LibraryMediaItemStreamDTO
{
    /// <summary>
    /// Unique stream identifier.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Stream type (1=video, 2=audio, 3=subtitle).
    /// </summary>
    public required StreamType StreamType { get; init; }

    /// <summary>
    /// Indicates if this stream is default.
    /// </summary>
    public required bool? Default { get; init; }

    /// <summary>
    /// Codec used by the stream.
    /// </summary>
    public required string Codec { get; init; }

    /// <summary>
    /// Index of the stream.
    /// </summary>
    public required int? Index { get; init; }

    /// <summary>
    /// Bitrate of the stream.
    /// </summary>
    public required int Bitrate { get; init; }

    /// <summary>
    /// Language of the stream.
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// Language tag (e.g., en).
    /// </summary>
    public required string LanguageTag { get; init; }

    /// <summary>
    /// ISO language code.
    /// </summary>
    public required string LanguageCode { get; init; }

    /// <summary>
    /// Dolby Vision BL compatibility ID.
    /// </summary>
    public required int? DOVIBLCompatID { get; init; }

    /// <summary>
    /// Indicates if Dolby Vision BL is present.
    /// </summary>
    public required bool? DOVIBLPresent { get; init; }

    /// <summary>
    /// Indicates if Dolby Vision EL is present.
    /// </summary>
    public required bool? DOVIELPresent { get; init; }

    /// <summary>
    /// Dolby Vision level.
    /// </summary>
    public required int? DOVILevel { get; init; }

    /// <summary>
    /// Indicates if Dolby Vision is present.
    /// </summary>
    public required bool? DOVIPresent { get; init; }

    /// <summary>
    /// Dolby Vision profile.
    /// </summary>
    public required int? DOVIProfile { get; init; }

    /// <summary>
    /// Indicates if Dolby Vision RPU is present.
    /// </summary>
    public required bool? DOVIRPUPresent { get; init; }

    /// <summary>
    /// Dolby Vision version.
    /// </summary>
    public required string? DOVIVersion { get; init; }

    /// <summary>
    /// Bit depth of the video stream.
    /// </summary>
    public required int? BitDepth { get; init; }

    /// <summary>
    /// Chroma sample location.
    /// </summary>
    public required string? ChromaLocation { get; init; }

    /// <summary>
    /// Chroma subsampling format.
    /// </summary>
    public required string? ChromaSubsampling { get; init; }

    /// <summary>
    /// Coded video height.
    /// </summary>
    public required int? CodedHeight { get; init; }

    /// <summary>
    /// Coded video width.
    /// </summary>
    public required int? CodedWidth { get; init; }

    /// <summary>
    /// Color primaries used.
    /// </summary>
    public required string? ColorPrimaries { get; init; }

    /// <summary>
    /// Color range (e.g., tv).
    /// </summary>
    public required string? ColorRange { get; init; }

    /// <summary>
    /// Color space.
    /// </summary>
    public required string? ColorSpace { get; init; }

    /// <summary>
    /// Color transfer characteristics.
    /// </summary>
    public required string? ColorTrc { get; init; }

    /// <summary>
    /// Frame rate of the stream.
    /// </summary>
    public required float? FrameRate { get; init; }

    /// <summary>
    /// Height of the video stream.
    /// </summary>
    public required int? Height { get; init; }

    /// <summary>
    /// Video level.
    /// </summary>
    public required int? Level { get; init; }

    /// <summary>
    /// Indicates if this is the original stream.
    /// </summary>
    public required bool? Original { get; init; }

    /// <summary>
    /// Indicates if a scaling matrix is present.
    /// </summary>
    public required bool? HasScalingMatrix { get; init; }

    /// <summary>
    /// Video profile.
    /// </summary>
    public required string? Profile { get; init; }

    /// <summary>
    /// Scan type.
    /// </summary>
    public required string? ScanType { get; init; }

    /// <summary>
    /// Number of reference frames.
    /// </summary>
    public required int? RefFrames { get; init; }

    /// <summary>
    /// Width of the video stream.
    /// </summary>
    public required int? Width { get; init; }

    /// <summary>
    /// Display title for the stream.
    /// </summary>
    public required string DisplayTitle { get; init; }

    /// <summary>
    /// Extended display title for the stream.
    /// </summary>
    public required string ExtendedDisplayTitle { get; init; }

    /// <summary>
    /// Indicates if this stream is selected (applicable for audio streams).
    /// </summary>
    public required bool? Selected { get; init; }

    /// <summary>
    /// Indicates if this stream is forced.
    /// </summary>
    public required bool? Forced { get; init; }

    /// <summary>
    /// Number of audio channels (for audio streams).
    /// </summary>
    public required int? Channels { get; init; }

    /// <summary>
    /// Audio channel layout.
    /// </summary>
    public required string? AudioChannelLayout { get; init; }

    /// <summary>
    /// Sampling rate for the audio stream.
    /// </summary>
    public required int? SamplingRate { get; init; }

    /// <summary>
    /// Indicates if the stream can auto-sync.
    /// </summary>
    public required bool? CanAutoSync { get; init; }

    /// <summary>
    /// Indicates if the stream is for the hearing impaired.
    /// </summary>
    public required bool? HearingImpaired { get; init; }

    /// <summary>
    /// Indicates if the stream is a dub.
    /// </summary>
    public required bool? Dub { get; init; }

    /// <summary>
    /// Optional title for the stream (e.g., language variant).
    /// </summary>
    public required string? Title { get; init; }
}
