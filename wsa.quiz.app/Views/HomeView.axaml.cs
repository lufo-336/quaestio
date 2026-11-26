using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Wsa.Quiz.App.State;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Models.Laboratorio;
using Wsa.Quiz.Core.Models.Statistiche;

using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.Views;

/// <summary>
/// Schermata di configurazione di una nuova sessione di quiz.
/// E' la sua stessa DataContext (no MVVM): espone proprieta' observable
/// usate dai binding XAML, e gestisce l'avvio della sessione tramite event.
/// </summary>
public partial class HomeView : UserControl, INotifyPropertyChanged
{
    /// <summary>Sollevato al click di "Avvia quiz". L'argomento e' la sessione gia' configurata ma non ancora avviata.</summary>
    public event EventHandler<SessioneQuiz>? QuizAvviato;

    /// <summary>Sollevato al click di "Inizia" in modalita' Laboratorio.</summary>
    public event EventHandler<SessioneLaboratorio>? LaboratorioAvviato;

    public new event PropertyChangedEventHandler? PropertyChanged;

    // ------------------------------------------------------------------ DATI

    /// <summary>Tutte le domande caricate (passate dalla MainWindow). Servono per costruire il pool al click di Avvia.</summary>
    private List<Domanda> _tutteDomande = new();
    private List<SchedaLab> _schedeLab = new();
    private IReadOnlyList<RisultatoQuiz> _cronologiaLab = Array.Empty<RisultatoQuiz>();
    private string _labLinguaggio = string.Empty;

    public ObservableCollection<LinguaggioLabItem> LinguaggiLab { get; } = new();

    /// <summary>Mappa materiaId -> tutte le sue categorie (preservando lo stato IsSelected fra rebuild).</summary>
    private readonly Dictionary<string, List<CategoriaSelezionabile>> _categoriePerMateria = new();

    public ObservableCollection<MateriaSelezionabile> Materie { get; } = new();
    public ObservableCollection<CategoriaSelezionabile> CategorieVisibili { get; } = new();

    /// <summary>I tre pulsanti del riquadro "Tipo di domande" (codice / teoria / architettura).</summary>
    public ObservableCollection<TipoSelezionabile> Tipi { get; } =
        new(TipiDomanda.Tutti.Select(t => new TipoSelezionabile(t)));

    /// <summary>Tipi spenti (chiavi canoniche). Vuoto = nessun filtro (tutti inclusi). Persistito da MainView.</summary>
    public List<string> TipiEsclusi =>
        Tipi.All(t => !t.IsSelected) || Tipi.All(t => t.IsSelected)
            ? new List<string>()
            : Tipi.Where(t => !t.IsSelected).Select(t => t.Id).ToList();

    /// <summary>I quattro pulsanti del riquadro "Punti deboli" (rosse / ambra / verdi / nuove).</summary>
    public ObservableCollection<LivelloSelezionabile> Livelli { get; } =
        new(LivelliDebolezza.Tutti.Select(l => new LivelloSelezionabile(l)));

    /// <summary>Livelli spenti (chiavi canoniche). Vuoto = nessun filtro (tutti inclusi). Persistito da MainView.</summary>
    public List<string> LivelliEsclusi =>
        Livelli.All(l => !l.IsSelected) || Livelli.All(l => l.IsSelected)
            ? new List<string>()
            : Livelli.Where(l => !l.IsSelected).Select(l => l.Id).ToList();

