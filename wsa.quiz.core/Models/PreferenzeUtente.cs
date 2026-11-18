namespace Wsa.Quiz.Core.Models;

/// <summary>
/// Preferenze utente persistite in preferenze.json: ultima sezione aperta,
/// tema, ultime opzioni quiz. I default coincidono con quelli hardcoded in
/// HomeView, cosi' senza il file (utenti esistenti) il comportamento resta
/// identico a prima dell'introduzione di questa persistenza.
/// </summary>
public class PreferenzeUtente
{
    public int TabIniziale { get; set; } = 0;
    public bool TemaScuro { get; set; } = false;

    /// <summary>Id del colore d'accento scelto dall'utente ("blu", "viola", ...). Default: "blu".</summary>
    public string ColoreAccento { get; set; } = "blu";

    /// <summary>Codice lingua ("it", "en"); vuoto = segui la lingua di sistema.</summary>
    public string Lingua { get; set; } = "";

    public bool RandomizzaOrdine { get; set; } = true;
    public bool Rotazione { get; set; } = false;
    public bool SecondaPossibilita { get; set; } = false;
    public bool Cronometro { get; set; } = false;
    public bool LimitaDomande { get; set; } = false;
    public int LimiteN { get; set; } = 10;
    public bool ModalitaTempo { get; set; } = false;
    public int MinutiTempo { get; set; } = 10;

    /// <summary>Tipi di domanda spenti nel riquadro "Tipo di domande" della Home. Vuoto = tutti.</summary>
    public List<string> TipiEsclusi { get; set; } = new();
}
