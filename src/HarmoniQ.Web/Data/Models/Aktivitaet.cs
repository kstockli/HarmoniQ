namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Append-only Feed-Ereignis. Entweder ein automatisch erzeugtes System-Ereignis
/// (Bewertung, Video, Freundschaft, Mitwirkung) oder ein selbst geschriebener
/// Freitext-Beitrag (<see cref="AktivitaetTyp.Beitrag"/>) an Freund:innen/Bandkolleg:innen.
/// Der Feed zeigt Aktivitäten von Personen, die der Betrachter sehen darf, nach
/// <see cref="Zeitpunkt"/> absteigend.
/// </summary>
public class Aktivitaet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Wer die Aktivität ausgelöst / den Beitrag geschrieben hat.</summary>
    public Guid AkteurPersonId { get; set; }
    public Person AkteurPerson { get; set; } = null!;

    public AktivitaetTyp Typ { get; set; }

    /// <summary>Freitext; Pflicht bei <see cref="AktivitaetTyp.Beitrag"/>, sonst optionale Notiz.</summary>
    public string? Text { get; set; }

    /// <summary>Lose Referenz auf das betroffene Objekt (null bei reinem Beitrag ohne Bezug).</summary>
    public AktivitaetZielTyp? ZielTyp { get; set; }
    public Guid? ZielId { get; set; }

    /// <summary>Optionale zweite Person, z. B. die neue Freundin bei FreundschaftBestaetigt.</summary>
    public Guid? NebenPersonId { get; set; }
    public Person? NebenPerson { get; set; }

    public DateTime Zeitpunkt { get; set; } = DateTime.UtcNow;
}

public enum AktivitaetTyp
{
    /// <summary>Selbst geschriebener Freitext-Beitrag.</summary>
    Beitrag = 0,
    BewertungAbgegeben = 1,
    VideoHinzugefuegt = 2,
    FreundschaftBestaetigt = 3,
    MitwirkungHinzugefuegt = 4
}

public enum AktivitaetZielTyp
{
    Video = 0,
    Person = 1,
    Band = 2,
    Konzert = 3,
    Stueck = 4
}
