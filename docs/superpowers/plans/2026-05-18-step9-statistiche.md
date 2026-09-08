# Step 9 — Tab Statistiche Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** aggiungere una quarta tab "Statistiche" all'app Avalonia che mostri una "mappa di calore" pedagogica delle debolezze per categoria, con drill-down sulle domande sbagliate e filtro periodo (Tutto / Ultime N partite).

**Architecture:** logica di aggregazione pura in `Wsa.Quiz.Core.Services.StatisticheService` (testabile, riusabile); due UserControl in `Wsa.Quiz.App.Views` (`StatisticheView` + `CategoriaDettaglioView`) seguendo il pattern code-behind+INPC del resto del progetto; swap heatmap↔dettaglio interno alla `StatisticheView` come fa `CronologiaView`. Zero nuove dipendenze grafiche.

**Tech Stack:** .NET 10, Avalonia 12.0.2, code-behind + INPC, `ItemsControl`/`WrapPanel`/`Border` per la heatmap (no LiveCharts2).

**Spec di riferimento:** `docs/superpowers/specs/2026-05-18-step9-grafici-statistiche-design.md`

**Nota su TDD:** il progetto non ha test automatici e non ne aggiungiamo qui (scope creep). Verifica per task = build OK + smoke test manuale al Task 11. La logica più rischiosa (`StatisticheService`) è una funzione pura su `IEnumerable<RisultatoQuiz>` che si potrà sempre coprire con xUnit in un futuro Task se servirà.

---

## Task 1: Modelli record di aggregazione

Tre `record` immutabili nel Core. Nessuna dipendenza esterna, niente logica.

**Files:**
- Create: `wsa.quiz.core/Models/Statistiche/CategoriaStat.cs`
- Create: `wsa.quiz.core/Models/Statistiche/DomandaSbagliata.cs`
- Create: `wsa.quiz.core/Models/Statistiche/StatistichePerMateria.cs`

- [ ] **Step 1: Creare `CategoriaStat.cs`**

```csharp
namespace Wsa.Quiz.Core.Models.Statistiche;

/// <summary>
/// Aggregato per una singola categoria all'interno di una materia.
/// </summary>
public record CategoriaStat(
    string Materia,
    string Categoria,
    int Visti,
    int Errori,
    double PctErrore,
    IReadOnlyList<DomandaSbagliata> Sbagliate);
```

- [ ] **Step 2: Creare `DomandaSbagliata.cs`**

```csharp
namespace Wsa.Quiz.Core.Models.Statistiche;

/// <summary>
/// Domanda sbagliata almeno una volta in una data categoria,
/// con conteggio di quante volte e' stata vista e quante sbagliata.
/// </summary>
public record DomandaSbagliata(
    string IdDomanda,
    string Testo,
    string RispostaCorretta,
    string Spiegazione,
    int VolteSbagliata,
    int VolteVista);
```

- [ ] **Step 3: Creare `StatistichePerMateria.cs`**

```csharp
namespace Wsa.Quiz.Core.Models.Statistiche;

/// <summary>
/// Aggregato di tutte le categorie di una materia, ordinate per debolezza
/// decrescente (calcolato da <see cref="Services.StatisticheService"/>).
/// </summary>
public record StatistichePerMateria(
    string Materia,
    IReadOnlyList<CategoriaStat> Categorie);
```

- [ ] **Step 4: Build per verificare la compilazione**

```bash
dotnet build Wsa.Quiz.sln
```

Atteso: 0 errori. Eventuali warning preesistenti restano (4 noti).

- [ ] **Step 5: Commit**

```bash
git add wsa.quiz.core/Models/Statistiche/
git commit -m "feat(step9): modelli record per aggregazione statistiche"
```

---

## Task 2: `StatisticheService` — logica di aggregazione

**Files:**
- Create: `wsa.quiz.core/Services/StatisticheService.cs`

- [ ] **Step 1: Creare `StatisticheService.cs`**

```csharp
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Models.Statistiche;

namespace Wsa.Quiz.Core.Services;

/// <summary>
/// Aggrega la cronologia in una "mappa di calore" per categoria.
/// Funzione pura su <see cref="RisultatoQuiz"/>: non fa I/O.
/// </summary>
public class StatisticheService
{
    /// <summary>
    /// Calcola le statistiche per materia/categoria. Errore =
    /// !(Corretta &amp;&amp; Tentativi == 1): unifica modalita' classica
    /// e rotazione (in rotazione, "sbagliata al primo colpo" = Tentativi&gt;1).
    /// </summary>
    /// <param name="sessioni">Cronologia completa (anche abbandonati).</param>
    /// <param name="limiteUltimeN">Se valorizzato, considera solo le ultime N
    /// sessioni per DataOra decrescente.</param>
    public IReadOnlyList<StatistichePerMateria> Calcola(
        IEnumerable<RisultatoQuiz> sessioni,
        int? limiteUltimeN = null)
    {
        var filtrate = sessioni;
        if (limiteUltimeN is int n && n > 0)
        {
            filtrate = sessioni
                .OrderByDescending(s => s.DataOra)
                .Take(n);
        }

        // Tutti i dettagli, appiattiti.
        var dettagli = filtrate.SelectMany(s => s.Dettagli).ToList();
        if (dettagli.Count == 0) return Array.Empty<StatistichePerMateria>();

        // Raggruppa per (materia, categoria).
        var gruppi = dettagli
            .GroupBy(d => (d.MateriaNome, d.Categoria))
            .Select(g => CostruisciCategoriaStat(g.Key.MateriaNome, g.Key.Categoria, g.ToList()))
            .ToList();

        // Raggruppa per materia, ordina materie alfabetico, categorie per
        // PctErrore desc, poi alfabetico a parita'.
        return gruppi
            .GroupBy(c => c.Materia)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new StatistichePerMateria(
                g.Key,
                g.OrderByDescending(c => c.PctErrore)
                 .ThenBy(c => c.Categoria, StringComparer.OrdinalIgnoreCase)
                 .ToList()))
            .ToList();
    }

    private static CategoriaStat CostruisciCategoriaStat(
        string materia, string categoria, List<DettaglioRisposta> dettagli)
    {
        int visti = dettagli.Count;
        int errori = dettagli.Count(d => !(d.Corretta && d.Tentativi == 1));
        double pct = visti == 0 ? 0.0 : (double)errori / visti;

        var sbagliate = dettagli
            .Where(d => !(d.Corretta && d.Tentativi == 1))
            .GroupBy(d => d.IdDomanda)
            .Select(g =>
            {
                var ultimo = g.Last();
                int volteSbagliata = g.Count();
                int volteVista = dettagli.Count(d => d.IdDomanda == g.Key);
                return new DomandaSbagliata(
                    IdDomanda: g.Key,
                    Testo: ultimo.TestoDomanda,
                    RispostaCorretta: ultimo.RispostaCorretta,
                    Spiegazione: ultimo.Spiegazione,
                    VolteSbagliata: volteSbagliata,
                    VolteVista: volteVista);
            })
            .OrderByDescending(d => d.VolteSbagliata)
            .ThenBy(d => d.Testo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CategoriaStat(materia, categoria, visti, errori, pct, sbagliate);
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build Wsa.Quiz.sln
```

