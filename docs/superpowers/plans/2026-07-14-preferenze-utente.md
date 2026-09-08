# Preferenze utente persistite (Step 13 + Step 10 dark mode) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Al riavvio l'app ricorda l'ultima sezione aperta, le ultime opzioni quiz (randomizza/rotazione/cronometro/limite/modalità a tempo) e una scelta esplicita di tema Chiaro/Scuro, persistite in `preferenze.json` nella cartella utente. Introduce anche una 6ª sezione "Impostazioni" (il toggle tema non aveva alcuna UI prima d'ora) e corregge l'unico problema di leggibilità dark-mode già noto (cronometro).

**Architecture:** Nuovo modello `PreferenzeUtente` (`Wsa.Quiz.Core.Models`) e due metodi `CaricaPreferenze`/`SalvaPreferenze` su `StorageService`, stesso pattern (scrittura atomica, fallback ai default se il file è assente/corrotto) già usato per `cronologia.json`/`quiz_in_pausa.json`. `MainView.Caricamento()` carica le preferenze una sola volta all'avvio, applica subito il tema (`Application.Current.RequestedThemeVariant`), passa le opzioni quiz a `HomeView.Inizializza(...)` e ripristina l'ultima sezione. I salvataggi avvengono on-change: cambio tab (una volta sola, non al ripristino), cambio di una opzione quiz in `HomeView` (via l'evento `PropertyChanged` già esistente), toggle tema nella nuova `ImpostazioniView`. Ogni salvataggio ricarica il file corrente e ne aggiorna solo il campo pertinente (stesso pattern read-modify-write di `SalvaPausa`).

**Tech Stack:** .NET 10, Avalonia 12.0.2 (Fluent), C# code-behind + INPC (no MVVM), `System.Text.Json`.

## Global Constraints

- **`net10.0`**, **Avalonia 12.0.2** (API v12).
- **Compiled bindings**: `x:DataType` obbligatorio su ogni `UserControl` che usa `{Binding}` (Trappola 5 in CLAUDE.md).
- **Nessun framework di test nel repo** (nessun progetto xunit/nunit/mstest). Verifica = `dotnet build` verde + verifica manuale desktop (nessun test automatico UI/unit nel repo). Non esistono file di test da scrivere in questo piano.
- **Verifica su device Android fisico esplicitamente rimandata** (deciso in fase di brainstorming): si verifica solo che `dotnet build wsa.quiz.android -c Release` resti verde, non il comportamento touch reale a schermo.
- **`docs/` è in `.gitignore`** in questo repo (sezione "Aggiunti manualmente"): la spec e questo piano restano solo locali, non vanno committati né pushati — solo i file applicativi elencati sotto "File Structure" fanno parte dei commit.
- **APK installabile** solo con `dotnet build wsa.quiz.android -c Release` (Debug non produce APK).
- Spec: `docs/superpowers/specs/2026-07-14-preferenze-utente-design.md`.

---

## File Structure

- `wsa.quiz.core/Models/PreferenzeUtente.cs` — **nuovo**: modello dati (tab iniziale, tema, opzioni quiz), default identici a quelli oggi hardcoded in `HomeView`.
- `wsa.quiz.core/Services/StorageService.cs` — modifica: `CaricaPreferenze()`/`SalvaPreferenze(...)`, stesso pattern I/O atomico di cronologia/pausa.
- `wsa.quiz.app/QuizColors.cs` — modifica: `TestoNeutro` (colore neutro del cronometro) da `#1F1F1F` (quasi-nero, illeggibile su sfondo scuro) a un grigio medio leggibile su entrambi i temi.
- `wsa.quiz.app/Views/ImpostazioniView.axaml` + `.axaml.cs` — **nuovo**: toggle Chiaro/Scuro.
- `wsa.quiz.app/MainView.axaml` + `.axaml.cs` — modifica: 6ª sezione "Impostazioni" (tab/icona/pagina touch), caricamento preferenze al boot + applicazione tema, ripristino ultima sezione, salvataggio on-change.
- `wsa.quiz.app/Views/HomeView.axaml.cs` — modifica: `Inizializza(...)` riceve `PreferenzeUtente` e inizializza le opzioni da lì invece che dai default hardcoded.

---

### Task 1: Modello `PreferenzeUtente`

**Files:**
- Create: `wsa.quiz.core/Models/PreferenzeUtente.cs`

**Interfaces:**
- Produces: `PreferenzeUtente` con proprietà `TabIniziale` (int, default 0), `TemaScuro` (bool, default false), `RandomizzaOrdine` (bool, default true), `Rotazione`/`Cronometro`/`LimitaDomande`/`ModalitaTempo` (bool, default false), `LimiteN`/`MinutiTempo` (int, default 10). Stesso stile di `SessionePausa.cs` (nessun `[JsonPropertyName]`: è un file interno, non un file da editare a mano come `materie.json`).

- [ ] **Step 1: Creare il modello**

Creare `wsa.quiz.core/Models/PreferenzeUtente.cs`:

```csharp
namespace Wsa.Quiz.Core.Models;

/// <summary>
/// Preferenze utente persistite in preferenze.json: ultima sezione aperta,
/// tema, ultime opzioni quiz. I default coincidono con quelli hardcoded in
/// HomeView, cosi' senza il file (utenti esistenti) il comportamento resta
/// identico a prima dell'introduzione di questa persistenza.
/// </summary>
public class PreferenzeUtente
{
    public int TabIniziale { get; set; } = 0;
    public bool TemaScuro { get; set; } = false;

    public bool RandomizzaOrdine { get; set; } = true;
    public bool Rotazione { get; set; } = false;
    public bool Cronometro { get; set; } = false;
    public bool LimitaDomande { get; set; } = false;
    public int LimiteN { get; set; } = 10;
    public bool ModalitaTempo { get; set; } = false;
    public int MinutiTempo { get; set; } = 10;
}
```

- [ ] **Step 2: Build libreria core**

Run: `dotnet build "wsa.quiz.core/Wsa.Quiz.Core.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 3: Commit**

```bash
git add wsa.quiz.core/Models/PreferenzeUtente.cs
git commit -m "feat(core): modello PreferenzeUtente"
```

---

### Task 2: `StorageService` — `CaricaPreferenze`/`SalvaPreferenze`

**Files:**
- Modify: `wsa.quiz.core/Services/StorageService.cs`

**Interfaces:**
- Consumes: `PreferenzeUtente` (Task 1).
- Produces: `StorageService.CaricaPreferenze()` → `PreferenzeUtente` (mai null, default se file assente/corrotto); `StorageService.SalvaPreferenze(PreferenzeUtente)` → `void`, scrittura atomica.

- [ ] **Step 1: Aggiungere il campo file e la validazione nel costruttore**

In `wsa.quiz.core/Services/StorageService.cs`, il campo:

```csharp
    private readonly string _filePausa;
```

diventa:

```csharp
    private readonly string _filePausa;
    private readonly string _filePreferenze;
```

Nel costruttore `StorageService(IFonteDati fonte, string cartellaUtente)`, la riga:

```csharp
        _filePausa      = Path.GetFullPath(Path.Combine(_cartellaUtente, "quiz_in_pausa.json"));
```

diventa:

```csharp
        _filePausa      = Path.GetFullPath(Path.Combine(_cartellaUtente, "quiz_in_pausa.json"));
        _filePreferenze = Path.GetFullPath(Path.Combine(_cartellaUtente, "preferenze.json"));
```

e, subito dopo il controllo esistente:

```csharp
        if (!_filePausa.StartsWith(ConSep(_cartellaUtente), StringComparison.Ordinal))
            throw new ArgumentException("Percorso quiz_in_pausa.json fuori dalla cartella utente.", nameof(cartellaUtente));
```

aggiungere:

```csharp
        if (!_filePreferenze.StartsWith(ConSep(_cartellaUtente), StringComparison.Ordinal))
            throw new ArgumentException("Percorso preferenze.json fuori dalla cartella utente.", nameof(cartellaUtente));
```

- [ ] **Step 2: Aggiungere i due metodi**

Alla fine della classe (dopo il metodo `EliminaPausa`, prima della graffa di chiusura finale `}` della classe), aggiungere una nuova regione:

```csharp

    // ---------------------------------------------------------------- PREFERENZE UTENTE

    public PreferenzeUtente CaricaPreferenze()
    {
        if (!File.Exists(_filePreferenze)) return new PreferenzeUtente();
        try
        {
            string json = LeggiFileCondiviso(_filePreferenze);
            return JsonSerializer.Deserialize<PreferenzeUtente>(json, OpzioniLettura) ?? new PreferenzeUtente();
        }
        catch
        {
            // File assente/corrotto: non blocco l'app, ricomincio dai default.
            return new PreferenzeUtente();
        }
    }

    public void SalvaPreferenze(PreferenzeUtente preferenze)
    {
        string json = JsonSerializer.Serialize(preferenze, OpzioniScrittura);
        ScriviAtomico(_filePreferenze, json);
    }