    private Dictionary<(string Materia, string Categoria), string> _livelliPerCategoria = new();
    private HashSet<string> _idSbagliate = new(StringComparer.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ OPZIONI

    private bool _randomizzaOrdine = true;
    public bool RandomizzaOrdine
    {
        get => _randomizzaOrdine;
        set { if (_randomizzaOrdine != value) { _randomizzaOrdine = value; Raise(); AggiornaRiepilogo(); } }
    }

    private bool _rotazione;
    public bool Rotazione
    {
        get => _rotazione;
        set { if (_rotazione != value) { _rotazione = value; Raise(); AggiornaRiepilogo(); } }
    }

    private bool _secondaPossibilita;
    public bool SecondaPossibilita
    {
        get => _secondaPossibilita;
        set { if (_secondaPossibilita != value) { _secondaPossibilita = value; Raise(); AggiornaRiepilogo(); } }
    }

    private bool _cronometro;
    public bool Cronometro
    {
        get => _cronometro;
        set { if (_cronometro != value) { _cronometro = value; Raise(); } }
    }

    private bool _limitaDomande;
    public bool LimitaDomande
    {
        get => _limitaDomande;
        set { if (_limitaDomande != value) { _limitaDomande = value; Raise(); AggiornaRiepilogo(); } }
    }

    private decimal _limiteN = 10;
    public decimal LimiteN
    {
        get => _limiteN;
        set { if (_limiteN != value) { _limiteN = value; Raise(); AggiornaRiepilogo(); } }
    }

    private bool _modalitaTempo;
    public bool ModalitaTempo
    {
        get => _modalitaTempo;
        set { if (_modalitaTempo != value) { _modalitaTempo = value; Raise(); AggiornaRiepilogo(); } }
    }

    private decimal _minutiTempo = 10;
    public decimal MinutiTempo
    {
        get => _minutiTempo;
        set { if (_minutiTempo != value) { _minutiTempo = value; Raise(); AggiornaRiepilogo(); } }
    }

    // ------------------------------------------------------------------ OPZIONI LABORATORIO

    private bool _laboratorio;
    public bool Laboratorio
    {
        get => _laboratorio;
        set
        {
            if (_laboratorio != value)
            {
                _laboratorio = value;
                Raise();
                Raise(nameof(EtichettaAvvia));
                AggiornaRiepilogo();
            }
        }
    }

    private string _labLivello = LivelliLab.Base;
    public string LabLivello
    {
        get => _labLivello;
        set
        {
            if (!string.Equals(_labLivello, value, StringComparison.OrdinalIgnoreCase))
            {
                _labLivello = value;
                Raise();
                Raise(nameof(IsLabBase));
                Raise(nameof(IsLabMedia));
                Raise(nameof(IsLabAlta));
                AggiornaRiepilogo();
            }
        }
    }

    private bool _labRipassa;
    public bool LabRipassa
    {
        get => _labRipassa;
        set
        {
            if (_labRipassa != value)
            {
                _labRipassa = value;
                Raise();
                Raise(nameof(LabSoloNonViste));
                AggiornaRiepilogo();
            }
        }
    }

    public bool LabSoloNonViste
    {
        get => !_labRipassa;
        set => LabRipassa = !value;
    }

    private decimal _labSchede = 5;
    public decimal LabSchede
    {
        get => _labSchede;
        set { value = Math.Clamp(value, 1, 50); if (_labSchede != value) { _labSchede = value; Raise(); AggiornaRiepilogo(); } }
    }

    public string EtichettaAvvia => _laboratorio ? Strings.Lab_Inizia : Strings.Home_AvviaQuiz;
    public bool NessunLinguaggioLab => LinguaggiLab.Count == 0;
    public bool IsLabBase => string.Equals(_labLivello, LivelliLab.Base, StringComparison.OrdinalIgnoreCase);
    public bool IsLabMedia => string.Equals(_labLivello, LivelliLab.Media, StringComparison.OrdinalIgnoreCase);
    public bool IsLabAlta => string.Equals(_labLivello, LivelliLab.Alta, StringComparison.OrdinalIgnoreCase);
    public bool HaSchedeBase => ContaSchedeLingLivello(LivelliLab.Base) > 0;
    public bool HaSchedeMedia => ContaSchedeLingLivello(LivelliLab.Media) > 0;
    public bool HaSchedeAlta => ContaSchedeLingLivello(LivelliLab.Alta) > 0;
    public string EtichettaLabBase => $"{Strings.Lab_Livello_Base} · {ContaSchedeLingLivello(LivelliLab.Base)}";
    public string EtichettaLabMedia => $"{Strings.Lab_Livello_Media} · {ContaSchedeLingLivello(LivelliLab.Media)}";
    public string EtichettaLabAlta => $"{Strings.Lab_Livello_Alta} · {ContaSchedeLingLivello(LivelliLab.Alta)}";

    // ------------------------------------------------------------------ STATO DERIVATO (per UI)

    public bool NessunaCategoriaDisponibile => CategorieVisibili.Count == 0;

    private bool _haCategorieSelezionate;
    public bool HaCategorieSelezionate
    {
        get => _haCategorieSelezionate;
        private set { if (_haCategorieSelezionate != value) { _haCategorieSelezionate = value; Raise(); } }
    }

    private string _riepilogoSelezione = string.Empty;
    public string RiepilogoSelezione
    {
        get => _riepilogoSelezione;
        private set { if (_riepilogoSelezione != value) { _riepilogoSelezione = value; Raise(); } }
    }

    private string _avvisoSelezione = string.Empty;
    public string AvvisoSelezione
    {
        get => _avvisoSelezione;
        private set { if (_avvisoSelezione != value) { _avvisoSelezione = value; Raise(); } }
    }

    public bool HaAvviso => !string.IsNullOrEmpty(_avvisoSelezione);

    private bool _puoAvviare;
    public bool PuoAvviare
    {
        get => _puoAvviare;
        private set { if (_puoAvviare != value) { _puoAvviare = value; Raise(); } }
    }

    // ------------------------------------------------------------------ COSTRUZIONE

    public HomeView()
    {
        InitializeComponent();
        DataContext = this;
        foreach (var t in Tipi) t.PropertyChanged += OnTipoPropertyChanged;
        foreach (var l in Livelli) l.PropertyChanged += OnLivelloPropertyChanged;
        AggiornaRiepilogo();
        if (AppEnv.TouchMode)
        {
            var g = GrigliaMaterieCategorie;
            g.ColumnDefinitions = new ColumnDefinitions("*");
            g.RowDefinitions = new RowDefinitions("Auto,Auto");
            // ZonaMaterie resta in Row 0 / Col 0. Spostare ZonaCategorie sotto.
            Grid.SetColumn(ZonaCategorie, 0);
            Grid.SetRow(ZonaCategorie, 1);
            ZonaCategorie.Margin = new Thickness(0, 12, 0, 0);
        }
        // Step 15: Tunnel + handledEventsToo per intercettare anche eventi gia'
        // marcati Handled da controlli intermedi (ScrollViewer, ListBoxItem).
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel, handledEventsToo: true);
        // Se il focus arriva sullo UserControl stesso (es. Ctrl+Tab dal
        // MainWindow), lo trasferiamo al primo elemento focusable della prima
        // zona, cosi' le frecce e Tab funzionano immediatamente.
        GotFocus += (_, e) =>
        {
            if (e.Source == this)
                this.FindControl<Button>("AvviaBtn")?.Focus();
        };
    }

