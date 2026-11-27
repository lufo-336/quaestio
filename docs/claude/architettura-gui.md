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
`Content` viene swappato fra `HomeView`, `QuizView`, `LaboratorioView`, `RiepilogoView`. La tab
Cronologia ha `CronologiaView` con swap interno fra lista e
`CronologiaDettaglioView`. La tab Sospesi ha `SospesiView`.

`Ctrl+Tab` / `Ctrl+Shift+Tab` (gestiti in `MainWindow.OnKeyDown`) ciclano fra
le 3 tab principali con re-focus della view attiva.

### Laboratorio di codice (`SessioneLaboratorio`, `LaboratorioView`, interruttore globale e pause)

Formato delle schede, convenzioni e servizi (`LaboratorioService`) sono descritti in `docs/claude/contenuti-e-dati.md`.

#### `SessioneLaboratorio` (`wsa.quiz.app/State/SessioneLaboratorio.cs`)

Macchina a stati dedicata (eredita `ObservableObject`), separata da `SessioneQuiz`. Riceve la lista di coppie
`(SchedaLab, VarianteLab)` selezionata da `LaboratorioService.Prepara` (o `LaboratorioService.CoppieDaId` quando
si avvia da *Ripassa* in Statistiche), il nome del linguaggio e il livello; espone `LivelloCorrente`
(`SchedaCorrente?.Livello ?? Livello`) così ogni scheda usa le forme del proprio livello anche in sessioni miste.

- **Passaggi (`PassoLab`)**: per ogni scheda attraversa `PrevediOutput` → `TrovaBug` → `RompiFunzione` (fino a
  `Concluso`). Metodi di risposta: `RispondiOutput(righe)`, `TentaRigaBug(riga)` + `RispondiBugTipo(riga, tipo)`
  (livello `base`) o `RispondiBugCorrezione(riga, correzione)` (livello `media`), `RispondiBugReview(segni)`
  (livello `alta`), `RispondiInput(indice)`; `Avanti()` passa al passo o alla scheda successivi; `Abbandona()`
  chiude mantenendo i passi già svolti.
- **Seconda possibilità e review completa**:
  - Nel passo 2 di `base` e `media`, un primo click su una riga errata attiva `SecondaPossibilitaInCorso`,
    espone `IndizioCorrente` e salva `PrimaRigaErrata` (disabilitata al secondo tentativo). Se al secondo
    tentativo la riga è giusta e il tipo/correzione scelto è giusto, il dettaglio viene registrato con
    `Corretta = true` e `SecondoTentativo = true` (peso errore ½). Se la riga è giusta ma il tipo o la
    correzione sono sbagliati, il passo è errato senza secondo tentativo.
  - Nel passo 2 di `alta` (review completa su 2–3 difetti, nessuna seconda possibilità),
    `LaboratorioService.PunteggioReview` calcola `(+1 per riga+tipo giusti, +0,5 per riga giusta con tipo sbagliato, -1 per ogni riga sana segnata) / numeroDifetti`,
    limitato a `[0, 1]`. `LaboratorioService.EsitoReview` traduce `1` in corretto, `[0,5, 1)` in
    `Corretta = true, SecondoTentativo = true` (peso ½) e `< 0,5` in errato.
- **Ordine degli assegnamenti e `PropertyChanged`**: `LaboratorioView` si aggiorna in risposta a
  `PropertyChanged` su `IndiceScheda`, `SchedaCorrente`, `VarianteCorrente`, `Passo`, `RigaTrovata` e
  `InAttesaDiAvanti` (`PreparaSchedaSeNecessario` e `PreparaPassoCorrente`). In `SessioneLaboratorio`
  (`CaricaScheda`, `Avanti`, `Concludi`, `RiprendiDa`) gli assegnamenti che fanno scattare `PropertyChanged`
  sulle proprietà osservate dalla vista (`Passo`, `IndiceScheda`) **vanno eseguiti solo dopo** aver preparato
  tutto lo stato letto dalla vista (`ResetStatoPasso2()`, `UltimoEsitoCorretto`, `InAttesaDiAvanti`,
  `SchedaCorrente`, `VarianteCorrente`); invertire l'ordine fa sì che la vista ricostruisca il passo leggendo
  ancora `InAttesaDiAvanti = true` o la variante precedente (righe non cliccabili al passo 2, codice della
  scheda precedente al cambio scheda).
