namespace Reaparr.Data;

public class DownloadTaskPhotoFileConfiguration : IEntityTypeConfiguration<DownloadTaskPhotoImageFile>
{
    public void Configure(EntityTypeBuilder<DownloadTaskPhotoImageFile> builder)
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
        builder.Property(x => x.DirectoryMeta).HasJsonConversion().IsUnicode();
        builder.Property(x => x.DirectDownloadSnapshot).HasJsonConversion().IsUnicode();

        builder
            .Property(x => x.DownloadClientType)
            .HasMaxLength(10)
            .HasConversion(x => x.ToPlexDownloadClientTypeString(), x => x.ToPlexDownloadClientType())
            .HasDefaultValue(PlexDownloadClientType.Direct)
            .IsUnicode(false);
    }
}
