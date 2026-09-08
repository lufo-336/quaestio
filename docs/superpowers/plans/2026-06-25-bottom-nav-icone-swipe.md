# Bottom-nav a icone + swipe tra sezioni — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to
> implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Su touch, bottom navigation a sole icone + swipe orizzontale con
animazione slide per cambiare le 4 sezioni, lasciando il desktop invariato.

**Architecture:** Su touch il contenuto passa da `TabControl` a un `Carousel`
(`PageSlide`); il desktop tiene il `TabControl`. Le 4 aree contenuto sono
instradate via campi `_*Area` così la logica esistente non cambia. La bottom-nav
mostra `Path` invece di testo. Lo swipe è rilevato con pointer event + soglia.

**Tech Stack:** Avalonia 12, C# code-behind (no MVVM), .NET 10, Android head.

## Global Constraints

- Desktop **invariato**: `TabControl`, Ctrl+Tab, FocusAdorner/IsTabStop sui
  TabItem restano come oggi. Tutto il nuovo comportamento gated da
  `AppEnv.TouchMode`.
- Build di verifica: libreria `dotnet build wsa.quiz.app -c Debug` (errori) +
  APK `dotnet build wsa.quiz.android -c Release` → install via
  `adb install -r …-Signed.apk` → verifica visiva sul device.
- Nessuna unit test UI (gesture/visivo): la verifica è on-device + build desktop
  di non-regressione.
- Spec: `docs/superpowers/specs/2026-06-25-bottom-nav-icone-swipe-design.md`.

---

### Task 1: Bottom-nav a sole icone

**Files:**
- Modify: `wsa.quiz.app/MainView.axaml` (bottom nav: testo → `Path`)
- Modify: `wsa.quiz.app/MainView.axaml.cs` (`AggiornaNav`: evidenzia il `Path`)

**Interfaces:**
- Consumes: `AppEnv.TouchMode`, `Tabs.SelectedIndex` (ancora host attuale).
- Produces: 4 `Path` con `x:Name` (`NavHomeIcon`…`NavStatisticheIcon`);
  `AggiornaNav()` aggiorna `Fill`/`Opacity` delle icone.

- [ ] **Step 1: Sostituire i 4 `TextBlock` etichetta con `Path` icone**

In `MainView.axaml`, dentro ogni `Button.nav`, rimpiazzare il `TextBlock` con un
`Path` (geometrie monocrome, `Stretch="Uniform"`, ~22px). Esempi di geometria:

```xml
<!-- Home -->
<Path x:Name="NavHomeIcon" Width="22" Height="22" Stretch="Uniform"
      Fill="{DynamicResource SystemBaseHighColor}"
      Data="M12 3 2 12h3v8h6v-5h2v5h6v-8h3z"/>
<!-- Cronologia (orologio) -->
<Path x:Name="NavCronologiaIcon" Width="22" Height="22" Stretch="Uniform"
      Fill="{DynamicResource SystemBaseHighColor}"
      Data="M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20zm0 18a8 8 0 1 1 0-16 8 8 0 0 1 0 16zM12.5 7h-1.5v6l5.2 3.1.8-1.3-4.5-2.7z"/>
<!-- Sospesi (pausa) -->
<Path x:Name="NavSospesiIcon" Width="22" Height="22" Stretch="Uniform"
      Fill="{DynamicResource SystemBaseHighColor}"
      Data="M6 4h4v16H6zM14 4h4v16h-4z"/>
<!-- Statistiche (barre) -->
<Path x:Name="NavStatisticheIcon" Width="22" Height="22" Stretch="Uniform"
      Fill="{DynamicResource SystemBaseHighColor}"
      Data="M4 13h4v7H4zM10 4h4v16h-4zM16 9h4v11h-4z"/>
```

