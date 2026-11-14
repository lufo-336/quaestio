# M3 — Layout touch-friendly — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rendere usabili al tocco su Android tutte le schermate di WSA Quiz, senza far regredire il desktop (UI in libreria condivisa `Wsa.Quiz.App`).

**Architecture:** Un flag di piattaforma `AppEnv.TouchMode` (acceso solo nel ramo Android/single-view di `App`) seleziona, via due `ResourceDictionary` con chiavi omonime, dimensioni maggiorate (target tap, font, spaziature). Stili globali in `App.axaml` applicano `MinHeight` ai controlli interattivi così i target tap crescono ovunque automaticamente. Le poche modifiche strutturali (layout affiancati/tabellari che non refluiscono) sono pilotate dallo stesso flag nel code-behind delle view.

**Tech Stack:** .NET 10, Avalonia 12.0.2 (Fluent), C# code-behind + INPC (no MVVM), `Avalonia.Headless` per smoke test.

## Global Constraints

- **Desktop invariato**: con `AppEnv.TouchMode == false` (default desktop) ogni schermata deve restare identica a oggi. I valori desktop dei dizionari = valori attuali hard-coded.
- **`net10.0`** su tutti i progetti; **Avalonia 12.0.2** (API v12: vedi Trappole 14 nel CLAUDE.md per le differenze con v11).
- **Compiled bindings**: `x:DataType` resta obbligatorio su ogni `UserControl`/`DataTemplate` (Trappola 5). Non rimuoverlo.
- **Target primario**: telefono in **portrait**. Non ottimizzare per landscape/tablet.
- **APK installabile** solo con `dotnet build wsa.quiz.android -c Release` (Debug non produce APK). Prima dei build Android: `$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"`.
- **Verifica visiva Android = solo su device fisico** (questa VM non esegue l'emulatore). Il gate automatico è build verde + smoke headless.
- **Fasatura**: Fase A (Task 1–7) consegnabile da sola; Fase B (Task 8–11) dopo.

---

## File Structure

- `wsa.quiz.app/AppEnv.cs` — **nuovo**. Flag statico `TouchMode`.
- `wsa.quiz.app/Themes/Sizes.Desktop.axaml` — **nuovo**. Dimensioni desktop (= attuali).
- `wsa.quiz.app/Themes/Sizes.Touch.axaml` — **nuovo**. Dimensioni touch (maggiorate).
- `wsa.quiz.app/App.axaml` — modifica: include dizionario desktop + stili globali `MinHeight`.
- `wsa.quiz.app/App.axaml.cs` — modifica: set `TouchMode` + merge dizionario touch su Android.
- `wsa.quiz.app/Views/QuizView.axaml` — modifica: sizing.
- `wsa.quiz.app/Views/HomeView.axaml` + `.axaml.cs` — modifica: sizing + stack Materie/Categorie.
- `wsa.quiz.app/Views/RiepilogoView.axaml` — modifica: sizing + wrap statistiche.
- `wsa.quiz.app/MainView.axaml` — verifica/eventuale ritocco header tab (sizing arriva da stile globale `TabItem`).
- `wsa.quiz.app/Views/SospesiView.axaml` — modifica: sizing.
- `wsa.quiz.app/Views/StatisticheView.axaml` — modifica: header wrap + padding celle.
- `wsa.quiz.app/Views/CronologiaView.axaml` + `.axaml.cs` — modifica: card touch + header colonne nascosto.
- `%TEMP%\m2verify\Program.cs` — modifica: aggiunge smoke "touch" (risoluzione risorse). Harness fuori dal repo, già riparato per M3 parte 1.

> **`x:Double`/`Thickness` come risorse**: nei `.axaml` dei dizionari servono i namespace `xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"`. `Thickness` ha un type converter, quindi `<Thickness x:Key="...">14,12</Thickness>` è valido in Avalonia 12.

---

## FASE A — Fondazione + flusso core

### Task 1: Flag di piattaforma `AppEnv.TouchMode`

**Files:**
- Create: `wsa.quiz.app/AppEnv.cs`
- Modify: `wsa.quiz.app/App.axaml.cs:33-37` (ramo single-view) e `:19-32` (ramo desktop)

**Interfaces:**
- Produces: `Wsa.Quiz.App.AppEnv.TouchMode` (`static bool`), default `false`.

- [ ] **Step 1: Creare la classe del flag**

`wsa.quiz.app/AppEnv.cs`:
```csharp
namespace Wsa.Quiz.App;

/// <summary>
/// Ambiente di esecuzione corrente, deciso una sola volta all'avvio in
/// <see cref="App.OnFrameworkInitializationCompleted"/>. Unico punto di verità
/// per distinguere desktop da touch (Android) senza #if sparsi nelle view.
/// </summary>
public static class AppEnv
{
    /// <summary>True su Android/single-view: abilita dimensioni e layout touch.</summary>
    public static bool TouchMode { get; set; }
}
```

- [ ] **Step 2: Impostare il flag nei due rami del lifetime**

In `wsa.quiz.app/App.axaml.cs`, dentro `OnFrameworkInitializationCompleted`, impostare il flag PRIMA di costruire la `MainView`. Sostituire il corpo attuale:
```csharp
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        AppEnv.TouchMode = false;
        desktop.MainWindow = new Window
        {
            Title = "WSA Quiz",
            Width = 1080,
            Height = 760,
            MinWidth = 780,
            MinHeight = 540,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new MainView()
        };
    }
    else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
    {
        AppEnv.TouchMode = true;
        singleView.MainView = new MainView();
    }

    base.OnFrameworkInitializationCompleted();
}
```

- [ ] **Step 3: Build di verifica (desktop/cli)**

Run: `dotnet build Wsa.Quiz.sln`
Expected: build verde, 0 errori.

- [ ] **Step 4: Commit**

```bash
git add wsa.quiz.app/AppEnv.cs wsa.quiz.app/App.axaml.cs
git commit -m "feat(android): flag AppEnv.TouchMode acceso su single-view"
```

---

### Task 2: Dizionari dimensioni + merge su Android

**Files:**
- Create: `wsa.quiz.app/Themes/Sizes.Desktop.axaml`
- Create: `wsa.quiz.app/Themes/Sizes.Touch.axaml`
- Modify: `wsa.quiz.app/App.axaml` (aggiunge `Application.Resources` con dizionario desktop)
- Modify: `wsa.quiz.app/App.axaml.cs` (merge dizionario touch nel ramo Android)

**Interfaces:**
- Consumes: `AppEnv.TouchMode` (Task 1).
- Produces: chiavi risorsa risolvibili via `DynamicResource`: `SzTapMin`, `SzFontBody`, `SzFontDomanda`, `SzFontTitolo`, `SzGap` (tutte `x:Double`), `SzAnswerPadding`, `SzBtnPadding`, `SzPaginaMargin` (tutte `Thickness`).

- [ ] **Step 1: Creare il dizionario desktop (valori attuali)**

`wsa.quiz.app/Themes/Sizes.Desktop.axaml`:
```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <x:Double x:Key="SzTapMin">32</x:Double>
    <x:Double x:Key="SzFontBody">14</x:Double>
    <x:Double x:Key="SzFontDomanda">16</x:Double>
    <x:Double x:Key="SzFontTitolo">22</x:Double>
    <x:Double x:Key="SzGap">14</x:Double>
    <Thickness x:Key="SzAnswerPadding">14,12</Thickness>
    <Thickness x:Key="SzBtnPadding">14,6</Thickness>
    <Thickness x:Key="SzPaginaMargin">20</Thickness>
</ResourceDictionary>
```

- [ ] **Step 2: Creare il dizionario touch (valori maggiorati)**

`wsa.quiz.app/Themes/Sizes.Touch.axaml` — stesse chiavi, valori touch:
```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <x:Double x:Key="SzTapMin">48</x:Double>
    <x:Double x:Key="SzFontBody">16</x:Double>
    <x:Double x:Key="SzFontDomanda">19</x:Double>
    <x:Double x:Key="SzFontTitolo">24</x:Double>
    <x:Double x:Key="SzGap">18</x:Double>
    <Thickness x:Key="SzAnswerPadding">18,16</Thickness>
    <Thickness x:Key="SzBtnPadding">16,10</Thickness>
    <Thickness x:Key="SzPaginaMargin">16</Thickness>
</ResourceDictionary>
```

- [ ] **Step 3: Includere il dizionario desktop in `App.axaml`**

In `wsa.quiz.app/App.axaml`, subito dopo l'apertura di `<Application ...>` e PRIMA di `<Application.Styles>`, aggiungere:
```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceInclude Source="avares://Wsa.Quiz.App/Themes/Sizes.Desktop.axaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

- [ ] **Step 4: Merge del dizionario touch su Android**

In `wsa.quiz.app/App.axaml.cs`, nel ramo `ISingleViewApplicationLifetime`, dopo `AppEnv.TouchMode = true;` e prima di assegnare `MainView`, aggiungere il merge (le chiavi omonime aggiunte dopo vincono nella risoluzione `DynamicResource`):
```csharp
AppEnv.TouchMode = true;
Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Wsa.Quiz.App/"))
{
    Source = new Uri("avares://Wsa.Quiz.App/Themes/Sizes.Touch.axaml")
});
singleView.MainView = new MainView();
```
Aggiungere `using Avalonia.Markup.Xaml.Styling;` (per `ResourceInclude`) e `using System;` in cima al file se non presenti.

> **Rischio noto (verificare in Step 6)**: se l'aggiunta in coda NON facesse vincere le chiavi touch, sostituire il dizionario desktop invece di accodare: rimuovere il primo da `Resources.MergedDictionaries` e aggiungere il touch. Tracciato in spec §"Trappole/rischi".

- [ ] **Step 5: Build di verifica (desktop + APK Release)**

Run: `dotnet build Wsa.Quiz.sln`
Expected: build verde.
Run (con `$env:ANDROID_HOME` impostato): `dotnet build wsa.quiz.android -c Release`
Expected: build verde, APK prodotto in `bin/Release/net10.0-android/it.eduits.wsaquiz-Signed.apk`.

- [ ] **Step 6: Smoke headless — risoluzione risorse touch**

Modificare `%TEMP%\m2verify\Program.cs`: dopo il blocco M3 esistente (o in un blocco a sé prima di `EseguiCheck`), aggiungere un check che, simulando il merge touch, la chiave `SzTapMin` risolva a 48. Inserire all'inizio di `EseguiCheck` (dopo `win.Show()`):
```csharp
// Smoke touch: simula il merge del dizionario touch e verifica la risoluzione.
Application.Current!.Resources.MergedDictionaries.Add(
    new Avalonia.Markup.Xaml.Styling.ResourceInclude(new Uri("avares://Wsa.Quiz.App/"))
    {
        Source = new Uri("avares://Wsa.Quiz.App/Themes/Sizes.Touch.axaml")
    });
