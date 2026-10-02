namespace Reaparr.Data;

public class PlexMusicArtistConfiguration : IEntityTypeConfiguration<PlexMusicArtist>
{
    public void Configure(EntityTypeBuilder<PlexMusicArtist> builder)
    {
        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiRatingKey }).IsUnique();
        builder.Property(x => x.SearchTitle).UseCollation("NOCASE");
        builder.HasIndex(x => new { x.PlexLibraryId, x.SearchTitle });
        builder.HasIndex(x => new { x.PlexLibraryId, x.SortIndex });
        builder
            .HasOne(x => x.PlexLibrary)
            .WithMany(x => x.Artists)
            .HasForeignKey(x => x.PlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.PlexServer)
            .WithMany()
            .HasForeignKey(x => x.PlexServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
