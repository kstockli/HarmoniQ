using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Adress-Geocoding über <b>Nominatim/OpenStreetMap</b> (gratis, kein API-Key). Wandelt einen
/// Adress-/Ortstext oder eine PLZ in Koordinaten um – für die Koordinaten-Erfassung im Lokal-CRUD,
/// den Admin-Batch und den PLZ-Bezugspunkt des Distanz-Filters. Nominatim verlangt einen
/// aussagekräftigen User-Agent (aus <c>Crawler:UserAgent</c>) und max. ~1 Anfrage/Sekunde.
/// </summary>
public class GeocodingService(HttpClient http)
{
    public record Treffer(double Lat, double Lng, string? Kanton);

    /// <summary>Freitext-Adresse/Name → Koordinaten (ohne Kanton).</summary>
    public async Task<(double Lat, double Lng)?> GeocodeAsync(string? query, CancellationToken ct = default)
    {
        var t = await AbfrageAsync("format=jsonv2&limit=1&q=" + Uri.EscapeDataString(query ?? ""), query, ct);
        return t is null ? null : (t.Lat, t.Lng);
    }

    /// <summary>Freitext-Adresse/Name → Koordinaten inkl. Kanton (address.state) für den Batch.</summary>
    public Task<Treffer?> GeocodeDetailAsync(string? query, CancellationToken ct = default)
        => AbfrageAsync("format=jsonv2&addressdetails=1&limit=1&q=" + Uri.EscapeDataString(query ?? ""), query, ct);

    /// <summary>Schweizer PLZ → Koordinaten (Bezugspunkt für den Distanz-Filter).</summary>
    public async Task<(double Lat, double Lng)?> GeocodePlzAsync(string? plz, CancellationToken ct = default)
    {
        plz = plz?.Trim();
        if (string.IsNullOrWhiteSpace(plz)) return null;
        var t = await AbfrageAsync("format=jsonv2&limit=1&countrycodes=ch&postalcode=" + Uri.EscapeDataString(plz), plz, ct);
        return t is null ? null : (t.Lat, t.Lng);
    }

    /// <summary>
    /// Ermittelt Koordinaten aus freiem Text ODER einem Google-Maps-Link (inkl. Kurzlink
    /// <c>maps.app.goo.gl</c>/<c>goo.gl/maps</c>): erst direktes Parsen, sonst der Weiterleitung folgen
    /// und die Ziel-URL/den Seiteninhalt parsen.
    /// </summary>
    public async Task<(double Lat, double Lng)?> KoordinatenAusLinkAsync(string? input, CancellationToken ct = default)
    {
        input = input?.Trim();
        if (string.IsNullOrWhiteSpace(input)) return null;

        // 1. Direkt parsen (fertige URL mit @…/!3d!4d oder „lat, lng").
        if (ParseKoord(input) is { } direkt) return direkt;

        // 2. Kurzlink auflösen (HttpClient folgt Weiterleitungen automatisch).
        if (input.Contains("goo.gl", StringComparison.OrdinalIgnoreCase)
            || input.Contains("maps.app", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var resp = await http.GetAsync(input, ct);
                if (resp.RequestMessage?.RequestUri?.ToString() is { } ziel && ParseKoord(ziel) is { } p1)
                    return p1;
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (ParseKoord(body) is { } p2) return p2;
            }
            catch { /* nicht auflösbar */ }
        }
        return null;
    }

    /// <summary>Extrahiert Koordinaten aus Google-Maps-URLs/Text (!3d!4d, @lat,lng, „lat, lng").</summary>
    public static (double Lat, double Lng)? ParseKoord(string s)
    {
        foreach (var pat in new[]
        {
            @"!3d(-?\d+\.?\d*)!4d(-?\d+\.?\d*)",
            @"@(-?\d+\.\d+),(-?\d+\.\d+)",
            @"[?&]q=(-?\d+\.\d+),(-?\d+\.\d+)",
            @"(-?\d+\.\d+)\s*,\s*(-?\d+\.\d+)"
        })
        {
            var m = Regex.Match(s, pat);
            if (m.Success
                && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var la)
                && double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lo)
                && Math.Abs(la) <= 90 && Math.Abs(lo) <= 180)
                return (la, lo);
        }
        return null;
    }

    private async Task<Treffer?> AbfrageAsync(string queryString, string? original, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(original)) return null;
        try
        {
            using var resp = await http.GetAsync("https://nominatim.openstreetmap.org/search?" + queryString, ct);
            if (!resp.IsSuccessStatusCode) return null;
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            var first = doc.RootElement[0];
            if (!double.TryParse(first.GetProperty("lat").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                || !double.TryParse(first.GetProperty("lon").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
                return null;

            string? kanton = null;
            if (first.TryGetProperty("address", out var addr) && addr.TryGetProperty("state", out var state))
                kanton = state.GetString();
            return new Treffer(lat, lng, kanton);
        }
        catch { return null; }
    }
}
