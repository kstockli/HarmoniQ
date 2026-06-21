namespace HarmoniQ.Web.Data.Models;

/// <summary>Geschätzte Verlässlichkeit eines Funds (aus Heuristik bzw. LLM).</summary>
public enum Konfidenz
{
    Tief = 0,
    Mittel = 1,
    Hoch = 2
}
