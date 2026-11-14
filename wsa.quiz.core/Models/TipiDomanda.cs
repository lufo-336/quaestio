namespace Wsa.Quiz.Core.Models;

/// <summary>
/// Valori ammessi per <see cref="Domanda.Tipo"/>: sono anche le chiavi usate
/// nelle preferenze (<see cref="PreferenzeUtente.TipiEsclusi"/>).
/// </summary>
public static class TipiDomanda
{
    public const string Codice = "codice";
    public const string Teoria = "teoria";
    public const string Architettura = "architettura";

    /// <summary>Nell'ordine in cui compaiono in Home.</summary>
    public static readonly string[] Tutti = { Codice, Teoria, Architettura };
}
