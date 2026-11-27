using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Wsa.Quiz.App.State;
using Wsa.Quiz.App.Views;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Models.Laboratorio;
using Wsa.Quiz.Core.Models.Statistiche;
using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App;

/// <summary>
/// Shell principale dell'app. Orchestra il caricamento dati al boot,
/// il TabControl con le tre sezioni (Home / Cronologia / Sospesi)
/// e la transizione fra Home / Quiz / Riepilogo all'interno del tab Home.
/// </summary>
public partial class MainView : UserControl
{
    private StorageService? _storage;
    private List<Materia> _materie = new();
    private List<Domanda> _tutteDomande = new();
    private List<Domanda> _domandeAttive = new();
    private Dictionary<string, Domanda> _mappaDomande = new();
    private List<SchedaLab> _schedeLab = new();
    private ContenutiUtenteService? _contenutiUtente;

    private HomeView? _homeView;
    private CronologiaView? _cronologiaView;
    private SospesiView? _sospesiView;
    private StatisticheView? _statisticheView;
    private GestioneContenutiView? _gestioneView;
    private ImpostazioniView? _impostazioniView;
    private PreferenzeUtente _preferenze = new();
    private int _ultimoIndiceSalvato = -1;
    private bool _propagandoLaboratorio;

    // Aree contenuto dell'host attivo: su desktop puntano ai ContentControl dentro
    // il TabControl, su touch a quelli dentro il Carousel. Impostate in
    // ConfiguraNavigazione; tutta la logica di swap passa di qui (vedi _homeArea.Content).
    private ContentControl _homeArea = null!;
    private ContentControl _cronologiaArea = null!;
    private ContentControl _sospesiArea = null!;
    private ContentControl _statisticheArea = null!;
    private ContentControl _gestioneArea = null!;
    private ContentControl _impostazioniArea = null!;

    // Animazione dello scroll orizzontale per il tap sulla bottom nav (lo swipe col
    // dito usa l'inerzia nativa, qui serve solo per il cambio tab da bottone).
    private DispatcherTimer? _scrollAnim;

    // Debounce del salvataggio tab su touch: ScrollChangedEvent spara molte
    // volte durante un'animazione o uno swipe con inerzia (indici intermedi
    // diversi da quello finale). Si salva solo ~300ms dopo l'ultimo evento,
    // quando lo scroll si e' fermato — coerente con lo scopo dichiarato di
    // SalvaTabSeCambiata (non riscrivere il file ad ogni frame di scroll).
    private DispatcherTimer? _salvaTabDebounce;

    public MainView()
    {
        InitializeComponent();
        ConfiguraNavigazione();
        // Il cambio lingua e' bloccato durante un quiz (lo farebbe perdere): una sola
        // sottoscrizione copre tutti i punti che cambiano la Home (avvio, ripresa,
        // riepilogo, ritorno).
        _homeArea.PropertyChanged += (_, e) =>
        {
            if (e.Property == ContentControl.ContentProperty && _impostazioniView != null)
                _impostazioniView.QuizInCorso = _homeArea.Content is QuizView or LaboratorioView;
        };
        Caricamento();
    }

    // ------------------------------------------------------------------ NAVIGAZIONE (touch vs desktop)

    /// <summary>Larghezza di una pagina = viewport dello SwipeHost. Le pagine sono
    /// affiancate, quindi lo scroll-offset orizzontale è (indice × larghezza).</summary>
    private double LarghezzaPagina => SwipeHost.Bounds.Width;

    /// <summary>Indice della sezione attiva, indipendente dall'host: TabControl su
    /// desktop, posizione dello scroll orizzontale su touch.</summary>
    private int IndiceSezione
    {
        get
        {
            if (!AppEnv.TouchMode) return Tabs.SelectedIndex;
            double w = LarghezzaPagina;
            if (w <= 0) return 0;
            return Math.Clamp((int)Math.Round(SwipeHost.Offset.X / w), 0, 5);
        }
        set
        {
            if (AppEnv.TouchMode) ScorriAllaPagina(value, animato: true);
            else Tabs.SelectedIndex = value;
        }
    }

