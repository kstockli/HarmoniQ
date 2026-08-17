namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Moderationsstatus einer öffentlichen Video-<see cref="Bewertung"/>. Kommentare werden bei der Abgabe
/// per KI gegen die Verhaltensregeln (Nutzungsbedingungen §3) geprüft: bestanden → sofort
/// <see cref="Freigegeben"/>; auffällig → <see cref="ZurPruefung"/> (erst nach Admin-Freigabe sichtbar).
/// Bewertungen ohne Kommentar sind immer freigegeben.
/// </summary>
public enum BewertungStatus
{
    Freigegeben = 0,
    ZurPruefung = 1
}
