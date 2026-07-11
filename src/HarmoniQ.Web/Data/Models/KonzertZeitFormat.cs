using System.Globalization;

namespace HarmoniQ.Web.Data.Models;

/// <summary>
/// Einheitliche Anzeige von Konzert-Datum plus optionaler <see cref="Konzert.Uhrzeit"/>.
/// Damit die Zeit „durchgängig" gleich dargestellt wird (Startseite, Listen, Detail, Digest …).
/// </summary>
public static class KonzertZeitFormat
{
    /// <summary>Zeit als „20:00" (24h, kulturunabhängig).</summary>
    public static string Zeit(TimeOnly u) => u.ToString("HH\\:mm", CultureInfo.InvariantCulture);

    /// <summary>Kompakt: „15.09.2026" bzw. mit Zeit „15.09.2026 · 20:00".</summary>
    public static string Kompakt(DateOnly datum, TimeOnly? uhrzeit)
        => datum.ToString("dd.MM.yyyy") + (uhrzeit is TimeOnly u ? " · " + Zeit(u) : "");

    /// <summary>Nur der Zeit-Zusatz zum Anhängen an einen bestehenden Datumstext:
    /// „ · 20:00 Uhr" (Prosa) bzw. „ · 20:00" (<paramref name="mitUhr"/> = false); „" ohne Zeit.</summary>
    public static string ZeitZusatz(TimeOnly? uhrzeit, bool mitUhr = true)
        => uhrzeit is TimeOnly u ? " · " + Zeit(u) + (mitUhr ? " Uhr" : "") : "";
}