Mantenere `Button.nav` con `Padding="2,2"` e `MinHeight="0"` (altezza guidata
dall'icona). Rimuovere lo style font dei vecchi `TextBlock`.

- [ ] **Step 2: Aggiornare `AggiornaNav` per evidenziare le icone**

In `MainView.axaml.cs`, sostituire l'array di label con l'array di `Path`:

```csharp
private void AggiornaNav()
{
    if (!AppEnv.TouchMode) return;
    var icone = new[] { NavHomeIcon, NavCronologiaIcon, NavSospesiIcon, NavStatisticheIcon };
    var accent = (IBrush?)this.FindResource("SystemAccentColorBrush");
    var neutro = (IBrush?)this.FindResource("SystemBaseHighColor");
    for (int i = 0; i < icone.Length; i++)
    {
        bool attiva = i == IndiceSezione;       // IndiceSezione: vedi Task 2; per ora usare Tabs.SelectedIndex
        icone[i].Fill = attiva ? accent : neutro;
        icone[i].Opacity = attiva ? 1.0 : 0.5;
    }
}
```

(In questo task `IndiceSezione` non esiste ancora: usare `Tabs.SelectedIndex`.
Verrà sostituito nel Task 2.)

- [ ] **Step 3: Build libreria**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 4: Build APK + install + verifica device**

Build Release, `adb install -r`, lancia. Verifica: 4 icone in basso, l'attiva
in accent, le altre smorzate; tap su un'icona cambia sezione e aggiorna
l'evidenziazione. Barra bassa.

- [ ] **Step 5: Commit**

```bash
git add wsa.quiz.app/MainView.axaml wsa.quiz.app/MainView.axaml.cs
git commit -m "feat(android): bottom navigation a sole icone"
```

---

### Task 2: Host Carousel su touch + instradamento indice

**Files:**
- Modify: `wsa.quiz.app/MainView.axaml` (aggiungere `Carousel` touch; visibilità)
- Modify: `wsa.quiz.app/MainView.axaml.cs` (campi `_*Area`, `IndiceSezione`,
  riferimenti)

**Interfaces:**
- Consumes: `AppEnv.TouchMode`.
- Produces: campi `ContentControl _homeArea/_cronologiaArea/_sospesiArea/_statisticheArea`;
  proprietà privata `int IndiceSezione { get; set; }` che agisce su
  `Tabs.SelectedIndex` (desktop) o `CarouselTouch.SelectedIndex` (touch).

- [ ] **Step 1: Aggiungere il Carousel touch in XAML**

In `MainView.axaml`, dopo il `TabControl`, aggiungere (stessa cella del DockPanel
fill):

```xml
<Carousel x:Name="CarouselTouch" IsVisible="False">
    <Carousel.PageTransition>
        <PageSlide Duration="0:0:0.25" Orientation="Horizontal"/>
    </Carousel.PageTransition>
    <ContentControl x:Name="HomeAreaTouch"/>
    <ContentControl x:Name="CronologiaAreaTouch"/>
    <ContentControl x:Name="SospesiAreaTouch"/>
    <ContentControl x:Name="StatisticheAreaTouch"/>
</Carousel>
```

Il `TabControl` esistente resta; la sua visibilità sarà gestita in code-behind.

- [ ] **Step 2: Campi area + helper indice + selezione host**

In `MainView.axaml.cs` aggiungere i campi e l'helper, e popolarli in
`ConfiguraNavigazione`:

```csharp
private ContentControl _homeArea = null!, _cronologiaArea = null!,
                       _sospesiArea = null!, _statisticheArea = null!;

private int IndiceSezione
{
    get => AppEnv.TouchMode ? CarouselTouch.SelectedIndex : Tabs.SelectedIndex;
    set { if (AppEnv.TouchMode) CarouselTouch.SelectedIndex = value; else Tabs.SelectedIndex = value; }
}
```

In `ConfiguraNavigazione`, prima di usare le aree:

```csharp
if (AppEnv.TouchMode)
{
    _homeArea = HomeAreaTouch; _cronologiaArea = CronologiaAreaTouch;
    _sospesiArea = SospesiAreaTouch; _statisticheArea = StatisticheAreaTouch;
    Tabs.IsVisible = false; CarouselTouch.IsVisible = true;
    CarouselTouch.SelectionChanged += (_, _) => AggiornaNav();
    BottomNav.IsVisible = true; FooterBar.IsVisible = false;
    AggiornaNav();
}
else
{
    _homeArea = HomeArea; _cronologiaArea = CronologiaArea;
    _sospesiArea = SospesiArea; _statisticheArea = StatisticheArea;
    Tabs.SelectionChanged += (_, _) => AggiornaNav();
}
```

(Rimuovere il vecchio `Tabs.Classes.Add("touchnav")` poiché su touch il
TabControl non è più visibile; oppure lasciarlo innocuo. Preferito: rimuoverlo.)

- [ ] **Step 3: Instradare i riferimenti alle aree**

Sostituire in `Caricamento`, `AvviaQuizView`, `OnQuizConcluso`,
`RitornaAllaHome`: `HomeArea.Content` → `_homeArea.Content`,
`CronologiaArea.Content` → `_cronologiaArea.Content`, ecc. Sostituire
`Tabs.SelectedIndex = 0` in `AvviaQuizView` con `IndiceSezione = 0`. In
`OnKeyDown` (Ctrl+Tab) sostituire `Tabs.SelectedIndex`/`Tabs.ItemCount` con
`IndiceSezione`/4 e usare `_*Area.Content` per il re-focus.

- [ ] **Step 4: Build libreria**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 5: Build desktop + verifica non-regressione**

Run: `dotnet build "wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj" -c Debug` (o run)
Expected: tab in alto funzionanti, Ctrl+Tab ok (desktop usa ancora TabControl).

- [ ] **Step 6: Build APK + install + verifica device**

Verifica: tap sulle icone cambia sezione con **animazione di scorrimento**;
l'evidenziazione segue; quiz avviato → cambio sezione e ritorno → quiz vivo.

- [ ] **Step 7: Commit**

```bash
git add wsa.quiz.app/MainView.axaml wsa.quiz.app/MainView.axaml.cs
git commit -m "feat(android): Carousel con slide su touch, desktop invariato"
```

---

### Task 3: Swipe orizzontale per cambiare sezione

**Files:**
- Modify: `wsa.quiz.app/MainView.axaml.cs` (pointer handlers sul Carousel)

**Interfaces:**
- Consumes: `CarouselTouch`, `IndiceSezione` (Task 2).
- Produces: gesto swipe → `IndiceSezione ± 1` clampato `[0,3]`.

- [ ] **Step 1: Aggiungere il rilevamento swipe**

In `ConfiguraNavigazione` (ramo touch) agganciare gli handler tunnel sul
Carousel, e implementare:

```csharp
private Point _swipeStart;
private bool _swipeTracking;
private const double SogliaSwipe = 60; // px logici

// in ConfiguraNavigazione (touch):
CarouselTouch.AddHandler(PointerPressedEvent, OnSwipePressed, RoutingStrategies.Tunnel);
CarouselTouch.AddHandler(PointerReleasedEvent, OnSwipeReleased, RoutingStrategies.Tunnel);

private void OnSwipePressed(object? s, PointerPressedEventArgs e)
{
    _swipeStart = e.GetPosition(CarouselTouch);
    _swipeTracking = true;
}

private void OnSwipeReleased(object? s, PointerReleasedEventArgs e)
{
    if (!_swipeTracking) return;
    _swipeTracking = false;
    var d = e.GetPosition(CarouselTouch) - _swipeStart;
    if (Math.Abs(d.X) < SogliaSwipe || Math.Abs(d.X) <= Math.Abs(d.Y)) return; // non è uno swipe orizzontale
    int nuovo = IndiceSezione + (d.X < 0 ? 1 : -1);
    if (nuovo >= 0 && nuovo <= 3) IndiceSezione = nuovo; // niente wrap
}
```

(Tunnel + niente `e.Handled`: osserviamo senza rubare tap/scroll; il cambio
avviene solo al rilascio se il movimento orizzontale supera la soglia.)

- [ ] **Step 2: Build libreria**

Run: `dotnet build "wsa.quiz.app/Wsa.Quiz.App.csproj" -c Debug`
Expected: `Errori: 0`.

- [ ] **Step 3: Build APK + install + verifica device (cruciale)**

Verifica: swipe sx→sezione successiva, dx→precedente, con slide; nessun wrap ai
bordi; **scroll verticale (Home/liste) e tap (risposte quiz, checkbox) restano
fluidi**, niente cambi accidentali. Se i figli rubano il pointer, valutare
`handledEventsToo: true` sugli AddHandler (cfr. Trappola 13) e ri-testare.

- [ ] **Step 4: Commit**

```bash
git add wsa.quiz.app/MainView.axaml.cs
git commit -m "feat(android): swipe orizzontale per cambiare sezione"
```

---

## Self-Review

- **Spec coverage:** icone (Task 1), Carousel+slide+host+indice (Task 2), swipe
  (Task 3), desktop invariato (gating + build desktop Task 2 Step 5), quiz vivo
  (Task 2 Step 6). Tutto coperto.
- **Placeholder scan:** nessun TODO; geometrie icone e codice swipe espliciti.
- **Type consistency:** `IndiceSezione` definito in Task 2 e usato in Task 1
  (nota: Task 1 usa `Tabs.SelectedIndex` finché Task 2 non introduce
  `IndiceSezione`, poi `AggiornaNav` vi punta) e Task 3. `_*Area` coerenti.
