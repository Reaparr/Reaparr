namespace Reaparr.Data;

public sealed class PlexPhotoFolderConfiguration : IEntityTypeConfiguration<PlexPhotoFolder>
{
    public void Configure(EntityTypeBuilder<PlexPhotoFolder> builder)
    {
        builder.Property(x => x.RelativePath).UseCollation("BINARY");
        builder.Property(x => x.RootIdentityKey).UseCollation("BINARY");
        builder.Property(x => x.SourceRootPath).UseCollation("BINARY");
        builder.Property(x => x.Title).UseCollation(OrderByNaturalExtensions.CollationName);

        builder.HasIndex(x => new { x.PlexLibraryId, x.RootIdentityKey }).IsUnique().HasFilter("\"ParentId\" IS NULL");
        builder.HasIndex(x => new { x.ParentId, x.RelativePath }).IsUnique().HasFilter("\"ParentId\" IS NOT NULL");
        builder.HasIndex(x => new
        {
            x.PlexLibraryId,
            x.ParentId,
            x.Title,
            x.Id,
        });
        builder.HasAlternateKey(x => new { x.PlexLibraryId, x.Id });

        builder.ToTable(
            "PlexPhotoFolders",
            tableBuilder =>
                tableBuilder.HasCheckConstraint(
                    "CK_PlexPhotoFolders_RootShape",
                    """
                    (
                        "ParentId" IS NULL
                        AND "RelativePath" = ''
                        AND "RootIdentityKey" IS NOT NULL
                        AND "RootIdentityKey" <> ''
                        AND "SourceRootPath" IS NOT NULL
                        AND "SourceRootPath" <> ''
                        AND "IsActive" IS NOT NULL
                    )
                    OR
                    (
                        "ParentId" IS NOT NULL
                        AND "RelativePath" <> ''
                        AND "RootIdentityKey" IS NULL
                        AND "SourceRootPath" IS NULL
                        AND "PlexApiLocationId" IS NULL
                        AND "IsActive" IS NULL
                    )
                    """
                )
        );

        builder
            .HasOne(x => x.PlexLibrary)
            .WithMany(x => x.PhotoFolders)
            .HasForeignKey(x => x.PlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.PlexServer)
            .WithMany()
            .HasForeignKey(x => x.PlexServerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => new { x.PlexLibraryId, x.ParentId })
            .HasPrincipalKey(x => new { x.PlexLibraryId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