    /// <summary>
    /// Desktop: tab in alto + footer diagnostico (TabControl, invariato). Touch:
    /// SwipeHost (ScrollViewer orizzontale con snap) pilotato da swipe nativo + bottom
    /// navigation a icone; il TabControl viene nascosto. Le 4 aree contenuto vengono
    /// instradate ai ContentControl dell'host attivo via i campi _*Area.
    /// </summary>
    private void ConfiguraNavigazione()
    {
        if (AppEnv.TouchMode)
        {
            _homeArea = HomeAreaTouch;
            _cronologiaArea = CronologiaAreaTouch;
            _sospesiArea = SospesiAreaTouch;
            _statisticheArea = StatisticheAreaTouch;
            _gestioneArea = GestioneAreaTouch;
            _impostazioniArea = ImpostazioniAreaTouch;

            Tabs.IsVisible = false;
            SwipeHost.IsVisible = true;
            FooterBar.IsVisible = false;    // footer diagnostico solo su desktop
            BottomNav.IsVisible = true;

            // Riserva iniziale per la barra C2 (56 + respiro); SizeChanged la aggiorna
            // con l'altezza reale (inset Android incluso) appena il layout e' pronto.
            if (Application.Current is { } app)
                app.Resources["SzRiservaNav"] = new Thickness(0, 0, 0, 70);

            // Ogni pagina larga/alta quanto il viewport: lo snap aggancia ai bordi pagina
            // e il contenuto verticale resta dentro il proprio ScrollViewer.
            SwipeHost.SizeChanged += (_, _) => DimensionaPagine();
            // La barra C2 copre il fondo delle pagine: ognuna riserva in coda la sua
            // altezza reale (inset Android incluso) + un respiro, cosi' l'ultimo
            // contenuto si puo' sempre scorrere fin sopra la barra.
            BottomNav.SizeChanged += (_, e) =>
            {
                if (e.NewSize.Height > 0 && Application.Current is { } current)
                    current.Resources["SzRiservaNav"] = new Thickness(0, 0, 0, e.NewSize.Height + 14);
            };
            // Lo scroll (swipe o animazione) aggiorna la sezione attiva nella bottom nav.
            SwipeHost.AddHandler(ScrollViewer.ScrollChangedEvent,
                new EventHandler<ScrollChangedEventArgs>((_, _) =>
                {
                    if (BloccaDerivaOrizzontale()) return;
                    CampionaOffset();
                    AggiornaNav(); SalvaTabSeCambiataDebounced();
                }));
            // Blocco d'asse: il gesto orizzontale parte solo dopo 60 px, quello verticale delle
            // pagine dopo 30: un trascinamento in prevalenza verticale vince sempre lo scroll.
            // Il recognizer lo crea lo ScrollViewer con il template: si imposta a template pronto.
            SwipeHost.TemplateApplied += (_, te) =>
            {
                if (te.NameScope.Find("PART_ContentPresenter") is InputElement presenter)
                    foreach (var r in presenter.GestureRecognizers.OfType<Avalonia.Input.GestureRecognizers.ScrollGestureRecognizer>())
                        r.ScrollStartDistance = 60;
            };
            // Pager fatto in casa: il dito trascina la pagina (scroll nativo, senza inerzia);
            // al rilascio uno scatto veloce o un trascinamento di ~10% porta alla pagina
            // vicina con un'animazione. handledEventsToo: lo ScrollViewer marca l'evento gestito.
            // Punto di inizio/ultimo punto del dito: serve a scartare i gesti in prevalenza verticali.
            SwipeHost.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>((_, pe) =>
            {
                _scrollAnim?.Stop();   // il dito riprende il controllo di una pagina ancora in animazione
                _puntoInizio = _puntoUltimo = pe.GetPosition(SwipeHost);
                _orizzontale = false; _paginaInizioGesto = IndiceSezione;
            }), RoutingStrategies.Tunnel, handledEventsToo: true);
            SwipeHost.AddHandler(InputElement.PointerMovedEvent, new EventHandler<PointerEventArgs>((_, pe) =>
                _puntoUltimo = pe.GetPosition(SwipeHost)), RoutingStrategies.Tunnel, handledEventsToo: true);
            // Rete di sicurezza: se alzando il dito la pagina resta a meta' (ScrollGestureEnded
            // non arrivato o arrivato prima dell'ultimo spostamento), la si assesta.
            EventHandler<RoutedEventArgs> rilascio = (_, _) => AssestaDopoRilascio();
            SwipeHost.AddHandler(InputElement.PointerReleasedEvent, new EventHandler<PointerReleasedEventArgs>(rilascio),
                RoutingStrategies.Tunnel, handledEventsToo: true);
            SwipeHost.AddHandler(InputElement.PointerCaptureLostEvent, new EventHandler<PointerCaptureLostEventArgs>(rilascio),
                RoutingStrategies.Tunnel, handledEventsToo: true);
            SwipeHost.AddHandler(InputElement.ScrollGestureEvent,
                new EventHandler<ScrollGestureEventArgs>((_, ge) =>
                {
                    // Due recognizer (pagina verticale e host orizzontale) possono emettere
                    // gesti con id diversi nello stesso trascinamento: si parte una volta sola.
                    if (!_gestoAttivo)
                    {
                        _gestoAttivo = true; _idGesto = ge.Id; _campioni.Clear();
                    }
                }),
                handledEventsToo: true);
            SwipeHost.AddHandler(InputElement.ScrollGestureEndedEvent,
                new EventHandler<ScrollGestureEndedEventArgs>((_, _) => FineGesto()),
                handledEventsToo: true);

            AggiornaColoriNav();
            QuizColors.CollegaCambioTema(this, AggiornaColoriNav);
            AggiornaNav();
        }
        else
        {
            _homeArea = HomeArea;
            _cronologiaArea = CronologiaArea;
            _sospesiArea = SospesiArea;
            _statisticheArea = StatisticheArea;
            _gestioneArea = GestioneArea;
            _impostazioniArea = ImpostazioniArea;

            // Evidenzia (no-op su desktop) tenuta per simmetria; AggiornaNav esce subito.
            Tabs.SelectionChanged += (_, _) => { AggiornaNav(); SalvaTabSeCambiata(); };

            AggiornaColoriTab();
            QuizColors.CollegaCambioTema(this, AggiornaColoriTab);
        }

