namespace Reaparr.Domain;

public class DownloadTaskEpisodeFileValidator : AbstractValidator<DownloadTaskTvShowEpisodeFile>
{
    public DownloadTaskEpisodeFileValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.DownloadStatus).Equal(DownloadStatus.Queued);
        RuleFor(x => x.DownloadTaskType).Equal(DownloadTaskType.EpisodeData);
        RuleFor(x => x.MediaType).Equal(PlexMediaType.Episode);
        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.PlexServerId).GreaterThan(0);
        RuleFor(x => x.CreatedAt).GreaterThan(DateTime.MinValue);
        RuleFor(x => x.DataTotal).GreaterThan(0);
        RuleFor(x => x.DestinationDirectory).NotEmpty();
        RuleFor(x => x.DownloadDirectory).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.FileLocationUrl).NotEmpty();
        RuleFor(x => x.VideoQuality).NotEmpty();
        RuleFor(x => x.ParentId).NotEqual(Guid.Empty);
    }
}
