using System;
using System.Collections.Generic;
using System.Linq;
using Wsa.Quiz.App.State;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.Views;

/// <summary>
/// Tab "Gestione contenuti": elenco materie (built-in + importate) con
/// rimozione delle importate (riuso di TabellaViewBase, stesso pattern di
/// SospesiView). I due flussi di import da JSON sono nei task successivi.
/// </summary>
public partial class GestioneContenutiView : TabellaViewBase<MateriaListItem>
{
    /// <summary>Sollevato dopo un import o una rimozione riusciti: la MainView ricarica tutti i dati.</summary>
    public event EventHandler? ContenutiCambiati;

    private ContenutiUtenteService? _contenutiUtente;
    private List<Materia> _materie = new();
    private List<Domanda> _tutteDomande = new();

    public GestioneContenutiView()
    {
        InitializeComponent();
    }

    public void Inizializza(StorageService storage, ContenutiUtenteService contenutiUtente,
        List<Materia> materie, List<Domanda> tutteDomande)
    {
        _contenutiUtente = contenutiUtente;
        _materie = materie;
        _tutteDomande = tutteDomande;
        base.Inizializza(storage); // TabellaViewBase.Inizializza: setta _storage e chiama Ricarica()
    }

    protected override void RicaricaCore()
    {
        var conteggi = _tutteDomande
            .GroupBy(d => d.MateriaId)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var m in _materie.OrderBy(m => m.Nome))
            Sessioni.Add(new MateriaListItem(m, conteggi.TryGetValue(m.Id, out var c) ? c : 0));
    }

    protected override string FormatSottotitolo(int count) =>
        count == 0 ? "Nessuna materia disponibile." : $"{count} materie disponibili.";

    protected override void EliminaDaStorage(MateriaListItem item)
    {
        if (!item.Importata || _contenutiUtente == null) return;
        _contenutiUtente.RimuoviMateria(item.Id);
        ContenutiCambiati?.Invoke(this, EventArgs.Empty);
    }
}
