namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Persönliches „Ich war dabei" eines eingeloggten Users zu einem <see cref="Konzert"/> –
/// das Konzert-Tagebuch (Leit-Feature, UX-Spec Block 4). <b>Privat by default</b>: bewusst
/// getrennt von der öffentlich kuratierten Zuhörer:in-Verknüpfung (<see cref="KonzertPerson"/>),
/// weil der Besuch privat ist und über Jahre wächst. Ein Eintrag pro (User, Konzert).
/// Die stück-genauen Live-Eindrücke hängen als <see cref="StueckEindruck"/> daran.
/// </summary>
public class KonzertBesuch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid KonzertId { get; set; }
    public Konzert Konzert { get; set; } = null!;

    /// <summary>Eingeloggte:r Nutzer:in (AspNet-Identity-Id). Tagebuch erfordert Login.</summary>
    public string BenutzerId { get; set; } = null!;
    public ApplicationUser? Benutzer { get; set; }

    /// <summary>Optionale private Gesamt-Notiz zum Konzertbesuch (auch im Voraus erfassbar).</summary>
    public string? Notiz { get; set; }

    /// <summary>
    /// Ein Regler pro Konzert-Eintrag, der den gesamten Eintrag regelt (Anwesenheit + Konzert-Notiz +
    /// alle <see cref="StueckEindruck"/> dieses Konzerts – diese <b>erben</b> die Stufe).
    /// </summary>
    public TagebuchSichtbarkeit Sichtbarkeit { get; set; } = TagebuchSichtbarkeit.FreundeAnwesenheit;

    public DateTime ErstelltAm { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Wie weit ein Tagebuch-Eintrag sichtbar ist. Gestufter Einzelregler; der Anwesenheits-„Boden"
/// (Freunde sehen, dass ich dabei war) ist bewusst eingebaut. Aggregiert-öffentliches Rating
/// ist NICHT Teil von v1 – „Öffentlich" zeigt den Eintrag als solchen (moderierbar).
/// </summary>
public enum TagebuchSichtbarkeit
{
    /// <summary>Nichts geteilt – auch nicht die Anwesenheit („privat privat").</summary>
    NurIch = 0,
    /// <summary>Freunde sehen nur, DASS ich dabei war; Notiz + Bewertungen bleiben privat. (Default)</summary>
    FreundeAnwesenheit = 1,
    /// <summary>Freunde sehen den ganzen Eintrag (Anwesenheit + Notiz + Stück-Bewertungen).</summary>
    Freunde = 2,
    /// <summary>Öffentlich sichtbar (Admin kann anstössige Einträge moderieren/löschen).</summary>
    Oeffentlich = 3
}
