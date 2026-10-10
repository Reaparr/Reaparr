namespace Reaparr.Application.Contracts.Validators;

public class DownloadMediaDTOValidator : AbstractValidator<DownloadMediaDTO>
{
    public DownloadMediaDTOValidator()
    {
        RuleFor(x => x.PlexServerId)
            .GreaterThan(0)
            .WithMessage($"Every entry should have a valid {nameof(DownloadMediaDTO.PlexServerId)} set");

        RuleFor(x => x.PlexLibraryId)
            .GreaterThan(0)
            .WithMessage($"Every entry should have a valid {nameof(DownloadMediaDTO.PlexLibraryId)} set");

        RuleFor(x => x.MediaIds).NotEmpty();
        RuleFor(x => x.Type)
            .Must(x =>
            {
                switch (x)
                {
                    case PlexMediaType.Movie:
                    case PlexMediaType.TvShow:
                    case PlexMediaType.Season:
                    case PlexMediaType.Episode:
                    case PlexMediaType.PhotoAlbum:
                    case PlexMediaType.PhotoImage:
                    case PlexMediaType.MusicArtist:
                    case PlexMediaType.MusicAlbum:
                    case PlexMediaType.MusicTrack:
                    case PlexMediaType.OtherVideos:
                        return true;
                    default:
                        return false;
                }
            });
    }
}