Atteso: 0 errori.

- [ ] **Step 3: Commit**

```bash
git add wsa.quiz.core/Services/StatisticheService.cs
git commit -m "feat(step9): StatisticheService aggrega cronologia per categoria"
```

---

## Task 3: Helper colori heatmap in `QuizColors`

Centralizza le tre fasce di colore della heatmap (verde/ambra/rosso in base a `pctErrore` 0..1) seguendo il pattern di `Percentuale(pct)`/`Esito(bool)` gia' presente.

**Files:**
- Modify: `wsa.quiz.app/QuizColors.cs`

- [ ] **Step 1: Aggiungere brush e helper in `QuizColors.cs`**

Modifica il file aggiungendo subito sotto i brush esistenti e prima del metodo `Percentuale`:

```csharp
    // Sfondi tenui per le celle della heatmap statistiche (step 9).
    private static readonly IBrush _verdeBrushSfondo = new SolidColorBrush(Color.Parse("#E5F4EC"));
    private static readonly IBrush _ambraBrushSfondo = new SolidColorBrush(Color.Parse("#FFF4D6"));
    private static readonly IBrush _rossoBrushSfondo = new SolidColorBrush(Color.Parse("#F9E7E6"));

    /// <summary>
    /// Bordo cella heatmap in base alla percentuale di errore (0..1):
    /// verde &lt; 20%, ambra 20-59%, rosso &gt;= 60%.
    /// </summary>
    public static IBrush HeatmapBordo(double pctErrore)
    {
        if (pctErrore < 0.20) return _verdeBrush;
        if (pctErrore < 0.60) return _ambraBrush;
        return _rossoBrush;
    }

    /// <summary>
    /// Sfondo tenue cella heatmap, abbinato a <see cref="HeatmapBordo"/>.
    /// </summary>
    public static IBrush HeatmapSfondo(double pctErrore)
    {
        if (pctErrore < 0.20) return _verdeBrushSfondo;
        if (pctErrore < 0.60) return _ambraBrushSfondo;
        return _rossoBrushSfondo;
    }
```

Inseriscilo dopo la riga `private static readonly IBrush _rossoBrush = ...` (linea 23) e prima del commento `/// <summary> Restituisce il colore associato...`.

- [ ] **Step 2: Build**

```bash
dotnet build Wsa.Quiz.sln
```

Atteso: 0 errori.

- [ ] **Step 3: Commit**

```bash
git add wsa.quiz.app/QuizColors.cs
git commit -m "feat(step9): QuizColors.HeatmapBordo/HeatmapSfondo per celle statistiche"
```

---

## Task 4: State wrappers per la heatmap

Wrapper non-INPC (i dati sono immutabili dopo il calcolo; al cambio filtro si ricrea la collection). Mantengono allineamento col pattern `RisultatoCronologiaItem`.

**Files:**
- Create: `wsa.quiz.app/State/CategoriaCellaItem.cs`
- Create: `wsa.quiz.app/State/MateriaRigaItem.cs`

- [ ] **Step 1: Creare `CategoriaCellaItem.cs`**

```csharp
using Avalonia.Media;
using Wsa.Quiz.Core.Models.Statistiche;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Wrapper per una cella della heatmap statistiche: espone etichetta
/// formattata e i brush di bordo/sfondo dipendenti dalla PctErrore.
/// </summary>
public class CategoriaCellaItem
{
    public CategoriaStat Stat { get; }

    public CategoriaCellaItem(CategoriaStat stat)
    {
        Stat = stat;
    }

    public string Etichetta => $"{Stat.Categoria} {Stat.PctErrore:P0}";
    public string Tooltip => $"{Stat.Errori} errori su {Stat.Visti} viste";
    public IBrush ColoreBordo => QuizColors.HeatmapBordo(Stat.PctErrore);
    public IBrush ColoreSfondo => QuizColors.HeatmapSfondo(Stat.PctErrore);
}
```

- [ ] **Step 2: Creare `MateriaRigaItem.cs`**

