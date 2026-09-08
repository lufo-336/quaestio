# M3 — Layout touch-friendly (porting Android)

> Spec di design. Parte di **Port Android — M3** (UX mobile). La parte 1 di M3
> (dialoghi modali → overlay in-page) è già fatta e verificata; questa spec copre
> la seconda parte: rendere il layout usabile col dito su telefono **senza far
> regredire il desktop**, dato che la UI vive in una libreria condivisa
> (`Wsa.Quiz.App`) consumata da entrambi gli head.

## Obiettivo

Su Android tutte le schermate devono essere comode al tocco: target tap di
dimensione adeguata (~48px), testi e spaziature leggibili, e i layout pensati per
schermo largo (affiancamenti, tabelle) devono reflowire in verticale. Il desktop
(`Wsa.Quiz.Desktop`) deve restare **pixel-identico a oggi**.

Non in scope: dark mode (Step 10), icona/splash definitivi (resto di M3),
gesture avanzate (swipe), tablet/landscape ottimizzati. Target primario: **telefono
in verticale (portrait)**.

## Principio architetturale: un flag, risorse, stili globali

L'adattamento NON si fa con `#if ANDROID` sparsi né duplicando view. Si concentra
in tre punti, così la maggior parte dell'effetto è automatica e il desktop non si
tocca:

### 1. Flag di piattaforma — `AppEnv.TouchMode`

Nuova classe statica `Wsa.Quiz.App.AppEnv` con `public static bool TouchMode { get; set; }`.
Impostato una sola volta in `App.OnFrameworkInitializationCompleted`:
- ramo `IClassicDesktopStyleApplicationLifetime` (desktop) → `false`
- ramo `ISingleViewApplicationLifetime` (Android) → `true`

Unico punto di verità. Il flag è impostato **prima** che le view vengano
costruite (la `MainView` e il suo contenuto si creano dopo questo punto), così i
costruttori delle view possono leggerlo.

### 2. Dimensioni come risorse, non hard-coded — due `ResourceDictionary`

Due dizionari con le **stesse chiavi**, valori diversi:
- `Themes/Sizes.Desktop.axaml` — valori = comportamento attuale
- `Themes/Sizes.Touch.axaml` — valori maggiorati

Il dizionario desktop è incluso di default in `App.axaml`
(`Application.Resources.MergedDictionaries`). Su Android, in
`OnFrameworkInitializationCompleted` (ramo single-view), si **aggiunge** il
dizionario touch alle merged dictionaries: le chiavi omonime risolte via
`DynamicResource` prendono il valore touch.

Chiavi previste (nome → desktop → touch):

| Chiave | Tipo | Desktop | Touch | Uso |
|---|---|---|---|---|
| `SzTapMin` | double | 32 | 48 | `MinHeight` controlli interattivi |
| `SzFontBody` | double | 14 | 16 | testo corpo / risposte |
| `SzFontDomanda` | double | 16 | 19 | testo della domanda |
| `SzFontTitolo` | double | 22 | 24 | titoli schermata |
| `SzGap` | double | 14 | 18 | spacing fra elementi in lista |
| `SzAnswerPadding` | Thickness | 14,12 | 18,16 | padding bottoni risposta |
| `SzBtnPadding` | Thickness | 14,6 | 16,10 | padding bottoni azione |
| `SzPaginaMargin` | Thickness | 20 | 16 | margine esterno pagina |

In XAML i valori si usano via `{DynamicResource Sz...}`. (Valori indicativi: si
rifiniscono in implementazione, ma le chiavi e la struttura sono queste.)

### 3. Target tap grandi via stili globali

In `App.axaml` (`Application.Styles`) si aggiungono stili globali che impostano
`MinHeight="{DynamicResource SzTapMin}"` su `Button`, `CheckBox`, `NumericUpDown`,
`ListBoxItem`, e header `TabItem`. Su desktop `SzTapMin`=32 (≈ neutro, layout
invariato); su Android sale a 48 **ovunque automaticamente**. Questo, con le
risorse del punto 2 applicate ai font/padding nelle view, copre da solo la gran
parte del touch-friendly senza modifiche strutturali.

> Nota: gli stili globali esistenti (`Button.accent`, `Button.danger`,
> `FocusAdorner`, `TabItem IsTabStop=False`) restano invariati; si aggiungono
> solo i setter di `MinHeight`. Verificare che `MinHeight` non rompa
> l'allineamento dei bottoni inline piccoli sul desktop (resta 32 = come oggi).

## Modifiche strutturali per-schermata

Sizing (font/padding/min-height) si applica a tutte; sotto solo i punti
**strutturali** (layout che in verticale non basta ingrandire). Ogni reflow è
pilotato da `AppEnv.TouchMode`, gestito nel **code-behind** della view (le view
sono già code-behind + INPC, è il posto naturale e isolato) oppure via
`IsVisible` legato a una proprietà `TouchMode` esposta dalla view.

