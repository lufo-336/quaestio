using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Wsa.Quiz.App.State;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.Views;

/// <summary>
/// Tab "Statistiche": vista adattiva Master-Detail.
/// Su desktop mostra la lista materie a sinistra e il dettaglio ordinato della materia
/// selezionata a destra (con filtri, ordinamento, errori espandibili e Ripassa).
/// Su mobile (AppEnv.TouchMode) alterna lista materie e dettaglio materia in 2 step.
/// </summary>
public partial class StatisticheView : UserControl, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<RichiestaRipasso>? RipassaRichiesto;
    public event EventHandler<bool>? LaboratorioCambiato;

    private StorageService? _storage;
    private readonly StatisticheService _servizio = new();

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
                Raise(nameof(TitoloVuoto));
                Raise(nameof(DescrizioneVuoto));
                Ricarica();
                LaboratorioCambiato?.Invoke(this, _laboratorio);
            }
        }
    }

    public string TitoloVuoto => _laboratorio ? Strings.Lab_StatisticheVuoto : Strings.Statistiche_NessunQuiz;
    public string DescrizioneVuoto => _laboratorio ? Strings.Lab_StatisticheVuotoDesc : Strings.Statistiche_NessunQuizDesc;

    public ObservableCollection<MateriaStatItem> Materie { get; } = new();
    public ObservableCollection<CategoriaStatItem> CategorieFiltrate { get; } = new();

    // ------------------------------------------------------------------ PERIODO

    private bool _modalitaTutto = false;
    public bool ModalitaTutto
    {
        get => _modalitaTutto;
        set
        {
            if (_modalitaTutto != value)
            {
                _modalitaTutto = value;
                Raise();
                Ricarica();
            }
        }
    }

    private int _limiteUltimeN = 30;
    public int LimiteUltimeN
    {
        get => _limiteUltimeN;
        set
        {
            if (_limiteUltimeN != value)
            {
                _limiteUltimeN = value;
                Raise();
            }
        }
    }

    // ------------------------------------------------------------------ STATO VUOTO / LAYOUT

    private bool _nessunDato = true;
    public bool NessunDato
    {
        get => _nessunDato;
        private set
        {
            if (_nessunDato != value)
            {
                _nessunDato = value;
                Raise();
                NotificaVisibilita();
            }
        }
    }

    private bool _filtroTroppoStretto;
    public bool FiltroTroppoStretto
    {
        get => _filtroTroppoStretto;
        private set
        {
            if (_filtroTroppoStretto != value)
            {
                _filtroTroppoStretto = value;
                Raise();
                NotificaVisibilita();
            }
        }
    }

    private bool _dettaglioApertoTouch;
    public bool ContenutoVisibile => !NessunDato && !FiltroTroppoStretto;
    public bool MostraListaMaterie => ContenutoVisibile && (!AppEnv.TouchMode || !_dettaglioApertoTouch);
    public bool MostraDettaglioMateria => ContenutoVisibile && _materiaSelezionata != null && (!AppEnv.TouchMode || _dettaglioApertoTouch);
    public bool MostraIndietro => AppEnv.TouchMode && _dettaglioApertoTouch;
    public bool MostraSottotitoloMaterie => AppEnv.TouchMode;

    // ------------------------------------------------------------------ MATERIA SELEZIONATA + FILTRI

    private MateriaStatItem? _materiaSelezionata;
    public MateriaStatItem? MateriaSelezionata => _materiaSelezionata;

    public string MateriaTitolo => _materiaSelezionata?.MateriaMostrata ?? string.Empty;
    public string MateriaPct => _materiaSelezionata?.PctFormattata ?? string.Empty;
    public IBrush MateriaColore => _materiaSelezionata?.ColoreLivelloTesto ?? QuizColors.TempoNeutro;
    public string MateriaRiepilogo => _materiaSelezionata?.RiepilogoDettaglio ?? string.Empty;
    public bool MateriaHaSbagliate => _materiaSelezionata?.HaSbagliate ?? false;
    public string MateriaEtichettaRipassa => _materiaSelezionata?.EtichettaRipassaMateria ?? string.Empty;

    public string EtichettaFiltroTutte => string.Format(Strings.Statistiche_FiltroTutte, _materiaSelezionata?.TotaleCategorie ?? 0);
    public string EtichettaFiltroRosse => string.Format(Strings.Statistiche_FiltroLivello, Strings.Home_LivelloRosse, _materiaSelezionata?.CountRosse ?? 0);
    public string EtichettaFiltroAmbra => string.Format(Strings.Statistiche_FiltroLivello, Strings.Home_LivelloAmbra, _materiaSelezionata?.CountAmbra ?? 0);
    public string EtichettaFiltroVerdi => string.Format(Strings.Statistiche_FiltroLivello, Strings.Home_LivelloVerdi, _materiaSelezionata?.CountVerdi ?? 0);

    private string _filtroLivello = string.Empty;
    public bool FiltroTutteAttivo => string.IsNullOrEmpty(_filtroLivello);
    public bool FiltroRosseAttivo => _filtroLivello == LivelliDebolezza.Rosse;
    public bool FiltroAmbraAttivo => _filtroLivello == LivelliDebolezza.Ambra;
    public bool FiltroVerdiAttivo => _filtroLivello == LivelliDebolezza.Verdi;

    private int _indiceOrdinamento; // 0 = più deboli, 1 = A-Z, 2 = più viste
    public int IndiceOrdinamento
    {
        get => _indiceOrdinamento;
        set
        {
            if (_indiceOrdinamento != value)
            {
                _indiceOrdinamento = value;
                Raise();
                AggiornaCategorieFiltrate();
            }
        }
    }

    public bool NessunaCategoriaFiltrata => _materiaSelezionata != null && CategorieFiltrate.Count == 0;

    // ------------------------------------------------------------------ COSTRUZIONE

    public StatisticheView()
    {
        InitializeComponent();
        DataContext = this;

        if (AppEnv.TouchMode)
        {
            HeaderGrid.Margin = new Thickness(20, 12, 20, 8);
            ContenutoGrid.Margin = new Thickness(20, 0, 20, 0);
            ContenutoGrid.ColumnDefinitions = new ColumnDefinitions("*");
            Grid.SetColumn(PannelloMaterie, 0);
            Grid.SetColumn(PannelloDettaglio, 0);
            PannelloDettaglio.BorderThickness = new Thickness(0);
            PannelloDettaglio.Padding = new Thickness(0);
            DettaglioHeaderGrid.ColumnDefinitions = new ColumnDefinitions("*");
            Grid.SetColumn(RipassaMateriaBtn, 0);
            Grid.SetRow(RipassaMateriaBtn, 1);
            RipassaMateriaBtn.HorizontalAlignment = HorizontalAlignment.Left;
            RipassaMateriaBtn.Margin = new Thickness(0, 8, 0, 0);
        }

        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel, handledEventsToo: true);
        QuizColors.CollegaCambioTema(this, AggiornaColoriTema);
    }

    private void AggiornaColoriTema()
    {
        foreach (var m in Materie)
            m.NotificaCambioTema();
        Raise(nameof(MateriaColore));
    }

    public void Inizializza(StorageService storage, bool laboratorio = false)
    {
        _storage = storage;
        _laboratorio = laboratorio;
        Raise(nameof(Laboratorio));
        Raise(nameof(TitoloVuoto));
        Raise(nameof(DescrizioneVuoto));
        Ricarica();
    }

    public void Ricarica()
    {
        if (_storage == null) return;
        string? precedente = _materiaSelezionata?.MateriaCanon;

        Materie.Clear();
        CategorieFiltrate.Clear();
        _materiaSelezionata = null;

        try
        {
            var cronologiaModo = _storage.CaricaCronologia()
                .Where(r => LaboratorioService.ERisultatoLab(r) == _laboratorio)
                .ToList();
            int? limite = ModalitaTutto ? null : LimiteUltimeN;
            var stat = _servizio.Calcola(cronologiaModo, limite)
                .Where(r => r.Materia.EndsWith(LaboratorioService.SuffissoMateria, StringComparison.Ordinal) == _laboratorio);

            var items = stat
                .Select(r => new MateriaStatItem(r))
                .OrderByDescending(m => m.PctErrore)
                .ThenBy(m => m.MateriaMostrata, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var item in items)
                Materie.Add(item);

            NessunDato = cronologiaModo.Count == 0;
            FiltroTroppoStretto = !NessunDato && Materie.Count == 0;

            var daSelezionare = precedente != null
                ? Materie.FirstOrDefault(m => string.Equals(m.MateriaCanon, precedente, StringComparison.OrdinalIgnoreCase))
                : null;
            if (daSelezionare == null)
            {
                _dettaglioApertoTouch = false;
                daSelezionare = Materie.FirstOrDefault();
            }

            // Materia diversa (es. cambio Quiz | Laboratorio): il filtro Rosse/Gialle/... della vecchia non vale più.
            bool stessaMateria = string.Equals(precedente, daSelezionare?.MateriaCanon, StringComparison.OrdinalIgnoreCase);
            ImpostaMateriaSelezionata(daSelezionare, resetFiltro: !stessaMateria);
        }
        catch
        {
            NessunDato = true;
            FiltroTroppoStretto = false;
            _dettaglioApertoTouch = false;
            ImpostaMateriaSelezionata(null, resetFiltro: true);
        }
    }

    // ------------------------------------------------------------------ INTERAZIONI

    private void OnModoQuizClick(object? sender, RoutedEventArgs e) => Laboratorio = false;

    private void OnModoLabClick(object? sender, RoutedEventArgs e) => Laboratorio = true;

    private void OnAggiornaClick(object? sender, RoutedEventArgs e) => Ricarica();

    private void OnMateriaBorderInitialized(object? sender, EventArgs e)
    {
        if (AppEnv.TouchMode && sender is Border b)
            b.Padding = new Thickness(0, 12);
    }

    private void OnLimiteChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (!ModalitaTutto) Ricarica();
    }

    private void OnMateriaPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border b) return;
        if (b.DataContext is not MateriaStatItem item) return;

        bool cambiata = _materiaSelezionata != item;
        ImpostaMateriaSelezionata(item, resetFiltro: cambiata);
        if (AppEnv.TouchMode)
        {
            _dettaglioApertoTouch = true;
            NotificaVisibilita();
        }
    }

    private void OnIndietroMaterieClick(object? sender, RoutedEventArgs e)
    {
        _dettaglioApertoTouch = false;
        NotificaVisibilita();
    }

    private void OnFiltroLivelloClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        if (_filtroLivello == tag) return;
        _filtroLivello = tag;
        NotificaFiltriLivello();
        AggiornaCategorieFiltrate();
    }

    private void OnCategoriaPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Grid g) return;
        if (g.DataContext is not CategoriaStatItem cat) return;
        if (!cat.HaSbagliate) return;
        cat.IsExpanded = !cat.IsExpanded;
    }

    private void OnRipassaMateriaClick(object? sender, RoutedEventArgs e)
    {
        if (_materiaSelezionata is { HaSbagliate: true })
            RipassaRichiesto?.Invoke(this, _materiaSelezionata.CreaRichiestaRipasso());
    }

    private void OnRipassaMateriaCardClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MateriaStatItem item } && item.HaSbagliate)
        {
            e.Handled = true;
            RipassaRichiesto?.Invoke(this, item.CreaRichiestaRipasso());
        }
    }

    private void OnRipassaCategoriaClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CategoriaStatItem cat } && cat.HaSbagliate)
        {
            e.Handled = true;
            RipassaRichiesto?.Invoke(this, cat.CreaRichiestaRipasso());
        }
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && AppEnv.TouchMode && _dettaglioApertoTouch)
        {
            _dettaglioApertoTouch = false;
            NotificaVisibilita();
            e.Handled = true;
        }
    }

    // ------------------------------------------------------------------ HELPER

    private void ImpostaMateriaSelezionata(MateriaStatItem? item, bool resetFiltro)
    {
        foreach (var m in Materie)
            m.IsSelected = !AppEnv.TouchMode && ReferenceEquals(m, item);

        _materiaSelezionata = item;

        if (resetFiltro && !string.IsNullOrEmpty(_filtroLivello))
        {
            _filtroLivello = string.Empty;
            NotificaFiltriLivello();
        }

        Raise(nameof(MateriaSelezionata));
        Raise(nameof(MateriaTitolo));
        Raise(nameof(MateriaPct));
        Raise(nameof(MateriaColore));
        Raise(nameof(MateriaRiepilogo));
        Raise(nameof(MateriaHaSbagliate));
        Raise(nameof(MateriaEtichettaRipassa));
        Raise(nameof(EtichettaFiltroTutte));
        Raise(nameof(EtichettaFiltroRosse));
        Raise(nameof(EtichettaFiltroAmbra));
        Raise(nameof(EtichettaFiltroVerdi));

        AggiornaCategorieFiltrate();
        NotificaVisibilita();
    }

    private void AggiornaCategorieFiltrate()
    {
        CategorieFiltrate.Clear();
        if (_materiaSelezionata != null)
        {
            IEnumerable<CategoriaStatItem> q = _materiaSelezionata.Categorie;
            if (!string.IsNullOrEmpty(_filtroLivello))
                q = q.Where(c => string.Equals(c.Livello, _filtroLivello, StringComparison.OrdinalIgnoreCase));

            q = _indiceOrdinamento switch
            {
                1 => q.OrderBy(c => c.NomeMostrato, StringComparer.OrdinalIgnoreCase),
                2 => q.OrderByDescending(c => c.Visti).ThenByDescending(c => c.PctErrore),
                _ => q.OrderByDescending(c => c.PctErrore).ThenBy(c => c.NomeMostrato, StringComparer.OrdinalIgnoreCase)
            };

            foreach (var c in q)
                CategorieFiltrate.Add(c);
        }
        Raise(nameof(NessunaCategoriaFiltrata));
    }

    private void NotificaVisibilita()
    {
        Raise(nameof(ContenutoVisibile));
        Raise(nameof(MostraListaMaterie));
        Raise(nameof(MostraDettaglioMateria));
        Raise(nameof(MostraIndietro));
    }

    private void NotificaFiltriLivello()
    {
        Raise(nameof(FiltroTutteAttivo));
        Raise(nameof(FiltroRosseAttivo));
        Raise(nameof(FiltroAmbraAttivo));
        Raise(nameof(FiltroVerdiAttivo));
    }

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