- **Esito e *Ripassa* da Statistiche**: a fine sessione `MainView.OnLaboratorioConcluso` salva `sessione.Risultato`
  in `cronologia.json` (se ha almeno un passo) e apre `RiepilogoView`. In `MainView.OnRipassaRichiesto`, se
  `MateriaCanon` termina con `LaboratorioService.SuffissoMateria` (`" · Laboratorio"`), gli id sbagliati vengono
  convertiti in coppie tramite `LaboratorioService.CoppieDaId(_schedeLab, req.IdDomandeSbagliate)` (al più una
  variante per scheda, scartando schede o varianti non più esistenti) e viene avviata una `SessioneLaboratorio`.

#### `LaboratorioView` (`wsa.quiz.app/Views/LaboratorioView.axaml(.cs)`, item in `State/LaboratorioItems.cs`)

Costruita come `QuizView` (riceve `SessioneLaboratorio` e `StorageService`, avvia la sessione in
`OnAttachedToVisualTree`, solleva `LaboratorioConcluso` e `LaboratorioMessoInPausa`).

- **Struttura**: in testa la barra dei 3 passaggi (`BarraPasso0/1/2Brush`), titolo della scheda, metadati
  (`"Scheda N di M · C# · Livello"`) e riquadro del requisito; nel corpo scorrevole il codice numerato in
  `FontCodice` (`JetBrains Mono NL`) dentro uno `ScrollViewer` con scroll solo orizzontale
  (`TextWrapping="NoWrap"`, così i numeri di riga restano sempre fedeli) e i pannelli del passo attivo
  (`MenuOutputItem` per il passo 1 base; `TesseraOutputItem` + `TesseraCompostaItem` per il passo 1 media/alta;
  `TipoDifettoItem`, `CorrezioneBugItem` o `SegnoBugItem` per il passo 2; `InputCandidatoItem` con confronto
  *atteso*/*effettivo* per il passo 3); in basso la barra azioni (*Conferma* / *Avanti* / *Fine*, *Pausa*, *Esci*).
- **Accorgimenti per il telefono (`AppEnv.TouchMode`)**:
  - Poiché su schermo stretto le righe di codice escono a destra (scroll orizzontale), un indicatore posto solo
    in fondo alla riga (come il badge `EtichettaSegno`) finisce fuori schermo: ciò che segnala una riga
    (`Button.rigaCodice.segnata`) sta quindi **a sinistra**, con una barra verticale di 6 px
    (`BorderThickness="6,0,0,1"`) e il numero di riga in grassetto colorati con `AccentoBrush`.
  - In `TouchMode` il requisito in testa (`RequisitoText`) è compresso a **2 righe** (`MaxLines = 2`,
    `TextTrimming.CharacterEllipsis`) per non sottrarre spazio verticale al codice, e si apre/chiude al tocco
    su `RequisitoBorder`.

#### Interruttore globale `Quiz | Laboratorio`

Un interruttore a due voci in cima a `HomeView`, `CronologiaView`, `StatisticheView` e `SospesiView` governa
l'intera app tramite `PreferenzeUtente.Laboratorio` (affiancato da `LabLivello`, `LabRipassa`, `LabSchede`):

- Ogni vista espone la proprietà `Laboratorio` e l'evento `LaboratorioCambiato`; `MainView.OnLaboratorioCambiato`
  propaga la scelta alle altre tre viste (protetta dal flag `_propagandoLaboratorio`) e salva `preferenze.json`
  una volta sola.
- **Home**: commuta fra i riquadri dei quiz e `ZonaLaboratorio` (*Linguaggio*, *Livello* Base/Media/Alta con
  conteggio schede, *Schede* nuove o da ripassare + numero di schede per sessione, riepilogo da
  `LaboratorioService.Conteggi`).
- **Cronologia**: imposta `FiltroCronologia.Laboratorio`, che filtra i record con
  `LaboratorioService.ERisultatoLab(r) == Laboratorio` **senza** entrare in `FiltroCronologia.Attivo` (non è un
  filtro del pannello e non accende l'indicatore "filtri attivi"), e popola la tendina delle materie solo dai
  record del modo selezionato.
- **Statistiche**: mostra solo le materie senza `SuffissoMateria` in modalità *Quiz*, e solo quelle con
  `SuffissoMateria` in modalità *Laboratorio*.
- **Sospesi**: mostra le pause di `quiz_in_pausa.json` con *Quiz* e quelle di `laboratorio_in_pausa.json` con
  *Laboratorio*.

#### Pause separate del Laboratorio (`PausaLaboratorio`)

- Persistite in `laboratorio_in_pausa.json` (`StorageService.CaricaPauseLab`, `SalvaPausaLab`, `EliminaPausaLab`),
  separato da `quiz_in_pausa.json` per non toccare le pause dei quiz né la CLI.
- **Granularità per scheda**: `SessioneLaboratorio.CreaPausa()` salva in `PausaLaboratorio` (`SessioneId`,
  `DataOraPausa`, `Linguaggio`, `Livello`, `Coppie` nel formato `"schedaId/varianteId"`, `IndiceScheda`,
  `TempoTrascorso`) solo i `Dettagli` delle schede **già concluse** (`_dettagliSchedeConcluse`), scartando
  i passi eventualmente già risposti sulla scheda in corso.
- **Ripresa**: `SessioneLaboratorio.RiprendiDa(pausa, schede)` ricostruisce la sessione ripartendo dal passo 1
  della scheda `IndiceScheda`. Se una scheda o variante della pausa non esiste più, lancia
  `InvalidOperationException`, catturata da `MainView.OnRiprendiSospesaLab` (messaggio in `ErrorePanel` ed
  eliminazione della pausa orfana). Alla conclusione o all'abbandono della sessione ripresa,
  `MainView.OnLaboratorioConcluso` elimina la pausa tramite `sessione.IdSessionePausa`.
- In `SospesiView`, `SessioneSospesaItem` incapsula sia `SessionePausa` sia `PausaLaboratorio` (`ELaboratorio`),
  mostrando per il Laboratorio `"<Linguaggio> · Laboratorio"`, il livello, `"Scheda N di M"` e la durata, e
  sollevando `RiprendiLabRichiesto` alla pressione di *Riprendi*.

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

### Revisione grafica lucafoglia

Spec di riferimento: `docs/superpowers/specs/2026-11-25-revisione-grafica-design.md`. Strato di token e stili sopra `FluentTheme`, con font UI **Archivo** (OFL; variante stretta ExtraBold `Archivo Titoli` per `FontTitoli`) al posto di Inter.

- **Token (`wsa.quiz.app/Themes/Tokens.axaml`)**: inclusi in `App.axaml` via `MergedDictionaries`. Espongono `FontTitoli`, i brush pieni degli esiti (`VerdeBrush` `#40a79b`, `AmbraBrush` `#ebaf00`, `RossoBrush` `#d6414b`) e i `ThemeDictionaries` `Light`/`Dark` (tutti `SolidColorBrush`, mai `Color`, per Trappola 7).
- **Neutri chiaro/scuro (`Tokens.axaml`)**:
  - *Light (Grafite)*: `Sfondo` `#e8e7e3`, `SfondoRialzato` `#f1f0ec`, `TestoPrincipale` `#141413`, `TestoAttenuato` `#57564f`, `RigaForte` `#141413`, `RigaDebole` `#d6d5d0`.
  - *Dark (Arena)*: `Sfondo` `#0c0c0c`, `SfondoRialzato` `#161616`, `TestoPrincipale` `#f4f4f2`, `TestoAttenuato` `#a6a6a6`, `RigaForte` `#f4f4f2`, `RigaDebole` `#212120`.
- **16 inchiostri (`wsa.quiz.core/Services/Inchiostri.cs` e `wsa.quiz.app/Tavolozza.cs`)**:
  - `Inchiostri.Tutti` definisce i 16 inchiostri (`rosso`, `terracotta`, `cacao`, `ocra`, `oliva`, `prato`, `verdeacqua`, `petrolio`, `azzurro`, `blu`, `indaco`, `viola`, `prugna`, `rosaantico`, `ardesia`, `grigio`), con hex, `TestoNero`, `Sceglibile` (12 sceglibili; `rosso`, `ocra`, `verdeacqua`, `grigio` riservati; `grigio` ha `HexChiaro` `#c4c3bd` oltre a `#d7d7d7`), `HexTestoSuChiaro` e calcolo WCAG (`Contrasto`, `VarianteLeggibile`).
  - Migrazione id legacy (`Inchiostri.MigraIdColore`): `blunotte`→`indaco`, `magenta`→`prugna`, `verdeacqua`→`petrolio`, `grafite`→`ardesia`; id sconosciuto/vuoto/non sceglibile → `blu` (`Predefinito`).
  - `Tavolozza` espone i 12 `ColoreAccento` sceglibili (hover +10% bianco, pressed +20% nero, testo nero o bianco da `TestoNero`). `Tavolozza.Applica` (e `Riapplica` al cambio tema) imposta in `Application.Current.Resources` `AccentoBrush`, `AccentoHoverBrush`, `AccentoPressedBrush`, `AccentoTestoBrush`, `AccentoLinkBrush` (reso leggibile sul fondo attivo) e `SystemAccentColor*`.
- **Navigazione (`MainView.axaml` / `MainView.axaml.cs`)**: sei sezioni con inchiostri fissi (`blu` Home, `viola` Cronologia, `ocra` Sospesi, `terracotta` Statistiche, `verdeacqua` Gestione, `grigio` Impostazioni):
  - *Telefono/touch (`BottomNav`, C2)*: barra a tutta larghezza (altezza 56 + padding safe-area, riserva `SzRiservaNav`), angoli vivi e niente ombra; 6 `Button.nav` a campo pieno del colore di sezione (inattivi `1*` solo icona; attivo `2.8*` con classe `attiva`, icona + nome nel colore di contrasto).
  - *Desktop/web (`Tabs`, `TabsStripTheme` + `TabSezioneTheme`)*: ogni `TabItem` ha una fascia inferiore di 4 px (`FasciaSotto`) del colore di sezione (`BorderBrush`); la tab attiva diventa campo pieno con testo bianco o nero (classe `testo-nero`).
- **Colori degli esiti (`wsa.quiz.app/QuizColors.cs`)**: derivati da `verdeacqua` (`#40a79b`), `ocra` (`#ebaf00`) e `rosso` (`#d6414b`, usato anche per `Danger*` e `Button.danger` con testo nero). Riempimenti/bordi usano i brush pieni (`VerdeBrush`, `AmbraBrush`, `RossoBrush`, `Percentuale`, `Esito`, `HeatmapBordo`); i testi su fondo neutro usano `VerdeTestoBrush`, `AmbraTestoBrush`, `RossoTestoBrush` (`PercentualeTesto`, `EsitoTesto`, `HeatmapTesto`, `TempoRimanente`), pari a `HexTestoSuChiaro` (`#2b7168`, `#836100`, `#b83740`) in chiaro e a `VarianteLeggibile` su `#161616` (`#40a79b`, `#ebaf00`, `#d9535c`) in scuro, ricalcolati via `QuizColors.CollegaCambioTema`.
- **Colore utente su intestazione e titoli**: `HeaderBorder` (`MainView.axaml`) ha fondo neutro `Sfondo`, marchio vettoriale `MarchioQuaestio` (a6-2, anima `TestoPrincipale`) e bordo inferiore di 3 px in `AccentoBrush`; i titoli dei riquadri usano `Border.trattino` (`App.axaml`, bordo sinistro di 4 px in `AccentoBrush`, padding sinistro 7 px).
- **Angoli vivi e niente `BoxShadow`**: in `App.axaml` `ControlCornerRadius` e `OverlayCornerRadius` valgono `0`; rimossi `BoxShadow` e raggi dalle viste, con riquadri bordati da 1 px (`RigaDebole`/`RigaForte`) ed elenchi (Cronologia, Sospesi, Statistiche) a righe separate da filetti di 1 px.

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