| Schermata | Intervento strutturale | Peso |
|---|---|---|
| **QuizView** | nessuno (solo sizing) | minimo |
| **SospesiView** | nessuno: già a card verticali (solo sizing bottoni) | minimo |
| **MainView** (shell) | header tab più alti/toccabili (sizing su `TabItem`) | piccolo |
| **HomeView** | Materie/Categorie: da Grid 2 colonne a impilamento verticale su touch | medio |
| **RiepilogoView** | le 5 metriche statistiche: da Grid 5 colonne a wrap su touch | medio |
| **StatisticheView** | header (Radio + NumericUpDown) va a capo; celle heatmap con padding maggiore | medio |
| **CronologiaView** | tabella a colonne fisse → secondo `DataTemplate` a **card** verticale per touch; header colonne nascosto su touch | grande |

### Dettaglio HomeView

Oggi: `Grid ColumnDefinitions="*,16,2*"` con Materie (col 0) e Categorie (col 2).
Touch: stessa coppia di `Border` impilata verticalmente (Materie sopra, Categorie
sotto). Implementazione: nel code-behind, se `TouchMode`, riconfigurare il
contenitore (passare a una sola colonna / due righe). Le righe opzioni con
`NumericUpDown Width=120` restano, ma vanno verificate a larghezza telefono: se
sforano, lasciare andare a capo l'etichetta o ridurre la `Width`.

### Dettaglio CronologiaView (il pezzo grande)

La lista usa oggi un `Grid` riga con colonne fisse (`6,180,*,90,90,90,160`) +
un header colonne separato. Non reflua. Su touch:
- nascondere l'header colonne (`IsVisible="{Binding !TouchMode}"` o simile);
- fornire un **secondo `DataTemplate`** a card verticale (data + %, modalità,
  conteggio/durata su righe, bottone Elimina/conferma sotto), modellato su quello
  già esistente in `SospesiView`;
- selezione del template per `TouchMode`: due `ListBox`/`ItemsControl` alternati
  via `IsVisible`, oppure un `DataTemplateSelector`. Si preferisce **due liste
  alternate** per chiarezza (no logica di selezione nascosta), accettando la
  duplicazione di markup come costo esplicito.

Il desktop continua a usare la tabella invariata.

## Fasatura

Copertura totale (come richiesto), ma consegnata in due fasi per de-rischiare:

- **Fase A — fondazione + flusso core**: `AppEnv`, i due `ResourceDictionary`,
  gli stili globali, e l'applicazione a **QuizView, HomeView, RiepilogoView,
  MainView**. Alla fine il ciclo "fai un quiz su telefono" è comodo.
- **Fase B — tab pesanti**: **CronologiaView** (card), **StatisticheView**,
  rifinitura **SospesiView**.

Le due fasi sono commit/PR separati; la Fase A è autoconsistente e verificabile da
sola.

## Verifica

- **Build verde** su tutti gli head: `dotnet build Wsa.Quiz.sln` (desktop/cli) e
  APK Release `dotnet build wsa.quiz.android -c Release`.
- **Regressione desktop**: con `TouchMode=false` (default desktop) il layout deve
  restare identico a prima — controllo visivo desktop + il fatto che i valori
  desktop dei dizionari = valori attuali.
- **Smoke headless touch**: estendere l'harness in `%TEMP%\m2verify` (lo stesso
  riparato per M3 parte 1) per impostare `AppEnv.TouchMode=true`, merge-are il
  dizionario touch, istanziare una view (es. QuizView con dati finti) e
  verificare che le risorse touch si risolvano (es. un `Button` ha
  `MinHeight==48`, un font corpo == 16). Driver UI: stesso pattern a `RunJobs()`
  con timeout già adottato.
- **Verifica reale**: Luca installa l'APK su **device fisico** (`adb install`) e
  controlla a mano i target tap e i reflow. Questa VM non esegue l'emulatore
  (no nested virtualization), quindi la verifica visiva su Android è solo su
  dispositivo.

## Trappole/rischi noti

- **`MinHeight` globale sul desktop**: deve restare 32 per non alterare i bottoni
  inline piccoli (Elimina/Annulla) già esistenti. Verificare.
- **Risoluzione `DynamicResource` dopo merge a runtime**: confermare in
  implementazione che aggiungere il dizionario touch alle merged dictionaries
  faccia vincere le chiavi omonime. Se l'ordine non bastasse, su Android si
  **sostituisce** il dizionario desktop invece di accodarlo.
- **`NumericUpDown` larghi a larghezza telefono** (Home, Statistiche): verificare
  che non sforino; eventualmente ridurre `Width` o mandare a capo l'etichetta.
- **Duplicazione template Cronologia**: costo accettato e localizzato; tenuto in
  un solo file, commentato, sul modello di SospesiView.
