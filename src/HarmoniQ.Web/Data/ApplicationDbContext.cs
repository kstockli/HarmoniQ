using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    /// <summary>Persistente DataProtection-Schlüssel (Cookies/Tokens überleben Neustart/Redeploy).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Stueck> Stuecke => Set<Stueck>();
    public DbSet<Band> Bands => Set<Band>();
    public DbSet<Video> Videos => Set<Video>();
    public DbSet<Bewertung> Bewertungen => Set<Bewertung>();

    // Phase 6 – Personen-/Rollen-Modell
    public DbSet<Person> Personen => Set<Person>();
    public DbSet<BandAlias> BandAliase => Set<BandAlias>();
    public DbSet<BandLink> BandLinks => Set<BandLink>();
    public DbSet<PersonRolle> PersonRollen => Set<PersonRolle>();
    public DbSet<PersonLink> PersonLinks => Set<PersonLink>();
    public DbSet<StueckBeitrag> StueckBeitraege => Set<StueckBeitrag>();
    public DbSet<StueckAlias> StueckAliase => Set<StueckAlias>();
    public DbSet<PersonAlias> PersonAliase => Set<PersonAlias>();
    public DbSet<Instrument> Instrumente => Set<Instrument>();
    public DbSet<Stimme> Stimmen => Set<Stimme>();
    public DbSet<PersonInstrument> PersonInstrumente => Set<PersonInstrument>();
    public DbSet<VideoMitwirkung> VideoMitwirkungen => Set<VideoMitwirkung>();
    public DbSet<Richtigstellung> Richtigstellungen => Set<Richtigstellung>();
    public DbSet<BandMitgliedschaft> BandMitgliedschaften => Set<BandMitgliedschaft>();
    public DbSet<PersonAnspruch> PersonAnsprueche => Set<PersonAnspruch>();
    public DbSet<BandbeitrittAntrag> BandbeitrittAntraege => Set<BandbeitrittAntrag>();

    // Phase 8 – Vernetzung & Konzerte
    public DbSet<Konzert> Konzerte => Set<Konzert>();
    public DbSet<KonzertBand> KonzertBands => Set<KonzertBand>();
    public DbSet<KonzertStueck> KonzertStuecke => Set<KonzertStueck>();
    public DbSet<KonzertPerson> KonzertPersonen => Set<KonzertPerson>();
    public DbSet<Freundschaft> Freundschaften => Set<Freundschaft>();
    public DbSet<Aktivitaet> Aktivitaeten => Set<Aktivitaet>();

    // Crawler / Import-Roboter (Spezifikation-Crawler.md §5) – isoliert vom Kernmodell.
    public DbSet<CrawlQuelle> CrawlQuellen => Set<CrawlQuelle>();
    public DbSet<CrawlLauf> CrawlLaeufe => Set<CrawlLauf>();
    public DbSet<CrawlFund> CrawlFunde => Set<CrawlFund>();
    public DbSet<CrawlSeite> CrawlSeiten => Set<CrawlSeite>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Bewertung>(e =>
        {
            e.HasIndex(b => new { b.VideoId, b.BenutzerId })
                .IsUnique()
                .HasFilter("\"BenutzerId\" IS NOT NULL");

            e.HasIndex(b => new { b.VideoId, b.AnonymerCookieId })
                .IsUnique()
                .HasFilter("\"AnonymerCookieId\" IS NOT NULL");

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

            // Optionaler Konzertbezug: Konzert löschen lässt Videos bestehen (KonzertId → null).
            e.HasOne(v => v.Konzert)
                .WithMany(k => k.Videos)
                .HasForeignKey(v => v.KonzertId)
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
        // Filter „alle Komponist:innen" (Startseite/Listen) sucht über die Rolle allein – der PK
        // beginnt mit PersonId, deckt das nicht ab. Separater Index auf Rolle.
        builder.Entity<PersonRolle>().HasIndex(r => r.Rolle);

        // Band-Aliase (alternative Namen) + Band-Links (analog PersonLink).
        builder.Entity<BandAlias>(e =>
        {
            e.HasOne(a => a.Band).WithMany(b => b.Aliase)
                .HasForeignKey(a => a.BandId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.BandId, a.Name }).IsUnique();
            // Wie BandLink/PersonLink: clientseitiger Guid-Key → ValueGeneratedNever, sonst hält EF
            // einen via Navigation an eine getrackte Band gehängten Alias für „existierend" → UPDATE
            // statt INSERT → „0 rows affected"-Concurrency-Fehler beim Anreichern.
            e.Property(a => a.Id).ValueGeneratedNever();
        });
        builder.Entity<BandLink>(e =>
        {
            e.HasOne(l => l.Band).WithMany(b => b.Links)
                .HasForeignKey(l => l.BandId).OnDelete(DeleteBehavior.Cascade);
            // Wie PersonLink: clientseitiger Guid-Key → ValueGeneratedNever, sonst UPDATE statt INSERT.
            e.Property(l => l.Id).ValueGeneratedNever();
        });

        builder.Entity<PersonLink>(e =>
        {
            e.HasOne(l => l.Person).WithMany(p => p.Links)
                .HasForeignKey(l => l.PersonId).OnDelete(DeleteBehavior.Cascade);
            // Schlüssel wird clientseitig vergeben (= Guid.NewGuid()). Ohne ValueGeneratedNever
            // hält EF einen NUR über die Navigation (Person.Links.Add via Komfort-Setter) an eine
            // bereits getrackte Person gehängten Link für „existierend" → UPDATE statt INSERT →
            // „0 rows affected"-Concurrency-Fehler beim Speichern.
            e.Property(l => l.Id).ValueGeneratedNever();
        });

        // Stück-Aliase (alternative Titel) – analog BandAlias.
        builder.Entity<StueckAlias>(e =>
        {
            e.HasOne(a => a.Stueck).WithMany(s => s.Aliase)
                .HasForeignKey(a => a.StueckId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.StueckId, a.Name }).IsUnique();
            // Clientseitiger Guid-Key → ValueGeneratedNever (siehe BandAlias/BandLink): sonst hält EF
            // einen via Navigation angehängten Alias für „existierend" → „0 rows affected".
            e.Property(a => a.Id).ValueGeneratedNever();
        });

        // Person-Aliase (alternative Namen) – analog StueckAlias.
        builder.Entity<PersonAlias>(e =>
        {
            e.HasOne(a => a.Person).WithMany(p => p.Aliase)
                .HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.PersonId, a.Name }).IsUnique();
            e.Property(a => a.Id).ValueGeneratedNever();
        });

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

        // BandMitgliedschaft (Person ↔ Band über die Zeit)
        builder.Entity<BandMitgliedschaft>(e =>
        {
            e.HasOne(m => m.Band).WithMany(b => b.Mitgliedschaften)
                .HasForeignKey(m => m.BandId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Person).WithMany(p => p.Bandmitgliedschaften)
                .HasForeignKey(m => m.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Instrument).WithMany()
                .HasForeignKey(m => m.InstrumentId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(m => new { m.BandId, m.PersonId });
        });

        builder.Entity<PersonAnspruch>(e =>
        {
            e.HasOne(a => a.Person).WithMany()
                .HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Benutzer).WithMany()
                .HasForeignKey(a => a.BenutzerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => a.Status);
            // Pro Benutzer nur ein offener Antrag je Person.
            e.HasIndex(a => new { a.PersonId, a.BenutzerId, a.Status });
            // BenutzerId ist FK → bereits per Konvention indiziert (deckt den Onboarding-Check
            // „hat dieser Benutzer einen offenen Antrag?" ab).
        });

        builder.Entity<BandbeitrittAntrag>(e =>
        {
            e.HasOne(a => a.Person).WithMany()
                .HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Band).WithMany()
                .HasForeignKey(a => a.BandId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Instrument).WithMany()
                .HasForeignKey(a => a.InstrumentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(a => a.BeantragtVon).WithMany()
                .HasForeignKey(a => a.BeantragtVonId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(a => a.Status);
        });

        builder.Entity<Richtigstellung>(e =>
        {
            e.HasOne(r => r.EingereichtVon).WithMany()
                .HasForeignKey(r => r.EingereichtVonId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(r => new { r.BetrifftTyp, r.BetrifftId });
            e.HasIndex(r => r.Status);
        });

        // ─── Phase 8 – Vernetzung & Konzerte ───────────────────────────────────

        // KonzertBand (n:m), zusammengesetzter Schlüssel
        builder.Entity<KonzertBand>().HasKey(kb => new { kb.KonzertId, kb.BandId });
        builder.Entity<KonzertBand>(e =>
        {
            e.HasOne(kb => kb.Konzert).WithMany(k => k.Bands)
                .HasForeignKey(kb => kb.KonzertId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(kb => kb.Band).WithMany(b => b.Konzertteilnahmen)
                .HasForeignKey(kb => kb.BandId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Konzert>().HasIndex(k => k.Datum);

        // KonzertStueck (Programm): n:m Konzert↔Stück, optionale Band je Programmpunkt.
        builder.Entity<KonzertStueck>(e =>
        {
            e.HasOne(ks => ks.Konzert).WithMany(k => k.Programm)
                .HasForeignKey(ks => ks.KonzertId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ks => ks.Stueck).WithMany()
                .HasForeignKey(ks => ks.StueckId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ks => ks.Band).WithMany()
                .HasForeignKey(ks => ks.BandId).OnDelete(DeleteBehavior.SetNull);
            // Dasselbe Stück nicht doppelt für dieselbe Band am selben Konzert.
            e.HasIndex(ks => new { ks.KonzertId, ks.StueckId, ks.BandId }).IsUnique();
            // StueckId ist FK → bereits per Konvention indiziert (deckt „kommt Stück X in einem
            // Programm vor?" ab); kein zusätzlicher Index nötig.
        });

        // KonzertPerson: n:m Konzert↔Person mit Rolle.
        builder.Entity<KonzertPerson>(e =>
        {
            e.HasOne(kp => kp.Konzert).WithMany(k => k.Mitwirkende)
                .HasForeignKey(kp => kp.KonzertId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(kp => kp.Person).WithMany()
                .HasForeignKey(kp => kp.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(kp => kp.Band).WithMany()
                .HasForeignKey(kp => kp.BandId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(kp => new { kp.KonzertId, kp.PersonId, kp.Rolle }).IsUnique();
        });

        // Freundschaft: zwei FKs auf Person. Restrict, um doppelte Kaskadenpfade auf dieselbe
        // Tabelle zu vermeiden (eine gelöschte Person wird vorher aus Freundschaften entfernt).
        builder.Entity<Freundschaft>(e =>
        {
            e.HasOne(f => f.AnfragerPerson).WithMany()
                .HasForeignKey(f => f.AnfragerPersonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.EmpfaengerPerson).WithMany()
                .HasForeignKey(f => f.EmpfaengerPersonId).OnDelete(DeleteBehavior.Restrict);
            // Höchstens eine Verbindung je gerichtetem Paar.
            e.HasIndex(f => new { f.AnfragerPersonId, f.EmpfaengerPersonId }).IsUnique();
            e.HasIndex(f => f.Status);
            // EmpfaengerPersonId ist FK → bereits per Konvention indiziert (deckt „offene Anfragen
            // an mich" ab).
        });

        // Aktivitaet: Feed-Ereignis. Index auf Zeitpunkt (Feed nach Datum absteigend).
        builder.Entity<Aktivitaet>(e =>
        {
            e.HasOne(a => a.AkteurPerson).WithMany()
                .HasForeignKey(a => a.AkteurPersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.NebenPerson).WithMany()
                .HasForeignKey(a => a.NebenPersonId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(a => a.Zeitpunkt);
            e.HasIndex(a => a.AkteurPersonId);
        });

        // ─── Crawler / Import-Roboter ──────────────────────────────────────────

        builder.Entity<CrawlQuelle>(e =>
        {
            // Optionale Ziel-Band; Band löschen lässt die Quelle bestehen (BandId → null).
            e.HasOne(q => q.Band).WithMany()
                .HasForeignKey(q => q.BandId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(q => q.Aktiv);
        });

        builder.Entity<CrawlLauf>(e =>
        {
            e.HasOne(l => l.Quelle).WithMany(q => q.Laeufe)
                .HasForeignKey(l => l.QuelleId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(l => l.Status);
            e.HasIndex(l => l.StartAm);
        });

        builder.Entity<CrawlFund>(e =>
        {
            // LaufId optional (Anreicherungs-Funde haben keinen Lauf); Lauf-Löschung kaskadiert auf seine Funde.
            e.HasOne(f => f.Lauf).WithMany(l => l.Funde)
                .HasForeignKey(f => f.LaufId).OnDelete(DeleteBehavior.Cascade).IsRequired(false);
            e.HasIndex(f => f.Status);
            e.HasIndex(f => f.Typ);
        });

        builder.Entity<CrawlSeite>(e =>
        {
            e.HasOne(s => s.Quelle).WithMany()
                .HasForeignKey(s => s.QuelleId).OnDelete(DeleteBehavior.Cascade);
            // Dedup/Politeness: je Quelle jede URL nur einmal.
            e.HasIndex(s => new { s.QuelleId, s.Url }).IsUnique();
        });
    }
}
