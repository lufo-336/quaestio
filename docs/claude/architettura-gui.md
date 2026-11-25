# Architettura GUI, decisioni UX, note per i prossimi step

<!-- Spostato da CLAUDE.md il 2026-11-05 per alleggerire il contesto: testo invariato. -->

## Architettura GUI

**Code-behind + INPC, no MVVM.** Le UserControl sono il proprio DataContext.
Le proprietà notificano via `SetField` definito in `ObservableObject`. Il
`DataContext` è assegnato **una sola volta nel costruttore** di ogni view e
mai resettato (anti-pattern dello storico WPF — Trappola 3).

### `SessioneQuiz` — state machine

Rimpiazza i loop bloccanti del console:

| Console (loop in `Program.EseguiSessione*`) | GUI (state machine) |
|---|---|
| `for ... ChiediRisposta` | `Avvia()` → `CaricaProssimaDomanda()` |
| input utente | `RispondiA(int)` da click |
| `MostraFeedback` + ENTER | `Avanza()` da click "Prossima" |
| `break` (-1 abbandona) | `Abbandona()` da bottone |
| return RisultatoQuiz | evento `Concluso` |

L'algoritmo di rotazione (re-coda casuale delle sbagliate, conteggio tentativi,
% basata sul primo colpo) è 1:1 con quello del console. Il punteggio usa
`QuizService.CalcolaPunteggio` esistente.

### Pausa e ripresa

`SessioneQuiz` espone:
- `EsportaPausa()` — produce un `SessionePausa` consistente sia in stato "in
  attesa" sia "feedback pendente" (discriminante: `RispostaInviata`). In
  rotazione + in attesa, la domanda corrente viene rimessa in testa alla
  coda; in classica + in attesa, `EffettuateClassica = _indiceClassica +
  _offsetEffettuate`; in classica + post-feedback è `+ 1`. In view-mode
  chiama internamente `TornaACorrente()` come prima cosa per non catturare
  lo stato view-mode invece del live.
- `static RiprendiDa(SessionePausa, IDictionary<string,Domanda>)` — factory.
  Lancia `InvalidOperationException` se nessuno dei `CodaDomandeIds` è più
  presente nel pool (pausa orfana). La `MainWindow` cattura, mostra errore
  in `ErrorePanel`, ed elimina la pausa.
- `IdSessionePausa` — proprietà pubblica read-only: `_sessioneId` se
  `_avviataDaRipresa==true`, altrimenti `null`. Serve alla `MainWindow` per
  chiamare `EliminaPausa` al termine di una sessione ripresa.

Stato interno aggiunto: `_sessioneId`, `_avviataDaRipresa`, `_offsetCronometro`
(sommato ovunque si usi `_cron.Elapsed`), `_offsetEffettuate` (classica).

A fine sessione (anche per abbandono), `MainWindow.OnQuizConcluso` chiama
`_storage.EliminaPausa(sessione.IdSessionePausa)` se non null.

### Navigazione fra domande passate (view-mode)

Stato in `SessioneQuiz`: `_viewIndex` (`null`=corrente, altrimenti indice in
`Risultato.Dettagli`) + `_indiceHighlight`. Implementazione **Opzione A**:
riusa le proprietà observable correnti con backup/restore via `_StatoLive`
nei metodi privati `SalvaStatoLive()` / `RipristinaStatoLive()`. Ogni nuovo
campo observable di sessione live va aggiunto in entrambi i metodi.

`RispostaItem` ha 2 flag (`IsEnabled`, `IsHighlighted`) + computed
`PuoCliccare = IsEnabled && IsNeutra` che sostituisce `IsNeutra` nel binding
`Button.IsEnabled`. Necessario per disabilitare i bottoni in view-mode senza
cambiare il template XAML.

### Navigazione fra schermate

La `MainWindow` ha tre tab. Dentro la tab Home c'è un `ContentControl` il cui
`Content` viene swappato fra `HomeView`, `QuizView`, `RiepilogoView`. La tab
Cronologia ha `CronologiaView` con swap interno fra lista e
`CronologiaDettaglioView`. La tab Sospesi ha `SospesiView`.

`Ctrl+Tab` / `Ctrl+Shift+Tab` (gestiti in `MainWindow.OnKeyDown`) ciclano fra
le 3 tab principali con re-focus della view attiva.

### Style globali (App.axaml)