```csharp
using System.Linq;
using Wsa.Quiz.Core.Models.Statistiche;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Wrapper di riga heatmap: nome materia + lista celle categoria
/// gia' ordinate per debolezza.
/// </summary>
public class MateriaRigaItem
{
    public string Materia { get; }
    public IReadOnlyList<CategoriaCellaItem> Celle { get; }

    public MateriaRigaItem(StatistichePerMateria stat)
    {
        Materia = stat.Materia;
        Celle = stat.Categorie.Select(c => new CategoriaCellaItem(c)).ToList();
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build Wsa.Quiz.sln
```

Atteso: 0 errori.

- [ ] **Step 4: Commit**

```bash
git add wsa.quiz.app/State/CategoriaCellaItem.cs wsa.quiz.app/State/MateriaRigaItem.cs
git commit -m "feat(step9): wrapper di stato per heatmap statistiche"
```

---

## Task 5: `CategoriaDettaglioView` — lista domande sbagliate

Sub-view del drill-down. Riceve un `CategoriaStat`, mostra le sue `Sbagliate`.

**Files:**
- Create: `wsa.quiz.app/Views/CategoriaDettaglioView.axaml`
- Create: `wsa.quiz.app/Views/CategoriaDettaglioView.axaml.cs`

- [ ] **Step 1: Creare `CategoriaDettaglioView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:stats="using:Wsa.Quiz.Core.Models.Statistiche"
             xmlns:views="using:Wsa.Quiz.App.Views"
             mc:Ignorable="d"
             x:Class="Wsa.Quiz.App.Views.CategoriaDettaglioView"
             x:DataType="views:CategoriaDettaglioView">

    <DockPanel>

        <!-- Header con bottone indietro + titolo -->
        <Grid DockPanel.Dock="Top"
              ColumnDefinitions="Auto,*"
              Margin="20,16,20,8">
            <Button Content="&#x2190; Statistiche"
                    Click="OnIndietroClick"
                    Padding="14,6"
                    FontSize="12"/>
            <TextBlock Grid.Column="1"
                       Text="{Binding Titolo}"
                       FontSize="18" FontWeight="SemiBold"
                       VerticalAlignment="Center"
                       Margin="16,0,0,0"
                       TextTrimming="CharacterEllipsis"/>
        </Grid>

        <!-- Lista domande sbagliate -->
        <ScrollViewer Margin="20,0,20,16"
                      VerticalScrollBarVisibility="Auto">
            <ItemsControl ItemsSource="{Binding Sbagliate}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate DataType="stats:DomandaSbagliata">
                        <Border BorderBrush="#B85450"
                                Background="#F9E7E6"
                                BorderThickness="1"
                                CornerRadius="2"
                                Padding="10"
                                Margin="0,4">
                            <StackPanel Spacing="6">
                                <TextBlock FontWeight="SemiBold"
                                           Foreground="#B85450"
                                           FontSize="12">
                                    <Run Text="&#x274C; sbagliata "/>
                                    <Run Text="{Binding VolteSbagliata}"/>
                                    <Run Text="/"/>
                                    <Run Text="{Binding VolteVista}"/>
                                    <Run Text=" volte"/>
                                </TextBlock>
                                <TextBlock Text="{Binding Testo}"
                                           TextWrapping="Wrap"
                                           Foreground="#1F1F1F"/>
                                <TextBlock TextWrapping="Wrap"
                                           Foreground="#1F7A4D"
                                           FontSize="12">
                                    <Run Text="&#x2713; corretta: "/>
                                    <Run Text="{Binding RispostaCorretta}"/>
                                </TextBlock>
                                <TextBlock Text="{Binding Spiegazione}"
                                           TextWrapping="Wrap"
                                           FontStyle="Italic"
                                           FontSize="12"
                                           Foreground="#5A5A5A"
                                           IsVisible="{Binding Spiegazione, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
                            </StackPanel>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>

    </DockPanel>

</UserControl>
```

- [ ] **Step 2: Creare `CategoriaDettaglioView.axaml.cs`**