var tapMin = Application.Current.Resources["SzTapMin"];
Check(tapMin is double d && d == 48.0, $"Touch: SzTapMin risolve a 48 (era {tapMin})");
```
Run: `cd %TEMP%\m2verify && dotnet run -c Release`
Expected: tra i check compare `OK   Touch: SzTapMin risolve a 48`, e `TUTTI I CHECK OK`.

- [ ] **Step 7: Commit**

```bash
git add wsa.quiz.app/Themes/Sizes.Desktop.axaml wsa.quiz.app/Themes/Sizes.Touch.axaml wsa.quiz.app/App.axaml wsa.quiz.app/App.axaml.cs
git commit -m "feat(android): dizionari dimensioni desktop/touch + merge su single-view"
```

---

### Task 3: Stili globali per i target tap

**Files:**
- Modify: `wsa.quiz.app/App.axaml` (dentro `Application.Styles`)

**Interfaces:**
- Consumes: chiave `SzTapMin` (Task 2).

- [ ] **Step 1: Aggiungere gli stili `MinHeight`**

In `wsa.quiz.app/App.axaml`, dentro `<Application.Styles>` (dopo gli stili esistenti, prima di `</Application.Styles>`), aggiungere:
```xml
<!-- M3 touch: target tap minimi. Su desktop SzTapMin=32 (≈ come oggi),
     su Android 48. MinHeight non altera i bottoni desktop (gia' ~32). -->
<Style Selector="Button">
    <Setter Property="MinHeight" Value="{DynamicResource SzTapMin}"/>
</Style>
<Style Selector="CheckBox">
    <Setter Property="MinHeight" Value="{DynamicResource SzTapMin}"/>
</Style>
<Style Selector="NumericUpDown">
    <Setter Property="MinHeight" Value="{DynamicResource SzTapMin}"/>
</Style>
<Style Selector="ListBoxItem">
    <Setter Property="MinHeight" Value="{DynamicResource SzTapMin}"/>
</Style>
<Style Selector="TabItem">
    <Setter Property="MinHeight" Value="{DynamicResource SzTapMin}"/>
</Style>
```

- [ ] **Step 2: Build + regressione desktop**

Run: `dotnet build Wsa.Quiz.sln`
Expected: build verde.
Run: `dotnet run --project wsa.quiz.app`
Expected: la GUI desktop si apre e appare **identica a prima** (bottoni non più alti: `SzTapMin`=32 ≈ default Fluent). Chiudere dopo controllo visivo.

- [ ] **Step 3: Smoke headless — MinHeight sotto touch**

In `%TEMP%\m2verify\Program.cs`, dopo il check `SzTapMin` dello Step 6 del Task 2, aggiungere:
```csharp
var btnTest = new Button { Content = "x" };
win.Content = btnTest;            // attaccarlo per far valutare gli stili
Dispatcher.UIThread.RunJobs();
Check(btnTest.MinHeight == 48.0, $"Touch: Button.MinHeight == 48 (era {btnTest.MinHeight})");
win.Content = null;
```
Run: `cd %TEMP%\m2verify && dotnet run -c Release`
Expected: `OK   Touch: Button.MinHeight == 48`.

- [ ] **Step 4: Commit**

```bash
git add wsa.quiz.app/App.axaml
git commit -m "feat(android): stili globali MinHeight per target tap touch"
```

---

### Task 4: QuizView — sizing touch

**Files:**
- Modify: `wsa.quiz.app/Views/QuizView.axaml`

**Interfaces:**
- Consumes: `SzAnswerPadding`, `SzFontBody`, `SzFontDomanda`, `SzPaginaMargin` (Task 2).

- [ ] **Step 1: Padding/font dei bottoni risposta da risorsa**

In `wsa.quiz.app/Views/QuizView.axaml`, nello `Style Selector="Button.risposta"` (righe ~14-22), sostituire i valori hard-coded di `Padding` e `FontSize`:
```xml
<Style Selector="Button.risposta">
    <Setter Property="HorizontalAlignment" Value="Stretch"/>
    <Setter Property="HorizontalContentAlignment" Value="Left"/>
    <Setter Property="Padding" Value="{DynamicResource SzAnswerPadding}"/>
    <Setter Property="FontSize" Value="{DynamicResource SzFontBody}"/>
    <Setter Property="Margin" Value="0,4"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="6"/>
</Style>
```

- [ ] **Step 2: Font del testo domanda + margine pagina da risorsa**

Nel `TextBlock` del testo domanda (riga ~156), cambiare `FontSize="16"` in `FontSize="{DynamicResource SzFontDomanda}"`. Nel `Grid` radice (riga ~77), cambiare `Margin="20"` in `Margin="{DynamicResource SzPaginaMargin}"`.

- [ ] **Step 3: Build + regressione desktop**

Run: `dotnet build Wsa.Quiz.sln`
Expected: verde.
Run: `dotnet run --project wsa.quiz.app`, avviare un quiz: la QuizView appare **identica a oggi** su desktop (valori desktop = attuali). Chiudere.

- [ ] **Step 4: Commit**

```bash
git add wsa.quiz.app/Views/QuizView.axaml
git commit -m "feat(android): QuizView usa dimensioni da risorsa (touch-aware)"
```

---

### Task 5: HomeView — sizing + stack Materie/Categorie su touch

**Files:**
- Modify: `wsa.quiz.app/Views/HomeView.axaml` (Grid Materie/Categorie, margini)
- Modify: `wsa.quiz.app/Views/HomeView.axaml.cs` (reflow in code-behind)

**Interfaces:**
- Consumes: `AppEnv.TouchMode` (Task 1), `SzPaginaMargin` (Task 2).

- [ ] **Step 1: Dare un nome al Grid Materie/Categorie**

In `wsa.quiz.app/Views/HomeView.axaml`, al `Grid` che contiene Materie e Categorie (riga ~53, `ColumnDefinitions="*,16,2*"`), aggiungere `x:Name="GrigliaMaterieCategorie"`. Lasciare invariato il resto (desktop usa questa griglia a 2 colonne).

- [ ] **Step 2: Reflow verticale su touch nel code-behind**

In `wsa.quiz.app/Views/HomeView.axaml.cs`, nel costruttore DOPO `InitializeComponent()`, aggiungere il reflow condizionale. Su touch: una sola colonna, due righe; Materie in (riga 0), Categorie spostata in (riga 1) con margine sopra.
```csharp
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
```
Aggiungere `using Avalonia;` e `using Avalonia.Controls;` se non presenti. NB: `ZonaMaterie` e `ZonaCategorie` hanno già `x:Name` nel XAML, quindi sono accessibili.

- [ ] **Step 3: Margine pagina da risorsa**

Nel `ScrollViewer` del contenuto (riga ~49, `Padding="20"`) e nel `Border` `ZonaAvvia` (riga ~22, `Padding="20,12,20,12"`), lasciare i padding com'è (sono già ragionevoli). Nessuna modifica obbligatoria qui — il target tap dei `CheckBox` arriva dallo stile globale del Task 3.

- [ ] **Step 4: Build + regressione desktop**

Run: `dotnet build Wsa.Quiz.sln`
Expected: verde.
Run: `dotnet run --project wsa.quiz.app`: la Home desktop mostra Materie **a sinistra** e Categorie **a destra** come oggi (TouchMode=false ⇒ reflow non eseguito). Chiudere.

- [ ] **Step 5: Smoke headless — reflow attivo su touch**

In `%TEMP%\m2verify\Program.cs`, aggiungere un blocco che, con `Wsa.Quiz.App.AppEnv.TouchMode = true`, istanzia `HomeView` e verifica che `ZonaCategorie` sia in `Grid.Row==1`:
```csharp
Wsa.Quiz.App.AppEnv.TouchMode = true;
var home = new Wsa.Quiz.App.Views.HomeView();
win.Content = home;
Dispatcher.UIThread.RunJobs();
var zonaCat = home.FindControl<Control>("ZonaCategorie")!;
Check(Grid.GetRow(zonaCat) == 1, $"Home touch: ZonaCategorie impilata in Row 1 (era {Grid.GetRow(zonaCat)})");
win.Content = null;
```
> Se `HomeView` richiede un costruttore con dipendenze (controllare la firma reale prima di scrivere il check): se non è instanziabile senza dati, saltare lo smoke e affidarsi al build + verifica su device, annotandolo.
Run: `cd %TEMP%\m2verify && dotnet run -c Release`
Expected: `OK   Home touch: ZonaCategorie impilata in Row 1`.

- [ ] **Step 6: Commit**

```bash
git add wsa.quiz.app/Views/HomeView.axaml wsa.quiz.app/Views/HomeView.axaml.cs
git commit -m "feat(android): HomeView impila Materie/Categorie in verticale su touch"
```

---

### Task 6: RiepilogoView — wrap statistiche su touch

**Files:**
- Modify: `wsa.quiz.app/Views/RiepilogoView.axaml`
- Modify: `wsa.quiz.app/Views/RiepilogoView.axaml.cs` (reflow in code-behind)

**Interfaces:**
- Consumes: `AppEnv.TouchMode` (Task 1).

- [ ] **Step 1: Nominare il contenitore delle 5 metriche**

In `wsa.quiz.app/Views/RiepilogoView.axaml`, il `Grid ColumnDefinitions="*,*,*,*,*"` (riga ~30) contiene la percentuale (col 0) + 4 metriche (col 1-4) + la riga durata/modalità. Aggiungere `x:Name="GrigliaStat"` a quel `Grid`.

- [ ] **Step 2: Reflow su touch — percentuale sopra, metriche a wrap**

Su telefono portrait 5 colonne sono troppe. Strategia minima e chiara: su touch, collassare la griglia a 2 colonne così le 4 metriche vanno su due righe sotto la percentuale. In `wsa.quiz.app/Views/RiepilogoView.axaml.cs`, nel costruttore dopo `InitializeComponent()`:
```csharp
if (AppEnv.TouchMode)
{
    // 5 colonne -> percentuale a tutta riga in alto, metriche 2x2 sotto.
    var g = GrigliaStat;
    g.ColumnDefinitions = new ColumnDefinitions("*,*");
    g.RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto");

    // Percentuale (era Col0 RowSpan2): tutta la prima riga.
    Grid.SetColumn(BloccoPercentuale, 0);
    Grid.SetColumnSpan(BloccoPercentuale, 2);
    Grid.SetRow(BloccoPercentuale, 0);
    Grid.SetRowSpan(BloccoPercentuale, 1);

    // 4 metriche (Totale/Corrette/Errate/Punteggio) in griglia 2x2, righe 1-2.
    PosizionaMetrica(BloccoTotale,    col: 0, row: 1);
    PosizionaMetrica(BloccoCorrette,  col: 1, row: 1);
    PosizionaMetrica(BloccoErrate,    col: 0, row: 2);
    PosizionaMetrica(BloccoPunteggio, col: 1, row: 2);

    // Riga durata/modalità: ultima riga, due colonne.
    Grid.SetColumn(BloccoDurataModalita, 0);
    Grid.SetColumnSpan(BloccoDurataModalita, 2);
    Grid.SetRow(BloccoDurataModalita, 3);
}

static void PosizionaMetrica(Control c, int col, int row)
{
    Grid.SetColumn(c, col);
    Grid.SetColumnSpan(c, 1);
    Grid.SetRow(c, row);
    c.Margin = new Thickness(0, 8, 0, 0);
}
```
Aggiungere `using Avalonia;` e `using Avalonia.Controls;` se mancanti.

- [ ] **Step 3: Dare `x:Name` ai 6 blocchi nel XAML**

Nel `Grid` `GrigliaStat`, assegnare i nomi usati nel code-behind ai relativi `StackPanel`:
- percentuale (riga ~32) → `x:Name="BloccoPercentuale"`
- Totale (riga ~43) → `x:Name="BloccoTotale"`
- Corrette (riga ~50) → `x:Name="BloccoCorrette"`
- Errate (riga ~58) → `x:Name="BloccoErrate"`
- Punteggio (riga ~66) → `x:Name="BloccoPunteggio"`
- durata/modalità (riga ~74) → `x:Name="BloccoDurataModalita"`

- [ ] **Step 4: Build + regressione desktop**

Run: `dotnet build Wsa.Quiz.sln`
Expected: verde.
Run: `dotnet run --project wsa.quiz.app`, completare un quiz veloce (1 domanda) → il Riepilogo desktop mostra le 5 metriche **in fila come oggi**. Chiudere.

- [ ] **Step 5: Commit**

```bash
git add wsa.quiz.app/Views/RiepilogoView.axaml wsa.quiz.app/Views/RiepilogoView.axaml.cs
git commit -m "feat(android): RiepilogoView dispone le statistiche 2x2 su touch"
```

---

### Task 7: MainView — header tab toccabili (verifica)

**Files:**
- Modify (se necessario): `wsa.quiz.app/MainView.axaml`

**Interfaces:**
- Consumes: stile globale `TabItem` con `MinHeight` (Task 3).

- [ ] **Step 1: Leggere MainView e verificare gli header tab**

Aprire `wsa.quiz.app/MainView.axaml`. Confermare che le tab usino `TabItem` standard (così ereditano il `MinHeight` globale del Task 3). Se gli header hanno padding/font hard-coded troppo piccoli per il tocco, sostituirli con `Padding="{DynamicResource SzBtnPadding}"` e `FontSize="{DynamicResource SzFontBody}"`. Se sono già adeguati, nessuna modifica.

- [ ] **Step 2: Build + regressione desktop**

Run: `dotnet build Wsa.Quiz.sln`
Expected: verde.
Run: `dotnet run --project wsa.quiz.app`: le 4 tab desktop appaiono come oggi. Chiudere.

- [ ] **Step 3: Commit (se ci sono modifiche)**

```bash
git add wsa.quiz.app/MainView.axaml
git commit -m "feat(android): header tab MainView toccabili su touch"
```
Se non sono servite modifiche, saltare il commit e annotare nel report che MainView eredita già il sizing globale.

---

### Checkpoint Fase A

- [ ] Build verde: `dotnet build Wsa.Quiz.sln` **e** `dotnet build wsa.quiz.android -c Release` (APK prodotto).
- [ ] Smoke headless verde: `cd %TEMP%\m2verify && dotnet run -c Release` → `TUTTI I CHECK OK` (inclusi i check touch).
- [ ] Verifica desktop a vista: Home, Quiz, Riepilogo identici a prima.
- [ ] **Consegna a Luca**: installare l'APK su device fisico (`adb install ...-Signed.apk`) e verificare a mano i target tap e lo stack della Home. Punto di verifica reale prima di partire con la Fase B.

---

## FASE B — Tab pesanti

### Task 8: SospesiView — sizing bottoni

**Files:**
- Modify: `wsa.quiz.app/Views/SospesiView.axaml`

**Interfaces:**
- Consumes: `SzBtnPadding` (Task 2). I `MinHeight` arrivano dallo stile globale (Task 3).

- [ ] **Step 1: Padding bottoni da risorsa**

In `wsa.quiz.app/Views/SospesiView.axaml`, sui bottoni `Riprendi`/`Elimina`/`Si', elimina`/`Annulla` (righe ~137-173) sostituire `Padding="14,6"` con `Padding="{DynamicResource SzBtnPadding}"`. La struttura a card è già touch-friendly: nessuna modifica strutturale.

- [ ] **Step 2: Build + regressione desktop**

Run: `dotnet build Wsa.Quiz.sln`
Expected: verde. Verifica visiva desktop: Sospesi come oggi.

- [ ] **Step 3: Commit**

```bash
git add wsa.quiz.app/Views/SospesiView.axaml
git commit -m "feat(android): SospesiView padding bottoni da risorsa touch"
```

---

### Task 9: StatisticheView — header a capo + celle heatmap toccabili

**Files:**
- Modify: `wsa.quiz.app/Views/StatisticheView.axaml`

**Interfaces:**
- Consumes: `AppEnv.TouchMode` non necessario qui (si usa `WrapPanel`, valido anche desktop senza regressione visibile a larghezza piena).

- [ ] **Step 1: Header filtri che va a capo**

In `wsa.quiz.app/Views/StatisticheView.axaml`, lo `StackPanel Orientation="Horizontal"` dei filtri (Radio + NumericUpDown + "partite", righe ~20-38) su telefono stretto sfora. Sostituire `StackPanel Orientation="Horizontal"` con `WrapPanel Orientation="Horizontal"` così su schermo stretto gli elementi vanno a capo; su desktop (largo) restano in fila → nessuna regressione visibile.

- [ ] **Step 2: Padding celle heatmap da risorsa-tap**

Le celle heatmap (`Border ... Padding="8,4"`, riga ~76) sono piccole da toccare. Aumentarne il padding e l'altezza minima: cambiare `Padding="8,4"` in `Padding="10,8"` e aggiungere `MinHeight="{DynamicResource SzTapMin}"` al `Border` della cella. Su desktop `SzTapMin`=32 ⇒ celle un filo più alte ma accettabili; valutare a vista. Se la differenza desktop disturba, gating con `TouchMode` via `IsVisible`/code-behind (annotare nel report quale scelta è stata presa).

- [ ] **Step 3: Build + regressione desktop**

Run: `dotnet build Wsa.Quiz.sln`
Expected: verde. Verifica desktop: Statistiche leggibile, heatmap ok.

- [ ] **Step 4: Commit**

```bash
git add wsa.quiz.app/Views/StatisticheView.axaml
git commit -m "feat(android): StatisticheView header a capo + celle heatmap toccabili"
```

---

### Task 10: CronologiaView — card touch (il pezzo grande)

**Files:**
- Modify: `wsa.quiz.app/Views/CronologiaView.axaml` (header colonne condizionale + seconda lista a card)
- Modify: `wsa.quiz.app/Views/CronologiaView.axaml.cs` (toggle liste su `TouchMode`)

**Interfaces:**
- Consumes: `AppEnv.TouchMode` (Task 1). Riusa gli handler esistenti `OnSessioneDoubleTapped`, `OnEliminaClick`, `OnConfermaEliminaClick`, `OnAnnullaEliminaClick` e i binding di `RisultatoCronologiaItem`.

- [ ] **Step 1: Nascondere header colonne su touch**

In `wsa.quiz.app/Views/CronologiaView.axaml`, al `Border` dell'header colonne (riga ~71, quello con `IsVisible="{Binding !NessunaSessione}"` e il `Grid ColumnDefinitions="Auto,180,*,90,90,90,160"`) aggiungere un nome `x:Name="HeaderColonne"`. Verrà nascosto su touch dal code-behind (l'header tabellare non ha senso sulle card).

