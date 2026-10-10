namespace Reaparr.Data;

public class DownloadTaskTrackFileLogConfiguration : IEntityTypeConfiguration<DownloadTaskTrackFileLog>
{
    public void Configure(EntityTypeBuilder<DownloadTaskTrackFileLog> builder)
    {
        builder
            .Property(x => x.LogLevel)
            .HasMaxLength(20)
            .HasConversion(x => x.ToNotificationLevelString(), x => x.ToNotificationLevel())
            .HasDefaultValue(NotificationLevel.None)
            .HasSentinel(NotificationLevel.None)
            .IsUnicode(false);

        builder
            .Property(x => x.Status)
            .HasMaxLength(20)
            .HasConversion(x => x.ToDownloadStatusString(), x => x.ToDownloadStatus())
            .HasDefaultValue(DownloadStatus.Unknown)
            .HasSentinel(DownloadStatus.Unknown)
            .IsUnicode(false);

        builder
            .HasOne(x => x.DownloadTaskFile)
            .WithMany(x => x.Logs)
            .HasForeignKey(x => x.DownloadTaskFileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.DownloadTaskTrack)
            .WithMany()
            .HasForeignKey(x => x.DownloadTaskTrackId)
            .OnDelete(DeleteBehavior.NoAction);

        builder
            .HasOne(x => x.DownloadTaskAlbum)
            .WithMany()
            .HasForeignKey(x => x.DownloadTaskAlbumId)
            .OnDelete(DeleteBehavior.NoAction);

        builder
            .HasOne(x => x.DownloadTaskArtist)
            .WithMany()
            .HasForeignKey(x => x.DownloadTaskArtistId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