```csharp
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Wsa.Quiz.Core.Models.Statistiche;

namespace Wsa.Quiz.App.Views;

/// <summary>
/// Drill-down della heatmap statistiche: mostra le domande sbagliate
/// di una categoria. Espone <see cref="IndietroRichiesto"/> per tornare
/// alla heatmap.
/// </summary>
public partial class CategoriaDettaglioView : UserControl, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? IndietroRichiesto;

    public ObservableCollection<DomandaSbagliata> Sbagliate { get; } = new();

    private string _titolo = "";
    public string Titolo
    {
        get => _titolo;
        private set { if (_titolo != value) { _titolo = value; Raise(); } }
    }

    public CategoriaDettaglioView()
    {
        InitializeComponent();
        DataContext = this;
        // Stesso pattern Tunnel+handledEventsToo dello step 8/PR#2: tentiamo
        // di intercettare Esc per tornare indietro. Se non funziona, il
        // bottone resta sempre disponibile (vedi Trappola 13 in CLAUDE.md).
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    public void Mostra(CategoriaStat stat)
    {
        Titolo = $"{stat.Materia} / {stat.Categoria} — {stat.Errori} errori su {stat.Visti} viste ({stat.PctErrore:P0})";
        Sbagliate.Clear();
        foreach (var d in stat.Sbagliate)
            Sbagliate.Add(d);
    }

    private void OnIndietroClick(object? sender, RoutedEventArgs e)
        => IndietroRichiesto?.Invoke(this, EventArgs.Empty);

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            IndietroRichiesto?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 3: Build**

```bash
dotnet build Wsa.Quiz.sln
```

Atteso: 0 errori. Warning AVLN3001 sui XAML pre-esistenti tollerati.

- [ ] **Step 4: Commit**

```bash
git add wsa.quiz.app/Views/CategoriaDettaglioView.axaml wsa.quiz.app/Views/CategoriaDettaglioView.axaml.cs
git commit -m "feat(step9): CategoriaDettaglioView per drill-down domande sbagliate"
```

---

## Task 6: `StatisticheView` — scheletro heatmap (senza filtro, senza drill-down)

Prima versione minima: mostra la heatmap su "Tutto", senza toggle filtro e senza swap dettaglio. I task successivi aggiungono filtro, drill-down e empty state.

**Files:**
- Create: `wsa.quiz.app/Views/StatisticheView.axaml`
- Create: `wsa.quiz.app/Views/StatisticheView.axaml.cs`

- [ ] **Step 1: Creare `StatisticheView.axaml` (scheletro)**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:state="using:Wsa.Quiz.App.State"
             xmlns:views="using:Wsa.Quiz.App.Views"
             mc:Ignorable="d"
             x:Class="Wsa.Quiz.App.Views.StatisticheView"
             x:DataType="views:StatisticheView">

    <DockPanel>

        <!-- Header -->
        <Grid DockPanel.Dock="Top"
              ColumnDefinitions="*,Auto"
              Margin="20,16,20,8">
            <StackPanel Spacing="2">
                <TextBlock Text="Statistiche"
                           FontSize="22" FontWeight="SemiBold"/>
                <TextBlock Text="Categorie ordinate dalle piu' deboli alle piu' forti."
                           FontSize="12"
                           Foreground="{DynamicResource SystemBaseMediumColor}"/>
            </StackPanel>
            <Button Grid.Column="1"
                    Content="Aggiorna"
                    Click="OnAggiornaClick"
                    Padding="14,6"
                    FontSize="12"/>
        </Grid>

        <!-- Heatmap (riga = materia, celle = categorie) -->
        <ScrollViewer Margin="20,0,20,16"
                      VerticalScrollBarVisibility="Auto">
            <ItemsControl ItemsSource="{Binding Righe}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate DataType="state:MateriaRigaItem">
                        <Grid ColumnDefinitions="120,*" Margin="0,4">
                            <TextBlock Text="{Binding Materia}"
                                       FontWeight="SemiBold"
                                       VerticalAlignment="Center"/>
                            <ItemsControl Grid.Column="1"
                                          ItemsSource="{Binding Celle}">
                                <ItemsControl.ItemsPanel>
                                    <ItemsPanelTemplate>
                                        <WrapPanel Orientation="Horizontal"/>
                                    </ItemsPanelTemplate>
                                </ItemsControl.ItemsPanel>
                                <ItemsControl.ItemTemplate>
                                    <DataTemplate DataType="state:CategoriaCellaItem">
                                        <Border BorderBrush="{Binding ColoreBordo}"
                                                Background="{Binding ColoreSfondo}"
                                                BorderThickness="1"
                                                CornerRadius="2"
                                                Padding="8,4"
                                                Margin="2"
                                                Cursor="Hand"
                                                ToolTip.Tip="{Binding Tooltip}"
                                                PointerPressed="OnCellaPressed">
                                            <TextBlock Text="{Binding Etichetta}"
                                                       FontSize="12"
                                                       Foreground="#1F1F1F"/>
                                        </Border>
                                    </DataTemplate>
                                </ItemsControl.ItemTemplate>
                            </ItemsControl>
                        </Grid>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>

    </DockPanel>

</UserControl>
```

- [ ] **Step 2: Creare `StatisticheView.axaml.cs` (scheletro)**

```csharp
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Wsa.Quiz.App.State;
using Wsa.Quiz.Core.Models.Statistiche;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.Views;

/// <summary>
/// Tab "Statistiche": heatmap dei punti deboli per categoria.
/// I task successivi aggiungono filtro periodo (Tutto/Ultime N), drill-down
/// e empty state.
/// </summary>
public partial class StatisticheView : UserControl, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;

    private StorageService? _storage;
    private readonly StatisticheService _servizio = new();

    public ObservableCollection<MateriaRigaItem> Righe { get; } = new();

    public StatisticheView()
    {
        InitializeComponent();
        DataContext = this;
    }

    public void Inizializza(StorageService storage)
    {
        _storage = storage;
        Ricarica();
    }

    public void Ricarica()
    {
        if (_storage == null) return;
        Righe.Clear();
        try
        {
            var cronologia = _storage.CaricaCronologia();
            var stat = _servizio.Calcola(cronologia);
            foreach (var r in stat)
                Righe.Add(new MateriaRigaItem(r));
        }
        catch
        {
            // Best effort: lasciamo Righe vuoto. L'empty state arrivera' al Task 9.
        }
    }

    private void OnAggiornaClick(object? sender, RoutedEventArgs e) => Ricarica();

    private void OnCellaPressed(object? sender, PointerPressedEventArgs e)
    {
        // Verra' agganciato nel Task 8 (drill-down). Per ora niente.
    }

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 3: Build**

```bash
dotnet build Wsa.Quiz.sln
```

Atteso: 0 errori.

- [ ] **Step 4: Commit**

```bash
git add wsa.quiz.app/Views/StatisticheView.axaml wsa.quiz.app/Views/StatisticheView.axaml.cs
git commit -m "feat(step9): StatisticheView scheletro heatmap senza filtro/drill-down"
```

---

## Task 7: Filtro periodo (Tutto / Ultime N)

Aggiunge l'header con due RadioButton e NumericUpDown. Al cambio: ricalcolo immediato.

**Files:**
- Modify: `wsa.quiz.app/Views/StatisticheView.axaml`
- Modify: `wsa.quiz.app/Views/StatisticheView.axaml.cs`

- [ ] **Step 1: Aggiungere controlli filtro in `StatisticheView.axaml`**

Sostituisci il blocco `<StackPanel Spacing="2">...</StackPanel>` nel Grid header con uno `StackPanel` che contiene anche i controlli filtro:

```xml
            <StackPanel Spacing="6">
                <TextBlock Text="Statistiche"
                           FontSize="22" FontWeight="SemiBold"/>
                <StackPanel Orientation="Horizontal" Spacing="10" VerticalAlignment="Center">
                    <RadioButton Content="Tutto"
                                 GroupName="FiltroPeriodo"
                                 IsChecked="{Binding ModalitaTutto}"/>
                    <RadioButton Content="Ultime"
                                 GroupName="FiltroPeriodo"
                                 IsChecked="{Binding !ModalitaTutto}"/>
                    <NumericUpDown Value="{Binding LimiteUltimeN}"
                                   Minimum="1"
                                   Maximum="9999"
                                   Increment="1"
                                   FormatString="0"
                                   Width="100"
                                   IsEnabled="{Binding !ModalitaTutto}"
                                   ValueChanged="OnLimiteChanged"/>
                    <TextBlock Text="partite"
                               VerticalAlignment="Center"
                               Foreground="{DynamicResource SystemBaseMediumColor}"/>
                </StackPanel>
                <TextBlock Text="Categorie ordinate dalle piu' deboli alle piu' forti."
                           FontSize="12"
                           Foreground="{DynamicResource SystemBaseMediumColor}"/>
            </StackPanel>
