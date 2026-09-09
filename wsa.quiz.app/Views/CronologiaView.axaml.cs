using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Wsa.Quiz.App.State;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.Views;

/// <summary>
/// Tab "Cronologia": tabella delle sessioni passate in ordine cronologico inverso
/// (piu' recente in alto), con swap a una vista di dettaglio domanda-per-domanda
/// al doppio click di una riga. La cronologia viene ricaricata da disco a ogni
/// chiamata di <see cref="TabellaViewBase{TItem}.Ricarica"/> (al boot, al click di
/// "Aggiorna" e quando la <c>MainWindow</c> ne fa richiesta dopo la conclusione
/// di un quiz).
/// </summary>
public partial class CronologiaView : TabellaViewBase<RisultatoCronologiaItem>
{
    // ------------------------------------------------------------------ STATO

    private bool _modoDettaglio;
    /// <summary>
    /// True quando si sta visualizzando il dettaglio di una sessione (sub-view
    /// <see cref="CronologiaDettaglioView"/>); false quando si vede la lista.
    /// </summary>
    public bool ModoDettaglio
    {
        get => _modoDettaglio;
        private set { if (_modoDettaglio != value) { _modoDettaglio = value; Raise(); } }
    }

    // ------------------------------------------------------------------ PROPRIETA' TOUCH

    /// <summary>
    /// True su desktop (TouchMode off) quando ci sono sessioni: mostra la tabella.
    /// Deciso una volta da AppEnv.TouchMode (statico); reattivo a NessunaSessione
    /// (notificato dal base).
    /// </summary>
    public bool MostraTabella => !AppEnv.TouchMode && !NessunaSessione;

    /// <summary>
    /// True su touch (TouchMode on) quando ci sono sessioni: mostra le card verticali.
    /// </summary>
    public bool MostraCard => AppEnv.TouchMode && !NessunaSessione;

    /// <summary>
    /// True su desktop (TouchMode off) quando ci sono sessioni: mostra l'header colonne.
    /// </summary>
    public bool MostraHeaderColonne => !AppEnv.TouchMode && !NessunaSessione;

    // ------------------------------------------------------------------ COSTRUZIONE

