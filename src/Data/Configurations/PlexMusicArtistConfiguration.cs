namespace Reaparr.Data;

public class PlexMusicArtistConfiguration : IEntityTypeConfiguration<PlexMusicArtist>
{
    public void Configure(EntityTypeBuilder<PlexMusicArtist> builder)
    {
        builder.Ignore(x => x.VideoQuality);

        builder.HasIndex(x => new { x.PlexLibraryId, x.PlexApiRatingKey }).IsUnique();
        builder.HasIndex(x => new { x.PlexApiRatingKey, x.PlexServerId });
        builder.Property(x => x.SearchTitle).UseCollation("NOCASE");
        builder.HasIndex(x => new { x.PlexLibraryId, x.SearchTitle });
        builder.Property(x => x.MusicBrainzArtistId).UseCollation("BINARY");
        builder.HasIndex(x => new { x.PlexLibraryId, x.MusicBrainzArtistId });
        builder.Property(x => x.PlexApiIdentityId).UseCollation("BINARY");
        builder.HasIndex(x => new { x.PlexLibraryId, PlexIdentityId = x.PlexApiIdentityId });
        builder.HasIndex(x => new { x.PlexLibraryId, x.SortIndex });
        builder.HasAlternateKey(x => new { x.PlexLibraryId, x.Id });

        builder
            .HasOne(x => x.PlexLibrary)
            .WithMany(x => x.Music)
            .HasForeignKey(x => x.PlexLibraryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.PlexServer)
            .WithMany()
            .HasForeignKey(x => x.PlexServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.Actors)
            .WithMany(x => x.PlexMusicArtistActors)
            .UsingEntity<PlexMusicArtistActors>(
                l => l.HasOne<PlexActor>().WithMany().HasForeignKey(e => e.PlexActorId),
                r => r.HasOne<PlexMusicArtist>().WithMany().HasForeignKey(e => e.PlexMusicArtistId)
            );

        builder
            .HasMany(x => x.Genres)
            .WithMany(x => x.PlexMusicArtistGenres)
            .UsingEntity<PlexMusicArtistGenres>(
                l => l.HasOne<PlexGenreAudio>().WithMany().HasForeignKey(e => e.GenresId),
                r => r.HasOne<PlexMusicArtist>().WithMany().HasForeignKey(e => e.PlexMusicArtistId)
            );

        builder
            .HasMany(x => x.Countries)
            .WithMany(x => x.PlexMusicArtistCountries)
            .UsingEntity<PlexMusicArtistCountries>(
                l => l.HasOne<PlexCountry>().WithMany().HasForeignKey(e => e.CountryId),
                r => r.HasOne<PlexMusicArtist>().WithMany().HasForeignKey(e => e.PlexMusicArtistId)
            );
    }
}