        if (OperatingSystem.IsBrowser())
            FooterBar.IsVisible = false;
    }

    private bool _orizzontale;

    /// <summary>Blocco d'asse: il gesto e' orizzontale solo se il dito si e' mosso in modo netto
    /// di lato; altrimenti (verticale o ancora incerto) l'offset dell'host torna alla pagina di
    /// partenza, cosi' uno scroll verticale non trascina mai la pagina di lato. True = offset
    /// ripristinato (da ignorare).</summary>
    private bool BloccaDerivaOrizzontale()
    {
        double w = LarghezzaPagina;
        if (!_gestoAttivo || w <= 0 || _scrollAnim is { IsEnabled: true }) return false;
        double dx = Math.Abs(_puntoUltimo.X - _puntoInizio.X), dy = Math.Abs(_puntoUltimo.Y - _puntoInizio.Y);
        if (!_orizzontale && dx > 40 && dx > dy * 1.5) _orizzontale = true;   // ponytail: soglie a occhio, da tarare sul S21
        if (_orizzontale) return false;
        double base_ = _paginaInizioGesto * w;
        if (Math.Abs(SwipeHost.Offset.X - base_) < 0.5) return false;
        SwipeHost.Offset = new Vector(base_, 0);
        return true;
    }

    private void AssestaDopoRilascio()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            double w = LarghezzaPagina;
            if (w <= 0 || _scrollAnim is { IsEnabled: true }) return;
            double r = SwipeHost.Offset.X % w;
            if (r > 0.5 && w - r > 0.5) { _gestoAttivo = true; FineGesto(); }
            else _gestoAttivo = false;
        };
        t.Start();
    }

    private Point _puntoInizio, _puntoUltimo;
    private bool _gestoAttivo;
    private int _idGesto;
    private int _paginaInizioGesto;
    private readonly List<(long ms, double x)> _campioni = new();

    private void CampionaOffset()
    {
        if (!_gestoAttivo) return;
        _campioni.Add((Environment.TickCount64, SwipeHost.Offset.X));
        if (_campioni.Count > 12) _campioni.RemoveAt(0);
    }

    private void FineGesto()
    {
        if (!_gestoAttivo) return;
        _gestoAttivo = false;
        double w = LarghezzaPagina;
        if (w <= 0) return;
        double trascinato = SwipeHost.Offset.X - _paginaInizioGesto * w;
        // velocità sugli ultimi ~120 ms (px/ms, positiva = verso la pagina successiva)
        double v = 0;
        long adesso = Environment.TickCount64;
        var recenti = _campioni.Where(c => adesso - c.ms <= 120).ToList();
        if (recenti.Count >= 2 && recenti[^1].ms > recenti[0].ms)
            v = (recenti[^1].x - recenti[0].x) / (recenti[^1].ms - recenti[0].ms);
        int dir = 0;
        double dx = _puntoUltimo.X - _puntoInizio.X, dy = _puntoUltimo.Y - _puntoInizio.Y;
        bool inPrevalenzaVerticale = !_orizzontale && Math.Abs(dy) > Math.Abs(dx) * 0.6;  // 0/0 se i move non arrivano: si procede
        if (inPrevalenzaVerticale) dir = 0;
        else if (Math.Abs(v) > 0.12) dir = Math.Sign(v);                      // scatto
        else if (Math.Abs(trascinato) > w * 0.1) dir = Math.Sign(trascinato); // trascinamento
        ScorriAllaPagina(Math.Clamp(_paginaInizioGesto + dir, 0, 5), animato: true);
    }

    /// <summary>Allinea larghezza e altezza delle 4 pagine al viewport dello SwipeHost,
    /// così lo snap orizzontale cade esattamente su un confine di pagina.</summary>
    private void DimensionaPagine()
    {
        double w = SwipeHost.Bounds.Width;
        double h = SwipeHost.Bounds.Height;
        if (w <= 0 || h <= 0) return;
        foreach (var pagina in new[] { HomeAreaTouch, CronologiaAreaTouch, SospesiAreaTouch, StatisticheAreaTouch, GestioneAreaTouch, ImpostazioniAreaTouch })
        {
            pagina.Width = w;
            pagina.Height = h;
        }
    }

    /// <summary>Porta lo SwipeHost sulla pagina <paramref name="indice"/>. Con
    /// <paramref name="animato"/> anima l'offset (tap su bottom nav); lo swipe col dito
    /// usa invece l'inerzia nativa e non passa di qui.</summary>
    private void ScorriAllaPagina(int indice, bool animato)
    {
        double w = LarghezzaPagina;
        if (w <= 0) return;
        double target = Math.Clamp(indice, 0, 5) * w;
        _scrollAnim?.Stop();

        if (!animato)
        {
            SwipeHost.Offset = new Vector(target, 0);
            return;
        }

        double start = SwipeHost.Offset.X;
        if (Math.Abs(target - start) < 0.5) return;
        var orologio = System.Diagnostics.Stopwatch.StartNew();
        const double durataMs = 220;
        _scrollAnim = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        _scrollAnim.Tick += (_, _) =>
        {
            double t = Math.Min(1.0, orologio.Elapsed.TotalMilliseconds / durataMs);
            double e = 1 - Math.Pow(1 - t, 3); // easeOutCubic
            SwipeHost.Offset = new Vector(start + (target - start) * e, 0);
            if (t >= 1.0) _scrollAnim!.Stop();
        };
        _scrollAnim.Start();
    }

    private void OnNavClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string tag } && int.TryParse(tag, out int indice))
            IndiceSezione = indice;
    }

    /// <summary>Salva la sezione attiva in preferenze.json solo se e' cambiata
    /// rispetto all'ultimo salvataggio (Step 13): evita di riscrivere il file
    /// ad ogni evento di scroll touch e di contare il ripristino iniziale
    /// (IndiceSezione = _preferenze.TabIniziale in Caricamento) come un
    /// "cambio" da risalvare.</summary>
    private void SalvaTabSeCambiata()
    {
        if (_storage == null) return;
        int idx = IndiceSezione;
        if (idx == _ultimoIndiceSalvato) return;
        _ultimoIndiceSalvato = idx;

        var prefs = _storage.CaricaPreferenze();
        prefs.TabIniziale = idx;
        _storage.SalvaPreferenze(prefs);
    }

    /// <summary>Wrapper debounced di SalvaTabSeCambiata per il path touch
    /// (vedi commento sul campo _salvaTabDebounce): rimanda il salvataggio
    /// di 300ms ad ogni evento di scroll, cosi' in una transizione multi-tab
    /// (animazione o swipe) si salva una volta sola, a fermata avvenuta, con
    /// l'indice finale corretto.</summary>
    private void SalvaTabSeCambiataDebounced()
    {
        _salvaTabDebounce?.Stop();
        _salvaTabDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _salvaTabDebounce.Tick += (_, _) =>
        {
            _salvaTabDebounce!.Stop();
            SalvaTabSeCambiata();
        };
        _salvaTabDebounce.Start();
    }

    private static readonly string[] _inchiostriSezioni =
    [
        "blu",
        "viola",
        "ocra",
        "terracotta",
        "verdeacqua",
        "grigio"
    ];

    private Button[]? _bottoniNav;
    private TabItem[]? _tabSezioni;
    private int _navAttiva = -1;

    private Button[] BottoniNav => _bottoniNav ??=
    [
        NavHome,
        NavCronologia,
        NavSospesi,
        NavStatistiche,
        NavGestione,
        NavImpostazioni
    ];

    private TabItem[] TabSezioni => _tabSezioni ??=
    [
        TabHome,
        TabCronologia,
        TabSospesi,
        TabStatistiche,
        TabGestione,
        TabImpostazioni
    ];

    /// <summary>
    /// Risolve colore di sezione (usando HexChiaro nel tema chiaro, es. grigio)
    /// e colore del testo di contrasto da <see cref="Inchiostri"/>.
    /// </summary>
    private static (SolidColorBrush Colore, IBrush TestoSopra, bool TestoNero) RisolviColoreSezione(string inchiostroId, bool scuro)
    {
        var inchiostro = Inchiostri.Trova(inchiostroId);
        string hex = !scuro && inchiostro.HexChiaro != null ? inchiostro.HexChiaro : inchiostro.Hex;
        var colore = new SolidColorBrush(Color.Parse(hex));
        IBrush testo = inchiostro.TestoNero ? Brushes.Black : Brushes.White;
        return (colore, testo, inchiostro.TestoNero);
    }

    /// <summary>
    /// Applica a ogni pulsante della bottom-nav il colore pieno della propria sezione
    /// da <see cref="Inchiostri"/> (con HexChiaro per il grigio nel tema chiaro) e
    /// imposta icona e testo nel colore di contrasto dell'inchiostro.
    /// </summary>
    private void AggiornaColoriNav()
    {
        if (!AppEnv.TouchMode) return;
        bool scuro = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        var bottoni = BottoniNav;

        for (int i = 0; i < _inchiostriSezioni.Length; i++)
        {
            var (sfondo, testo, _) = RisolviColoreSezione(_inchiostriSezioni[i], scuro);
            var bottone = bottoni[i];

            bottone.Background = sfondo;
            bottone.Foreground = testo;
            if (bottone.Content is Panel contenuto)
            {
                foreach (var figlio in contenuto.Children)
                {
                    if (figlio is Avalonia.Controls.Shapes.Path icona)
                        icona.Stroke = testo;
                    else if (figlio is TextBlock etichetta)
                        etichetta.Foreground = testo;
                }
            }
        }
    }

    /// <summary>
    /// Applica a ogni tab desktop/web il colore della propria sezione su
    /// <c>BorderBrush</c> (fascia 4 px sotto e sfondo pieno della tab attiva)
    /// e la classe <c>testo-nero</c> per il testo sopra quando selezionata.
    /// </summary>
    private void AggiornaColoriTab()
    {
        if (AppEnv.TouchMode) return;
        bool scuro = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        var tabs = TabSezioni;

        for (int i = 0; i < _inchiostriSezioni.Length; i++)
        {
            var (colore, _, testoNero) = RisolviColoreSezione(_inchiostriSezioni[i], scuro);
            var tab = tabs[i];
            tab.BorderBrush = colore;
            tab.Classes.Set("testo-nero", testoNero);
        }
    }

    /// <summary>Evidenzia la sezione attiva della bottom nav allargandone la colonna a 2.8* (altre 1*).</summary>
    private void AggiornaNav()
    {
        if (!AppEnv.TouchMode) return;
        int attiva = IndiceSezione;
        if (attiva == _navAttiva) return;
        _navAttiva = attiva;

        var bottoni = BottoniNav;
        for (int i = 0; i < bottoni.Length; i++)
        {
            bool eAttiva = i == attiva;
            bottoni[i].Classes.Set("attiva", eAttiva);
            NavGrid.ColumnDefinitions[i].Width = new GridLength(eAttiva ? 2.8 : 1.0, GridUnitType.Star);
        }
    }

    // ------------------------------------------------------------------ SAFE AREA (edge-to-edge Android/iOS)

    /// <summary>
    /// Su Android con targetSdk 35+ l'edge-to-edge è forzato: l'app disegna dietro
    /// status bar e barra di navigazione di sistema, che altrimenti coprono header
    /// e bottom navigation. Le insets reali arrivano da <see cref="AppEnv.SystemBarInsetsPx"/>,
    /// popolate dalla MainActivity Android via <c>WindowInsets.systemBars()</c> (fonte
    /// autorevole: l'InsetsManager di Avalonia qui riporta bottom=0). Su iOS e nel browser
    /// mobile (quando TouchMode e' attivo) nessuno popola AppEnv: la fonte e'
    /// l'InsetsManager del TopLevel se presente, inoltrato da <see cref="InoltraSafeAreaIos"/>.
    /// Le applico come padding: header accent e bottom nav si estendono DIETRO le barre di sistema, ma
    /// il loro contenuto resta nell'area sicura. Solo su touch (desktop: return anticipato).
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!AppEnv.TouchMode) return;

        AppEnv.SystemBarInsetsChanged += OnSystemBarInsetsChanged;
        if (OperatingSystem.IsIOS() || OperatingSystem.IsBrowser())
        {
            _insetsIos = TopLevel.GetTopLevel(this)?.InsetsManager;
            if (_insetsIos != null)
            {
                _insetsIos.SafeAreaChanged += OnSafeAreaIosChanged;
                InoltraSafeAreaIos();
            }
        }
        ApplicaSafeArea();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        AppEnv.SystemBarInsetsChanged -= OnSystemBarInsetsChanged;
        if (_insetsIos != null)
        {
            _insetsIos.SafeAreaChanged -= OnSafeAreaIosChanged;
            _insetsIos = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    // Le insets Android arrivano su thread UI ma le rimando comunque sul dispatcher
    // per disaccoppiarle dal dispatch di sistema.
    private void OnSystemBarInsetsChanged() => Dispatcher.UIThread.Post(ApplicaSafeArea);

    // iOS: l'InsetsManager del TopLevel dà la safe area in unità logiche; la porto in
    // pixel fisici (come fa la MainActivity) e la passo ad AppEnv, così ApplicaSafeArea
    // resta l'unico punto che applica i padding.
    private IInsetsManager? _insetsIos;

    private void OnSafeAreaIosChanged(object? sender, SafeAreaChangedArgs e) => InoltraSafeAreaIos();

    private void InoltraSafeAreaIos()
    {
        if (_insetsIos == null) return;
        var p = _insetsIos.SafeAreaPadding;
        double scala = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        AppEnv.SetSystemBarInsetsPx(p.Left * scala, p.Top * scala, p.Right * scala, p.Bottom * scala);
    }

    private void ApplicaSafeArea()
    {
        if (!AppEnv.TouchMode) return;
        var s = AppEnv.SystemBarInsetsPx;
        // Prima del primo dispatch di WindowInsets le insets sono tutte-zero: non applico
        // nulla (eviterei comunque divisioni inutili) e attendo l'evento reale.
        if (s.Top == 0 && s.Bottom == 0 && s.Left == 0 && s.Right == 0) return;
        // s è in PIXEL FISICI. Le Thickness sono in unità di layout della MainView, che
        // vive dentro un LayoutTransformControl scalato ScalaTouch su un TopLevel con
        // RenderScaling: 1 unità locale = ScalaTouch × RenderScaling pixel fisici. Per
        // riservare S px servono S/(ScalaTouch×RenderScaling) unità. RenderScaling è letto
        // a runtime (su questo device vale 1, non la densità).
        double f = AppEnv.ScalaTouch * (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0);
        HeaderBorder.Padding = new Thickness(20, 12 + s.Top / f, 20, 12);
        BottomNav.Padding = new Thickness(0, 0, 0, s.Bottom / f);
        RootDock.Margin = new Thickness(s.Left / f, 0, s.Right / f, 0);
    }

    // ------------------------------------------------------------------ TASTIERA GLOBALE

    /// <summary>
    /// Step 8: Ctrl+Tab / Ctrl+Shift+Tab cambiano tab principale in modo ciclico.
    /// Funziona ovunque nell'app, anche dentro al quiz (il quiz resta vivo sotto
    /// e si ritrova quando si torna sulla tab Home).
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            const int n = 6;
            int delta = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : +1;
            IndiceSezione = (IndiceSezione + delta + n) % n;
            // Sposto il focus nella view appena attivata cosi' le scorciatoie locali
            // partono immediate senza dover cliccare.
            switch (IndiceSezione)
            {
                case 0: (_homeArea.Content as Control)?.Focus(); break;
                case 1: (_cronologiaArea.Content as Control)?.Focus(); break;
                case 2: (_sospesiArea.Content as Control)?.Focus(); break;
                case 3: (_statisticheArea.Content as Control)?.Focus(); break;
                case 4: (_gestioneArea.Content as Control)?.Focus(); break;
                case 5: (_impostazioniArea.Content as Control)?.Focus(); break;
            }
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    // ------------------------------------------------------------------ BOOT

    private void Caricamento()
    {
        string cartellaUtente = StorageService.CartellaUtenteDefault();

        // Scelta della sorgente dati read-only:
        // - desktop/CLI: i JSON sono copiati nel bin → file system (identico a prima).
        // - Android: nessun file accanto all'eseguibile → risorse embedded (avares://).
        string cartellaDati = AppContext.BaseDirectory;
        IFonteDati fonte;
        string origine;
        if (File.Exists(Path.Combine(cartellaDati, "materie.json")))
        {
            fonte = new FileSystemFonteDati(cartellaDati);
            origine = string.Format(Strings.Shell_OrigineFileSystem, cartellaDati);
        }
        else
        {
            fonte = new AvaloniaResourceFonteDati();
            origine = Strings.Shell_OrigineRisorse;
        }
        FooterText.Text = string.Format(Strings.Shell_Footer, origine, cartellaUtente);

        try
        {
            _storage = new StorageService(fonte, cartellaUtente);
            _storage.LinguaContenuti = Lingue.Corrente;
            _contenutiUtente = new ContenutiUtenteService(cartellaUtente);

            _preferenze = _storage.CaricaPreferenze();
            Application.Current!.RequestedThemeVariant = _preferenze.TemaScuro ? ThemeVariant.Dark : ThemeVariant.Light;
            Tavolozza.Applica(_preferenze.ColoreAccento);

            _materie = _storage.CaricaMaterie();
            var materieUtente = _contenutiUtente.CaricaMaterieUtente();
            foreach (var m in materieUtente) m.Importata = true;
            _materie.AddRange(materieUtente);

            _tutteDomande = _storage.CaricaTutteLeDomande(_materie);
            EtichetteContenuti.Correnti = _storage.Etichette;
            foreach (var m in _materie)
                _tutteDomande.AddRange(_contenutiUtente.CaricaDomandeUtente(m));

            // Sezioni nascoste dall'utente: (materiaId, categoria). _tutteDomande
            // resta completo (serve a Gestione per mostrarle/ripristinarle e alla
            // mappa per riprendere i sospesi); le domande ATTIVE che vanno nel
            // pool dei quiz (Home) escludono le sezioni nascoste.
            var esclSet = new HashSet<(string, string)>(
                _contenutiUtente.CaricaSezioniEscluse().Select(s => (s.MateriaId, s.Categoria)));
            _domandeAttive = esclSet.Count == 0
                ? _tutteDomande
                : _tutteDomande.Where(d => !esclSet.Contains((d.MateriaId, d.Categoria))).ToList();

            _mappaDomande = _tutteDomande.ToDictionary(d => d.Id, d => d);
            _schedeLab = LaboratorioService.CaricaSchede(fonte, _storage.LinguaContenuti);

            StatoHeaderText.Text = string.Format(Strings.Shell_Conteggi, _materie.Count, _domandeAttive.Count);

            // ---------- Tab Home ----------
            _homeView = new HomeView();
            _homeView.Inizializza(_materie, _domandeAttive, _preferenze, _schedeLab);
            AggiornaStoricoHome();
            _homeView.QuizAvviato += OnQuizAvviato;
            _homeView.LaboratorioAvviato += OnLaboratorioAvviato;
            _homeView.LaboratorioCambiato += OnLaboratorioCambiato;
            _homeView.PropertyChanged += OnHomeOpzioniCambiate;
            _homeArea.Content = _homeView;

            // ---------- Tab Cronologia ----------
            _cronologiaView = new CronologiaView();
            _cronologiaView.Inizializza(_storage, _preferenze.Laboratorio);
            _cronologiaView.LaboratorioCambiato += OnLaboratorioCambiato;
            _cronologiaArea.Content = _cronologiaView;

            // ---------- Tab Sospesi ----------
            _sospesiView = new SospesiView();
            _sospesiView.Inizializza(_storage, _preferenze.Laboratorio);
            _sospesiView.LaboratorioCambiato += OnLaboratorioCambiato;
            _sospesiView.RiprendiRichiesto += OnRiprendiSospesa;
            _sospesiView.RiprendiLabRichiesto += OnRiprendiSospesaLab;
            _sospesiArea.Content = _sospesiView;

            // ---------- Tab Statistiche ----------
            _statisticheView = new StatisticheView();
            _statisticheView.Inizializza(_storage, _preferenze.Laboratorio);
            _statisticheView.LaboratorioCambiato += OnLaboratorioCambiato;
            _statisticheView.RipassaRichiesto += OnRipassaRichiesto;
            _statisticheArea.Content = _statisticheView;

            // ---------- Tab Gestione contenuti ----------
            _gestioneView = new GestioneContenutiView();
            _gestioneView.Inizializza(_storage, _contenutiUtente!, _materie, _tutteDomande, esclSet);
            // Post invece di chiamata diretta: ContenutiCambiati viene sollevato da un
            // Click handler di _gestioneView stessa, che _gestioneArea.Content sta per
            // sostituire. Ricostruire subito l'albero visivo (sincrono, rientrante)
            // mentre Avalonia sta ancora smontando l'evento Click in corso blocca la
            // UI; Post rimanda il refresh al termine del dispatch dell'evento corrente.
            _gestioneView.ContenutiCambiati += (_, _) => Dispatcher.UIThread.Post(Caricamento);
            _gestioneArea.Content = _gestioneView;

            // ---------- Tab Impostazioni ----------
            _impostazioniView = new ImpostazioniView();
            // Stessi conteggi dell'header: materie e domande ATTIVE (al netto
            // delle sezioni nascoste), cosi' l'About non contraddice la barra in alto.
            _impostazioniView.Inizializza(_storage, _preferenze, _materie, _domandeAttive);
            _impostazioniArea.Content = _impostazioniView;

            // Ripristino l'ultima sezione aperta (Step 13). Clamp: un
            // preferenze.json modificato a mano con un TabIniziale fuori
            // range (JSON valido, valore assurdo) manderebbe Tabs.SelectedIndex
            // fuori range su desktop -> nessuna tab selezionata, e il successivo
            // SelectionChanged persisterebbe quel -1 rendendo il danno permanente.
            // Imposto _ultimoIndiceSalvato PRIMA di assegnare IndiceSezione:
            // l'assegnazione scatena comunque SelectionChanged/ScrollChanged ->
            // SalvaTabSeCambiata, che cosi' vede idx == _ultimoIndiceSalvato e non
            // ri-salva un "cambio" che in realta' e' solo il ripristino iniziale.
            int tabIniziale = Math.Clamp(_preferenze.TabIniziale, 0, 5);
            _ultimoIndiceSalvato = tabIniziale;
            IndiceSezione = tabIniziale;
        }
        catch (Exception ex)
        {
            ErrorePanel.IsVisible = true;
            ErroreText.Text = ex.Message;
            _homeArea.Content = new PlaceholderView(
                Strings.Shell_ImpossibileAvviare,
                Strings.Shell_ImpossibileAvviareDettaglio);
        }
    }

    // ------------------------------------------------------------------ NAVIGAZIONE TAB HOME

    private void OnQuizAvviato(object? sender, SessioneQuiz sessione)
    {
        AvviaQuizView(sessione);
    }

    private void OnLaboratorioAvviato(object? sender, SessioneLaboratorio sessione)
    {
        AvviaLaboratorioView(sessione);
    }

    private static readonly HashSet<string> _proprietaOpzioniQuiz = new(StringComparer.Ordinal)
    {
        nameof(HomeView.RandomizzaOrdine), nameof(HomeView.Rotazione), nameof(HomeView.SecondaPossibilita), nameof(HomeView.Cronometro),
        nameof(HomeView.LimitaDomande), nameof(HomeView.LimiteN), nameof(HomeView.ModalitaTempo), nameof(HomeView.MinutiTempo),
        nameof(HomeView.TipiEsclusi), nameof(HomeView.LivelliEsclusi),
        nameof(HomeView.LabLivello), nameof(HomeView.LabRipassa), nameof(HomeView.LabSchede)
    };

    private void OnLaboratorioCambiato(object? sender, bool laboratorio)
    {
        if (_propagandoLaboratorio) return;
        _propagandoLaboratorio = true;
        try
        {
            _preferenze.Laboratorio = laboratorio;

            if (_homeView != null && _homeView.Laboratorio != laboratorio)
                _homeView.Laboratorio = laboratorio;
            if (_cronologiaView != null && _cronologiaView.Laboratorio != laboratorio)
                _cronologiaView.Laboratorio = laboratorio;
            if (_statisticheView != null && _statisticheView.Laboratorio != laboratorio)
                _statisticheView.Laboratorio = laboratorio;
            if (_sospesiView != null && _sospesiView.Laboratorio != laboratorio)
                _sospesiView.Laboratorio = laboratorio;

            if (_storage != null)
            {
                var prefs = _storage.CaricaPreferenze();
                if (prefs.Laboratorio != laboratorio)
                {
                    prefs.Laboratorio = laboratorio;
                    _storage.SalvaPreferenze(prefs);
                }
            }
        }
        finally
        {
            _propagandoLaboratorio = false;
        }
    }

    /// <summary>Salva su preferenze.json ad ogni cambio di una delle opzioni
    /// quiz in Home (Step 13). Filtra sul nome proprieta' per non salvare
    /// per cambi di stato non rilevanti (es. RiepilogoSelezione).</summary>
    private void OnHomeOpzioniCambiate(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_storage == null || _homeView == null) return;
        if (e.PropertyName == null || !_proprietaOpzioniQuiz.Contains(e.PropertyName)) return;

        var prefs = _storage.CaricaPreferenze();
        prefs.RandomizzaOrdine   = _homeView.RandomizzaOrdine;
        prefs.Rotazione          = _homeView.Rotazione;
        prefs.SecondaPossibilita = _homeView.SecondaPossibilita;
        prefs.Cronometro         = _homeView.Cronometro;
        prefs.LimitaDomande    = _homeView.LimitaDomande;
        prefs.LimiteN          = (int)_homeView.LimiteN;
        prefs.ModalitaTempo    = _homeView.ModalitaTempo;
        prefs.MinutiTempo      = (int)_homeView.MinutiTempo;
        prefs.TipiEsclusi      = _homeView.TipiEsclusi;
        prefs.LivelliEsclusi   = _homeView.LivelliEsclusi;
        prefs.Laboratorio      = _homeView.Laboratorio;
        prefs.LabLivello       = _homeView.LabLivello;
        prefs.LabRipassa       = _homeView.LabRipassa;
        prefs.LabSchede        = (int)_homeView.LabSchede;
        _storage.SalvaPreferenze(prefs);
    }

    private void AvviaQuizView(SessioneQuiz sessione)
    {
        if (_storage == null) return;
        var quizView = new QuizView(sessione, _storage);
        quizView.QuizConcluso += OnQuizConcluso;
        quizView.QuizMessoInPausa += OnQuizMessoInPausa;
        _homeArea.Content = quizView;
        IndiceSezione = 0; // resta sulla sezione Home, ma sostituisce il contenuto
    }

    private void OnQuizConcluso(object? sender, SessioneQuiz sessione)
    {
        // Salvataggio cronologia (solo se c'e' almeno una risposta data)
        if (_storage != null && sessione.Risultato.TotaleDomande > 0)
        {
            try
            {
                _storage.SalvaRisultato(sessione.Risultato);
                // La nuova sessione dev'essere visibile la prossima volta che si apre il tab.
                _cronologiaView?.Ricarica();
                _statisticheView?.Ricarica();
                AggiornaStoricoHome();
            }
            catch (Exception ex)
            {
                ErrorePanel.IsVisible = true;
                ErroreText.Text = string.Format(Strings.Shell_ErroreSalvaCronologia, ex.Message);
            }
        }

        // Se la sessione era stata ripresa da una pausa, rimuovo la pausa originale.
        // Stesso comportamento del console (vedi Program.EseguiSessione*: EliminaPausa(stato.SessioneId)).
        if (_storage != null && sessione.IdSessionePausa is { } idPausa)
        {
            try
            {
                _storage.EliminaPausa(idPausa);
                _sospesiView?.Ricarica();
            }
            catch (Exception ex)
            {
                ErrorePanel.IsVisible = true;
                ErroreText.Text = string.Format(Strings.Shell_ErroreEliminaPausa, ex.Message);
            }
        }

        var riep = new RiepilogoView(sessione.Risultato);
        riep.TornaAllaHome += (_, _) => RitornaAllaHome();
        _homeArea.Content = riep;
    }

    private void AvviaLaboratorioView(SessioneLaboratorio sessione)
    {
        if (_storage == null) return;
        var labView = new LaboratorioView(sessione, _storage);
        labView.LaboratorioConcluso += OnLaboratorioConcluso;
        labView.LaboratorioMessoInPausa += OnLaboratorioMessoInPausa;
        _homeArea.Content = labView;
        IndiceSezione = 0;
    }

    private void OnLaboratorioConcluso(object? sender, SessioneLaboratorio sessione)
    {
        if (_storage != null && sessione.Risultato.TotaleDomande > 0)
        {
            try
            {
                _storage.SalvaRisultato(sessione.Risultato);
                _cronologiaView?.Ricarica();
                _statisticheView?.Ricarica();
                AggiornaStoricoHome();
            }
            catch (Exception ex)
            {
                ErrorePanel.IsVisible = true;
                ErroreText.Text = string.Format(Strings.Shell_ErroreSalvaCronologia, ex.Message);
            }
        }

        // Se la sessione era stata ripresa da una pausa, rimuovo la pausa originale.
        if (_storage != null && sessione.IdSessionePausa is { } idPausa)
        {
            try
            {
                _storage.EliminaPausaLab(idPausa);
                _sospesiView?.Ricarica();
            }
            catch (Exception ex)
            {
                ErrorePanel.IsVisible = true;
                ErroreText.Text = string.Format(Strings.Shell_ErroreEliminaPausa, ex.Message);
            }
        }

        var riep = new RiepilogoView(sessione.Risultato);
        riep.TornaAllaHome += (_, _) => RitornaAllaHome();
        _homeArea.Content = riep;
    }

    private void OnQuizMessoInPausa(object? sender, EventArgs e)
    {
        // La pausa e' gia' stata salvata dal QuizView. Torno alla Home e ricarico i sospesi.
        RitornaAllaHome();
        _sospesiView?.Ricarica();
    }

    private void OnLaboratorioMessoInPausa(object? sender, EventArgs e)
    {
        RitornaAllaHome();
        _sospesiView?.Ricarica();
    }

    private void OnRiprendiSospesa(object? sender, SessionePausa pausa)
    {
        if (_storage == null) return;
        try
        {
            var sessione = SessioneQuiz.RiprendiDa(pausa, _mappaDomande);
            ErrorePanel.IsVisible = false;
            AvviaQuizView(sessione);
        }
        catch (InvalidOperationException ex)
        {
            ErrorePanel.IsVisible = true;
            ErroreText.Text = string.Format(Strings.Shell_PausaRimossa, ex.Message);
            try { _storage.EliminaPausa(pausa.SessioneId); }
            catch { /* best effort: la card rimane finche' non si preme Aggiorna */ }
            _sospesiView?.Ricarica();
        }
    }

    private void OnRiprendiSospesaLab(object? sender, PausaLaboratorio pausa)
    {
        if (_storage == null) return;
        try
        {
            var sessione = SessioneLaboratorio.RiprendiDa(pausa, _schedeLab);
            ErrorePanel.IsVisible = false;
            AvviaLaboratorioView(sessione);
        }
        catch (InvalidOperationException ex)
        {
            ErrorePanel.IsVisible = true;
            ErroreText.Text = string.Format(Strings.Shell_PausaRimossa, ex.Message);
            try { _storage.EliminaPausaLab(pausa.SessioneId); }
            catch { /* best effort: la card rimane finche' non si preme Aggiorna */ }
            _sospesiView?.Ricarica();
        }
    }

    private void RitornaAllaHome()
    {
        ErrorePanel.IsVisible = false;
        if (_homeView == null)
        {
            _homeView = new HomeView();
            _homeView.Inizializza(_materie, _domandeAttive.Count > 0 ? _domandeAttive : _tutteDomande, _preferenze, _schedeLab);
            AggiornaStoricoHome();
            _homeView.QuizAvviato += OnQuizAvviato;
            _homeView.LaboratorioAvviato += OnLaboratorioAvviato;
            _homeView.LaboratorioCambiato += OnLaboratorioCambiato;
            _homeView.PropertyChanged += OnHomeOpzioniCambiate;
        }
        _homeArea.Content = _homeView;
    }

    private void AggiornaStoricoHome()
    {
        if (_homeView == null || _storage == null) return;
        try
        {
            var cronologia = _storage.CaricaCronologia();
            var stat = new StatisticheService().Calcola(cronologia);
            _homeView.AggiornaStorico(stat, cronologia);
        }
        catch
        {
            // Come in StatisticheView.Ricarica: non bloccare l'UI se la cronologia non e' leggibile
        }
    }

    private void OnRipassaRichiesto(object? sender, RichiestaRipasso req)
    {
        if (req.IdDomandeSbagliate == null || req.IdDomandeSbagliate.Count == 0) return;

        if (req.MateriaCanon.EndsWith(LaboratorioService.SuffissoMateria, StringComparison.Ordinal))
        {
            var coppie = LaboratorioService.CoppieDaId(_schedeLab, req.IdDomandeSbagliate);
            if (coppie.Count == 0) return;

            string nomeLinguaggio = req.MateriaCanon[..^LaboratorioService.SuffissoMateria.Length];
            var sessioneLab = new SessioneLaboratorio(coppie, nomeLinguaggio, coppie[0].Scheda.Livello);
            AvviaLaboratorioView(sessioneLab);
            return;
        }

        var mappaAttive = _domandeAttive.ToDictionary(d => d.Id, d => d, StringComparer.OrdinalIgnoreCase);
        var domande = new List<Domanda>();
        foreach (var id in req.IdDomandeSbagliate)
        {
            if (!string.IsNullOrEmpty(id) && mappaAttive.TryGetValue(id, out var d))
                domande.Add(d);
        }

        if (domande.Count == 0) return;

        var prefs = _storage?.CaricaPreferenze() ?? _preferenze;
        string materiaMostrata = EtichetteContenuti.Correnti.Materia(req.MateriaCanon);
        string nomeMod;
        List<string> categorie;
        if (!string.IsNullOrEmpty(req.CategoriaCanon))
        {
            string catMostrata = EtichetteContenuti.Correnti.Categoria(req.MateriaCanon, req.CategoriaCanon);
            nomeMod = string.Format(Strings.Statistiche_RipassaModalita, materiaMostrata, catMostrata);
            categorie = new List<string> { $"{req.MateriaCanon}/{req.CategoriaCanon}" };
        }
        else
        {
            nomeMod = string.Format(Strings.Statistiche_RipassaMateriaModalita, materiaMostrata);
            categorie = new List<string>();
        }

        var materiaObj = _materie.FirstOrDefault(m => string.Equals(m.Nome, req.MateriaCanon, StringComparison.OrdinalIgnoreCase));
        var materiaId = materiaObj?.Id ?? domande[0].MateriaId;

        var opz = new OpzioniQuiz
        {
            NomeModalita            = nomeMod,
            MateriaNomeLabel        = req.MateriaCanon,
            RandomizzaOrdineDomande = prefs.RandomizzaOrdine,
            Rotazione               = prefs.Rotazione,
            SecondaPossibilita      = prefs.SecondaPossibilita,
            Cronometro              = prefs.Cronometro,
            LimiteDomande           = 0,
            LimiteTempoMinuti       = 0,
            Materie                 = new List<string> { materiaId },
            Categorie               = categorie
        };

        var sessione = new SessioneQuiz(domande, opz);
        AvviaQuizView(sessione);
    }
}