```

- [ ] **Step 3: Build libreria core**

Run: `dotnet build "wsa.quiz.core/Wsa.Quiz.Core.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 4: Commit**

```bash
git add wsa.quiz.core/Services/StorageService.cs
git commit -m "feat(core): StorageService.CaricaPreferenze/SalvaPreferenze"
```

---

### Task 3: Fix leggibilità cronometro in dark mode (Step 10, problema noto)

**Files:**
- Modify: `wsa.quiz.app/QuizColors.cs`

**Interfaces:**
- Nessuna firma cambia: `QuizColors.TempoNeutro` resta `IBrush`, cambia solo il colore che rappresenta.

Contesto: `QuizView.axaml` lega `Foreground="{Binding TempoBrush}"` del cronometro dentro un `Border` con `Background="{DynamicResource SystemRegionBrush}"` (dinamico, cambia con il tema). `TempoBrush` di default vale `QuizColors.TempoNeutro`, oggi hardcoded `#1F1F1F` (quasi-nero): in tema scuro `SystemRegionBrush` diventa scuro e il testo diventa quasi invisibile. Fix: un grigio medio, leggibile su sfondo sia chiaro che scuro, senza dover rendere il brush theme-aware (nessun redesign).

- [ ] **Step 1: Cambiare il colore**