- [ ] **Step 2: Aggiungere la lista a card (touch), accanto alla tabella**

Dentro il `DockPanel` della lista (`IsVisible="{Binding !ModoDettaglio}"`), accanto allo `ScrollViewer` esistente con `ListaSessioni`, aggiungere un secondo `ScrollViewer` con `x:Name="ScrollCard"` e `IsVisible="False"` (verrà acceso su touch). Contiene una `ListBox x:Name="ListaSessioniCard"` con un `DataTemplate` a card modellato su SospesiView. Lo `ScrollViewer` tabellare esistente prende `x:Name="ScrollTabella"`. Template card:
```xml
<ScrollViewer x:Name="ScrollCard"
              Margin="20,0,20,0"
              VerticalScrollBarVisibility="Auto"
              IsVisible="False">
    <ListBox x:Name="ListaSessioniCard"
             ItemsSource="{Binding Sessioni}"
             SelectionMode="Single"
             DoubleTapped="OnSessioneDoubleTapped"
             Background="Transparent"
             BorderThickness="0">
        <ListBox.ItemTemplate>
            <DataTemplate DataType="state:RisultatoCronologiaItem">
                <Border Background="{DynamicResource SystemRegionBrush}"
                        BorderBrush="{DynamicResource SystemBaseLowColor}"
                        BorderThickness="1"
                        CornerRadius="6"
                        Padding="14,12"
                        Margin="0,0,0,8">
                    <Grid ColumnDefinitions="6,*" RowDefinitions="Auto,Auto,Auto">
                        <Border Grid.RowSpan="3" Width="6"
                                Background="{Binding ColorePercentuale}"
                                CornerRadius="3" Margin="0,0,10,0"/>

                        <!-- Riga 1: data + % -->
                        <Grid Grid.Column="1" Grid.Row="0" ColumnDefinitions="*,Auto">
                            <StackPanel Spacing="2">
                                <TextBlock Text="{Binding DataFormattata}"
                                           FontWeight="SemiBold"
                                           FontSize="{DynamicResource SzFontBody}"/>
                                <TextBlock Text="Abbandonato" FontSize="10" Foreground="#B85450"
                                           IsVisible="{Binding Abbandonato}"/>
                                <TextBlock Text="⏱ Tempo scaduto" FontSize="10" Foreground="#B8860B"
                                           IsVisible="{Binding TempoScaduto}"/>
                            </StackPanel>
                            <TextBlock Grid.Column="1"
                                       Text="{Binding PercentualeFormattata}"
                                       FontWeight="SemiBold"
                                       FontSize="{DynamicResource SzFontBody}"
                                       Foreground="{Binding ColorePercentuale}"
                                       VerticalAlignment="Center"/>
                        </Grid>

                        <!-- Riga 2: modalità + materia -->
                        <StackPanel Grid.Column="1" Grid.Row="1" Spacing="2" Margin="0,4,0,0">
                            <TextBlock Text="{Binding Modalita}" TextTrimming="CharacterEllipsis"/>
                            <TextBlock Text="{Binding MateriaEtichetta}" FontSize="11"
                                       Foreground="{DynamicResource SystemBaseMediumColor}"
                                       TextTrimming="CharacterEllipsis"/>
                        </StackPanel>

                        <!-- Riga 3: conteggio/durata + Elimina -->
                        <Grid Grid.Column="1" Grid.Row="2" ColumnDefinitions="*,Auto" Margin="0,6,0,0">
                            <StackPanel Orientation="Horizontal" Spacing="16" VerticalAlignment="Center">
                                <TextBlock Text="{Binding Conteggio}" FontSize="12"/>
                                <TextBlock Text="{Binding DurataFormattata}" FontSize="12"/>
                            </StackPanel>
                            <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="6"
                                        IsVisible="{Binding NonInAttesaConferma}">
                                <Button Content="Elimina" Click="OnEliminaClick"
                                        Classes="danger" Padding="{DynamicResource SzBtnPadding}"
                                        FontSize="11"/>
                            </StackPanel>
                            <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="6"
                                        IsVisible="{Binding InAttesaConfermaEliminazione}">
                                <Button Content="Si', elimina" Click="OnConfermaEliminaClick"
                                        Classes="danger" Padding="{DynamicResource SzBtnPadding}" FontSize="11"/>
                                <Button Content="Annulla" Click="OnAnnullaEliminaClick"
                                        Padding="{DynamicResource SzBtnPadding}" FontSize="11"/>
                            </StackPanel>
                        </Grid>
                    </Grid>
                </Border>
            </DataTemplate>
        </ListBox.ItemTemplate>
    </ListBox>
</ScrollViewer>
```

