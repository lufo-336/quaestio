using System.Text.Json.Serialization;

namespace Wsa.Quiz.Core.Models;

/// <summary>
/// Una sezione (categoria) di una materia nascosta dall'utente via "Gestione
/// contenuti". Persistita in ContenutiUtente/sezioni-escluse.json. Le domande
/// con questa (MateriaId, Categoria) vengono escluse dal pool dei quiz al
/// caricamento. Il dato built-in resta nell'APK, quindi l'operazione è
/// reversibile (Ripristina rimuove l'esclusione).
/// </summary>
public class SezioneEsclusa
{
    [JsonPropertyName("materiaId")]
    public string MateriaId { get; set; } = string.Empty;

    [JsonPropertyName("categoria")]
    public string Categoria { get; set; } = string.Empty;
}