In `wsa.quiz.app/QuizColors.cs`, le righe:

```csharp
    // Colore neutro del testo (usato dal cronometro fuori soglia di allerta).
    public const string TestoNeutro = "#1F1F1F";
```

diventano:

```csharp
    // Colore neutro del testo (usato dal cronometro fuori soglia di allerta).
    // Grigio medio invece di quasi-nero: leggibile sia su sfondo chiaro che
    // scuro senza dover rendere il brush theme-aware (Step 10, fix mirato).
    public const string TestoNeutro = "#767676";
```

- [ ] **Step 2: Build progetto app**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0`.

La verifica visiva (cronometro leggibile in tema scuro durante un quiz a tempo) avviene nel Task 8, quando il toggle tema è disponibile.

- [ ] **Step 3: Commit**

```bash
git add wsa.quiz.app/QuizColors.cs
git commit -m "fix(app): cronometro illeggibile in dark mode (TestoNeutro quasi-nero)"
```

---

### Task 4: `ImpostazioniView` — toggle Chiaro/Scuro

**Files:**
- Create: `wsa.quiz.app/Views/ImpostazioniView.axaml`
- Create: `wsa.quiz.app/Views/ImpostazioniView.axaml.cs`

**Interfaces:**
- Consumes: `PreferenzeUtente` (Task 1), `StorageService.CaricaPreferenze`/`SalvaPreferenze` (Task 2).
- Produces: `ImpostazioniView.Inizializza(StorageService storage, PreferenzeUtente preferenze)`; property `TemaScuro` (bool, INPC). Al cambio: salva su `preferenze.json` e applica `Application.Current.RequestedThemeVariant` dal vivo.

- [ ] **Step 1: Creare l'XAML**

Creare `wsa.quiz.app/Views/ImpostazioniView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:views="using:Wsa.Quiz.App.Views"
             mc:Ignorable="d"
             x:Class="Wsa.Quiz.App.Views.ImpostazioniView"
             x:DataType="views:ImpostazioniView">

    <DockPanel>

        <!-- ======================================== HEADER -->
        <Grid DockPanel.Dock="Top"
              ColumnDefinitions="*,Auto"
              Margin="20,16,20,8">
            <StackPanel Spacing="2">
                <TextBlock Text="Impostazioni"
                           FontSize="22" FontWeight="SemiBold"/>
                <TextBlock Text="Preferenze dell'app, salvate in locale."
                           FontSize="12"
                           Foreground="{DynamicResource SystemBaseMediumColor}"/>
            </StackPanel>
        </Grid>

        <ScrollViewer>
            <StackPanel Margin="20,8,20,20" Spacing="16">
                <Border Background="{DynamicResource SystemRegionBrush}"
                        BorderBrush="{DynamicResource SystemBaseLowColor}"
                        BorderThickness="1"
                        CornerRadius="6"
                        Padding="16,14">
                    <Grid ColumnDefinitions="*,Auto">
                        <StackPanel Spacing="2" VerticalAlignment="Center">
                            <TextBlock Text="Tema" FontWeight="SemiBold"/>
                            <TextBlock Text="Chiaro o scuro. La scelta viene ricordata."
                                       FontSize="12"
                                       Foreground="{DynamicResource SystemBaseMediumColor}"/>
                        </StackPanel>
                        <ToggleSwitch Grid.Column="1"
                                      x:Name="TemaToggle"
                                      OnContent="Scuro"
                                      OffContent="Chiaro"
                                      IsChecked="{Binding TemaScuro}"/>
                    </Grid>
                </Border>
            </StackPanel>
        </ScrollViewer>

    </DockPanel>

</UserControl>
```

- [ ] **Step 2: Creare il code-behind**

Creare `wsa.quiz.app/Views/ImpostazioniView.axaml.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.Views;

