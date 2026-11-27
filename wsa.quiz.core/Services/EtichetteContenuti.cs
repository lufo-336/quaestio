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
    {
        if (string.Equals(nomeCanonico, FiltroCronologia.EtichettaQuizzone, StringComparison.OrdinalIgnoreCase))
            return Strings.Materia_Quizzone;
        // "C# · Laboratorio": il suffisso canonico e' italiano, si traduce solo qui.
        if (nomeCanonico.EndsWith(LaboratorioService.SuffissoMateria, StringComparison.Ordinal))
            return nomeCanonico[..^LaboratorioService.SuffissoMateria.Length] + " · " + Strings.Lab_ModoLaboratorio;
        return _materie.TryGetValue(nomeCanonico, out var n) ? n : nomeCanonico;
    }

    public string Categoria(string nomeMateriaCanonico, string categoria)
    {
        // Nel Laboratorio la categoria e' l'etichetta italiana del tipo di difetto (TipiDifetto.EtichettaCanonica).
        if (nomeMateriaCanonico.EndsWith(LaboratorioService.SuffissoMateria, StringComparison.Ordinal))
        {
            string? id = Models.Laboratorio.TipiDifetto.Tutti
                .FirstOrDefault(t => Models.Laboratorio.TipiDifetto.EtichettaCanonica(t) == categoria);
            return id != null ? Models.Laboratorio.TipiDifetto.Etichetta(id) : categoria;
        }
        return _categorie.TryGetValue((nomeMateriaCanonico, categoria), out var n) ? n : categoria;
    }

    /// <summary>Modalita' salvata in cronologia: "Laboratorio" e' una costante canonica, le altre sono gia' testo libero.</summary>
    public static string Modalita(string modalita)
        => modalita == LaboratorioService.Modalita ? Strings.Lab_ModoLaboratorio : modalita;
}