```

- [ ] **Step 2: Aggiungere proprieta' filtro in `StatisticheView.axaml.cs`**

Tra `Righe` e il costruttore, aggiungi:

```csharp
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
```

- [ ] **Step 3: Aggiornare `Ricarica` per usare il filtro**

Sostituisci la riga `var stat = _servizio.Calcola(cronologia);` con:

```csharp
            int? limite = ModalitaTutto ? null : LimiteUltimeN;
            var stat = _servizio.Calcola(cronologia, limite);
```

- [ ] **Step 4: Aggiungere handler `OnLimiteChanged` per ricalcolo**

Aggiungi il metodo:

```csharp
    private void OnLimiteChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (!ModalitaTutto) Ricarica();
    }
```

- [ ] **Step 5: Build**

```bash
dotnet build Wsa.Quiz.sln
```

Atteso: 0 errori.

- [ ] **Step 6: Commit**

```bash
git add wsa.quiz.app/Views/StatisticheView.axaml wsa.quiz.app/Views/StatisticheView.axaml.cs
git commit -m "feat(step9): filtro Tutto/Ultime N nella StatisticheView"
```

---

## Task 8: Drill-down (swap heatmap ↔ CategoriaDettaglioView)

Aggancia il click sulla cella, swap interno alla view come fa `CronologiaView`.

**Files:**
- Modify: `wsa.quiz.app/Views/StatisticheView.axaml`
- Modify: `wsa.quiz.app/Views/StatisticheView.axaml.cs`

- [ ] **Step 1: Avvolgere il body in un `Grid` swap nel `.axaml`**

Sostituisci il blocco `<ScrollViewer Margin="20,0,20,16"...>...</ScrollViewer>` (il contenitore della heatmap) con un `Grid` che ospita heatmap e dettaglio:

```xml
        <!-- Heatmap + Dettaglio (swap) -->
        <Grid>
            <!-- HEATMAP -->
            <ScrollViewer Margin="20,0,20,16"
                          VerticalScrollBarVisibility="Auto"
                          IsVisible="{Binding !ModoDettaglio}">
                <ItemsControl ItemsSource="{Binding Righe}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate DataType="state:MateriaRigaItem">
                            <Grid ColumnDefinitions="120,*" Margin="0,4">
                                <TextBlock Text="{Binding Materia}"
                                           FontWeight="SemiBold"
                                           VerticalAlignment="Center"/>
                                <ItemsControl Grid.Column="1"
                                              ItemsSource="{Binding Celle}">
                                    <ItemsControl.ItemsPanel>
                                        <ItemsPanelTemplate>
                                            <WrapPanel Orientation="Horizontal"/>
                                        </ItemsPanelTemplate>
                                    </ItemsControl.ItemsPanel>
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate DataType="state:CategoriaCellaItem">
                                            <Border BorderBrush="{Binding ColoreBordo}"
                                                    Background="{Binding ColoreSfondo}"
                                                    BorderThickness="1"
                                                    CornerRadius="2"
                                                    Padding="8,4"
                                                    Margin="2"
                                                    Cursor="Hand"
                                                    ToolTip.Tip="{Binding Tooltip}"
                                                    PointerPressed="OnCellaPressed">
                                                <TextBlock Text="{Binding Etichetta}"
                                                           FontSize="12"
                                                           Foreground="#1F1F1F"/>
                                            </Border>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </Grid>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </ScrollViewer>

            <!-- DETTAGLIO -->
            <ContentControl x:Name="DettaglioArea"
                            IsVisible="{Binding ModoDettaglio}"/>
        </Grid>
```

- [ ] **Step 2: Aggiungere stato `ModoDettaglio` in `StatisticheView.axaml.cs`**

Sotto `LimiteUltimeN`:

```csharp
    private bool _modoDettaglio;
    public bool ModoDettaglio
    {
        get => _modoDettaglio;
        private set { if (_modoDettaglio != value) { _modoDettaglio = value; Raise(); } }
    }

    private CategoriaDettaglioView? _dettaglio;
```

- [ ] **Step 3: Implementare `OnCellaPressed` e swap**

Sostituisci il corpo (`// Verra' agganciato...`) di `OnCellaPressed` e aggiungi i metodi di swap:

```csharp
    private void OnCellaPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border b) return;
        if (b.DataContext is not CategoriaCellaItem cella) return;
        ApriDettaglio(cella.Stat);
    }

    private void ApriDettaglio(CategoriaStat stat)
    {
        if (_dettaglio == null)
        {
            _dettaglio = new CategoriaDettaglioView();
            _dettaglio.IndietroRichiesto += (_, _) => ChiudiDettaglio();
            DettaglioArea.Content = _dettaglio;
        }
        _dettaglio.Mostra(stat);
        ModoDettaglio = true;
    }

    private void ChiudiDettaglio() => ModoDettaglio = false;
```

- [ ] **Step 4: Build**

```bash
dotnet build Wsa.Quiz.sln
```

Atteso: 0 errori.

- [ ] **Step 5: Commit**

```bash
git add wsa.quiz.app/Views/StatisticheView.axaml wsa.quiz.app/Views/StatisticheView.axaml.cs
git commit -m "feat(step9): drill-down heatmap->CategoriaDettaglioView"
```

---

## Task 9: Empty state

Distingue "cronologia vuota" da "filtro troppo stretto".

**Files:**
- Modify: `wsa.quiz.app/Views/StatisticheView.axaml`
- Modify: `wsa.quiz.app/Views/StatisticheView.axaml.cs`

- [ ] **Step 1: Aggiungere stato empty in `StatisticheView.axaml.cs`**

Sotto `ModoDettaglio`:

```csharp
    private bool _nessunDato = true;
    public bool NessunDato
    {
        get => _nessunDato;
        private set { if (_nessunDato != value) { _nessunDato = value; Raise(); } }
    }

    private bool _filtroTroppoStretto;
    public bool FiltroTroppoStretto
    {
        get => _filtroTroppoStretto;
        private set { if (_filtroTroppoStretto != value) { _filtroTroppoStretto = value; Raise(); } }
    }
```

- [ ] **Step 2: Aggiornare `Ricarica` per popolare i flag**

Sostituisci il corpo di `Ricarica` con:

```csharp
    public void Ricarica()
    {
        if (_storage == null) return;
        Righe.Clear();
        try
        {
            var cronologia = _storage.CaricaCronologia();
            int? limite = ModalitaTutto ? null : LimiteUltimeN;
            var stat = _servizio.Calcola(cronologia, limite);
            foreach (var r in stat)
                Righe.Add(new MateriaRigaItem(r));

            NessunDato = cronologia.Count == 0;
            FiltroTroppoStretto = !NessunDato && Righe.Count == 0;
        }
        catch
        {
            NessunDato = true;
            FiltroTroppoStretto = false;
        }
    }
```

- [ ] **Step 3: Aggiungere pannelli empty state nello XAML**

Dentro il `<Grid>` che ospita heatmap e dettaglio (creato in Task 8), aggiungi due `<Border>` come figli aggiuntivi (l'ordine non conta perche' ogni pannello ha la sua `IsVisible` mutualmente esclusiva). Puoi metterli dopo la `<ScrollViewer>` e prima del `<ContentControl x:Name="DettaglioArea">`:

```xml
            <!-- EMPTY: cronologia totalmente vuota -->
            <Border IsVisible="{Binding NessunDato}"
                    Margin="20,8,20,16"
                    VerticalAlignment="Top"
                    Background="{DynamicResource SystemRegionBrush}"
                    BorderBrush="{DynamicResource SystemBaseLowColor}"
                    BorderThickness="1"
                    CornerRadius="6"
                    Padding="20">
                <StackPanel HorizontalAlignment="Center" Spacing="6">
                    <TextBlock Text="Nessun quiz completato"
                               FontWeight="SemiBold"
                               HorizontalAlignment="Center"/>
                    <TextBlock Text="Fai un quiz dalla Home per vedere le statistiche dei tuoi punti deboli."
                               FontSize="12"
                               Foreground="{DynamicResource SystemBaseMediumColor}"
                               TextWrapping="Wrap"
                               TextAlignment="Center"
                               MaxWidth="460"/>
                </StackPanel>
            </Border>

            <!-- EMPTY: filtro troppo stretto -->
            <Border IsVisible="{Binding FiltroTroppoStretto}"
                    Margin="20,8,20,16"
                    VerticalAlignment="Top"
                    Background="{DynamicResource SystemRegionBrush}"
                    BorderBrush="{DynamicResource SystemBaseLowColor}"
                    BorderThickness="1"
                    CornerRadius="6"
                    Padding="20">
                <StackPanel HorizontalAlignment="Center" Spacing="6">
                    <TextBlock Text="Nessuna sessione nelle ultime N partite"
                               FontWeight="SemiBold"
                               HorizontalAlignment="Center"/>
                    <TextBlock Text="Aumenta il limite N oppure passa a 'Tutto'."
                               FontSize="12"
                               Foreground="{DynamicResource SystemBaseMediumColor}"
                               TextWrapping="Wrap"
                               TextAlignment="Center"
                               MaxWidth="460"/>
                </StackPanel>
            </Border>
```

E aggiungi al binding della `ScrollViewer` la condizione di nascondersi se uno degli empty e' attivo. Cambia:

```xml
            <ScrollViewer Margin="20,0,20,16"
                          VerticalScrollBarVisibility="Auto"
                          IsVisible="{Binding !ModoDettaglio}">
```

in:

```xml
            <ScrollViewer Margin="20,0,20,16"
                          VerticalScrollBarVisibility="Auto"
                          IsVisible="{Binding HeatmapVisibile}">
```