/// <summary>
/// Schermata "Impostazioni": toggle tema chiaro/scuro (Step 10 + Step 13).
/// E' la sua stessa DataContext, stesso pattern non-MVVM delle altre view.
/// </summary>
public partial class ImpostazioniView : UserControl, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;

    private StorageService? _storage;

    private bool _temaScuro;
    public bool TemaScuro
    {
        get => _temaScuro;
        set
        {
            if (_temaScuro == value) return;
            _temaScuro = value;
            Raise();
            ApplicaTema();
            Salva();
        }
    }

    public ImpostazioniView()
    {
        InitializeComponent();
        DataContext = this;
    }

    /// <summary>Chiamato dalla MainView al boot con le preferenze gia' caricate.</summary>
    public void Inizializza(StorageService storage, PreferenzeUtente preferenze)
    {
        _storage = storage;
        _temaScuro = preferenze.TemaScuro;
        Raise(nameof(TemaScuro));
    }

    private void ApplicaTema()
    {
        Application.Current!.RequestedThemeVariant = _temaScuro ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private void Salva()
    {
        if (_storage == null) return;
        var prefs = _storage.CaricaPreferenze();
        prefs.TemaScuro = _temaScuro;
        _storage.SalvaPreferenze(prefs);
    }

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 3: Build progetto app**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0` (la view non è ancora referenziata da `MainView`, quindi non è raggiungibile a runtime: verifica solo di compilazione in questo task, il wiring è nel Task 5).

- [ ] **Step 4: Commit**

```bash
git add wsa.quiz.app/Views/ImpostazioniView.axaml wsa.quiz.app/Views/ImpostazioniView.axaml.cs
git commit -m "feat(app): ImpostazioniView con toggle tema chiaro/scuro"
```

---

### Task 5: Wiring 6ª sezione "Impostazioni" in `MainView` + caricamento preferenze al boot

**Files:**
- Modify: `wsa.quiz.app/MainView.axaml`
- Modify: `wsa.quiz.app/MainView.axaml.cs`

**Interfaces:**
- Consumes: `ImpostazioniView` (Task 4), `StorageService.CaricaPreferenze` (Task 2).
- Produces: campo `MainView._preferenze` (`PreferenzeUtente`, caricato una volta in `Caricamento()`), riusato dai Task 6 e 7.

- [ ] **Step 1: XAML — bottom-nav a 6 colonne + icona Impostazioni**

In `wsa.quiz.app/MainView.axaml`, la riga:

```xml
            <Grid ColumnDefinitions="*,*,*,*,*">
```

diventa:

```xml
            <Grid ColumnDefinitions="*,*,*,*,*,*">
```

Dopo il bottone `NavGestione` (che chiude con `</Button>` seguito da `</Grid>`), cioè dopo:

```xml
                <Button x:Name="NavGestione" Grid.Column="4" Classes="nav" Tag="4" Click="OnNavClick">
                    <Path x:Name="NavGestioneIcon" Width="24" Height="24" Stretch="Uniform"
                          Stroke="White" StrokeThickness="2.4"
                          StrokeLineCap="Round"
                          Data="M12 4 V20 M4 12 H20"/>
                </Button>
```

aggiungere (icona "sliders": tre tracce orizzontali con manopola, stile line-art coerente con le altre — le manopole sono disegnate con un arco quasi-chiuso, stessa tecnica del cerchio dell'icona Cronologia):

```xml
                <Button x:Name="NavImpostazioni" Grid.Column="5" Classes="nav" Tag="5" Click="OnNavClick">
                    <Path x:Name="NavImpostazioniIcon" Width="24" Height="24" Stretch="Uniform"
                          Stroke="White" StrokeThickness="2"
                          StrokeLineCap="Round"
                          Data="M4 6 H20 M10.6 6 A1.6 1.6 0 1 1 10.59 6 M4 12 H20 M16.6 12 A1.6 1.6 0 1 1 16.59 12 M4 18 H20 M12.6 18 A1.6 1.6 0 1 1 12.59 18"/>
                </Button>
```

- [ ] **Step 2: XAML — pagina touch aggiuntiva**

La riga:

```xml
                    <ContentControl x:Name="GestioneAreaTouch"/>
```

diventa:

```xml
                    <ContentControl x:Name="GestioneAreaTouch"/>
                    <ContentControl x:Name="ImpostazioniAreaTouch"/>
```

- [ ] **Step 3: XAML — tab desktop**

Il blocco:

```xml
                <TabItem Header="Gestione contenuti">
                    <ContentControl x:Name="GestioneArea"/>
                </TabItem>
            </TabControl>
```

diventa:

```xml
                <TabItem Header="Gestione contenuti">
                    <ContentControl x:Name="GestioneArea"/>
                </TabItem>
                <TabItem Header="Impostazioni">
                    <ContentControl x:Name="ImpostazioniArea"/>
                </TabItem>
            </TabControl>
```

- [ ] **Step 4: Code-behind — using e campi**

In `wsa.quiz.app/MainView.axaml.cs`, la riga:

```csharp
using Avalonia.Media;
```

diventa:

```csharp
using Avalonia.Media;
using Avalonia.Styling;
```

I campi:

```csharp
    private HomeView? _homeView;
    private CronologiaView? _cronologiaView;
    private SospesiView? _sospesiView;
    private StatisticheView? _statisticheView;
    private GestioneContenutiView? _gestioneView;
```

diventano:

```csharp
    private HomeView? _homeView;
    private CronologiaView? _cronologiaView;
    private SospesiView? _sospesiView;
    private StatisticheView? _statisticheView;
    private GestioneContenutiView? _gestioneView;
    private ImpostazioniView? _impostazioniView;
    private PreferenzeUtente _preferenze = new();
```

e:

```csharp
    private ContentControl _homeArea = null!;
    private ContentControl _cronologiaArea = null!;
    private ContentControl _sospesiArea = null!;
    private ContentControl _statisticheArea = null!;
    private ContentControl _gestioneArea = null!;
```

diventano:

```csharp
    private ContentControl _homeArea = null!;
    private ContentControl _cronologiaArea = null!;
    private ContentControl _sospesiArea = null!;
    private ContentControl _statisticheArea = null!;
    private ContentControl _gestioneArea = null!;
    private ContentControl _impostazioniArea = null!;
```

- [ ] **Step 5: Code-behind — clamp a 6 sezioni**

La riga in `IndiceSezione` (getter):

```csharp
            return Math.Clamp((int)Math.Round(SwipeHost.Offset.X / w), 0, 4);
```

diventa:

```csharp
            return Math.Clamp((int)Math.Round(SwipeHost.Offset.X / w), 0, 5);
```

La riga in `ScorriAllaPagina`:

```csharp
        double target = Math.Clamp(indice, 0, 4) * w;
```

diventa:

```csharp
        double target = Math.Clamp(indice, 0, 5) * w;
```

- [ ] **Step 6: Code-behind — `ConfiguraNavigazione` (entrambi i rami)**

Ramo touch, la riga:

```csharp
            _gestioneArea = GestioneAreaTouch;
```

diventa:

```csharp
            _gestioneArea = GestioneAreaTouch;
            _impostazioniArea = ImpostazioniAreaTouch;
```

Ramo desktop, la riga:

```csharp
            _gestioneArea = GestioneArea;
```

diventa:

```csharp
            _gestioneArea = GestioneArea;
            _impostazioniArea = ImpostazioniArea;
```

- [ ] **Step 7: Code-behind — `DimensionaPagine` e `AggiornaNav`**

In `DimensionaPagine`, la riga:

```csharp
        foreach (var pagina in new[] { HomeAreaTouch, CronologiaAreaTouch, SospesiAreaTouch, StatisticheAreaTouch, GestioneAreaTouch })
```

diventa:

```csharp
        foreach (var pagina in new[] { HomeAreaTouch, CronologiaAreaTouch, SospesiAreaTouch, StatisticheAreaTouch, GestioneAreaTouch, ImpostazioniAreaTouch })
```

In `AggiornaNav`, la riga:

```csharp
        var icone = new[] { NavHomeIcon, NavCronologiaIcon, NavSospesiIcon, NavStatisticheIcon, NavGestioneIcon };
```

diventa:

```csharp
        var icone = new[] { NavHomeIcon, NavCronologiaIcon, NavSospesiIcon, NavStatisticheIcon, NavGestioneIcon, NavImpostazioniIcon };
```

- [ ] **Step 8: Code-behind — `OnKeyDown` (Ctrl+Tab su 6 sezioni)**

Il blocco:

```csharp
        if (e.Key == Key.Tab && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            const int n = 5;
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
            }
            e.Handled = true;
            return;
        }
