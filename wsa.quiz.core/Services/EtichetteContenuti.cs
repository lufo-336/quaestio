using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Risorse;

namespace Wsa.Quiz.Core.Services;

/// <summary>
/// Nomi visualizzati di materie e categorie nella lingua corrente. Materia.Nome,
/// Domanda.MateriaNome e Domanda.Categoria restano chiavi canoniche (italiane):
/// statistiche e filtri cronologia raggruppano su quelle. Senza traduzione si
/// restituisce la chiave stessa.
/// </summary>
public sealed class EtichetteContenuti
{
    // ponytail: istanza globale perche' la lingua e' globale al processo;
    // MainView la sostituisce a ogni caricamento dei dati.
    public static EtichetteContenuti Correnti { get; set; } = new();

    private readonly Dictionary<string, string> _materie = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Materia, string Categoria), string> _categorie = new();

    public void Registra(string nomeMateriaCanonico, string? nome, IReadOnlyDictionary<string, string> categorie)
    {
        if (!string.IsNullOrWhiteSpace(nome))
            _materie[nomeMateriaCanonico] = nome;
        foreach (var (chiave, visualizzato) in categorie)
            if (!string.IsNullOrWhiteSpace(visualizzato))
                _categorie[(nomeMateriaCanonico, chiave)] = visualizzato;
    }

    // "Quizzone" non e' una materia ma una label multi-materia salvata in
    // cronologia: resta canonica nei dati, si traduce solo qui.
    public string Materia(string nomeCanonico)
        => string.Equals(nomeCanonico, FiltroCronologia.EtichettaQuizzone, StringComparison.OrdinalIgnoreCase)
            ? Strings.Materia_Quizzone
            : _materie.TryGetValue(nomeCanonico, out var n) ? n : nomeCanonico;

    public string Categoria(string nomeMateriaCanonico, string categoria)
        => _categorie.TryGetValue((nomeMateriaCanonico, categoria), out var n) ? n : categoria;
}
