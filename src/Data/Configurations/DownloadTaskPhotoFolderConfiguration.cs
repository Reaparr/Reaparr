namespace Reaparr.Data;

public sealed class DownloadTaskPhotoFolderConfiguration : IEntityTypeConfiguration<DownloadTaskPhotoFolder>
{
    public void Configure(EntityTypeBuilder<DownloadTaskPhotoFolder> builder)
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
        builder.Property(x => x.RelativePath).UseCollation("BINARY");
        builder.Property(x => x.DownloadRootPath).UseCollation("BINARY");
        builder.Property(x => x.DestinationRootPath).UseCollation("BINARY");

        builder
            .HasIndex(x => new
            {
                x.PlexServerId,
                x.PlexLibraryId,
                x.DownloadRootPath,
                x.DestinationRootPath,
            })
            .IsUnique()
            .HasFilter("\"ParentId\" IS NULL");
        builder.HasIndex(x => new { x.ParentId, x.RelativePath }).IsUnique().HasFilter("\"ParentId\" IS NOT NULL");
        builder.HasIndex(x => x.ParentId);
        builder.ToTable("DownloadTaskPhotoFolders", tableBuilder => tableBuilder.HasCheckConstraint(
                "CK_DownloadTaskPhotoFolders_RootShape",
                """
                ("ParentId" IS NULL AND "RelativePath" = '')
                OR
                ("ParentId" IS NOT NULL AND "RelativePath" <> '')
                """
            ));

        builder
            .HasMany(x => x.Children)
            .WithOne(x => x.Parent)
            .HasForeignKey(x => x.ParentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasMany(x => x.Files)
            .WithOne(x => x.Parent)
            .HasForeignKey(x => x.ParentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
