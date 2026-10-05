namespace Reaparr.Application.Contracts.Validators;

public class PlexMediaDTOValidator : AbstractValidator<PlexMediaDTO>
{
    public PlexMediaDTOValidator()
    {
        RuleFor(x => x.Id).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.Year).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Duration).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MediaSize).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ChildCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AddedAt).NotEmpty();
        RuleFor(x => x.UpdatedAt).NotEmpty();
        RuleFor(x => x.PlexLibraryId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PlexServerId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Type)
            .Must(x =>
                x is PlexMediaType.Movie
                    or PlexMediaType.TvShow
                    or PlexMediaType.Season
                    or PlexMediaType.Episode
                    or PlexMediaType.MusicArtist
                    or PlexMediaType.MusicAlbum
                    or PlexMediaType.MusicTrack
                    or PlexMediaType.PhotoAlbum
                    or PlexMediaType.PhotoImage
                    or PlexMediaType.OtherVideos
            );

        RuleFor(x => x.Summary).NotEmpty();
        RuleFor(x => x.UpdatedAt).NotEmpty();
        RuleForEach(x => x.Qualities)
            .ChildRules(y =>
            {
                y.RuleFor(z => z.Quality).NotEqual(VideoQuality.Unknown);
            })
            .When(x =>
                x.Type is not PlexMediaType.MusicArtist
                    and not PlexMediaType.MusicAlbum
                    and not PlexMediaType.MusicTrack
                    and not PlexMediaType.PhotoAlbum
                    and not PlexMediaType.PhotoImage
            );
    }
}