Due classi, `accent` (azzurro Fluent) e `danger` (rosso), ognuna con i 4
stati default/pointerover/pressed/disabled definiti sul
`ContentPresenter#PART_ContentPresenter` del template del Button. Risolve la
trappola del bottone Fluent disabled (vedi Trappola 6). Per usarle:
`<Button Classes="accent" Content="..." />` invece di `<Button Background="..." />`.

I valori del `:disabled` sono **espliciti in hex** (`#D6D6D6` su `#7A7A7A`),
non `DynamicResource Color`, per evitare problemi di conversione Color→Brush
nei `Setter` (Trappola 7).

`FocusAdorner` globale (bordo giallo `#FFD500` 3px che compare solo per focus
da tastiera, non da click) è applicato su Button/CheckBox/ListBoxItem/TabItem/
NumericUpDown.

### Colori condivisi — `QuizColors.cs`

Centralizzati nel C#:
- Verde `#1F7A4D`, Ambra `#B8860B`, Rosso `#B85450`
- Focus `#FFD500`, Disabilitato sfondo `#D6D6D6`, testo `#7A7A7A`
- API: `QuizColors.Percentuale(double pct)` (verde≥80, ambra≥50, rosso<50)
  e `QuizColors.Esito(bool corretto)`.

Nei `.axaml` gli stessi hex sono ancora hard-coded (XAML non legge facilmente
costanti C# senza risorse). La centralizzazione vera richiederebbe risorse
XAML — fuori scope finché non serve.

### Rendering del testo con codice (TestoConCodice)

Il controllo custom `TestoConCodice` (`wsa.quiz.app/Controls/TestoConCodice.cs`, che eredita
da `ContentControl`) si occupa del rendering di domande, risposte e spiegazioni riconoscendo
automaticamente codice inline e blocchi di codice. È interamente implementato in code-behind
(senza file XAML) e sostituisce i vecchi `TextBlock` semplici in tutte le viste dell'app
(`QuizView`, `RiepilogoView`, `CronologiaDettaglioView`, `StatisticheView`).

Funzionamento:
- Utilizza il parser condiviso in `Wsa.Quiz.Core.Services.TestoConCodice.Analizza(testo)`
  per suddividere la stringa in segmenti (`Testo`, `Inline`, `Blocco`).
- **Blocchi di codice multiriga**: vengono renderizzati all'interno di un `Border` con
  bordo arrotondato, padding e uno `ScrollViewer` configurato con scroll **esclusivamente orizzontale**
  (`VerticalScrollBarVisibility = Disabled`). Questo garantisce che il riquadro non intercetti lo
  scroll verticale della pagina, prevenendo anomalie di scrolling annidato (Trappola 11). All'interno
  del blocco, una `Grid` a due colonne affianca i numeri di riga a sinistra (allineati a destra) e il
  codice a destra (`TextWrapping = NoWrap`).
- **Codice inline**: viene inserito come `Run` con font ridotto al 90% e background semitrasparente
  (`ChipSfondo`), integrato negli inlines di un `TextBlock` wrapped.
- **Supporto dinamico al cambio tema**: per evitare le limitazioni dei Setter cromatici (Trappola 7),
  i controlli collegano i brush mediante `GetResourceObservable(chiave)` (`LegaRisorsa`),
  aggiornandosi reattivamente quando l'utente alterna tema chiaro e tema scuro.

#### Risorse grafiche per il codice (`App.axaml`)

- **`FontCodice`**: `avares://Wsa.Quiz.App/Assets/Fonts#JetBrains Mono NL`. Font a larghezza fissa
  incorporato come risorsa dell'app per assicurare un rendering identico su tutte le piattaforme
  (inclusa WebAssembly, che non dispone di font monospazio di sistema affidabili). La variante **NL**
  ("No Ligatures") è essenziale per il codice didattico: impedisce che operatori come `!=`, `==`, `>=`
  o `=>` vengano uniti in legature tipografiche (es. `≠`), preservando i caratteri esatti del codice.
- **ThemeDictionaries (`Light` e `Dark`)**:
  - `CodiceSfondo`: sfondo del riquadro (`#F3F4F6` Light / `#1E1F24` Dark).
  - `CodiceBordo`: bordo sottile del riquadro (`#DADDE2` Light / `#2E3038` Dark).
  - `CodiceTesto`: colore del testo del codice nel blocco (`#1F1F1F` Light / `#E6E6E6` Dark).
  - `CodiceNumeri`: colore attenuato dei numeri di riga (`#8A8F99` Light / `#6C7080` Dark).
  - `ChipSfondo`: sfondo semitrasparente del codice inline (`#1A000000` Light / `#26FFFFFF` Dark).
    Essendo semitrasparente (nero o bianco con alpha), si fonde con lo sfondo sottostante ed è
    perfettamente leggibile sia sul fondo pagina, sia sui bottoni di risposta colorati di verde o rosso
    nella fase di feedback (Trappola 12).

## Decisioni UX consolidate

- **Home unificata** ("Caso A"): materie a sinistra (checkbox), categorie
  filtrate dinamicamente a destra in base alle materie selezionate, opzioni
  in basso (rotazione, cronometro, randomizza, limita a N). Una sola
  schermata copre Quizzone (più materie spuntate), quiz singola materia, per
  categorie, di N domande.
- **Home — barra "Avvia" sticky in alto** (DockPanel.Dock="Top"); resto
  della pagina è una colonna unica scrollabile (Materie, Categorie, Opzioni).
  Solo Categorie ha scroll interno con `MaxHeight=240` (Materie ha 5 voci,
  non serve cap). Niente `BoxShadow` (Trappola 10).
- **Multi-materia con categorie**: chiave `"NomeMateria/Categoria"`. Categorie
  con stesso nome su materie diverse non si confondono.
- **Feedback risposta**: il bottone cliccato si colora rosso/verde, e quello
  con la risposta giusta si colora sempre verde. Pannello con titolo + spiegazione,
  bottone "Prossima domanda".
- **Anti doppio-click**: appena risposto, tutti i bottoni risposta vengono
  disabilitati (`IsEnabled = PuoCliccare`).
- **Cronologia con striscia colorata** a sinistra di ogni riga. Doppio click
  apre il dettaglio domanda-per-domanda; bottone "← Cronologia" per tornare.
  Cronologia auto-refresh dopo un quiz GUI (`MainWindow.OnQuizConcluso`
  chiama `_cronologiaView?.Ricarica()`).
- **Dettaglio cronologia**: bordo verde sulle corrette, rosso sulle sbagliate.
  Per le sbagliate mostra anche la risposta corretta. Per le rotazioni mostra
  "Risposta corretta dopo N tentativi" se >1.
- **Eliminazione cronologia** (step 5):
  - Bottone "Elimina" inline su ogni riga, conferma in-place (max una alla volta).
  - "Svuota cronologia" nell'header con dialog modale (pattern
    `ChiediConfermaAbbandono`, ESC=Annulla, bottone "Sì, cancella tutto"
    `Classes="danger"`, disabilitato quando la lista è vuota).
  - "Elimina questa partita" nel `CronologiaDettaglioView` con conferma
    inline ed evento `EliminazioneRichiesta` che la `CronologiaView` gestisce
    eseguendo l'eliminazione, chiudendo il dettaglio e ricaricando.