- [ ] **Step 3: Toggle tabella/card su touch nel code-behind**

In `wsa.quiz.app/Views/CronologiaView.axaml.cs`, nel costruttore dopo `InitializeComponent()`:
```csharp
if (AppEnv.TouchMode)
{
    HeaderColonne.IsVisible = false;   // header tabellare non serve sulle card
    ScrollTabella.IsVisible = false;
    ScrollCard.IsVisible = true;
}
```
> Attenzione: lo `ScrollViewer` tabellare esistente ha già `IsVisible="{Binding !NessunaSessione}"`. Su touch lo nascondiamo a priori e mostriamo le card; ma anche le card devono rispettare lo stato vuoto. Soluzione semplice: legare `ScrollCard.IsVisible` allo stesso `!NessunaSessione` in XAML e nel code-behind, su touch, scambiare solo tabella↔card lasciando intatto lo stato vuoto. Concretamente: in XAML dare a `ScrollCard` `IsVisible="False"` (default desktop) e gestire l'accensione SOLO via code-behind quando `TouchMode && !NessunaSessione`. Poiché `NessunaSessione` può cambiare a runtime (Aggiorna/Svuota), aggiungere nel metodo che ricarica la lista (`Ricarica()`/handler) la riga: `if (AppEnv.TouchMode) ScrollCard.IsVisible = !«nessunaSessione»;`. Verificare il nome reale del flag/metodo in `CronologiaView.axaml.cs` prima di scrivere questa riga.

