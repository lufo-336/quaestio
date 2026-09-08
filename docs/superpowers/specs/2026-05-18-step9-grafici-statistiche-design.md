# Step 9 — Tab Statistiche (mappa punti deboli)

> Data: 2026-05-18
> Stato: design approvato dall'utente, da pianificare con writing-plans.

## Obiettivo

Aggiungere una quarta tab "Statistiche" all'app Avalonia che mostri all'utente,
a colpo d'occhio, **dove deve studiare**. Visualizzazione principale: una
"heatmap" pedagogica — una riga per materia con celle colorate per categoria,
ordinate dalla più debole alla più forte. Click su una cella → lista delle
domande sbagliate di quella categoria.

La feature è puramente aggregativa sui dati già presenti in
`cronologia.json`: nessuna modifica al modello dati esistente, nessuna
migrazione, nessuna dipendenza grafica nuova.

## Non-obiettivi

- Grafici temporali (line chart andamento nel tempo): rinviati a un eventuale
  step successivo.
- Modalità "ripasso punti deboli" (rilanciare un quiz mirato sulle categorie
  più rosse): scartata storicamente, non in scope qui.
- Export delle statistiche: rientra in Step 11.
- Heatmap "matrice classica" materia × categoria-universale: scartata in
  brainstorming (categorie diverse per materia → matrice sparsa rumorosa).

## Scelte UX consolidate

Dal brainstorming del 2026-05-18:

1. **Insight primario**: mappa di calore punti deboli (per categoria).
2. **Layout**: una riga per materia, celle solo per le sue categorie (no
   matrice sparsa, no treemap). Niente LiveCharts2.
3. **Cell ordering**: per debolezza decrescente (più rosse a sinistra).
4. **Soglia minima dati**: nessuna soglia, mostra tutto da subito (anche
   categorie viste 1 sola volta).
5. **Definizione di "errore"**: `!(Corretta && Tentativi == 1)`. Unifica
   modalità classica (sbagliata = `Corretta==false`) e rotazione (sbagliata
   = `Tentativi > 1`).
6. **Filtro temporale**: toggle "Tutto" / "Ultime N partite", `N` impostato
   da `NumericUpDown` con default 30.
7. **Drill-down**: click su una cella → lista delle domande sbagliate di
   quella categoria (testo + risposta corretta + spiegazione).

## Architettura

### Progetti coinvolti

- **`Wsa.Quiz.Core`** (libreria): nuovo `Services/StatisticheService.cs` +
  i model record di aggregazione. Sta in Core perché la logica di
  aggregazione è pura e potrà servire anche alla CLI in futuro.
- **`Wsa.Quiz.App`** (Avalonia): nuovi `Views/StatisticheView.axaml(.cs)` e
  `Views/CategoriaDettaglioView.axaml(.cs)`. Modifica a
  `MainWindow.axaml(.cs)` per aggiungere la quarta tab e l'auto-refresh
  dopo conclusione quiz.

### Modello dati di aggregazione

Tutti `record` immutabili in `Wsa.Quiz.Core.Models.Statistiche` (sotto-namespace
nuovo per non inquinare i model esistenti):

```csharp
public record CategoriaStat(
    string Materia,
    string Categoria,
    int Visti,
    int Errori,
    double PctErrore,
    IReadOnlyList<DomandaSbagliata> Sbagliate);

public record DomandaSbagliata(
    string IdDomanda,
    string Testo,
    string RispostaCorretta,
    string Spiegazione,
    int VolteSbagliata,
    int VolteVista);

public record StatistichePerMateria(
    string Materia,
    IReadOnlyList<CategoriaStat> Categorie);
```

### `StatisticheService`

```csharp
namespace Wsa.Quiz.Core.Services;

public class StatisticheService
{
    public IReadOnlyList<StatistichePerMateria> Calcola(
        IEnumerable<RisultatoQuiz> sessioni,
        int? limiteUltimeN = null);
}
```

Comportamento:

1. Filtra `sessioni` su quelle non `Abbandonato` (i quiz abbandonati possono
   contenere `Dettagli` parziali; per coerenza con la "% al primo colpo" li
   includiamo comunque: un'abbandono dopo 5 domande è ancora informativo
   sulle 5 viste. **Decisione**: includiamo gli abbandonati. Se questa scelta
   produce numeri strani si rivedrà.)
