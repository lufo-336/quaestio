# Bottom-nav a icone + swipe tra sezioni (touch) — Design

Data: 2026-06-25
Branch: `feature/android`
Contesto: M3 (UX mobile). Prosecuzione del ridimensionamento UI touch.

## Obiettivo

Su Android (touch) sostituire le etichette testuali della bottom navigation con
**sole icone** e permettere di **scorrere (swipe) orizzontalmente** tra le 4
sezioni (Home / Cronologia / Sospesi / Statistiche), con animazione di
scorrimento. Migliora compattezza della barra e facilità di navigazione (lo
swipe aggira la difficoltà di centrare i tap).

## Vincolo guida

Il **desktop resta identico**: continua a usare il `TabControl` con i tab in alto
e tutta la navigazione tastiera esistente (Ctrl+Tab, FocusAdorner sui TabItem,
IsTabStop, zone Tab). Tutto il nuovo comportamento è gated da
`AppEnv.TouchMode` e vive solo nel ramo touch.

## Decisioni (da brainstorming)

- Bottom-nav **solo icone** (niente testo).
- Icona attiva: **tinta accent** + piena opacità; inattive smorzate (~0.5).
- Swipe **flick + scorrimento**: drag oltre soglia → cambio sezione con
  `PageSlide`. **Niente wrap** (al bordo non succede nulla).
- Swipe **attivo anche durante il quiz** (coerente con Ctrl+Tab; il quiz resta
  vivo sotto). Soglia conservativa per evitare cambi accidentali.

## Architettura

### 1. Host dei contenuti: TabControl (desktop) + Carousel (touch)

Oggi la `MainView` ha un `TabControl` con 4 `TabItem`, ognuno con un
`ContentControl` (`HomeArea`, `CronologiaArea`, `SospesiArea`,
`StatisticheArea`). Il code-behind assegna le view a quei ContentControl e
swappa `HomeArea.Content` tra HomeView/QuizView/RiepilogoView.

Nuovo impianto:

- **XAML**: accanto al `TabControl` (visibile solo desktop) si aggiunge un
  **`Carousel`** (`x:Name="CarouselTouch"`, visibile solo touch) con
  `PageTransition` = `PageSlide` orizzontale. Il Carousel contiene 4
  `ContentControl` paralleli (`HomeAreaTouch`, `CronologiaAreaTouch`,
  `SospesiAreaTouch`, `StatisticheAreaTouch`).
- **Visibilità**: `TabControl.IsVisible = !TouchMode`, `CarouselTouch.IsVisible =
  TouchMode` (impostate in `ConfiguraNavigazione`).
- **De-duplicazione logica**: si introducono 4 campi
  `_homeArea/_cronologiaArea/_sospesiArea/_statisticheArea` (tipo
  `ContentControl`) inizializzati al set desktop o touch a seconda di
  `TouchMode`. Tutti i riferimenti esistenti a `HomeArea.Content = …` ecc.
  passano per questi campi. **La logica di navigazione/swap/Ricarica non
  cambia**, cambia solo quale ContentControl ospita.

Direzione dello slide: il `Carousel` con `PageSlide` gestisce automaticamente
avanti/indietro in base all'aumento/diminuzione di `SelectedIndex`.

Persistenza stato: le istanze delle view (incluso un quiz in corso dentro
`HomeAreaTouch`) restano gli item del Carousel, quindi sopravvivono al cambio
pagina come oggi sopravvivono al cambio tab.

Sincronizzazione indice: su touch la sorgente di verità dell'indice diventa
`CarouselTouch.SelectedIndex`. La bottom-nav e lo swipe lo impostano;
`AggiornaNav` evidenzia l'icona corrispondente. Il codice esistente che fa
`Tabs.SelectedIndex = 0` (es. `AvviaQuizView`) va instradato verso l'indice
dell'host attivo (un helper `IndiceSezione` get/set che agisce su Tabs o
Carousel secondo TouchMode).

### 2. Bottom-nav a sole icone

- I 4 `Button.nav` perdono il `TextBlock` testuale e ricevono un `Path`
  (geometria vettoriale monocroma) come contenuto: Home (casa), Cronologia
  (orologio), Sospesi (pausa ⏸), Statistiche (barre). Geometrie semplici
  hard-coded nello XAML.
- Stato attivo gestito da `AggiornaNav` (già esistente): icona attiva
  `Fill`/`Foreground` accent + `Opacity=1`; inattive `Opacity≈0.5`. Si passa da
  evidenziare il `FontWeight`/opacity del testo a evidenziare il `Path`.
- Altezza barra guidata dall'icona (es. ~22–24 px logici) + padding minimo +
  l'inset di sistema già gestito (`ApplicaSafeArea`). Risultato: barra bassa.

### 3. Swipe (rilevamento gesto)

- Handler pointer sul `CarouselTouch` (`PointerPressed`/`PointerMoved`/
  `PointerReleased`), con **soglia** (~60 px logici) e **dominanza orizzontale**
  (|dx| > |dy| e |dx| > soglia) per non rubare lo scroll verticale né i tap.
- Su rilascio oltre soglia: `IndiceSezione += (dx < 0 ? +1 : -1)`, clampato a
  `[0, 3]` (niente wrap). Sotto soglia: nessun cambio (il tap/scroll prosegue
  normale).
- Per non interferire con i figli interattivi (bottoni risposta, ScrollViewer),
  gli handler osservano senza consumare finché non si supera la soglia; si valuta
  `RoutingStrategies.Tunnel` + `handledEventsToo` se in test pratico i figli
  marcano l'evento (lezione Trappola 13). Verifica obbligatoria sul device che
  tap e scroll verticale restino fluidi.

## Fuori scope

- Segui-il-dito reale (pagina che si muove col dito in tempo reale).
- Swipe con wrap-around.
- Animazioni custom oltre il `PageSlide`.
- Modifiche al desktop.

## Verifica (su device fisico)

1. Bottom-nav mostra 4 icone; l'attiva è accent, le altre smorzate; barra bassa.
2. Tap su un'icona porta alla sezione (con slide).
3. Swipe sx/dx cambia sezione con slide, senza wrap ai bordi.
4. Scroll verticale (Home, liste) e tap (risposte quiz, checkbox) non vengono
   rubati dallo swipe.
5. Avvio quiz + swipe via e ritorno: il quiz è ancora vivo.
6. Desktop invariato (tab in alto, Ctrl+Tab, focus) — build desktop di controllo.

## Rischi

- Routing dei pointer per lo swipe vs figli interattivi (area delicata, cfr.
  Step 8 tastiera). Mitigazione: soglia conservativa + test sul device.
- `Carousel` che de-realizza gli item non selezionati: verificare che lo stato
  delle view (quiz in corso) persista (atteso: gli item persistono come oggetti).
