using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Wsa.Quiz.App.State;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.Views;

/// <summary>
/// Tab "Gestione contenuti": elenco materie (built-in + importate) con
/// rimozione delle importate (riuso di TabellaViewBase, stesso pattern di
/// SospesiView), i due flussi di import da JSON (nuova materia / aggiungi a
/// esistente) e il download di guida + motore per generare domande con un'IA.
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

    private async void OnScegliFileAggiungiClick(object? sender, RoutedEventArgs e)
    {
        if (SelettoreMateria.SelectedItem is not MateriaListItem materia)
        {
            EsitoAggiungi.Text = "Scegli prima la materia a cui aggiungere le domande.";
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

        _pendingNuovaMateriaNome = null;
        _pendingMateriaEsistenteId = materia.Id;
        _pendingMateriaEsistenteNome = materia.Nome;
        _pendingDomande = domande;
        MostraAnteprima($"Aggiungi a \"{materia.Nome}\" — {domande.Count} domande nel file", domande);
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
        else if (_pendingMateriaEsistenteId != null)
        {
            var esistenti = _tutteDomande.Where(d => d.MateriaId == _pendingMateriaEsistenteId);
            var (aggiunte, duplicati) = _contenutiUtente.AggiungiDomande(_pendingMateriaEsistenteId, _pendingDomande, esistenti);
            EsitoAggiungi.Text = $"{aggiunte} domande aggiunte a \"{_pendingMateriaEsistenteNome}\", {duplicati} duplicate saltate.";
        }

        PannelloAnteprima.IsVisible = false;
        _pendingDomande = null;
        _pendingNuovaMateriaNome = null;
        _pendingMateriaEsistenteId = null;
        ContenutiCambiati?.Invoke(this, EventArgs.Empty);
    }

    private async void OnScaricaGuidaClick(object? sender, RoutedEventArgs e) =>
        await ScaricaFile("avares://Wsa.Quiz.App/Assets/Guida/GUIDA_GENERAZIONE_DOMANDE.md", "GUIDA_GENERAZIONE_DOMANDE.md");

    private async void OnScaricaMotoreClick(object? sender, RoutedEventArgs e) =>
        await ScaricaFile("avares://Wsa.Quiz.App/Assets/Guida/quiz-engine.js", "quiz-engine.js");

    private async Task ScaricaFile(string uriAsset, string nomeSuggerito)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } provider) return;

        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = nomeSuggerito });
        if (file == null) return;

        using var origine = AssetLoader.Open(new Uri(uriAsset));
        await using var destinazione = await file.OpenWriteAsync();
        await origine.CopyToAsync(destinazione);
    }
}