2. Se `limiteUltimeN != null`, ordina per `DataOra` decrescente e tiene le
   prime `limiteUltimeN` sessioni.
3. Itera tutti i `DettaglioRisposta` di tutte le sessioni filtrate.
4. Raggruppa per `(MateriaNome, Categoria)`. Per ogni gruppo:
   - `Visti` = numero di dettagli nel gruppo
   - `Errori` = numero di dettagli con `!(d.Corretta && d.Tentativi == 1)`
   - `PctErrore` = `Errori / Visti` (0..1, formattato come % in UI)
   - `Sbagliate` = sotto-aggregazione per `IdDomanda` dei soli dettagli
     errati, dove per ogni id si conta `VolteSbagliata`/`VolteVista` e si
     prende `Testo`/`RispostaCorretta`/`Spiegazione` dall'ultimo dettaglio
     visto (i metadati testuali sono ridondanti fra ripetizioni; l'ultimo è
     una scelta arbitraria stabile).
5. Raggruppa per `Materia` in ordine alfabetico crescente (stabile fra
   chiamate).
6. All'interno di ogni materia, ordina `Categorie` per `PctErrore`
   decrescente. A parità di percentuale, ordine alfabetico di `Categoria`.

Caso limite: `sessioni` vuoto (o filtrato a vuoto) → ritorna lista vuota.
Le view gestiscono l'empty state.

## UI: `StatisticheView`

### Layout

```
┌──────────────────────────────────────────────────────────────────┐
│ Statistiche                                                       │
│ ○ Tutto   ● Ultime N: [30 ▴▾]                       [Aggiorna]    │
│ Categorie ordinate dalle più deboli alle più forti.              │
│──────────────────────────────────────────────────────────────────│
│ cs        [POO 80%][generics 78%][LINQ 45%][async 10%]            │
│ cpp       [template 70%][ptr 65%][stl 55%]                        │
│ cybersec  [social 50%][owasp 35%][crypto 20%][net 8%]             │
│ frontend  [react 55%][js 35%][css 18%][html 10%]                  │
│ infra     [k8s 75%][docker 30%][linux 20%]                        │
│ sql       [join 60%][index 50%][select 20%][tx 5%]                │
└──────────────────────────────────────────────────────────────────┘
```

### Struttura XAML

- Root `UserControl` con `DataContext = this`, INPC come gli altri view.
- Header sticky in alto (`DockPanel.Dock="Top"`):
  - Titolo `TextBlock`
  - `StackPanel Orientation="Horizontal"`: due `RadioButton` (GroupName
    condivisa: "FiltroPeriodo") "Tutto" / "Ultime N", + `NumericUpDown`
    associato a "Ultime N" (disabilitato quando "Tutto" è attivo). Default
    "Ultime N" selezionato con `N=30`.
  - Bottone "Aggiorna" (riusa style esistente, no `Classes="accent"` per
    rimanere neutro come Cronologia).
  - Sottotitolo descrittivo statico.
- Body: `ScrollViewer` → `ItemsControl` su `IReadOnlyList<StatistichePerMateria>`.
- Item template materia: `Grid` con due colonne, la prima fissa (~120px)
  contiene il nome materia, la seconda contiene un `WrapPanel` di celle
  categoria (wrap a capo se necessario).
- Item template categoria: `Border`
  - `BorderThickness="1"`
  - `BorderBrush` e `Background` derivati da `PctErrore` (vedi sotto)
  - `Padding="6,3"`, `Margin="2"`, `CornerRadius="2"`
  - `Cursor="Hand"`
  - Contenuto: `TextBlock` con "categoria NN%" (formattazione "{0} {1:P0}")
  - `PointerPressed` → handler in code-behind che ricava `CategoriaStat` dal
    `DataContext` e invoca un evento `CategoriaSelezionata`.

### Colori delle celle

Tre fasce, allineate con `QuizColors`:

- `PctErrore < 0.20` → verde (BorderBrush `#1F7A4D`, Background `#E5F4EC`)
- `0.20 ≤ PctErrore < 0.60` → ambra (`#B8860B`, `#FFF4D6`)
- `PctErrore ≥ 0.60` → rosso (`#B85450`, `#F9E7E6`)