- **Conferma Elimina inline (no dialog modali)** sui sospesi: primo click →
  riga mostra "Sicuro? [Sì, elimina] [Annulla]"; secondo conferma. In Avalonia
  12 i `MessageBox.Show` non esistono nativamente.
- **Pausa unificata** (step 6): bottone in alto a destra del Quiz si chiama
  "**Pausa**". Stesso bottone e `ESC` aprono la stessa modale. Tre voci:
  Annulla / Abbandona (`danger`, a sinistra) / Salva e esci (`accent`, a destra).
  Modale 480×210, CenterOwner, no resize, no taskbar. Annulla è il focus di
  default. ESC dentro la modale = Annulla.
- **Tastiera nel quiz** (step 4 + 7):
  - `A/B/C/D` → risponde direttamente (solo se `InAttesaRisposta`).
  - `↑/↓` → highlight su una risposta in attesa (giallo `#FFD500` 3px;
    iterazione UX rispetto alla spec originale che usava bordo accent 2px,
    poco visibile in test pratico).
  - `Invio` → conferma highlight (se presente) o avanza (in feedback).
  - `←/→` → scorre passate già date (banner crema `#FFF8E1` + bottone
    "Torna alla corrente"; i 4 bottoni A/B/C/D ricostruiti con colorazione
    verde/rosso/neutra e `IsEnabled=false`).
  - `Esc` → modale pausa (anche in view-mode).
  - Override `OnKeyDown` con `Focusable=true` e `this.Focus()` in
    `OnAttachedToVisualTree`. Tutti i branch settano `e.Handled = true`.