- [ ] **Step 4: Aggiungere proprieta' computata `HeatmapVisibile`**

In `StatisticheView.axaml.cs`, sotto `FiltroTroppoStretto`:

```csharp
    public bool HeatmapVisibile => !ModoDettaglio && !NessunDato && !FiltroTroppoStretto;
```

E modifica i tre setter (`ModoDettaglio`, `NessunDato`, `FiltroTroppoStretto`) per notificare anche `HeatmapVisibile`. Ad esempio per `ModoDettaglio`:

```csharp
    public bool ModoDettaglio
    {
        get => _modoDettaglio;
        private set
        {
            if (_modoDettaglio != value)
            {
                _modoDettaglio = value;
                Raise();
                Raise(nameof(HeatmapVisibile));
            }
        }
    }
```

Applica lo stesso pattern (aggiungere `Raise(nameof(HeatmapVisibile));`) a `NessunDato` e `FiltroTroppoStretto`.

- [ ] **Step 5: Build**

```bash
dotnet build Wsa.Quiz.sln
```

Atteso: 0 errori.

- [ ] **Step 6: Commit**

```bash
git add wsa.quiz.app/Views/StatisticheView.axaml wsa.quiz.app/Views/StatisticheView.axaml.cs
git commit -m "feat(step9): empty state cronologia vuota / filtro troppo stretto"
```

---

## Task 10: Integrazione `MainWindow`

Aggiunge la quarta tab, istanzia la view, refresh post-quiz.

**Files:**
- Modify: `wsa.quiz.app/MainWindow.axaml:68-70`
- Modify: `wsa.quiz.app/MainWindow.axaml.cs` (campi privati, metodo `Caricamento`, metodo `OnQuizConcluso`)

- [ ] **Step 1: Aggiungere il `TabItem` nella `MainWindow.axaml`**

Sostituisci:

```xml
            <TabItem Header="Sospesi">
                <ContentControl x:Name="SospesiArea"/>
            </TabItem>
        </TabControl>
```

con:

```xml
            <TabItem Header="Sospesi">
                <ContentControl x:Name="SospesiArea"/>
            </TabItem>
            <TabItem Header="Statistiche">
                <ContentControl x:Name="StatisticheArea"/>
            </TabItem>
        </TabControl>
```

- [ ] **Step 2: Aggiungere il campo `_statisticheView` in `MainWindow.axaml.cs`**

Trova la riga `private SospesiView? _sospesiView;` (vicino agli altri campi privati delle view) e aggiungi subito sotto:

```csharp
    private StatisticheView? _statisticheView;
```

- [ ] **Step 3: Istanziare `StatisticheView` in `Caricamento()`**

Trova nel metodo `Caricamento()` il blocco:

```csharp
            // ---------- Tab Sospesi ----------
            _sospesiView = new SospesiView();
            _sospesiView.Inizializza(_storage);
            _sospesiView.RiprendiRichiesto += OnRiprendiSospesa;
            SospesiArea.Content = _sospesiView;
```

E aggiungi subito dopo, ancora dentro il `try`:

```csharp
            // ---------- Tab Statistiche ----------
            _statisticheView = new StatisticheView();
            _statisticheView.Inizializza(_storage);
            StatisticheArea.Content = _statisticheView;
```

- [ ] **Step 4: Aggiungere auto-refresh in `OnQuizConcluso`**

Trova in `OnQuizConcluso` la riga:

```csharp
                _cronologiaView?.Ricarica();
```

E aggiungi subito sotto:

```csharp
                _statisticheView?.Ricarica();
```

- [ ] **Step 5: Build**

```bash
dotnet build Wsa.Quiz.sln
```

Atteso: 0 errori.

- [ ] **Step 6: Commit**

```bash
git add wsa.quiz.app/MainWindow.axaml wsa.quiz.app/MainWindow.axaml.cs
git commit -m "feat(step9): tab Statistiche integrata in MainWindow + auto-refresh"
```

---

## Task 11: Smoke test manuale end-to-end

Verifica integrazione su scenario reale. Se la cronologia e' vuota, il test inizia facendo qualche quiz.

- [ ] **Step 1: Avviare l'app**

```bash
dotnet run --project wsa.quiz.app
```

- [ ] **Step 2: Verificare empty state iniziale (se cronologia vuota)**

Clicca sulla tab "Statistiche". Atteso:
- Header con titolo "Statistiche", RadioButton "Tutto"/"Ultime", NumericUpDown="30", bottone "Aggiorna" visibili
- Messaggio "Nessun quiz completato. Fai un quiz dalla Home..." al centro

Se cronologia non e' vuota, salta al passo 4.

- [ ] **Step 3: Fare 2-3 quiz misti**

Torna alla Home, fai almeno 2 quiz su materie diverse (es. uno su cs/POO e uno su sql/join), sbagliando di proposito qualche domanda al primo colpo. Torna alla tab Statistiche.

- [ ] **Step 4: Verificare la heatmap**

Atteso:
- Una riga per ciascuna materia toccata (in ordine alfabetico)
- Celle ordinate per % errore decrescente da sinistra a destra
- Bordo e sfondo: verde &lt;20%, ambra 20-59%, rosso &gt;=60%
- Etichetta cella: "categoria NN%"
- Tooltip al passaggio del mouse: "X errori su Y viste"

- [ ] **Step 5: Verificare il drill-down**

Click su una cella con almeno un errore. Atteso:
- Swap a `CategoriaDettaglioView`
- Titolo: "{materia} / {categoria} — X errori su Y viste (NN%)"
- Lista di card rosse con "❌ sbagliata X/Y volte", testo domanda, "✓ corretta: ...", spiegazione
- Bottone "← Statistiche" funzionante, torna alla heatmap

