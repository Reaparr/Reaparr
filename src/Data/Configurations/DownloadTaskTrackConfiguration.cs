namespace Reaparr.Data;

public class DownloadTaskTrackConfiguration : IEntityTypeConfiguration<DownloadTaskTrack>
{
    public void Configure(EntityTypeBuilder<DownloadTaskTrack> builder)
    {
        builder.HasIndex(x => x.DownloadStatus);
        builder.HasIndex(x => new
        {
            x.PlexLibraryId,
            x.PlexServerId,
            x.PlexApiRatingKey,
        });

        builder
            .Property(x => x.DownloadStatus)
            .HasMaxLength(20)
            .HasConversion(x => x.ToDownloadStatusString(), x => x.ToDownloadStatus())
            .IsUnicode(false);

        builder.Property(x => x.Title).UseCollation(OrderByNaturalExtensions.CollationName);

        builder
            .HasMany(x => x.Children)
            .WithOne(x => x.Parent)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
