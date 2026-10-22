namespace Reaparr.Data;

public sealed class PlexPhotoMediaDataConfiguration : IEntityTypeConfiguration<PlexPhotoMediaData>
{
    public void Configure(EntityTypeBuilder<PlexPhotoMediaData> builder)
    {
        builder.Property(x => x.SearchTitle).UseCollation("NOCASE");
        builder.Property(x => x.Title).UseCollation(OrderByNaturalExtensions.CollationName);
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiRatingKey }).IsUnique();
        builder.HasIndex(x => new { x.PlexApiRatingKey, x.PlexServerId });
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiPartId }).IsUnique();
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexPhotoFolderId, x.Title, x.Id });
        builder.HasIndex(x => new { x.PlexLibraryId, x.Title, x.Id });
        builder.HasIndex(x => new { x.PlexLibraryId, x.SearchTitle });
        builder
            .HasOne(x => x.PlexPhotoFolder)
            .WithMany(x => x.Files)
            .HasForeignKey(x => x.PlexPhotoFolderId)
            .OnDelete(DeleteBehavior.SetNull);
        builder
            .HasOne(x => x.PlexLibrary)
            .WithMany(x => x.PhotoMediaData)
            .HasForeignKey(x => x.PlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.PlexServer)
            .WithMany()
            .HasForeignKey(x => x.PlexServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
