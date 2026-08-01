using MudBlazor;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Components.Shared;

/// <summary>Gemeinsame Darstellungs-Hilfen für Schwierigkeitsgrade.</summary>
public static class Darstellung
{
    public static string GradText(Schwierigkeitsgrad g) => g switch
    {
        Schwierigkeitsgrad.Leicht     => "Leicht",
        Schwierigkeitsgrad.Mittel     => "Mittel",
        Schwierigkeitsgrad.Schwer     => "Schwer",
        Schwierigkeitsgrad.SehrSchwer => "Sehr schwer",
        _                             => "Unbekannt"
    };

    public static Color GradColor(Schwierigkeitsgrad g) => g switch
    {
        Schwierigkeitsgrad.Leicht     => Color.Success,
        Schwierigkeitsgrad.Mittel     => Color.Info,
        Schwierigkeitsgrad.Schwer     => Color.Warning,
        Schwierigkeitsgrad.SehrSchwer => Color.Error,
        _                             => Color.Default
    };

    // ── Instrumente ────────────────────────────────────────────────────────────
    private const string IkonBasis = "/img/instrumente/familie/";

    /// <summary>Symbol-URL eines Instruments: instrumenteigenes <paramref name="symbolUrl"/> hat Vorrang,
    /// sonst das Familien-Icon, sonst das generische Notensymbol. (Ausbaufähig auf Einzel-Instrument-Icons.)</summary>
    public static string InstrumentIcon(InstrumentFamilie? familie, string? symbolUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(symbolUrl)) return symbolUrl;
        return IkonBasis + familie switch
        {
            InstrumentFamilie.Holzblaeser  => "holz.svg",
            InstrumentFamilie.Blechblaeser => "blech.svg",
            InstrumentFamilie.Schlagwerk   => "schlag.svg",
            InstrumentFamilie.Saiten       => "saiten.svg",
            InstrumentFamilie.Tasten       => "tasten.svg",
            _                              => "note.svg"
        };
    }

    public static string FamilieLabel(InstrumentFamilie? f) => f switch
    {
        InstrumentFamilie.Holzblaeser  => "Holzbläser",
        InstrumentFamilie.Blechblaeser => "Blechbläser",
        InstrumentFamilie.Schlagwerk   => "Schlagwerk",
        InstrumentFamilie.Saiten       => "Saiten",
        InstrumentFamilie.Tasten       => "Tasten",
        _                              => "Sonstige"
    };

    // ── Rollen ─────────────────────────────────────────────────────────────────
    /// <summary>Symbol-URL einer Personen-Rolle (nur Komponist:in/Dirigent:in haben eines); sonst null.
    /// Musikant:innen werden über ihre Instrument-Symbole dargestellt.</summary>
    public static string? RolleIcon(PersonRolleTyp r) => r switch
    {
        PersonRolleTyp.Komponist => "/img/rollen/komponist.svg",
        PersonRolleTyp.Dirigent  => "/img/rollen/dirigent.svg",
        _ => null
    };
}