Le tre soglie e i tre brush vivono come metodo helper in `QuizColors.cs`
(`QuizColors.HeatmapBackground(double pctErrore)` e `HeatmapBorder(...)`),
non hardcoded nello XAML, per coerenza col resto del progetto.

### Empty state

Quando il servizio ritorna lista vuota:
- Se `cronologia.json` è vuoto in assoluto: messaggio "Nessun quiz
  completato. Fai un quiz dalla Home per vedere statistiche."
- Se il filtro "Ultime N" ha azzerato tutto ma cronologia non è vuota:
  "Nessuna sessione nelle ultime N partite. Aumenta il limite o passa a
  'Tutto'."

Un singolo `TextBlock` centrato con due `IsVisible` mutualmente esclusivi
gestiti da due bool observable (`NessunDato` e `FiltroTroppoStretto`).

### Refresh

- `Inizializza(StorageService)` chiamata dalla `MainWindow` al boot.
- `Ricarica()` pubblica: rilegge cronologia da disco, chiama
  `StatisticheService.Calcola` con il filtro corrente, popola la
  `ObservableCollection`. Chiamata automaticamente:
  - al boot, da `Inizializza`
  - quando l'utente cambia il RadioButton o `NumericUpDown.Value`
    (handler `ValueChanged` + `Checked`)
  - al click "Aggiorna"
  - dalla `MainWindow.OnQuizConcluso` (come già fa con `_cronologiaView`)

Dataset piccolo (qualche centinaio di dettagli al massimo), nessun debounce
sull'aggiornamento del NumericUpDown.

## UI: `CategoriaDettaglioView`

### Layout

```
┌──────────────────────────────────────────────────────────────────┐
│ ← Statistiche    cs / generics — 9 errori su 11 viste (82%)      │
│──────────────────────────────────────────────────────────────────│
│ ┌────────────────────────────────────────────────────────────┐    │
│ │ ❌ sbagliata 3/4 volte                                     │    │
│ │ Qual è la differenza tra IEnumerable<T> e IList<T>?       │    │
│ │ ✓ corretta: IList consente accesso indicizzato...         │    │
│ │ Spiegazione: ...                                           │    │
│ └────────────────────────────────────────────────────────────┘    │
│ ┌────────────────────────────────────────────────────────────┐    │
│ │ ❌ sbagliata 2/3 volte                                     │    │
│ │ ...                                                         │    │
│ └────────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────┘
```

### Struttura

- Header con bottone "← Statistiche" (handler `TornaIndietro`) e titolo
  generato (`"{Materia} / {Categoria} — {Errori} errori su {Visti} viste ({PctErrore:P0})"`).
- Body: `ScrollViewer` → `ItemsControl` (non `ListBox`, per evitare i
  problemi di selezione/Enter già noti dallo Step 8) sulla lista
  `Sbagliate`, ordinata `VolteSbagliata` desc poi alfabetico per `Testo`.
- Item template: `Border` rosso chiaro (`BorderBrush=#B85450`,
  `Background=#F9E7E6`, `BorderThickness=1`, `CornerRadius=2`,
  `Padding=10`, `Margin=0,4`):
  - "❌ sbagliata X/Y volte" in alto (font semibold, colore `#B85450`)
  - Testo domanda (wrap, font normale)
  - "✓ corretta: ..." (verde `#1F7A4D`)
  - "Spiegazione: ..." se non vuota (grigio `#5A5A5A`, italic)

### Comportamento

- Riceve un `CategoriaStat` via metodo `Mostra(CategoriaStat)`.
- Espone evento `TornaIndietroRichiesto` per la `StatisticheView`.
- Non rilegge dal disco: i dati arrivano già aggregati dal servizio.
- `Esc` → tentativo "← Statistiche". Si applica lo stesso pattern Tunnel
  + `handledEventsToo:true` usato in `CronologiaDettaglioView`, accettando
  l'incertezza dello Step 8: se non funziona, il click sul bottone è
  comunque sempre disponibile.

## Integrazione con `MainWindow`

Seguiamo lo stesso pattern di `CronologiaView`: lo **swap heatmap↔dettaglio
è interno alla `StatisticheView`** (un flag observable `ModoDettaglio` + un
`CategoriaDettaglioView` ospitato come figlio nello XAML, mostrato via
`IsVisible` o `ContentControl` interno). Niente swap pilotato dalla
`MainWindow`.

