namespace HarmoniQ.Web.Data.Models;

/// <summary>Datenschutz-Stufe: wie viel von einer Person öffentlich gezeigt wird.</summary>
public enum Sichtbarkeit
{
    Oeffentlich = 0,   // voller Name (Default für Komponist:in / Dirigent:in)
    NurInitialen = 1,  // nur Initialen, z. B. "K. S." (Default für Musikant:in)
    NichtBekannt = 2   // anonym, Anzeige als "?"
}