    private void OnGrigliaFiltriInitialized(object? sender, EventArgs e)
    {
        if (!AppEnv.TouchMode || sender is not UniformGrid ug) return;
        ug.Rows = 0;
        ug.Columns = 2;
    }

    private void OnFiltroToggleInitialized(object? sender, EventArgs e)
    {
        if (!AppEnv.TouchMode || sender is not ToggleButton tb) return;
        tb.Margin = new Thickness(0, 0, 6, 6);
    }

    // ------------------------------------------------------------------ TASTIERA HOME (step 8)

    /// <summary>
    /// Step 8: dentro una zona (Materie, Categorie, Opzioni, Avvia) le frecce su/giu'
    /// spostano il focus fra i controlli focusable della zona stessa.
    /// Filtro: sul NumericUpDown lasciamo che le frecce incrementino il valore.
    /// </summary>
    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Up && e.Key != Key.Down) return;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        if (focused == null) return;

        // Se il focus e' su un NumericUpDown (o suo interno), lascia il comportamento nativo.
        if (focused is NumericUpDown || focused.FindAncestorOfType<NumericUpDown>() != null)
            return;

        // Trova il Border-zona piu' vicino antenato.
        var zona = focused.GetVisualAncestors()
            .OfType<Border>()
            .FirstOrDefault(b => b.Name is "ZonaAvvia" or "ZonaModo" or "ZonaMaterie" or "ZonaCategorie" or "ZonaOpzioni" or "ZonaTipo" or "ZonaLivelli" or "ZonaLabLinguaggio" or "ZonaLabLivello" or "ZonaLabSchede");
        if (zona == null) return;

        // Raccogli tutti i controlli focusable dentro la zona, in ordine visivo.
        var focusables = zona.GetVisualDescendants()
            .OfType<Control>()
            .Where(c => c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled)
            .Where(c => c is Button || c is CheckBox || c is RadioButton || c is NumericUpDown)
            .ToList();
        if (focusables.Count == 0) return;

        int idx = focusables.IndexOf(focused);
        if (idx < 0)
        {
            focusables[0].Focus(NavigationMethod.Directional);
            e.Handled = true;
            return;
        }

        int delta = e.Key == Key.Down ? +1 : -1;
        int nuovo = idx + delta;
        // No wrap: se sforiamo, restiamo dove siamo (Tab serve a cambiare zona).
        if (nuovo < 0 || nuovo >= focusables.Count) { e.Handled = true; return; }
        // NavigationMethod.Directional e' obbligatorio, non decorativo: un Focus()
        // senza metodo NON accende lo pseudo-stato :focus-visible, e senza quello
        // il FocusAdorner (il bordo giallo di App.axaml) non viene disegnato. Il
        // focus si spostava correttamente ma in modo invisibile: si vedeva il bordo
        // solo sulla prima voce, quella raggiunta con Tab. Vedi Trappola 25.
        focusables[nuovo].Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    /// <summary>
    /// Inizializza la lista materie, l'indice di categorie e le schede del Laboratorio a partire dai dati caricati.
    /// Chiamato dalla <see cref="MainWindow"/> al boot.
    /// </summary>
    public void Inizializza(
        List<Materia> materie,
        List<Domanda> tutteDomande,
        PreferenzeUtente preferenze,
        IReadOnlyList<SchedaLab>? schedeLab = null)
    {
        _tutteDomande = tutteDomande;
        _schedeLab = schedeLab?.ToList() ?? new List<SchedaLab>();

        // Opzioni quiz: valori salvati invece dei default hardcoded (Step 13).
        // Uso le PROPRIETA' (non i campi diretti): i CheckBox/NumericUpDown in
        // XAML sono gia' bindati a queste proprieta' dal costruttore
        // (DataContext = this in InitializeComponent, eseguito PRIMA che
        // MainView chiami Inizializza) quindi serve sollevare PropertyChanged
        // perche' i controlli si aggiornino visivamente con il valore
        // ripristinato - assegnare solo ai campi lascia i controlli fermi al
        // valore hardcoded del costruttore. Non causa un salvataggio spurio:
        // MainView si iscrive a HomeView.PropertyChanged (OnHomeOpzioniCambiate)
        // SOLO dopo che questa chiamata e' tornata (vedi i due call site in
        // MainView.axaml.cs), quindi questi eventi non hanno ancora ascoltatori.
        RandomizzaOrdine   = preferenze.RandomizzaOrdine;
        Rotazione          = preferenze.Rotazione;
        SecondaPossibilita = preferenze.SecondaPossibilita;
        Cronometro         = preferenze.Cronometro;
        LimitaDomande    = preferenze.LimitaDomande;
        LimiteN          = preferenze.LimiteN;
        ModalitaTempo    = preferenze.ModalitaTempo;
        MinutiTempo      = preferenze.MinutiTempo;
        // Tipi e livelli: lista esclusi vuota (o tutti esclusi per file editato a mano)
        // = nessun filtro -> tutti i pulsanti spenti. Altrimenti si accendono solo quelli inclusi.
        var esclusi = preferenze.TipiEsclusi ?? new List<string>();
        bool nessunFiltroTipi = esclusi.Count == 0 || Tipi.All(t => esclusi.Contains(t.Id));
        foreach (var t in Tipi)
            t.IsSelected = !nessunFiltroTipi && !esclusi.Contains(t.Id);

        var esclusiLiv = preferenze.LivelliEsclusi ?? new List<string>();
        bool nessunFiltroLiv = esclusiLiv.Count == 0 || Livelli.All(l => esclusiLiv.Contains(l.Id));
        foreach (var l in Livelli)
            l.IsSelected = !nessunFiltroLiv && !esclusiLiv.Contains(l.Id);

        // 1. Popolo materie con conteggio domande
        Materie.Clear();
        foreach (var m in materie)
        {
            int n = tutteDomande.Count(d => d.MateriaId == m.Id);
            var ms = new MateriaSelezionabile(m, n);
            ms.PropertyChanged += OnMateriaPropertyChanged;
            Materie.Add(ms);
        }

        // 2. Per ogni materia, calcolo le sue categorie (con conteggio per-materia)
        _categoriePerMateria.Clear();
        foreach (var m in materie)
        {
            var domandeM = tutteDomande.Where(d => d.MateriaId == m.Id).ToList();
            var cats = domandeM
                .GroupBy(d => d.Categoria)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new CategoriaSelezionabile(m.Id, m.Nome, g.Key, g.Count()))
                .ToList();
            foreach (var c in cats)
                c.PropertyChanged += OnCategoriaPropertyChanged;
            _categoriePerMateria[m.Id] = cats;
        }

        AggiornaCategorieVisibili();

        // Opzioni e dati Laboratorio
        LinguaggiLab.Clear();
        foreach (var (id, nome) in LaboratorioService.Linguaggi)
        {
            int count = _schedeLab.Count(s => string.Equals(s.Linguaggio, id, StringComparison.OrdinalIgnoreCase));
            if (count > 0)
                LinguaggiLab.Add(new LinguaggioLabItem(id, nome, count));
        }
        if (LinguaggiLab.Count > 0)
        {
            LinguaggiLab[0].IsSelected = true;
            _labLinguaggio = LinguaggiLab[0].Id;
        }
        else
        {
            _labLinguaggio = string.Empty;
        }
        Raise(nameof(NessunLinguaggioLab));

        string livelloSalvato = LivelliLab.Tutti.FirstOrDefault(l =>
            string.Equals(l, preferenze.LabLivello, StringComparison.OrdinalIgnoreCase)) ?? LivelliLab.Base;
        if (ContaSchedeLingLivello(livelloSalvato) == 0)
        {
            string? primoDisponibile = LivelliLab.Tutti.FirstOrDefault(l => ContaSchedeLingLivello(l) > 0);
            if (primoDisponibile != null) livelloSalvato = primoDisponibile;
        }

        LabLivello = livelloSalvato;
        LabRipassa = preferenze.LabRipassa;
        LabSchede = Math.Clamp(preferenze.LabSchede, 1, 50);
        Laboratorio = preferenze.Laboratorio;
        NotificaConteggiLivelliLab();

        AggiornaRiepilogo();
    }

    // ------------------------------------------------------------------ EVENTI INTERNI

    private void OnMateriaPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MateriaSelezionabile.IsSelected)) return;
        AggiornaCategorieVisibili();
        AggiornaRiepilogo();
    }

    private void OnCategoriaPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CategoriaSelezionabile.IsSelected)) return;
        AggiornaRiepilogo();
    }

    private void OnTipoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TipoSelezionabile.IsSelected)) return;
        AggiornaRiepilogo();
        Raise(nameof(TipiEsclusi));
    }

    private void OnLivelloPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LivelloSelezionabile.IsSelected)) return;
        AggiornaRiepilogo();
        Raise(nameof(LivelliEsclusi));
    }

    /// <summary>
    /// Ricalcola i livelli di debolezza delle categorie e le domande sbagliate
    /// a partire dalle statistiche cronologia.
    /// </summary>
    public void AggiornaStorico(IReadOnlyList<StatistichePerMateria> stat, IReadOnlyList<RisultatoQuiz>? cronologia = null)
    {
        _livelliPerCategoria = QuizService.LivelliPerCategoria(stat);
        _idSbagliate = QuizService.IdSbagliate(stat);
        if (cronologia != null)
            _cronologiaLab = cronologia;
        AggiornaRiepilogo();
    }

    private void AggiornaCategorieVisibili()
    {
        CategorieVisibili.Clear();
        foreach (var m in Materie.Where(x => x.IsSelected))
        {
            if (_categoriePerMateria.TryGetValue(m.Id, out var cats))
                foreach (var c in cats) CategorieVisibili.Add(c);
        }
        Raise(nameof(NessunaCategoriaDisponibile));
    }

    private void OnPulisciCategorieClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        foreach (var c in CategorieVisibili) c.IsSelected = false;
    }

    // ------------------------------------------------------------------ RIEPILOGO + AVVIO

    /// <summary>
    /// Calcola il pool di domande corrente in base alla selezione di materie/categorie.
    /// Se ci sono categorie selezionate, filtra per coppia (MateriaId, Categoria) cosi'
    /// nomi categoria identici su materie diverse non si confondono.
    /// </summary>
    private List<Domanda> CalcolaPoolCorrente()
    {
        var poolMaterieCat = CalcolaPoolSenzaTipo();
        var poolLivelli = QuizService.FiltraPerLivello(poolMaterieCat, LivelliEsclusi, _livelliPerCategoria);
        return QuizService.FiltraPerTipo(poolLivelli, TipiEsclusi);
    }

    /// <summary>Pool di materie + categorie, prima del filtro per tipo: serve ai conteggi dei pulsanti tipo.</summary>
    private List<Domanda> CalcolaPoolSenzaTipo()
    {
        var materieSel = Materie.Where(m => m.IsSelected).Select(m => m.Id).ToHashSet();
        if (materieSel.Count == 0) return new List<Domanda>();

        var pool = _tutteDomande.Where(d => materieSel.Contains(d.MateriaId)).ToList();

        var catSel = CategorieVisibili.Where(c => c.IsSelected)
            .Select(c => (c.MateriaId, c.Categoria))
            .ToHashSet();
        if (catSel.Count > 0)
            pool = pool.Where(d => catSel.Contains((d.MateriaId, d.Categoria))).ToList();

        return pool;
    }

    private void OnModoQuizClick(object? sender, RoutedEventArgs e) => Laboratorio = false;

    private void OnModoLabClick(object? sender, RoutedEventArgs e) => Laboratorio = true;

    private void OnLinguaggioLabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { DataContext: LinguaggioLabItem item }) return;
        _labLinguaggio = item.Id;
        foreach (var l in LinguaggiLab)
            l.IsSelected = string.Equals(l.Id, item.Id, StringComparison.Ordinal);

        if (ContaSchedeLingLivello(LabLivello) == 0)
        {
            string? primoDisponibile = LivelliLab.Tutti.FirstOrDefault(l => ContaSchedeLingLivello(l) > 0);
            if (primoDisponibile != null) LabLivello = primoDisponibile;
        }
        NotificaConteggiLivelliLab();
        AggiornaRiepilogo();
    }

    private void OnLabLivelloClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string livello } && ContaSchedeLingLivello(livello) > 0)
            LabLivello = livello;
    }

    private int ContaSchedeLingLivello(string livello)
    {
        if (string.IsNullOrEmpty(_labLinguaggio)) return 0;
        return _schedeLab.Count(s =>
            string.Equals(s.Linguaggio, _labLinguaggio, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(s.Livello, livello, StringComparison.OrdinalIgnoreCase));
    }

    private void NotificaConteggiLivelliLab()
    {
        Raise(nameof(HaSchedeBase));
        Raise(nameof(HaSchedeMedia));
        Raise(nameof(HaSchedeAlta));
        Raise(nameof(EtichettaLabBase));
        Raise(nameof(EtichettaLabMedia));
        Raise(nameof(EtichettaLabAlta));
    }

    private static string EtichettaLivelloLab(string livello) => livello.ToLowerInvariant() switch
    {
        LivelliLab.Base => Strings.Lab_Livello_Base,
        LivelliLab.Media => Strings.Lab_Livello_Media,
        LivelliLab.Alta => Strings.Lab_Livello_Alta,
        _ => livello
    };

    private string NomeLinguaggioCorrente() =>
        LinguaggiLab.FirstOrDefault(l => string.Equals(l.Id, _labLinguaggio, StringComparison.OrdinalIgnoreCase))?.Nome
        ?? LaboratorioService.Linguaggi.FirstOrDefault(l => string.Equals(l.Id, _labLinguaggio, StringComparison.OrdinalIgnoreCase)).Nome
        ?? _labLinguaggio;

    private List<SchedaLab> SchedeDelLinguaggioCorrente() =>
        string.IsNullOrEmpty(_labLinguaggio)
            ? new List<SchedaLab>()
            : _schedeLab.Where(s => string.Equals(s.Linguaggio, _labLinguaggio, StringComparison.OrdinalIgnoreCase)).ToList();

    private void AggiornaRiepilogo()
    {
        if (Laboratorio)
        {
            AggiornaRiepilogoLaboratorio();
            return;
        }
        int nMaterie = Materie.Count(m => m.IsSelected);
        int nCat     = CategorieVisibili.Count(c => c.IsSelected);
        var poolSenzaTipo = CalcolaPoolSenzaTipo();
        var tipiEsclusi = TipiEsclusi;
        var livelliEsclusi = LivelliEsclusi;

        // Conteggi incrociati: ogni riquadro tiene conto del filtro attivo nell'altro,
        // cosi' se un incrocio porta a 0 domande si vede subito sul pulsante stesso.
        var poolPerTipi = QuizService.FiltraPerLivello(poolSenzaTipo, livelliEsclusi, _livelliPerCategoria);
        foreach (var t in Tipi)
            t.NumDomande = poolPerTipi.Count(d => string.Equals(d.Tipo, t.Id, StringComparison.OrdinalIgnoreCase));

        var poolPerLivelli = QuizService.FiltraPerTipo(poolSenzaTipo, tipiEsclusi);
        foreach (var l in Livelli)
        {
            l.NumDomande = poolPerLivelli.Count(d =>
            {
                string liv = _livelliPerCategoria.TryGetValue((d.MateriaNome, d.Categoria), out var v)
                    ? v
                    : LivelliDebolezza.Nuove;
                return string.Equals(liv, l.Id, StringComparison.OrdinalIgnoreCase);
            });
        }

        var pool = QuizService.FiltraPerTipo(poolPerTipi, tipiEsclusi);
        int disponibili = pool.Count;

        // Quante davvero giocheremo (limite N)
        int giocate = disponibili;
        if (LimitaDomande && (int)LimiteN > 0 && giocate > (int)LimiteN)
            giocate = (int)LimiteN;

        string testoMaterie = nMaterie switch
        {
            0 => Strings.Home_NessunaMateria,
            1 => string.Format(Strings.Home_UnaMateria, EtichetteContenuti.Correnti.Materia(Materie.First(m => m.IsSelected).Nome)),
            _ => string.Format(Strings.Home_NMaterie, nMaterie)
        };
        string testoCat = nCat == 0 ? Strings.Home_TutteLeCategorie : string.Format(Strings.Home_NCategorie, nCat);
        string testoDom = giocate == disponibili
            ? string.Format(Strings.Home_NDomande, disponibili)
            : string.Format(Strings.Home_NDomandeLimitate, giocate, disponibili);

        string testoTempo = (ModalitaTempo && (int)MinutiTempo > 0)
            ? "  ·  " + string.Format(Strings.Home_MaxMinuti, (int)MinutiTempo)
            : string.Empty;

        RiepilogoSelezione = nMaterie == 0
            ? testoMaterie
            : $"{testoMaterie}  ·  {testoCat}{TestoLivelli("  ·  ")}{TestoTipi("  ·  ")}  ·  {testoDom}{testoTempo}";

        if (nMaterie == 0)            AvvisoSelezione = Strings.Home_AvvisoNessunaMateria;
        else if (disponibili == 0)    AvvisoSelezione = Strings.Home_AvvisoNessunaDomanda;
        else                          AvvisoSelezione = string.Empty;

        Raise(nameof(HaAvviso));
        PuoAvviare = (nMaterie > 0 && disponibili > 0);
        HaCategorieSelezionate = (nCat > 0);
    }

    private void AggiornaRiepilogoLaboratorio()
    {
        var schedeLing = SchedeDelLinguaggioCorrente();
        if (schedeLing.Count == 0 || string.IsNullOrEmpty(_labLinguaggio))
        {
            RiepilogoSelezione = Strings.Lab_AvvisoNessunaScheda;
            AvvisoSelezione = Strings.Lab_AvvisoNessunaScheda;
            Raise(nameof(HaAvviso));
            PuoAvviare = false;
            return;
        }

        string nomeLing = NomeLinguaggioCorrente();
        string nomeLiv = EtichettaLivelloLab(LabLivello);
        var (nuove, totale, daRipassare) = LaboratorioService.Conteggi(schedeLing, LabLivello, _cronologiaLab);

        if (totale == 0)
        {
            RiepilogoSelezione = string.Format(Strings.Lab_RiepilogoVuoto, nomeLing, nomeLiv);
            AvvisoSelezione = Strings.Lab_AvvisoNessunaScheda;
            Raise(nameof(HaAvviso));
            PuoAvviare = false;
            return;
        }

        if (LabRipassa)
        {
            if (daRipassare == 0)
            {
                RiepilogoSelezione = $"{nomeLing} · {nomeLiv} · {string.Format(Strings.Lab_DaRipassareN, 0)}";
                AvvisoSelezione = Strings.Lab_AvvisoNullaDaRipassare;
                Raise(nameof(HaAvviso));
                PuoAvviare = false;
                return;
            }

            int previste = Math.Min(daRipassare, (int)LabSchede);
            RiepilogoSelezione = $"{nomeLing} · {nomeLiv} · {string.Format(Strings.Lab_DaRipassareN, daRipassare)} · {string.Format(Strings.Lab_SchedeSessioneN, previste)}";
        }
        else
        {
            string statoNuove = nuove > 0
                ? string.Format(Strings.Lab_NuoveSuTotale, nuove, totale)
                : string.Format(Strings.Lab_NessunaNuovaMenoRecenti, totale);
            int previste = Math.Min(totale, (int)LabSchede);
            RiepilogoSelezione = $"{nomeLing} · {nomeLiv} · {statoNuove} · {string.Format(Strings.Lab_SchedeSessioneN, previste)}";
        }

        AvvisoSelezione = string.Empty;
        Raise(nameof(HaAvviso));
        PuoAvviare = true;
    }

    private void OnAvviaClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Laboratorio)
        {
            AvviaSessioneLaboratorio();
            return;
        }
        var pool = CalcolaPoolCorrente();
        if (pool.Count == 0) return;

        var materieSel = Materie.Where(m => m.IsSelected).ToList();
        var catSel     = CategorieVisibili.Where(c => c.IsSelected).ToList();

        bool quizzone = materieSel.Count > 1;
        // materiaLabel e' una chiave (finisce in cronologia e nei filtri): canonica.
        // nomeMod e' testo descrittivo, salvato nella lingua in cui si gioca.
        string materiaLabel = quizzone ? "Quizzone" : materieSel[0].Nome;
        string materiaMostrata = EtichetteContenuti.Correnti.Materia(materiaLabel);
        string nomeMod;
        if (catSel.Count > 0)
            nomeMod = $"{materiaMostrata} — {Strings.Home_ModPerCategoria}";
        else if (LimitaDomande && (int)LimiteN > 0)
            nomeMod = $"{materiaMostrata} — {string.Format(Strings.Home_NDomande, (int)LimiteN)}";
        else
            nomeMod = $"{materiaMostrata} — {(quizzone ? Strings.Home_ModCompletoRandomizzato : Strings.Home_ModCompleto)}";

        nomeMod += TestoLivelli(" — ");
        nomeMod += TestoTipi(" — ");

        var opz = new OpzioniQuiz
        {
            NomeModalita            = nomeMod,
            MateriaNomeLabel        = materiaLabel,
            RandomizzaOrdineDomande = RandomizzaOrdine,
            Rotazione               = Rotazione,
            SecondaPossibilita      = SecondaPossibilita,
            Cronometro              = Cronometro,
            LimiteDomande           = LimitaDomande ? (int)LimiteN : 0,
            LimiteTempoMinuti       = ModalitaTempo ? (int)MinutiTempo : 0,
            IdPrioritari            = LivelliEsclusi.Count > 0 ? _idSbagliate.ToList() : new List<string>(),
            Categorie               = catSel.Select(c => c.ChiaveCombinata).ToList(),
            Materie                 = materieSel.Select(m => m.Id).ToList()
        };

        var sessione = new SessioneQuiz(pool, opz);
        QuizAvviato?.Invoke(this, sessione);
    }

    private void AvviaSessioneLaboratorio()
    {
        var schedeLing = SchedeDelLinguaggioCorrente();
        if (schedeLing.Count == 0) return;

        var coppie = LaboratorioService.Prepara(
            schedeLing,
            LabLivello,
            LabRipassa,
            (int)LabSchede,
            _cronologiaLab,
            Random.Shared);
        if (coppie.Count == 0) return;

        var sessione = new SessioneLaboratorio(coppie, NomeLinguaggioCorrente(), LabLivello);
        LaboratorioAvviato?.Invoke(this, sessione);
    }

    /// <summary>
    /// "solo categorie Rosse + Ambra" preceduto dal separatore, o stringa vuota se
    /// nessun livello e' escluso (caso normale: non serve dirlo).
    /// </summary>
    private string TestoLivelli(string separatore)
    {
        if (LivelliEsclusi.Count == 0) return string.Empty;
        string nomi = string.Join(" + ", Livelli.Where(l => l.IsSelected).Select(l => l.Nome));
        return separatore + string.Format(Strings.Home_SoloLivelli, nomi);
    }

    /// <summary>
    /// "solo Codice + Teoria" preceduto dal separatore, o stringa vuota se
    /// nessun tipo e' escluso (caso normale: non serve dirlo).
    /// </summary>
    private string TestoTipi(string separatore)
    {
        if (TipiEsclusi.Count == 0) return string.Empty;
        string nomi = string.Join(" + ", Tipi.Where(t => t.IsSelected).Select(t => t.Nome));
        return separatore + string.Format(Strings.Home_SoloTipi, nomi);
    }

    // ------------------------------------------------------------------ INPC helper

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
