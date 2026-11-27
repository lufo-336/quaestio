namespace Wsa.Quiz.Core.Models.Laboratorio;

/// <summary>
/// Snapshot serializzabile di una sessione del Laboratorio messa in pausa.
/// Persistita in <c>laboratorio_in_pausa.json</c>, separato dalle pause dei quiz.
/// </summary>
public class PausaLaboratorio
{
    /// <summary>Identificativo univoco della pausa (Guid formato "N").</summary>
    public string SessioneId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Data e ora in cui la sessione è stata messa in pausa.</summary>
    public DateTime DataOraPausa { get; set; } = DateTime.Now;

    /// <summary>Identificativo del linguaggio (es. "cs").</summary>
    public string Linguaggio { get; set; } = string.Empty;

    /// <summary>Livello della sessione ("base", "media", "alta").</summary>
    public string Livello { get; set; } = string.Empty;

    /// <summary>Coppie <c>schedaId/varianteId</c> della sessione, nell'ordine originale.</summary>
    public List<string> Coppie { get; set; } = new();

    /// <summary>Indice 0-based della scheda in corso da cui ripartire al passo 1.</summary>
    public int IndiceScheda { get; set; }

    /// <summary>Dettagli delle sole schede già concluse prima della pausa.</summary>
    public List<DettaglioRisposta> Dettagli { get; set; } = new();

    /// <summary>Tempo già trascorso prima della pausa.</summary>
    public TimeSpan TempoTrascorso { get; set; }
}