- [ ] **Step 4: Build + regressione desktop**

Run: `dotnet build Wsa.Quiz.sln`
Expected: verde.
Run: `dotnet run --project wsa.quiz.app`, tab Cronologia: mostra la **tabella come oggi** (TouchMode=false ⇒ card nascoste). Doppio click apre il dettaglio, Elimina/conferma funzionano. Chiudere.

- [ ] **Step 5: Smoke headless — su touch compaiono le card**

In `%TEMP%\m2verify\Program.cs`, con `AppEnv.TouchMode=true`, istanziare `CronologiaView`, e verificare `ScrollCard.IsVisible==true` quando ci sono sessioni (o, se servono dati, almeno che la tabella sia nascosta):
```csharp
Wsa.Quiz.App.AppEnv.TouchMode = true;
var crono = new Wsa.Quiz.App.Views.CronologiaView();
win.Content = crono;
Dispatcher.UIThread.RunJobs();
var tab = crono.FindControl<Control>("ScrollTabella")!;
Check(tab.IsVisible == false, "Cronologia touch: tabella nascosta");
win.Content = null;
```
> Verificare la firma del costruttore di `CronologiaView`: se richiede uno `StorageService`/dati, passare una fonte vuota o saltare lo smoke e affidarsi a build + device, annotandolo.
Run: `cd %TEMP%\m2verify && dotnet run -c Release`
Expected: `OK   Cronologia touch: tabella nascosta`.

