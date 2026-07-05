namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Anlass eines Digest-Bausteins (Wiederkehr-Schleife, UX-Spec 4.2). Dient auch als Dedup-Schlüssel
/// im <see cref="BenachrichtigungGesendet"/>-Log (zusammen mit der EntitätsId).
/// </summary>
public enum BenachrichtigungTyp
{
    /// <summary>A: Kommendes Konzert einer Mitglied-/gefolgten Band.</summary>
    KommendesKonzert = 0,

    /// <summary>B: Nachfrage zu einem kürzlich vergangenen Konzert (Tagebuch füllen).</summary>
    TagebuchNachfrage = 1,

    /// <summary>C: Neues Video einer Mitglied-/gefolgten Band.</summary>
    NeuesVideo = 2,

    /// <summary>F: Kommendes Konzert (fremder Bands) in der Nähe des Heim-Standorts.</summary>
    NahesKonzert = 3,
}
