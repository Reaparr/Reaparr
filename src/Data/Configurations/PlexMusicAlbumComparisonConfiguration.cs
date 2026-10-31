namespace Reaparr.Data;

public class PlexMusicAlbumComparisonConfiguration : IEntityTypeConfiguration<PlexMusicAlbumComparison>
{
    public void Configure(EntityTypeBuilder<PlexMusicAlbumComparison> builder)
    {
        builder.HasIndex(x => new { x.OwnedPlexLibraryId, x.OwnedPlexMediaId });
        builder
            .HasIndex(x => new
            {
                x.RemotePlexLibraryId,
                x.OwnedPlexLibraryId,
                x.RemotePlexMediaId,
                x.OwnedPlexMediaId,
            })
            .IsUnique();
        builder
            .HasOne<PlexLibrary>()
            .WithMany()
            .HasForeignKey(x => x.RemotePlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<PlexLibrary>()
            .WithMany()
            .HasForeignKey(x => x.OwnedPlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<PlexMusicAlbum>()
            .WithMany()
            .HasForeignKey(x => new { x.RemotePlexLibraryId, x.RemotePlexMediaId })
            .HasPrincipalKey(x => new { x.PlexLibraryId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne<PlexMusicAlbum>()
            .WithMany()
            .HasForeignKey(x => new { x.OwnedPlexLibraryId, x.OwnedPlexMediaId })
            .HasPrincipalKey(x => new { x.PlexLibraryId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
