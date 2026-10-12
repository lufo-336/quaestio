using System.Text.Json.Serialization;

namespace Wsa.Quiz.Core.Models;

/// <summary>
/// File traduzioni/&lt;lingua&gt;/&lt;materiaId&gt;.json. Ogni campo e' opzionale:
/// cio' che manca resta in italiano. Le domande sono indicizzate per Domanda.Id,
/// calcolato sempre sul testo italiano.
/// </summary>
public class TraduzioneMateria
{
    [JsonPropertyName("nome")]
    public string? Nome { get; set; }

    /// <summary>Categoria canonica (italiana) -> nome visualizzato.</summary>
    [JsonPropertyName("categorie")]
    public Dictionary<string, string> Categorie { get; set; } = new();

    [JsonPropertyName("domande")]
    public Dictionary<string, TraduzioneDomanda> Domande { get; set; } = new();
}

public class TraduzioneDomanda
{
    [JsonPropertyName("domanda")]
    public string? Domanda { get; set; }

    /// <summary>Stesso ordine e stesso numero dell'originale: RispostaCorretta non si traduce.</summary>
    [JsonPropertyName("risposte")]
    public List<string>? Risposte { get; set; }

    [JsonPropertyName("spiegazione")]
    public string? Spiegazione { get; set; }
}
