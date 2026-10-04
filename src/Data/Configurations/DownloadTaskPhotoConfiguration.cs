namespace Reaparr.Data;

public class DownloadTaskPhotoConfiguration : IEntityTypeConfiguration<DownloadTaskPhotoImage>
{
    public void Configure(EntityTypeBuilder<DownloadTaskPhotoImage> builder)
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