- [ ] **Step 6: Verificare il toggle filtro**

In Statistiche: seleziona "Ultime", imposta NumericUpDown a 1. Atteso:
- Heatmap si ricalcola con i soli dati dell'ultimo quiz
- Se 1 e' troppo stretto e azzera tutto, appare l'empty "Nessuna sessione nelle ultime N partite"

Rimetti N=30 e seleziona "Tutto". Verifica che la heatmap torna piena.

- [ ] **Step 7: Verificare l'auto-refresh post-quiz**

Vai alla tab Statistiche (heatmap visibile). Vai alla Home, lancia un quiz, completalo. Torna alla tab Statistiche. Atteso: i numeri sono aggiornati senza dover cliccare "Aggiorna".

- [ ] **Step 8: Verificare la shared-storage console/GUI**

Chiudi l'app. Avvia la console:

```bash
dotnet run --project wsa.quiz.cli
```

Fai un quiz dalla console, esci. Riavvia la GUI, vai a Statistiche. Atteso: il quiz fatto da console contribuisce ai numeri.

- [ ] **Step 9: Verificare Esc sul drill-down**

Apri un drill-down. Premi `Esc`. Atteso (incerto, vedi Trappola 13 di CLAUDE.md): torna alla heatmap. Se non funziona, il bottone "← Statistiche" continua a funzionare. Annotare il risultato in CLAUDE.md al Task 12.

Nessun commit a questo step.

---

## Task 12: Aggiornamento `CLAUDE.md`

Aggiornare la roadmap, le note tecniche e — se rilevante — la trappola 13.

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Aggiornare la sezione "Stato attuale"**

Cambia:

```markdown
**Step 7 completo + Step 8 ancora parziale.** Due tentativi falliti sulla
navigazione tastiera nelle liste/Home: vedi sezione Step 8 e Trappola 13.
```

in:

```markdown
**Step 7 + Step 9 completi. Step 8 ancora parziale.** Due tentativi falliti
sulla navigazione tastiera nelle liste/Home (vedi sezione Step 8 e
Trappola 13). Step 9: tab Statistiche con heatmap punti deboli per
categoria + drill-down domande sbagliate (no LiveCharts2, no grafici veri).
```

- [ ] **Step 2: Aggiornare il layout repo per i nuovi file**

Nella sezione `wsa.quiz.app/`, aggiorna `State/` e `Views/` per includere:
- `State/MateriaRigaItem.cs`
- `State/CategoriaCellaItem.cs`
- `Views/StatisticheView.axaml(.cs)`
- `Views/CategoriaDettaglioView.axaml(.cs)`

E nella sezione `wsa.quiz.core/`:
- `Models/Statistiche/` (CategoriaStat, DomandaSbagliata, StatistichePerMateria)
- `Services/StatisticheService.cs`

- [ ] **Step 3: Aggiornare la roadmap dello Step 9**

Cambia il blocco dello Step 9 da:

```markdown
### ⏳ Step 9 — Grafici
LiveCharts2 in nuova tab "Statistiche" (quarta). % corrette per materia
(bar chart), drill-down su categorie. Verificare al momento dell'installazione
una versione di LiveCharts2 compatibile con Avalonia 12.
```

in:

```markdown
### ✅ Step 9 — Statistiche (mappa punti deboli)
Tab "Statistiche" (quarta) con heatmap per categoria: una riga per materia,
celle ordinate per % errore decrescente, colori verde/ambra/rosso. Click su
una cella apre il drill-down con le domande sbagliate di quella categoria.
Toggle "Tutto" / "Ultime N partite" (NumericUpDown, default 30). **Senza
LiveCharts2**: tutto fatto con ItemsControl/WrapPanel/Border. Definizione di
"errore" = !(Corretta && Tentativi==1) — unifica modalita' classica e
rotazione. Servizio puro in Wsa.Quiz.Core.Services.StatisticheService.
Spec: docs/superpowers/specs/2026-05-18-step9-grafici-statistiche-design.md.
```

- [ ] **Step 4: Annotare il risultato di Esc su drill-down**

In base all'esito del Task 11 step 9, aggiungere una nota nella sezione Step 8 ("NON funziona ancora"): se `Esc` su `CategoriaDettaglioView` funziona, aggiornare anche la Trappola 13 menzionando che in questo caso specifico il pattern Tunnel+handledEventsToo ha funzionato. Se non funziona, aggiungerlo all'elenco.

- [ ] **Step 5: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: aggiorna roadmap e layout per Step 9 completato"
```

---

## Self-review checklist (gia' eseguita)

- **Spec coverage:** ogni sezione del design (modello dati, servizio, heatmap, drill-down, empty state, integrazione MainWindow, testing manuale) ha task corrispondente. Aggiornamento CLAUDE.md aggiunto come task finale per non lasciare la doc out-of-date.
- **Placeholder scan:** nessun TBD/TODO/"fill in" nel piano. Tutti i blocchi di codice sono completi.
- **Type consistency:** `CategoriaStat`/`DomandaSbagliata`/`StatistichePerMateria` definiti in Task 1 e usati con la stessa firma in Task 2/4/5/8. `Mostra(CategoriaStat)` e `Inizializza(StorageService)` e `Ricarica()` consistent. `HeatmapBordo`/`HeatmapSfondo` definiti in Task 3 e usati in Task 4. `IndietroRichiesto` evento in Task 5 consumato in Task 8.
- **Ambiguity:** percentuali sempre in scala 0..1 nei calcoli; formattazione `:P0` in UI. Soglie 20%/60%. Ordinamento materie alfabetico, categorie per `PctErrore` desc poi alfabetico.
