using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Stueck> Stuecke => Set<Stueck>();
    public DbSet<Band> Bands => Set<Band>();
    public DbSet<Video> Videos => Set<Video>();
    public DbSet<Bewertung> Bewertungen => Set<Bewertung>();

    // Phase 6 – Personen-/Rollen-Modell
    public DbSet<Person> Personen => Set<Person>();
    public DbSet<PersonRolle> PersonRollen => Set<PersonRolle>();
    public DbSet<PersonLink> PersonLinks => Set<PersonLink>();
    public DbSet<StueckBeitrag> StueckBeitraege => Set<StueckBeitrag>();
    public DbSet<Instrument> Instrumente => Set<Instrument>();
    public DbSet<Stimme> Stimmen => Set<Stimme>();
    public DbSet<PersonInstrument> PersonInstrumente => Set<PersonInstrument>();
    public DbSet<VideoMitwirkung> VideoMitwirkungen => Set<VideoMitwirkung>();
    public DbSet<Richtigstellung> Richtigstellungen => Set<Richtigstellung>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Bewertung>(e =>
        {
            e.HasIndex(b => new { b.VideoId, b.BenutzerId })
                .IsUnique()
                .HasFilter("[BenutzerId] IS NOT NULL");

            e.HasIndex(b => new { b.VideoId, b.AnonymerCookieId })
                .IsUnique()
                .HasFilter("[AnonymerCookieId] IS NOT NULL");

            e.Property(b => b.GesamtEindruck).HasColumnType("INTEGER");
            e.Property(b => b.Praezision).HasColumnType("INTEGER");
            e.Property(b => b.Musikalitaet).HasColumnType("INTEGER");
            e.Property(b => b.AkustischeQualitaet).HasColumnType("INTEGER");
            e.Property(b => b.VideoQualitaet).HasColumnType("INTEGER");
        });

        builder.Entity<Video>(e =>
        {
            e.HasOne(v => v.VorgeschlagenVon)
                .WithMany()
                .HasForeignKey(v => v.VorgeschlagenVonId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Person>(e =>
        {
            // Optionale, eindeutige Verknüpfung Person ↔ Benutzerkonto ("das bin ich").
            // SQLite behandelt mehrere NULLs als verschieden → viele Personen ohne Konto erlaubt.
            e.HasIndex(p => p.BenutzerId).IsUnique();
            e.HasOne(p => p.Benutzer)
                .WithMany()
                .HasForeignKey(p => p.BenutzerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // PersonRolle: zusammengesetzter Schlüssel (eine Rolle nur einmal je Person).
        builder.Entity<PersonRolle>().HasKey(r => new { r.PersonId, r.Rolle });
        builder.Entity<PersonRolle>()
            .HasOne(r => r.Person).WithMany(p => p.Rollen)
            .HasForeignKey(r => r.PersonId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PersonLink>()
            .HasOne(l => l.Person).WithMany(p => p.Links)
            .HasForeignKey(l => l.PersonId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<StueckBeitrag>(e =>
        {
            e.HasOne(b => b.Stueck).WithMany(s => s.Beitraege)
                .HasForeignKey(b => b.StueckId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(b => b.Person).WithMany(p => p.StueckBeitraege)
                .HasForeignKey(b => b.PersonId).OnDelete(DeleteBehavior.Cascade);
            // Dieselbe Person nicht mehrfach in derselben Rolle am selben Stück.
            e.HasIndex(b => new { b.StueckId, b.PersonId, b.Rolle }).IsUnique();
        });

        // Instrument / Stimme (Nachschlage-Tabellen)
        builder.Entity<Instrument>().HasIndex(i => i.Name).IsUnique();
        builder.Entity<Stimme>(e =>
        {
            e.HasOne(s => s.Instrument).WithMany(i => i.Stimmen)
                .HasForeignKey(s => s.InstrumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.InstrumentId, s.Bezeichnung }).IsUnique();
        });

        // PersonInstrument (n:m), zusammengesetzter Schlüssel
        builder.Entity<PersonInstrument>().HasKey(pi => new { pi.PersonId, pi.InstrumentId });
        builder.Entity<PersonInstrument>(e =>
        {
            e.HasOne(pi => pi.Person).WithMany(p => p.Instrumente)
                .HasForeignKey(pi => pi.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pi => pi.Instrument).WithMany()
                .HasForeignKey(pi => pi.InstrumentId).OnDelete(DeleteBehavior.Cascade);
        });

        // VideoMitwirkung (Besetzungsliste)
        builder.Entity<VideoMitwirkung>(e =>
        {
            e.HasOne(m => m.Video).WithMany(v => v.Mitwirkungen)
                .HasForeignKey(m => m.VideoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Person).WithMany(p => p.Mitwirkungen)
                .HasForeignKey(m => m.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Instrument).WithMany()
                .HasForeignKey(m => m.InstrumentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(m => m.Stimme).WithMany()
                .HasForeignKey(m => m.StimmeId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(m => m.VorgeschlagenVon).WithMany()
                .HasForeignKey(m => m.VorgeschlagenVonId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Richtigstellung>(e =>
        {
            e.HasOne(r => r.EingereichtVon).WithMany()
                .HasForeignKey(r => r.EingereichtVonId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(r => new { r.BetrifftTyp, r.BetrifftId });
            e.HasIndex(r => r.Status);
        });
    }
}
