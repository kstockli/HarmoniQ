namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Rendert eine Seite mit ausgeführtem JavaScript (für SPA/Wix-Seiten, Spec §4.1/C2) und gibt das
/// resultierende HTML zurück. <c>null</c> = nicht verfügbar/fehlgeschlagen → Aufrufer fällt auf
/// einfachen HTTP-Abruf zurück.
/// </summary>
public interface ISeitenRenderer
{
    Task<string?> RenderAsync(string url, CancellationToken ct = default);

    /// <summary>Rendert die Seite und sammelt (a) die JSON-Antwortkörper aller Netzwerk-Antworten, deren URL
    /// <paramref name="apiUrlEnthaelt"/> enthält (z. B. vivenu-Events beim KKL), und (b) die <c>href</c>s aller
    /// Links, die <paramref name="linkEnthaelt"/> enthalten (z. B. „/events/" für die echten KKL-Detail-Slugs;
    /// null = keine Links sammeln). Spec §4.3. Leere Sammlung, wenn nichts erfasst / Browser nicht verfügbar.</summary>
    Task<GerenderteSammlung> RenderUndSammleAsync(string url, string apiUrlEnthaelt, string? linkEnthaelt = null, CancellationToken ct = default);

    /// <summary>Rendert eine Detailseite, akzeptiert den Cookie-Banner und klickt nacheinander die
    /// angegebenen Tabs/Reiter an, um deren (sonst verborgenen) Inhalt sichtbar zu machen. Gibt je Tab
    /// den danach sichtbaren Seitentext (<c>body innerText</c>) zurück (z. B. KKL „Programm"/„Mitwirkende",
    /// Spec §4.3). Leeres Dictionary, wenn der Browser nicht verfügbar ist.</summary>
    Task<IReadOnlyDictionary<string, string>> RenderUndTabsAsync(
        string url, IReadOnlyList<string> tabBeschriftungen, CancellationToken ct = default);
}

/// <summary>Ergebnis von <see cref="ISeitenRenderer.RenderUndSammleAsync"/>: erfasste API-Antwortkörper und
/// die gefundenen Link-<c>href</c>s (für die Slug-Zuordnung, z. B. KKL-Detailseiten).</summary>
public record GerenderteSammlung(IReadOnlyList<string> ApiKoerper, IReadOnlyList<string> Links)
{
    public static readonly GerenderteSammlung Leer = new([], []);
}
