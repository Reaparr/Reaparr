namespace Reaparr.Data;

public class DownloadTaskOtherVideoFileLogConfiguration : IEntityTypeConfiguration<DownloadTaskOtherVideoFileLog>
{
    public void Configure(EntityTypeBuilder<DownloadTaskOtherVideoFileLog> builder)
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
            .HasOne(x => x.DownloadTaskOtherVideo)
            .WithMany()
            .HasForeignKey(x => x.DownloadTaskOtherVideoId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
