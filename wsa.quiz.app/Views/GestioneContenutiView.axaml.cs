using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

    private string? _pendingNuovaMateriaNome;
    private string? _pendingMateriaEsistenteId;
    private string? _pendingMateriaEsistenteNome;
    private List<Domanda>? _pendingDomande;

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

    private async Task<string?> ScegliFileJsonAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } provider) return null;

        var risultati = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Scegli il file JSON delle domande",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } }
        });
        if (risultati.Count == 0) return null;

        await using var stream = await risultati[0].OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private void MostraErrori(List<string> errori)
    {
        PannelloAnteprima.IsVisible = true;
        AnteprimaTitoloText.Text = $"File non valido — {errori.Count} errori";
        AnteprimaLista.ItemsSource = errori;
        BottoneConferma.IsVisible = false;
        _pendingDomande = null;
    }

    private void MostraAnteprima(string titolo, List<Domanda> domande)
    {
        PannelloAnteprima.IsVisible = true;
        AnteprimaTitoloText.Text = titolo;
        AnteprimaLista.ItemsSource = domande
            .Select(d => $"[{d.Categoria}] {(d.DomandaTesto.Length <= 70 ? d.DomandaTesto : d.DomandaTesto[..70] + "…")}")
            .ToList();
        BottoneConferma.IsVisible = true;
    }

    private void OnAnnullaImportClick(object? sender, RoutedEventArgs e)
    {
        PannelloAnteprima.IsVisible = false;
        _pendingDomande = null;
        _pendingNuovaMateriaNome = null;
        _pendingMateriaEsistenteId = null;
    }

    private async void OnScegliFileNuovaMateriaClick(object? sender, RoutedEventArgs e)
    {
        string nome = NomeNuovaMateria.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(nome))
        {
            EsitoNuovaMateria.Text = "Inserisci un nome per la materia prima di scegliere il file.";
            return;
        }

        string? json = await ScegliFileJsonAsync();
        if (json == null) return;

        var (domande, errori) = ValidatoreImportDomande.Valida(json);
        if (errori.Count > 0)
        {
            MostraErrori(errori);
            return;
        }

        _pendingNuovaMateriaNome = nome;
        _pendingMateriaEsistenteId = null;
        _pendingDomande = domande;
        MostraAnteprima($"Nuova materia \"{nome}\" — {domande.Count} domande", domande);
    }

    private void OnConfermaImportClick(object? sender, RoutedEventArgs e)
    {
        if (_pendingDomande == null || _contenutiUtente == null) return;

        if (_pendingNuovaMateriaNome != null)
        {
            _contenutiUtente.CreaMateria(_pendingNuovaMateriaNome, _pendingDomande, _materie.Select(m => m.Id));
            EsitoNuovaMateria.Text = $"Materia \"{_pendingNuovaMateriaNome}\" creata con {_pendingDomande.Count} domande.";
            NomeNuovaMateria.Text = "";
        }

        PannelloAnteprima.IsVisible = false;
        _pendingDomande = null;
        _pendingNuovaMateriaNome = null;
        _pendingMateriaEsistenteId = null;
        ContenutiCambiati?.Invoke(this, EventArgs.Empty);
    }
}
