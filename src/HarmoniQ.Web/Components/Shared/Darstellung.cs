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
}