- **Focus dopo click mouse**: in `QuizView.OnRispostaClick` /
  `OnAvanzaClick` / `OnTornaACorrenteClick` chiama esplicitamente `Focus()`,
  altrimenti dopo un click del mouse il focus resta sul bottone che diventa
  invisibile/disabilitato e i tasti non arrivano più al `OnKeyDown` del Quiz.
- **Modalità "ripasso punti deboli"** (categorie ≥60% errore): **scartata**.

## Note per i prossimi step

### Cronologia + Sospesi
- I wrapper observable in `State/` (`RisultatoCronologiaItem`,
  `SessioneSospesaItem`, `DettaglioRispostaItem`) tengono i colori e la
  formattazione fuori dalla XAML.
- `CronologiaView.Ricarica()` è idempotente e thread-safe nel singolo
  dispatcher; viene chiamata al boot, al click di "Aggiorna", e dalla
  `MainWindow` dopo `SalvaRisultato`.
- `SessioneSospesaItem.InAttesaConfermaEliminazione` è observable; la
  SospesiView toggla questo flag per swappare i bottoni. `OnEliminaClick`
  azzera prima eventuali altre conferme aperte (al massimo una alla volta).
- L'`App.axaml` espone le classi `accent` e `danger`. Per usarle:
  `<Button Classes="accent" Content="..." />`.

### Storage atomico (post code-review PR #1)
- `StorageService` valida i path (anti directory-traversal) tramite
  `Path.GetFullPath` + check `StartsWith`.
- Scritture su file (`SalvaRisultato`, `SalvaPausa`, `SvuotaCronologia`,
  `EliminaRisultato`, `EliminaPausa`) usano `ScriviAtomico` (temp + Move
  con `overwrite:true`).
- Letture (`LeggiFileCondiviso`) usano `FileShare.Read` per consentire letture
  concorrenti.
- `JsonSerializerOptions` ha `MaxDepth=64`. Niente `UnsafeRelaxedJsonEscaping`.
- `Random.Shared` (.NET 6+) sostituisce le istanze statiche `Random` non
  thread-safe in `QuizService` e `SessioneQuiz`.

## Per ripartire

1. Leggi questa pagina (in particolare il riquadro in cima a "Stato attuale",
   che è il riassunto aggiornato).
2. Conferma lo stato: si lavora su **`main`** (nessun branch feature aperto —
   `feature/android` e `feature/preferenze-utente` sono già mersati e
   cancellati), repo GitHub `lufo-336/quaestio`.
3. Candidati per il prossimo step, in ordine di utilità. **La roadmap numerata è
   finita**: quel che resta sono verifiche e rifiniture.
   - **Giro di verifiche a mano sulla GUI desktop**: Step 8 (tastiera vera),
     Step 11 (filtri + export), Step 13 (preferenze), Step 14 (About, icona).
     Sono quattro step chiusi ma mai toccati con le mani. Su Fedora l'app parte,
     quindi si può fare senza Windows.
   - **Bug bottom-nav dopo cold start** (serve device Android collegato) —
     l'unico difetto che l'utente finale vede; sospetta radice `RenderScaling`
     1→3 al boot, cfr. Trappola 17. Nella stessa sessione col telefono si guarda
     anche lo **splash** (fatto il 09/09, mai visto in esecuzione).
   - **Verifiche a mano ancora scoperte**, in ordine di importanza: una
     **partita intera** (risposte, feedback, pausa/ripresa, navigazione fra le
     passate, modalità a tempo, riepilogo) — è il cuore dell'app e non è mai
     stata provata a mano in questa fase; poi **Gestione contenuti** (import,
     nascondi/ripristina sezione) e **Statistiche** (heatmap, drill-down).
   - **Debito minore**: le sezioni "Layout repo" e "Stack" parlano ancora di
     *tre* progetti (da M1 sono cinque); 5 warning di build innocui
     (`TextBox.Watermark` obsoleto, un evento mai usato in `RiepilogoView`,
     tre `AVLN3001`); il binario Mac va firmato **su** un Mac.
4. Nota PR #2 (Facundo Fanti, 14 mag 2026): refactor che ha estratto
   `TabellaViewBase<TItem>`, `PausaDialog`, `ConfermaDialog`, fixato il bug
   timestamp UTC tornando a `DateTime.Now` ovunque, e tentato (senza
   successo) di chiudere Step 8 con `handledEventsToo: true`.