Modifiche a `MainWindow.axaml`:
- Aggiunge un quarto `<TabItem Header="Statistiche">` con dentro
  `<ContentControl x:Name="StatisticheArea" />` (gemello di
  `HomeArea`/`CronologiaArea`/`SospesiArea`).

Modifiche a `MainWindow.axaml.cs`:
- Campo `private StatisticheView? _statisticheView`.
- Nel metodo `Caricamento()` (boot, dopo `_sospesiView`): istanzia
  `_statisticheView = new StatisticheView();`,
  `_statisticheView.Inizializza(_storage);`,
  `StatisticheArea.Content = _statisticheView;`.
- In `OnQuizConcluso`: aggiungere `_statisticheView?.Ricarica()` accanto a
  `_cronologiaView?.Ricarica()`.

Nota staleness drill-down: se l'utente è dentro il drill-down quando un
nuovo quiz viene concluso, `Ricarica()` aggiorna lo stato della heatmap in
memoria ma il pannello di dettaglio mostra ancora il `CategoriaStat`
catturato al momento del click. Quando l'utente torna alla heatmap ("←
Statistiche") vedrà i numeri aggiornati. Comportamento accettabile,
documentato qui per non sorprendere.

## Error handling

- Letture cronologia delegate a `StorageService.CaricaCronologia()` che già
  gestisce errori IO/JSON con catch+default vuoto.
- Il `StatisticheService` non fa I/O, è una funzione pura su `IEnumerable`.
  Non lancia eccezioni per dati vuoti — ritorna lista vuota.
- Se per qualche motivo `MateriaNome` o `Categoria` sono stringhe vuote in
  un `DettaglioRisposta` (record vecchi pre-step 7?), il `GroupBy` li
  raggruppa comunque sotto chiave `("", "")`; in heatmap apparirebbe una
  riga "[vuoto]". Improbabile in pratica; se succede si interviene allora.

## Testing

Il progetto non ha test automatici. Verifica manuale:

1. **Cronologia vuota** → tab Statistiche mostra empty state corretto.
2. **Cronologia piena** → heatmap popolata, materie in ordine alfabetico,
   celle ordinate per %errore decrescente.
3. **Soglie colori** → categoria con 10% errori = verde, 40% = ambra,
   80% = rosso.
4. **Click su una cella** → drill-down apre la giusta categoria, con
   numeri coerenti rispetto a quelli mostrati nella cella.
5. **Drill-down** → mostra solo le domande sbagliate almeno una volta in
   quella categoria, ordinate per #errori.
6. **Toggle Tutto / Ultime N** → cambiando N i numeri cambiano coerentemente.
7. **Filtro che azzera tutto** → empty state "filtro troppo stretto".
8. **Quiz nuovo mentre la tab è aperta** → fine quiz → tornando alla tab
   i numeri sono aggiornati senza dover cliccare "Aggiorna".
9. **Shared storage console/GUI** → un quiz fatto dalla console appare
   nei numeri della GUI dopo `Aggiorna` o riavvio.
10. **`Esc` su drill-down** → torna alla heatmap (con la consapevolezza
    che potrebbe fallire come negli altri view, vedi Step 8).

## Implicazioni e note

- **Niente LiveCharts2**: confermato. L'eventuale Step 9-bis (line chart
  andamento nel tempo) lo introdurrà in modo isolato, senza toccare questa
  tab.
- **`docs/` è in `.gitignore`**: questa spec è locale; per condividerla con
  un futuro collaboratore va o committata manualmente con `git add -f`, o
  rimossa la riga `docs/` dal `.gitignore`. Decisione rimandata.
- **Tastiera**: lo Step 8 resta aperto. Questa feature non aggiunge nuovi
  tentativi di fix né nuova superficie di problemi (usiamo `ItemsControl`
  invece di `ListBox` nel drill-down proprio per evitare il pattern del
  ListBoxItem che intrappola `Enter`).
- **Aggiornamento CLAUDE.md**: a feature completa, la roadmap passa
  Step 9 da ⏳ a ✅ e va riformulata (non è più "grafici LiveCharts2" ma
  "mappa punti deboli").