- [ ] **Step 6: Commit**

```bash
git add wsa.quiz.app/Views/CronologiaView.axaml wsa.quiz.app/Views/CronologiaView.axaml.cs
git commit -m "feat(android): CronologiaView mostra card verticali su touch"
```

---

### Task 11: Aggiornare CLAUDE.md e memoria

**Files:**
- Modify: `CLAUDE.md` (roadmap M3 + Stato attuale)
- Modify (memoria, fuori repo): `…/memory/wsa-quiz-android-port.md`

- [ ] **Step 1: Roadmap M3 nel CLAUDE.md**

Aggiornare la sezione "🚧 Port Android — M3": marcare "layout touch-friendly" come fatto (con riferimento a questa spec/plan), elencando il meccanismo (`AppEnv.TouchMode` + dizionari + stili globali) e le schermate toccate. Lasciare aperti gli ultimi residui M3 (icona/splash) se non in scope.

- [ ] **Step 2: Stato attuale nel CLAUDE.md**

Aggiornare il paragrafo "Stato attuale" in cima per riflettere M3 layout touch completato (build verde + smoke; verifica device a carico di Luca).

- [ ] **Step 3: Memoria di progetto**

Aggiornare `…/memory/wsa-quiz-android-port.md`: M3 layout touch fatto, restano icona/splash + verifica su device.