```

diventa:

```csharp
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
```

- [ ] **Step 9: Code-behind — caricare le preferenze e applicare il tema in `Caricamento()`**

La riga:

```csharp
            _storage = new StorageService(fonte, cartellaUtente);
            _contenutiUtente = new ContenutiUtenteService(cartellaUtente);
```

diventa:

```csharp
            _storage = new StorageService(fonte, cartellaUtente);
            _contenutiUtente = new ContenutiUtenteService(cartellaUtente);

            _preferenze = _storage.CaricaPreferenze();
            Application.Current!.RequestedThemeVariant = _preferenze.TemaScuro ? ThemeVariant.Dark : ThemeVariant.Light;
```

- [ ] **Step 10: Code-behind — wiring della 6ª sezione**

Il blocco:

```csharp
            // ---------- Tab Gestione contenuti ----------
            _gestioneView = new GestioneContenutiView();
            _gestioneView.Inizializza(_storage, _contenutiUtente!, _materie, _tutteDomande);
            // Post invece di chiamata diretta: ContenutiCambiati viene sollevato da un
            // Click handler di _gestioneView stessa, che _gestioneArea.Content sta per
            // sostituire. Ricostruire subito l'albero visivo (sincrono, rientrante)
            // mentre Avalonia sta ancora smontando l'evento Click in corso blocca la
            // UI; Post rimanda il refresh al termine del dispatch dell'evento corrente.
            _gestioneView.ContenutiCambiati += (_, _) => Dispatcher.UIThread.Post(Caricamento);
            _gestioneArea.Content = _gestioneView;
        }
```

diventa:

```csharp
            // ---------- Tab Gestione contenuti ----------
            _gestioneView = new GestioneContenutiView();
            _gestioneView.Inizializza(_storage, _contenutiUtente!, _materie, _tutteDomande);
            // Post invece di chiamata diretta: ContenutiCambiati viene sollevato da un
            // Click handler di _gestioneView stessa, che _gestioneArea.Content sta per
            // sostituire. Ricostruire subito l'albero visivo (sincrono, rientrante)
            // mentre Avalonia sta ancora smontando l'evento Click in corso blocca la
            // UI; Post rimanda il refresh al termine del dispatch dell'evento corrente.
            _gestioneView.ContenutiCambiati += (_, _) => Dispatcher.UIThread.Post(Caricamento);
            _gestioneArea.Content = _gestioneView;

            // ---------- Tab Impostazioni ----------
            _impostazioniView = new ImpostazioniView();
            _impostazioniView.Inizializza(_storage, _preferenze);
            _impostazioniArea.Content = _impostazioniView;
        }
