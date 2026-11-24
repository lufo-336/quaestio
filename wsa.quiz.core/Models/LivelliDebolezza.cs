namespace Wsa.Quiz.Core.Models;

/// <summary>
/// Livelli di debolezza calcolati per le categorie in base alla percentuale di errore:
/// sono anche le chiavi usate nelle preferenze (<see cref="PreferenzeUtente.LivelliEsclusi"/>).
/// </summary>
public static class LivelliDebolezza
{
    public const string Rosse = "rosse";
    public const string Ambra = "ambra";
    public const string Verdi = "verdi";
    public const string Nuove = "nuove";

    /// <summary>Nell'ordine in cui compaiono in Home.</summary>
    public static readonly string[] Tutti = { Rosse, Ambra, Verdi, Nuove };
}
