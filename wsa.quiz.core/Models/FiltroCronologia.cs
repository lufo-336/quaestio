namespace Wsa.Quiz.Core.Models;

/// <summary>
/// Criteri di filtro applicati alla cronologia (step 11). Un'istanza appena
/// costruita non nasconde nulla: ogni criterio nullo o al valore di default
/// significa "non filtrare".
/// </summary>
public class FiltroCronologia
{
    /// <summary>Nome della materia da isolare; null = tutte.</summary>
    public string? Materia { get; set; }

    /// <summary>Estremo iniziale del periodo (incluso); si considera solo la data, non l'ora.</summary>
    public DateTime? Da { get; set; }

    /// <summary>Estremo finale del periodo (incluso per l'INTERA giornata).</summary>
    public DateTime? A { get; set; }

    public double? PctMin { get; set; }
    public double? PctMax { get; set; }

    public bool IncludiConclusi { get; set; } = true;
    public bool IncludiAbbandonati { get; set; } = true;
    public bool IncludiTempoScaduto { get; set; } = true;

    /// <summary>True se almeno un criterio e' impostato (usato per il sottotitolo "X di Y").</summary>
    public bool Attivo =>
        !string.IsNullOrWhiteSpace(Materia)
        || Da.HasValue || A.HasValue
        || PctMin.HasValue || PctMax.HasValue
        || !IncludiConclusi || !IncludiAbbandonati || !IncludiTempoScaduto;

    /// <summary>Applica tutti i criteri in AND, preservando l'ordine di partenza.</summary>
    public IEnumerable<RisultatoQuiz> Applica(IEnumerable<RisultatoQuiz> cronologia)
        => cronologia.Where(Passa);

    private bool Passa(RisultatoQuiz r)
    {
        if (!string.IsNullOrWhiteSpace(Materia) && !RiguardaMateria(r, Materia))
            return false;

        if (Da.HasValue && r.DataOra < Da.Value.Date)
            return false;

        // A e' inclusivo sull'intera giornata: una partita delle 18:30 del giorno
        // scelto come estremo finale deve passare.
        if (A.HasValue && r.DataOra >= A.Value.Date.AddDays(1))
            return false;

        if (PctMin.HasValue && r.PercentualeCorrette < PctMin.Value)
            return false;

        if (PctMax.HasValue && r.PercentualeCorrette > PctMax.Value)
            return false;

        return r.Abbandonato ? IncludiAbbandonati
             : r.TempoScaduto ? IncludiTempoScaduto
             : IncludiConclusi;
    }

    /// <summary>
    /// Una partita "riguarda" una materia se contiene almeno una domanda di quella
    /// materia. Le materie vere stanno nei Dettagli: <see cref="RisultatoQuiz.MateriaNome"/>
    /// per il multi-materia vale la label "Quizzone" (vedi HomeView), quindi da sola
    /// non basta. La label resta il fallback per i record senza dettagli (partite
    /// abbandonate subito, record vecchi).
    /// </summary>
    private static bool RiguardaMateria(RisultatoQuiz r, string materia)
        => r.Dettagli.Count > 0
            ? r.Dettagli.Any(d => string.Equals(d.MateriaNome, materia, StringComparison.OrdinalIgnoreCase))
            : string.Equals(r.MateriaNome, materia, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Materie presenti nella cronologia, per popolare la tendina del filtro.
    /// Ricavate dai dettagli (cosi' compaiono anche le materie poi cancellate) piu'
    /// le label dei record senza dettagli. La label "Quizzone" non e' una materia
    /// e viene esclusa.
    /// </summary>
    public static IReadOnlyList<string> MaterieDisponibili(IEnumerable<RisultatoQuiz> cronologia)
    {
        var nomi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in cronologia)
        {
            if (r.Dettagli.Count > 0)
            {
                foreach (var d in r.Dettagli)
                    if (!string.IsNullOrWhiteSpace(d.MateriaNome))
                        nomi.Add(d.MateriaNome);
            }
            else if (!string.IsNullOrWhiteSpace(r.MateriaNome))
            {
                nomi.Add(r.MateriaNome);
            }
        }

        nomi.Remove(EtichettaQuizzone);

        return nomi.OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    /// <summary>Label usata da HomeView/Cli quando le materie selezionate sono piu' d'una.</summary>
    public const string EtichettaQuizzone = "Quizzone";
}