    public CronologiaView()
    {
        InitializeComponent();
        Focusable = true;

        // Su touch i tre bottoni dell'header (Aggiorna / Esporta / Svuota) non
        // stanno accanto al titolo: su un 1080px lo coprivano (visto su device il
        // 2026-09-09). Li mando sotto, dove il WrapPanel li manda a capo da solo.
        if (AppEnv.TouchMode)
        {
            DockPanel.SetDock(HeaderAzioni, Dock.Bottom);
            HeaderAzioni.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            HeaderAzioni.Margin = new Avalonia.Thickness(0, 8, 0, 0);
        }

        // Step 15: Tunnel + handledEventsToo per intercettare anche eventi
        // gia' marcati Handled da ListBoxItem o ScrollViewer.
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel, handledEventsToo: true);
        // Se il focus arriva sullo UserControl stesso (es. Ctrl+Tab dal
        // MainWindow), lo trasferiamo alla RIGA selezionata (non alla ListBox,
        // che non e' focusable: vedi FocusRigaSelezionata e Trappola 21).
        GotFocus += (_, e) =>
        {
            if (e.Source == this && !ModoDettaglio)
                FocusRigaSelezionata(ListaSessioni);
        };
        // Mantieni le proprieta' touch sincronizzate quando NessunaSessione cambia
        // (Ricarica() e EseguiEliminazione() nel base lo notificano).
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NessunaSessione))
            {
                Raise(nameof(MostraTabella));
                Raise(nameof(MostraCard));
                Raise(nameof(MostraHeaderColonne));
            }
        };
    }

    // ------------------------------------------------------------------ FILTRI (step 11)

    /// <summary>Prima voce della tendina: nessun filtro per materia.</summary>
    public const string TutteLeMaterie = "Tutte le materie";

    /// <summary>
    /// Cronologia completa caricata da disco, gia' ordinata dalla piu' recente.
    /// <see cref="TabellaViewBase{TItem}.Sessioni"/> ne mostra il sottoinsieme
    /// che passa i filtri.
    /// </summary>
    private List<RisultatoQuiz> _tutte = new();

    private readonly FiltroCronologia _filtro = new();

    /// <summary>Materie da mostrare nella tendina, con la voce "tutte" in testa.</summary>
    public ObservableCollection<string> MaterieFiltro { get; } = new() { TutteLeMaterie };

    private string? _filtroMateria = TutteLeMaterie;
    public string? FiltroMateria
    {
        get => _filtroMateria;
        set { if (_filtroMateria != value) { _filtroMateria = value; Raise(); ApplicaFiltri(); } }
    }

    private int _indicePeriodo;
    /// <summary>0 = tutto, 1 = ultimi 7 giorni, 2 = ultimi 30 giorni, 3 = personalizzato.</summary>
    public int IndicePeriodo
    {
        get => _indicePeriodo;
        set
        {
            if (_indicePeriodo == value) return;
            _indicePeriodo = value;
            Raise();
            Raise(nameof(MostraDatePersonalizzate));
            ApplicaFiltri();
        }
    }

    /// <summary>I due DatePicker compaiono solo sulla voce "Personalizzato".</summary>
    public bool MostraDatePersonalizzate => _indicePeriodo == 3;

    private DateTimeOffset? _dataDa;
    public DateTimeOffset? DataDa
    {
        get => _dataDa;
        set { if (_dataDa != value) { _dataDa = value; Raise(); ApplicaFiltri(); } }
    }

    private DateTimeOffset? _dataA;
    public DateTimeOffset? DataA
    {
        get => _dataA;
        set { if (_dataA != value) { _dataA = value; Raise(); ApplicaFiltri(); } }
    }

    private decimal? _pctMin;
    public decimal? PctMin
    {
        get => _pctMin;
        set { if (_pctMin != value) { _pctMin = value; Raise(); ApplicaFiltri(); } }
    }

    private decimal? _pctMax;
    public decimal? PctMax
    {
        get => _pctMax;
        set { if (_pctMax != value) { _pctMax = value; Raise(); ApplicaFiltri(); } }
    }

    private bool _statoConclusi = true;
    public bool StatoConclusi
    {
        get => _statoConclusi;
        set { if (_statoConclusi != value) { _statoConclusi = value; Raise(); ApplicaFiltri(); } }
    }

    private bool _statoAbbandonati = true;
    public bool StatoAbbandonati
    {
        get => _statoAbbandonati;
        set { if (_statoAbbandonati != value) { _statoAbbandonati = value; Raise(); ApplicaFiltri(); } }
    }

    private bool _statoTempoScaduto = true;
    public bool StatoTempoScaduto
    {
        get => _statoTempoScaduto;
        set { if (_statoTempoScaduto != value) { _statoTempoScaduto = value; Raise(); ApplicaFiltri(); } }
    }

    /// <summary>True se almeno un criterio e' impostato.</summary>
    public bool FiltriAttivi => _filtro.Attivo;

    /// <summary>Le partite che passano i filtri: sono quelle che finiscono nell'export.</summary>
    public IReadOnlyList<RisultatoQuiz> RisultatiFiltrati { get; private set; } = Array.Empty<RisultatoQuiz>();

    /// <summary>Testo del bottone di export, col numero di partite che verranno scritte.</summary>
    public string EtichettaEsporta => $"Esporta ({RisultatiFiltrati.Count})";

    /// <summary>
    /// Messaggio dello stato vuoto: distingue "non hai mai giocato" da "i filtri
    /// non lasciano passare niente", altrimenti sembra che la cronologia sia sparita.
    /// </summary>
    public string TestoVuoto => FiltriAttivi
        ? "Nessuna partita corrisponde ai filtri impostati."
        : "Avvia un quiz dalla Home per iniziare a costruire la tua cronologia.";

    /// <summary>Riporta ogni criterio al valore neutro e rimostra tutto.</summary>
    public void AzzeraFiltri()
    {
        _filtroMateria = TutteLeMaterie;
        _indicePeriodo = 0;
        _dataDa = null;
        _dataA = null;
        _pctMin = null;
        _pctMax = null;
        _statoConclusi = true;
        _statoAbbandonati = true;
        _statoTempoScaduto = true;

        Raise(nameof(FiltroMateria));
        Raise(nameof(IndicePeriodo));
        Raise(nameof(MostraDatePersonalizzate));
        Raise(nameof(DataDa));
        Raise(nameof(DataA));
        Raise(nameof(PctMin));
        Raise(nameof(PctMax));
        Raise(nameof(StatoConclusi));
        Raise(nameof(StatoAbbandonati));
        Raise(nameof(StatoTempoScaduto));

        ApplicaFiltri();
    }

    private void OnAzzeraFiltriClick(object? sender, RoutedEventArgs e) => AzzeraFiltri();

    /// <summary>
    /// Travasa lo stato della UI nel <see cref="FiltroCronologia"/> e ripopola
    /// <see cref="TabellaViewBase{TItem}.Sessioni"/>. Unico punto in cui la lista
    /// visibile viene ricalcolata: sia i filtri sia <see cref="RicaricaCore"/>
    /// passano di qui.
    /// </summary>
    private void ApplicaFiltri()
    {
        _filtro.Materia = _filtroMateria == TutteLeMaterie ? null : _filtroMateria;

        (_filtro.Da, _filtro.A) = _indicePeriodo switch
        {
            1 => (DateTime.Today.AddDays(-6), (DateTime?)DateTime.Today),
            2 => (DateTime.Today.AddDays(-29), (DateTime?)DateTime.Today),
            3 => (_dataDa?.DateTime, _dataA?.DateTime),
            _ => (null, null),
        };

        _filtro.PctMin = (double?)_pctMin;
        _filtro.PctMax = (double?)_pctMax;
        _filtro.IncludiConclusi = _statoConclusi;
        _filtro.IncludiAbbandonati = _statoAbbandonati;
        _filtro.IncludiTempoScaduto = _statoTempoScaduto;

        RisultatiFiltrati = _filtro.Applica(_tutte).ToList();

        Sessioni.Clear();
        foreach (var r in RisultatiFiltrati)
            Sessioni.Add(new RisultatoCronologiaItem(r));

        NessunaSessione = Sessioni.Count == 0;
        Sottotitolo = FormatSottotitolo(Sessioni.Count);

        Raise(nameof(FiltriAttivi));
        Raise(nameof(RisultatiFiltrati));
        Raise(nameof(EtichettaEsporta));
        Raise(nameof(TestoVuoto));
    }

    /// <summary>
    /// Aggiorna la tendina materie tenendo la scelta corrente se esiste ancora
    /// (una materia puo' sparire se si elimina l'ultima partita che la conteneva).
    /// </summary>
    private void AggiornaMaterieFiltro()
    {
        var disponibili = FiltroCronologia.MaterieDisponibili(_tutte);

        // Il binding TwoWay del ComboBox rimanda indietro SelectedItem = null appena
        // la collezione viene svuotata: la scelta va salvata PRIMA della Clear,
        // altrimenti dopo ogni ricarica la tendina resta in bianco.
        string? scelta = _filtroMateria;

        MaterieFiltro.Clear();
        MaterieFiltro.Add(TutteLeMaterie);
        foreach (var m in disponibili)
            MaterieFiltro.Add(m);

        // Se la materia scelta non esiste piu' (eliminata l'ultima partita che la
        // conteneva) si torna a "tutte": meglio che lasciare una lista vuota.
        string ripristino = scelta != null && MaterieFiltro.Contains(scelta)
            ? scelta
            : TutteLeMaterie;

        if (_filtroMateria != ripristino)
        {
            _filtroMateria = ripristino;
            Raise(nameof(FiltroMateria));
        }
    }

    // ------------------------------------------------------------------ CARICAMENTO

    protected override void RicaricaCore()
    {
        _tutte = _storage!.CaricaCronologia()
            .OrderByDescending(x => x.DataOra)
            .ToList();

        AggiornaMaterieFiltro();
        ApplicaFiltri();
    }

    protected override string FormatSottotitolo(int count) =>
        FiltriAttivi ? $"{count} di {_tutte.Count} sessioni."
        : count == 0 ? "Nessuna sessione registrata."
        : $"{count} sessioni registrate.";

    protected override void EliminaDaStorage(RisultatoCronologiaItem item)
        => _storage!.EliminaRisultato(item.Id);

    // ------------------------------------------------------------------ HANDLER

    private void OnSessioneDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not ListBox lb) return;
        if (lb.SelectedItem is not RisultatoCronologiaItem item) return;
        ApriDettaglio(item);
    }

    // ------------------------------------------------------------------ TASTIERA (step 8)

    /// <summary>
    /// Step 8: Invio apre il dettaglio della riga selezionata; Canc avvia la
    /// conferma inline (e secondo Canc la conferma definitiva); Esc annulla la
    /// conferma se attiva. Attivo solo quando NON siamo nel dettaglio.
    /// </summary>
    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (ModoDettaglio) return;
        if (ListaSessioni.SelectedItem is not RisultatoCronologiaItem item) return;

        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                ApriDettaglio(item);
                return;

            case Key.Delete:
                e.Handled = true;
                if (item.InAttesaConfermaEliminazione)
                {
                    EseguiEliminazione(item);
                }
                else
                {
                    AzzeraConferme();
                    item.InAttesaConfermaEliminazione = true;
                }
                return;

            case Key.Escape:
                if (item.InAttesaConfermaEliminazione)
                {
                    e.Handled = true;
                    item.InAttesaConfermaEliminazione = false;
                }
                return;
        }
    }

    // ------------------------------------------------------------------ DETTAGLIO

    private void ApriDettaglio(RisultatoCronologiaItem item)
    {
        var dettaglio = new CronologiaDettaglioView(item.Risultato);
        dettaglio.IndietroRichiesto += OnIndietroDalDettaglio;
        dettaglio.EliminazioneRichiesta += OnEliminaDalDettaglio;
        DettaglioArea.Content = dettaglio;
        ModoDettaglio = true;
        // Defer focus al prossimo frame: il binding IsVisible deve aggiornarsi
        // prima che il dettaglio possa ricevere focus (Focus() fallisce se
        // l'elemento non e' ancora IsVisible).
        Avalonia.Threading.Dispatcher.UIThread.Post(() => dettaglio.Focus());
    }

    private void OnIndietroDalDettaglio(object? sender, EventArgs e)
    {
        DettaglioArea.Content = null;
        ModoDettaglio = false;
        // Senza questo, il focus resta sul bottone "Indietro" del dettaglio che
        // pero' e' appena stato rimosso dal visual tree: di conseguenza Tab e
        // frecce smettono di funzionare finche' non si clicca con il mouse.
        FocusRigaSelezionata(ListaSessioni);
    }

    private void OnEliminaDalDettaglio(object? sender, string id)
    {
        try
        {
            _storage!.EliminaRisultato(id);
        }
        catch (Exception ex)
        {
            Sottotitolo = $"Errore nell'eliminazione: {ex.Message}";
            return;
        }

        // chiudi il dettaglio e torna alla lista, ricaricata da disco
        DettaglioArea.Content = null;
        ModoDettaglio = false;
        Ricarica();
    }

    // ------------------------------------------------------------------ EXPORT (step 11)

    private async void OnEsportaClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await EsportaAsync();
        }
        catch (Exception ex)
        {
            Sottotitolo = $"Errore nell'esportazione: {ex.Message}";
        }
    }

    /// <summary>
    /// Salva le partite filtrate. Il formato non ha un controllo dedicato: lo
    /// decide l'estensione scelta nel file picker (stesso pattern di
    /// <see cref="GestioneContenutiView"/> per la guida e il motore).
    /// </summary>
    private async Task EsportaAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } provider) return;

        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Esporta cronologia",
            SuggestedFileName = $"quaestio-cronologia-{DateTime.Now:yyyy-MM-dd}.csv",
            DefaultExtension = "csv",
            FileTypeChoices =
            [
                new FilePickerFileType("CSV (Excel, Fogli Google)") { Patterns = ["*.csv"] },
                new FilePickerFileType("JSON") { Patterns = ["*.json"] },
            ],
        });

        if (file == null) return;

        bool json = file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        string contenuto = json
            ? EsportazioneService.VersoJson(RisultatiFiltrati)
            : EsportazioneService.VersoCsv(RisultatiFiltrati);

        // BOM solo sul CSV: senza, Excel su Windows sbaglia gli accenti.
        var codifica = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: !json);

        await using (var stream = await file.OpenWriteAsync())
        await using (var writer = new System.IO.StreamWriter(stream, codifica))
        {
            await writer.WriteAsync(contenuto);
        }

        Sottotitolo = $"Esportate {RisultatiFiltrati.Count} partite in {file.Name}.";
    }

    // ------------------------------------------------------------------ SVUOTA

    private async void OnSvuotaCronologiaClick(object? sender, RoutedEventArgs e)
    {
        try { await ChiediConfermaSvuotaCronologiaAsync(); }
        catch (Exception) { /* best effort */ }
    }

    /// <summary>
    /// Dialog modale di conferma per "Svuota cronologia".
    /// </summary>
    private async Task ChiediConfermaSvuotaCronologiaAsync()
    {
        if (_storage == null) return;

        int n = Sessioni.Count;
        var dialog = new ConfermaDialog(
            "Svuota cronologia",
            "Svuota tutta la cronologia?",
            $"Verranno eliminate {n} partite dalla cronologia. L'azione non e' reversibile.",
            "Si', cancella tutto",
            "danger");

        await dialog.ShowOverlayAsync(this);

        if (!dialog.Confermato) return;

        try
        {
            _storage.SvuotaCronologia();
        }
        catch (Exception ex)
        {
            Sottotitolo = $"Errore nello svuotamento: {ex.Message}";
            return;
        }

        Ricarica();
    }
}