- [ ] **Step 4: Commit (solo CLAUDE.md; docs/ e memoria sono gitignored/fuori repo)**

```bash
git add CLAUDE.md
git commit -m "docs: M3 layout touch-friendly completato (roadmap + stato)"
```

---

## Self-Review (esito)

- **Copertura spec**: meccanismo (Task 1-3) ✓; per-schermata QuizView (4), HomeView (5), RiepilogoView (6), MainView (7), SospesiView (8), StatisticheView (9), CronologiaView (10) ✓; verifica build+smoke+device nei checkpoint ✓; fasatura A/B ✓; aggiornamento docs (11) ✓.
- **Placeholder**: i punti "verificare la firma reale del costruttore/flag" in Task 5/10 sono rischi d'integrazione reali (le view sono code-behind con dipendenze non note a priori), non placeholder pigri: ognuno indica l'azione concreta e il fallback (saltare lo smoke, affidarsi a build+device).
- **Coerenza tipi/nomi**: chiavi risorsa (`Sz*`) definite in Task 2 e usate identiche in Task 4/6/8/9/10; `AppEnv.TouchMode` definito in Task 1 e consumato in 5/6/10; handler Cronologia riusati per nome dal XAML esistente.
- **Rischio principale**: risoluzione `DynamicResource` dopo merge a runtime (Task 2 Step 4) — con fallback "sostituisci invece di accodare" già scritto.
