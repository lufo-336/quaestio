using System.Text.Json.Serialization;

namespace Wsa.Quiz.Core.Models.Laboratorio;

/// <summary>
/// File <c>traduzioni/&lt;lingua&gt;/laboratorio/&lt;linguaggio&gt;/&lt;schedaId&gt;.json</c>.
/// Ogni campo è opzionale: ciò che manca resta in italiano.
/// </summary>
public class TraduzioneScheda
{
    [JsonPropertyName("titolo")]
    public string? Titolo { get; set; }

    [JsonPropertyName("requisito")]
    public string? Requisito { get; set; }

    /// <summary>Traduzioni indicizzate per <see cref="VarianteLab.Id"/>.</summary>
    [JsonPropertyName("varianti")]
    public Dictionary<string, TraduzioneVariante> Varianti { get; set; } = new();
}

/// <summary>
/// Traduzione dei difetti di una singola <see cref="VarianteLab"/>.
/// </summary>
public class TraduzioneVariante
{
    /// <summary>Stesso ordine e stesso numero di <see cref="VarianteLab.Difetti"/>.</summary>
    [JsonPropertyName("difetti")]
    public List<TraduzioneDifetto>? Difetti { get; set; }
}

/// <summary>
/// Traduzione dei testi didattici di un <see cref="DifettoLab"/>.
/// </summary>
public class TraduzioneDifetto
{
    [JsonPropertyName("indizio")]
    public string? Indizio { get; set; }

    [JsonPropertyName("spiegazione")]
    public string? Spiegazione { get; set; }
}