```

- [ ] **Step 11: Build progetto app**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 12: Verifica manuale desktop**

`dotnet run --project wsa.quiz.desktop`. Verificare:
1. Compaiono 6 tab in alto: Home, Cronologia, Sospesi, Statistiche, Gestione contenuti, **Impostazioni**.
2. `Ctrl+Tab`/`Ctrl+Shift+Tab` ciclano correttamente su tutte e 6 (compresa Impostazioni, senza saltarla né bloccarsi).
3. Aprendo "Impostazioni" appare il riquadro "Tema" con lo `ToggleSwitch` su "Chiaro" (default).
4. Attivando il toggle ("Scuro"), **l'intera app cambia tema dal vivo**, senza riavviare.
5. Avviare un quiz in **modalità a tempo** (checkbox "Modalità a tempo" in Home) e verificare che il cronometro sia leggibile (grigio medio, non quasi-nero) sia in tema Chiaro che Scuro — conferma del fix Task 3.

- [ ] **Step 13: Commit**

```bash
git add wsa.quiz.app/MainView.axaml wsa.quiz.app/MainView.axaml.cs
git commit -m "feat(app): 6a sezione Impostazioni, caricamento preferenze e tema al boot"
```

---

### Task 6: Persistenza opzioni quiz

**Files:**
- Modify: `wsa.quiz.app/Views/HomeView.axaml.cs`
- Modify: `wsa.quiz.app/MainView.axaml.cs`

**Interfaces:**
- Consumes: `PreferenzeUtente` (Task 1), `MainView._preferenze` (Task 5).
- Produces: `HomeView.Inizializza(List<Materia>, List<Domanda>, PreferenzeUtente)` (nuova firma, sostituisce quella a 2 parametri).

- [ ] **Step 1: `HomeView.Inizializza` riceve le preferenze**

In `wsa.quiz.app/Views/HomeView.axaml.cs`, il blocco:

```csharp
    public void Inizializza(List<Materia> materie, List<Domanda> tutteDomande)
    {
        _tutteDomande = tutteDomande;

        // 1. Popolo materie con conteggio domande
```

diventa:

```csharp
    public void Inizializza(List<Materia> materie, List<Domanda> tutteDomande, PreferenzeUtente preferenze)
    {
        _tutteDomande = tutteDomande;

        // Opzioni quiz: valori salvati invece dei default hardcoded (Step 13).
        // Assegnazione diretta ai campi (non alle proprieta'): evita di
        // sollevare PropertyChanged/AggiornaRiepilogo per ogni singolo campo
        // durante l'inizializzazione (AggiornaRiepilogo viene comunque
        // chiamato una volta sola in fondo al metodo).
        _randomizzaOrdine = preferenze.RandomizzaOrdine;
        _rotazione        = preferenze.Rotazione;
        _cronometro       = preferenze.Cronometro;
        _limitaDomande    = preferenze.LimitaDomande;
        _limiteN          = preferenze.LimiteN;
        _modalitaTempo    = preferenze.ModalitaTempo;
        _minutiTempo      = preferenze.MinutiTempo;

        // 1. Popolo materie con conteggio domande
```

- [ ] **Step 2: Build progetto app (atteso: errori nei chiamanti)**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori` sui due call site in `MainView.axaml.cs` (`Inizializza(_materie, _tutteDomande)` con 2 argomenti invece di 3) — atteso, si sistema nello step successivo.

- [ ] **Step 3: Aggiornare i due call site + salvataggio on-change in `MainView.axaml.cs`**

Nel metodo `Caricamento()`, il blocco:

```csharp
            // ---------- Tab Home ----------
            _homeView = new HomeView();
            _homeView.Inizializza(_materie, _tutteDomande);
            _homeView.QuizAvviato += OnQuizAvviato;
            _homeArea.Content = _homeView;
```

diventa:

```csharp
            // ---------- Tab Home ----------
            _homeView = new HomeView();
            _homeView.Inizializza(_materie, _tutteDomande, _preferenze);
            _homeView.QuizAvviato += OnQuizAvviato;
            _homeView.PropertyChanged += OnHomeOpzioniCambiate;
            _homeArea.Content = _homeView;
```

Nel metodo `RitornaAllaHome()`, il blocco:

```csharp
        if (_homeView == null)
        {
            _homeView = new HomeView();
            _homeView.Inizializza(_materie, _tutteDomande);
            _homeView.QuizAvviato += OnQuizAvviato;
        }
```

diventa:

```csharp
        if (_homeView == null)
        {
            _homeView = new HomeView();
            _homeView.Inizializza(_materie, _tutteDomande, _preferenze);
            _homeView.QuizAvviato += OnQuizAvviato;
            _homeView.PropertyChanged += OnHomeOpzioniCambiate;
        }
```

Aggiungere il metodo di salvataggio, subito dopo `OnQuizAvviato`:

```csharp
    private static readonly HashSet<string> _proprietaOpzioniQuiz = new(StringComparer.Ordinal)
    {
        nameof(HomeView.RandomizzaOrdine), nameof(HomeView.Rotazione), nameof(HomeView.Cronometro),
        nameof(HomeView.LimitaDomande), nameof(HomeView.LimiteN), nameof(HomeView.ModalitaTempo), nameof(HomeView.MinutiTempo)
    };

    /// <summary>Salva su preferenze.json ad ogni cambio di una delle opzioni
    /// quiz in Home (Step 13). Filtra sul nome proprieta' per non salvare
    /// per cambi di stato non rilevanti (es. RiepilogoSelezione).</summary>
    private void OnHomeOpzioniCambiate(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_storage == null || _homeView == null) return;
        if (e.PropertyName == null || !_proprietaOpzioniQuiz.Contains(e.PropertyName)) return;

        var prefs = _storage.CaricaPreferenze();
        prefs.RandomizzaOrdine = _homeView.RandomizzaOrdine;
        prefs.Rotazione        = _homeView.Rotazione;
        prefs.Cronometro       = _homeView.Cronometro;
        prefs.LimitaDomande    = _homeView.LimitaDomande;
        prefs.LimiteN          = (int)_homeView.LimiteN;
        prefs.ModalitaTempo    = _homeView.ModalitaTempo;
        prefs.MinutiTempo      = (int)_homeView.MinutiTempo;
        _storage.SalvaPreferenze(prefs);
    }
```

- [ ] **Step 4: Build progetto app**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 5: Verifica manuale desktop end-to-end**

`dotnet run --project wsa.quiz.desktop`. In Home:
1. Deselezionare "Randomizza ordine", attivare "Cronometro", attivare "Limita numero di domande" con N=5.
2. Chiudere completamente l'app (non solo la finestra: uscire dal processo).
3. Riavviare `dotnet run --project wsa.quiz.desktop`.
4. Verificare in Home: "Randomizza ordine" deselezionato, "Cronometro" attivo, "Limita numero di domande" attivo con N=5 — esattamente lo stato lasciato al punto 1.

- [ ] **Step 6: Commit**

```bash
git add wsa.quiz.app/Views/HomeView.axaml.cs wsa.quiz.app/MainView.axaml.cs
git commit -m "feat(app): persistenza opzioni quiz (Step 13)"
```

---

### Task 7: Persistenza ultima sezione aperta

**Files:**
- Modify: `wsa.quiz.app/MainView.axaml.cs`

**Interfaces:**
- Consumes: `MainView._preferenze` (Task 5).
- Produces: campo `MainView._ultimoIndiceSalvato`, metodo `MainView.SalvaTabSeCambiata()`.

- [ ] **Step 1: Aggiungere il campo e il metodo di salvataggio**

Dopo il campo `_impostazioniView`/`_preferenze` (aggiunti nel Task 5), aggiungere:

```csharp
    private int _ultimoIndiceSalvato = -1;
```

Aggiungere il metodo, subito dopo `OnNavClick`:

```csharp
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
```

- [ ] **Step 2: Agganciare il salvataggio ai due handler di cambio sezione**

In `ConfiguraNavigazione`, ramo touch, la riga:

```csharp
            SwipeHost.AddHandler(ScrollViewer.ScrollChangedEvent,
                new EventHandler<ScrollChangedEventArgs>((_, _) => AggiornaNav()));
```

diventa:

```csharp
            SwipeHost.AddHandler(ScrollViewer.ScrollChangedEvent,
                new EventHandler<ScrollChangedEventArgs>((_, _) => { AggiornaNav(); SalvaTabSeCambiata(); }));
```

Ramo desktop, la riga:

```csharp
            Tabs.SelectionChanged += (_, _) => AggiornaNav();
```

diventa:

```csharp
            Tabs.SelectionChanged += (_, _) => { AggiornaNav(); SalvaTabSeCambiata(); };
```

- [ ] **Step 3: Ripristinare l'ultima sezione al boot**

Nel metodo `Caricamento()`, il blocco (aggiunto nel Task 5):

```csharp
            // ---------- Tab Impostazioni ----------
            _impostazioniView = new ImpostazioniView();
            _impostazioniView.Inizializza(_storage, _preferenze);
            _impostazioniArea.Content = _impostazioniView;
        }
```

diventa:

```csharp
            // ---------- Tab Impostazioni ----------
            _impostazioniView = new ImpostazioniView();
            _impostazioniView.Inizializza(_storage, _preferenze);
            _impostazioniArea.Content = _impostazioniView;

            // Ripristino l'ultima sezione aperta (Step 13). Imposto
            // _ultimoIndiceSalvato PRIMA di assegnare IndiceSezione: l'assegnazione
            // scatena comunque SelectionChanged/ScrollChanged -> SalvaTabSeCambiata,
            // che cosi' vede idx == _ultimoIndiceSalvato e non ri-salva un
            // "cambio" che in realta' e' solo il ripristino iniziale.
            _ultimoIndiceSalvato = _preferenze.TabIniziale;
            IndiceSezione = _preferenze.TabIniziale;
        }
```

- [ ] **Step 4: Build progetto app**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 5: Verifica manuale desktop end-to-end**

`dotnet run --project wsa.quiz.desktop`.
1. Aprire la tab "Statistiche" (indice 3).
2. Chiudere completamente l'app.
3. Riavviare `dotnet run --project wsa.quiz.desktop`.
4. Verificare che l'app si apra direttamente sulla tab "Statistiche", non su "Home".
5. Tornare su "Home", chiudere e riaprire: verificare che riparta su "Home" (nessun salvataggio spurio al ripristino: l'ultimo cambio esplicito era Statistiche→Home al punto 5, quindi il comportamento atteso è tornare su Home).

- [ ] **Step 6: Commit**

```bash
git add wsa.quiz.app/MainView.axaml.cs
git commit -m "feat(app): persistenza ultima sezione aperta (Step 13)"
```

---

### Task 8: Verifica finale end-to-end

**Files:** nessuna modifica, solo verifica.

- [ ] **Step 1: Build completa Debug**

```bash
dotnet build Wsa.Quiz.sln -c Debug
```
Expected: `Errori: 0` su tutti i 5 progetti (Core, App, Cli, Desktop, Android).

- [ ] **Step 2: Verifica desktop completa**

`dotnet run --project wsa.quiz.desktop`. Percorrere l'intero elenco "Piano di verifica" della spec (`docs/superpowers/specs/2026-07-14-preferenze-utente-design.md`):

1. Cambiare opzioni quiz + tab attiva + tema (Scuro) → chiudere l'app → riaprire → verificare che tab/opzioni/tema ripristinati corrispondano a quanto lasciato.
2. Con tema Scuro attivo, percorrere tutte e 6 le sezioni (Home, Cronologia, Sospesi, Statistiche, Gestione contenuti, Impostazioni) e verificare che non ci siano testi illeggibili (in particolare: cronometro in un quiz a tempo, box di feedback risposta corretta/sbagliata durante un quiz, heatmap in Statistiche, banner di errore/conferma "Sicuro?" nelle liste con eliminazione).
3. Verificare che i due dialoghi overlay (Pausa, Conferma eliminazione) restino leggibili (sfondo chiaro forzato, comportamento invariato) sia in tema Chiaro che Scuro.
4. Tornare a tema Chiaro e verificare che l'app riprenda l'aspetto originale (nessuna regressione visiva rispetto a prima di questo piano).

- [ ] **Step 3: Build Android Release (nessuna regressione di build)**

```bash
dotnet build wsa.quiz.android -c Release
```
Expected: `Errori: 0`, APK firmato prodotto in `wsa.quiz.android/bin/Release/net10.0-android/it.eduits.wsaquiz-Signed.apk`. Nessuna verifica runtime su device/emulatore in questo piano (rimandata, vedi Global Constraints).

- [ ] **Step 4: Aggiornare `dist/wsa.quiz-android.apk`**

```bash
cp wsa.quiz.android/bin/Release/net10.0-android/it.eduits.wsaquiz-Signed.apk dist/wsa.quiz-android.apk
```

- [ ] **Step 5: Commit finale**

```bash
git add dist/wsa.quiz-android.apk
git commit -m "chore(dist): rigenera APK con preferenze utente + dark mode (Step 13 + Step 10)"
```

---

## Self-Review

**Copertura spec:** modello+persistenza (Task 1-2) ✓, fix dark mode noto (Task 3) ✓, sezione Impostazioni/toggle (Task 4-5) ✓, caricamento boot + applicazione tema (Task 5) ✓, wiring 6 sezioni bottom-nav/TabControl/Ctrl+Tab (Task 5) ✓, persistenza opzioni quiz (Task 6) ✓, persistenza ultima tab con guardia anti-doppio-salvataggio (Task 7) ✓, audit degli altri colori hardcoded: eseguito durante la stesura di questo piano (non a runtime) — verificato che `StatisticheView.axaml:93`, `QuizView.axaml:200/206` e `CategoriaDettaglioView.axaml:53` (gli altri usi di `#1F1F1F`) sono già abbinati a un `Background` hardcoded chiaro nello stesso contenitore (pattern Trappola 9, immuni al tema); i colori di brand (`#B85450`/`#1F7A4D`/`#B8860B`/ecc.) usati come testo direttamente su sfondo dinamico sono stati valutati e lasciati invariati per decisione di scope (non sono nel pattern "quasi-nero/quasi-bianco su sfondo che li rende invisibili", sono una scelta di design pre-esistente) — nessun task extra necessario, coerente con "Non-obiettivi" della spec. Verifica finale end-to-end ✓ (Task 8).

**Scan placeholder:** nessun "TBD"/vago residuo; ogni step con codice mostra il codice completo (old→new) o il file completo.

**Coerenza tipi/nomi:** `PreferenzeUtente` (Task 1) usato identico in `StorageService` (Task 2), `ImpostazioniView` (Task 4), `HomeView.Inizializza` (Task 6) e `MainView._preferenze` (Task 5); `CaricaPreferenze()`/`SalvaPreferenze(PreferenzeUtente)` uguali in tutti i punti di chiamata; nomi proprietà `HomeView` (`RandomizzaOrdine`, `Rotazione`, `Cronometro`, `LimitaDomande`, `LimiteN`, `ModalitaTempo`, `MinutiTempo`) verificati contro il file sorgente attuale, usati identici nel `HashSet` di Task 6.
