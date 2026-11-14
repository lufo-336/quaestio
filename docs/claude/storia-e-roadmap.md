# Storia del progetto: stato dettagliato e roadmap degli step chiusi

<!-- Spostato da CLAUDE.md il 2026-11-05 per alleggerire il contesto: testo invariato. -->

## Stato attuale

> **2026-10-23**: interfaccia disponibile in **italiano e inglese** (selettore
> in Impostazioni; le domande restano in italiano) e nuova materia **Python** →
> **11 materie / 2383 domande**. Vedi "Localizzazione dell'interfaccia it/en".
>
> **Aggiornato al 2026-09-09.** In sintesi: **tutta la roadmap Step 1–14 è
> chiusa** (Step 11 ed export/filtri e Step 14 About+icona+publish chiusi oggi;
> l'installer di Step 14 è escluso per scelta). App desktop + Android su `main`,
> nessun branch feature aperto, **10 materie / 2163 domande**.
>
> **Verificati a mano** il 2026-09-09 su GUI desktop reale: Step 8 (tastiera),
> 11 (filtri + export, export aperto in un foglio di calcolo), 13 (preferenze) e
> 14 (About, icona). Quel giro ha prodotto tre correzioni — vedi "Verifiche a
> mano sulla GUI desktop". **Restano scoperte**: una **partita intera**
> (risposte, feedback, pausa/ripresa, view-mode, modalità a tempo, riepilogo —
> è il cuore dell'app), **Gestione contenuti** e **Statistiche**.
>
> Il **bug della bottom-nav dopo cold start non si riproduce più** su Android 15
> (sessione device del 2026-09-09): vedi la nota in fondo a "Gestione contenuti
> — eliminazione per sezione".
>
> Dal 2026-09-09 si sviluppa anche su **Fedora**, dove la solution compila per
> intero (Android incluso) e c'è `adb` — vedi "Sul PC Linux (Fedora)".
>
> **APK aggiornato**: la release corrente è `apk-2026-09-09` e contiene tutto
> quanto sopra. Attenzione: è firmata con una debug key diversa da quella di
> luglio (macchina diversa), quindi non si installa sopra la precedente — vedi
> Trappola 24.

**Port Android M1, M2 e M3 completi** (M1+M2 il 2026-06-18, M3 chiuso il
2026-09-09 con lo splash; l'icona era stata fatta col rebrand Quaestio del 18/07). M1 (struttura) +
M2 (dati via risorse embedded — allora 7 materie / 1540 domande, oggi 10 / 2163)
committati, build verde, APK firmato ~30 MB. **M3 parte 1 (dialoghi →
overlay in-page) committata (`8b666eb`) e verificata headless (18/18 OK).
M3 parte 2 (layout touch-friendly) committata e verificata (build + smoke,
2026-06-19): flag `AppEnv.TouchMode` + dizionari Sizes Desktop/Touch + stili
globali MinHeight; tutte le schermate adattate, desktop invariato per
costruzione.** **M3 parte 3 (bottom-nav + swipe + safe-area edge-to-edge):
implementata e VERIFICATA SU DEVICE FISICO (Samsung Galaxy S21 5G, 2026-06-26).**
La bottom navigation a icone sta sopra i tasti Android grazie alle insets **native**
(non più `IInsetsManager.SafeAreaPadding`, che su device reale riporta `bottom=0` —
vedi Trappola 17); lo swipe fra tab usa lo **scroll orizzontale nativo con snap**
(non più gesture manuale né `Carousel` — vedi Trappola 18). Resta in M3 lo
**splash**, chiuso il 2026-09-09 (l'icona era stata fatta col rebrand). Vedi roadmap "Port Android", Trappole 14–18 e la
sezione "Test su device fisico (adb)" per il workflow adb. La struttura ora è una **libreria UI condivisa
(`Wsa.Quiz.App`) + 2 head** (`Wsa.Quiz.Desktop`, `Wsa.Quiz.Android`): le sezioni
"Layout repo"/"Stack" più sotto parlano ancora di "tre progetti/csproj" — è
stale rispetto a M1 (ora 5 progetti), non ancora riscritte.

**Step 7, 8, 9, 10, 12, 13 completi + Extra "tempo" (2026-06-03).** Lo Step 8
(navigazione tastiera) è stato chiuso il 2026-07-28 dopo due tentativi falliti:
la causa non era il routing ma il focus (Trappola 21). Step 9: tab Statistiche con heatmap punti
deboli per categoria + drill-down domande sbagliate (no LiveCharts2, no grafici
veri). Extra 2026-06-03: la modale pausa **congela il cronometro** (riparte su
Annulla) e c'è una **modalità a tempo** (countdown in alto, ambra <60s / rosso
<10s; allo scadere la partita termina subito, esito "Tempo scaduto", combinabile
con tutte le altre opzioni). Vedi sezione roadmap "Extra".

L'app Avalonia ha navigazione tastiera completa **dentro il quiz**:
`A/B/C/D` rispondono direttamente, `↑/↓` evidenziano una risposta con bordo
giallo `#FFD500` 3px e `Invio` la conferma, `←/→` scorrono le passate già date
in modalità read-only (banner giallo crema in alto + bottone "Torna alla
corrente"), `Invio` in feedback avanza, `ESC` apre la modale pausa unificata
Annulla/Abbandona/Salva e esci. Il ciclo pausa→sospeso→ripresa funziona
end-to-end nella GUI: anche dopo una ripresa le passate pre-pausa restano
navigabili con i 4 bottoni colorati. Cronologia condivisa fra console e GUI
verificata via shared storage in `%APPDATA%\WsaQuiz`.

## Roadmap

### ✅ Step 1 — Fondazione
Estrazione Core, riorganizzazione cartelle, rinomina namespace `QuizFdP`→`Wsa.Quiz.*`,
scheletro Avalonia. Cartella utente condivisa cross-platform per cronologia/sospesi.

### ✅ Step 2 — Porting interfaccia in Avalonia
Home (selezione + opzioni + Avvia), Quiz (domanda + 4 risposte + feedback +
progresso), Riepilogo (% + sbagliate + ritorno). INPC corretto, niente reset
di `DataContext`. Quiz dall'inizio alla fine come dal console, finisce in
cronologia condivisa.

### ✅ Step 3 — Cronologia + Sospesi come tab dedicate
Tab Cronologia (data, modalità, %, durata) + dettaglio domanda-per-domanda.
Tab Sospesi con Riprendi (allora disabilitato) / Elimina (conferma inline).
Style globali `Button.accent`/`Button.danger` per fix bottone disabled.

### ✅ Step 4 — Tastiera + Pausa
Invio = avanti, A/B/C/D = risposta, ESC = menu pausa. Pausa salvata dalla GUI
confluisce nei sospesi e Riprendi ricostruisce la sessione (cumulativa:
dettagli, durata e contatori sopravvivono al ciclo). A fine sessione ripresa
la pausa originale viene rimossa.

### ✅ Step 5 — Eliminazione cronologia
`RisultatoQuiz.Id` (Guid) generato in `SalvaRisultato`, migrazione lazy
una-tantum dei record esistenti. `EliminaRisultato(string)` e
`SvuotaCronologia()` su `StorageService`. Vedi sezione "Decisioni UX".

### ✅ Step 6 — UX rifiniture (Home sticky + Pausa unificata)
Home: vedi sezione "Decisioni UX". Pausa: tre voci unificate. Fix laterali
applicati: feedback box con `Foreground="#1F1F1F"` esplicito (Trappola 9);
"Prossima domanda" con `Classes="accent"` per coerenza.

### ✅ Step 7 — Navigazione tra domande
Vedi sezione "Navigazione fra domande passate (view-mode)". Iterazione UX:
highlight giallo `#FFD500` 3px invece di accent 2px. Fix laterale: bottone
"Prossima domanda" testo bianco illeggibile in dark mode → style
`Button.prossima /template/ ContentPresenter` con `TextBlock.Foreground="#1F1F1F"`
(Trappola 12).

### ✅ Step 8 — Navigazione tastiera globale (chiuso 2026-07-28)

**Causa radice trovata** (dopo due tentativi falliti che avevano cercato il
problema nel *routing*): in Avalonia la **`ListBox` ha `Focusable = false`** —
il focus appartiene ai suoi `ListBoxItem`, esattamente come quando si clicca una
riga col mouse. `CronologiaView`/`SospesiView` chiamavano `ListaX.Focus()`, che
restituisce `false` e **non fa nulla**: il focus restava sullo `UserControl`, che
e' un *antenato* della lista. Il routing di `KeyDown` va dalla radice
all'elemento con focus, quindi **non attraversava mai la ListBox** e le frecce
non potevano muovere la selezione. Enter/Canc "funzionavano" solo perche' il
handler di view legge `SelectedItem`, che il fallback del `GotFocus` forzava a 0:
agivano quindi **sempre sulla prima riga**, qualunque cosa sembrasse selezionato
— ecco il "non funziona perfettamente" del 18 maggio. Il `Tunnel +
handledEventsToo` della PR #2 non c'entrava nulla (e infatti funziona: la
diagnostica mostra il handler di view che intercetta Invio prima della ListBox).

**Fix** (3 call site, un solo helper): `TabellaViewBase.FocusRigaSelezionata(ListBox)`
seleziona la prima riga se non c'e' selezione e mette il focus sul **container**
(`ContainerFromIndex(...)`), con un retry su `Dispatcher` a priorita' `Loaded`
per il caso "container non ancora realizzato". Usato nei `GotFocus` di
`CronologiaView`/`SospesiView` e al rientro dal dettaglio
(`OnIndietroDalDettaglio`). Vedi **Trappola 21**.

**Verifica**: harness headless (`Avalonia.Headless` + `window.KeyPress`) che
guida i veri controlli e asserisce i comportamenti della roadmap — **prima del
fix 11 PASS / 4 FAIL, dopo 15 PASS / 0 FAIL**, incluso "Invio riprende la riga
SELEZIONATA" (prima riprendeva sempre la riga 0). Le frecce della Home
risultano gia' funzionanti nella stessa verifica. Build 5/5 verde.
**Non riverificato a mano su tastiera reale** da Luca.

**Ricetta harness** (riusabile, throwaway fuori dal repo): progetto console
`net10.0` con `PackageReference Avalonia.Headless` + `ProjectReference` a
`Wsa.Quiz.App`; `AppBuilder.Configure<App>().UseHeadless(...).SetupWithoutStarting()`,
`Window` con dentro la view, `Dispatcher.UIThread.RunJobs()` come pompa,
`window.KeyPress(key, mods, physicalKey, null)` per i tasti, `StorageService`
puntato a una cartella temporanea (**mai** `%APPDATA%\WsaQuiz`). Per vedere il
routing: `AddHandler(KeyDownEvent, h, Tunnel|Bubble, handledEventsToo: true)` su
ogni nodo, loggando `e.Route`, `e.Source`, `e.Handled` e l'elemento con focus.

**Funziona**:
- `FocusAdorner` globale (bordo giallo `#FFD500` 3px sul focus da tastiera).
- `Ctrl+Tab` / `Ctrl+Shift+Tab` per cambiare tab principale.
- Frecce `←/→` dentro la modale pausa; `Ctrl+Tab` bloccato dentro la modale.
- `Tab` ciclico fra le 4 zone della Home (`KeyboardNavigation.TabNavigation="Once"`
  per zona + `Cycle` sul DockPanel root).
- `IsTabStop="False"` sui bottoni interni dei `ListBoxItem` (Elimina/Riprendi/...)
  e sugli header `TabItem`.
- `SospesiView` convertita da `ItemsControl` a `ListBox` (`SelectionMode="Single"`).

**Storico dei due tentativi falliti** (utile per non ripetere l'errore di
metodo: entrambi hanno cercato la causa nel routing senza mai stampare la
diagnostica, mentre il problema era il focus).

**Tentativo PR #2 (Facundo Fanti, 14 maggio 2026)**: introdotto pattern
`AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel, handledEventsToo: true)`
su `HomeView`, `CronologiaView` e `SospesiView`. La `handledEventsToo: true` era
la pezza mancante rispetto al tentativo originale dello step 8 (vedi
Trappola 13 aggiornata): intercetta gli eventi anche dopo che
`ListBoxItem`/`ScrollViewer` li hanno marcati `Handled`. Verifica pratica
del **18 maggio 2026**: la tastiera "continua a non funzionare perfettamente".
Non è stato fatto l'apporofondimento diagnostico raccomandato in Trappola 13
(stampare `e.Source`/`e.RoutedEvent`/`e.Handled` per vedere davvero il flusso
del routing) — quindi prima di tentare un terzo fix, **fare quello**.

**Ex elenco "NON funziona ancora", ora tutto verificato verde** (headless):
- Frecce `↑/↓` dentro la zona corrente della Home → funzionavano gia'.
- Frecce `↑/↓` per cambiare riga in Cronologia/Sospesi → **erano rotte**, fix.
- `Invio` per aprire dettaglio su una riga di Cronologia → ora sulla riga giusta.
- `Invio` per Riprendere su una riga di Sospesi → ora sulla riga giusta.
- `Canc` per avviare la conferma elimina inline da tastiera → ok.
- `Esc` sul `CronologiaDettaglioView` per tornare alla lista → ok, e ora il
  focus rientra sulla riga invece di sparire (prima la tastiera restava morta
  finche' non si cliccava col mouse).

### ✅ Step 9 — Statistiche (mappa punti deboli)
Tab "Statistiche" (quarta) con heatmap per categoria: una riga per materia,
celle ordinate per % errore decrescente, colori verde/ambra/rosso. Click su
una cella apre il drill-down con le domande sbagliate di quella categoria.
Toggle "Tutto" / "Ultime N partite" (NumericUpDown, default 30). **Senza
LiveCharts2**: tutto fatto con ItemsControl/WrapPanel/Border. Definizione di
"errore" = !(Corretta && Tentativi==1) — unifica modalita' classica e
rotazione. Servizio puro in `Wsa.Quiz.Core.Services.StatisticheService`.
Smoke test 2026-05-18: ok complessivo, `Esc` su drill-down funziona.
Spec: `docs/superpowers/specs/2026-05-18-step9-grafici-statistiche-design.md`.

### ✅ Extra (2026-06-03) — Pausa congela timer + Modalità a tempo
Due funzionalità richieste fuori roadmap:
- **Pausa congela il cronometro**: `SessioneQuiz.SospendiCronometro()` /
  `RiprendiCronometro()`. `QuizView.ApriMenuPausaAsync` sospende prima di
  aprire la modale e riprende solo sul ramo "Annulla". Usa `Stopwatch.Stop()/
  Start()` (accumula → nessuna aritmetica di offset). Abbandona/Salva non
  riprendono (la sessione finisce/esporta, `Stop()` idempotenti).
- **Modalità a tempo**: `OpzioniQuiz.LimiteTempoMinuti` (0 = off). Quando >0 il
  cronometro fa il **countdown** (visibile anche con "Mostra cronometro" off);
  colore via `QuizColors.TempoRimanente` (neutro, ambra ≤60s, rosso ≤10s),
  bindato a `SessioneQuiz.TempoBrush`. Allo scadere `AggiornaTempo` chiama
  `Concludi(abbandonato:false, tempoScaduto:true)` → **termina subito**; la
  domanda corrente non ancora risposta NON è conteggiata (non è in `Dettagli`).
  `RisultatoQuiz.TempoScaduto` (additivo) guida titolo "Tempo scaduto" nel
  Riepilogo e badge ambra "⏱ Tempo scaduto" in Cronologia (precede `Concluso`,
  dopo `Abbandonato`). Home: checkbox "Modalità a tempo" + NumericUpDown minuti
  (1–180, default 10), a specchio di "Limita a N domande". Pausa+ripresa
  coerenti: l'elapsed cavalca `_offsetCronometro`/`pausa.TempoTrascorso`, il
  limite vive in `pausa.Opzioni` — nessun nuovo campo in `SessionePausa`.
  `MostraTempo => Cronometro || HaLimiteTempo`; il timer parte su `MostraTempo`.
  CLI invariato (campi default neutri). Spec/plan in `docs/superpowers/`
  (`2026-06-03-pausa-timer-modalita-a-tempo*`). Build verde; smoke test
  interattivo confermato da Luca.

### ✅ Port Android — M1 (2026-06-18, *codice completo; APK firmato prodotto*)
`Wsa.Quiz.App` declassata a **libreria UI condivisa**; due head sottili la
consumano: `Wsa.Quiz.Desktop` (eseguibile Windows, invariato) e
`Wsa.Quiz.Android` (APK). `MainWindow`→`MainView : UserControl`,
`App.OnFrameworkInitializationCompleted` sceglie il lifetime
(desktop = `Window`; Android = single-view `MainView`). Solution flat a 5
progetti (Core, App, Cli, Desktop, Android). `dotnet build Wsa.Quiz.sln -c Debug`
verde su tutti. **APK installabile** via `dotnet build wsa.quiz.android -c Release`
→ `bin/Release/net10.0-android/it.eduits.wsaquiz-Signed.apk` (~29 MB, firmato,
verificato con `apksigner`). Adattamenti al piano dovuti all'API Avalonia 12 e
al setup APK: vedi **Trappole 14–16**. **Run su emulatore NON eseguito**: questa
VM non ha virtualizzazione annidata (`VMMonitorModeExtensions=False`), l'emulatore
x86_64 con WHPX non può girare; testare l'APK su un dispositivo reale o su una
macchina con accelerazione hardware. Spec/plan in `docs/superpowers/`
(`2026-06-16-android-port-*`). Fuori scope M1 (→ M2/M3): asset dati impacchettati
(oggi l'header materie/domande sarà 0 su Android finché i JSON non sono asset),
overlay dei dialoghi modali, layout touch.

### ✅ Port Android — M2 — Dati su Android (2026-06-18)
I dati read-only (materie + domande) ora si caricano anche su Android. Astrazione
**`IFonteDati`** (`Wsa.Quiz.Core.Services`) con `Esiste`/`LeggiTesto`/`ElencaJson`
su percorsi relativi; due implementazioni:
- **`FileSystemFonteDati`** (Core): legge dal bin via file system — desktop/CLI,
  comportamento identico a prima. I costruttori legacy di `StorageService`
  (`(cartella)`, `(cartellaDati, cartellaUtente)`) vi delegano; nuovo costruttore
  `StorageService(IFonteDati, cartellaUtente)`.
- **`AvaloniaResourceFonteDati`** (`Wsa.Quiz.App`): legge dalle risorse Avalonia
  embedded via `AssetLoader`/`avares://Wsa.Quiz.App/Assets/…` — Android (i JSON
  viaggiano nell'APK). `AssetLoader.GetAssets(dirUri, null)` enumera i `.json`.
I dati sono `<AvaloniaResource Include="..\materie.json"/>` +
`..\domande\**\*.json` **linkati** dalla radice repo nella libreria condivisa
(niente duplicazione). `MainView.Caricamento` sceglie la fonte: file system se
`materie.json` è nel bin (desktop), altrimenti risorse embedded (Android). I dati
utente (cronologia/pausa) restano su file (`CartellaUtenteDefault()`; su Android
è la app data dir scrivibile). **Materie: 7** (aggiunta **UX Design**, 100 dom.;
**Frontend** ampliato a ~210) per **1540 domande** totali, verificate headless via
`AssetLoader` (stesso path di Android). APK Release firmato ~30 MB. **Non
verificato su device** (vedi M1: emulatore non eseguibile su questa VM): testare
materie/domande su un dispositivo reale. Fuori scope (→ M3): dialoghi modali
overlay, layout touch.

### ✅ Port Android — M3 — UX mobile (completo: parti 1-3 nel 2026-06, splash il 2026-09-09)
**Parte 1 — dialoghi modali → overlay in-page: codice committato (`8b666eb`),
verifica runtime headless SUPERATA (18/18 check OK, 2026-06-18).** `PausaDialog`
e `ConfermaDialog`
erano `Window` + `ShowDialog(owner as Window)`: su Android (single-view) `owner`
è null e il fallback `Show()` non funziona → erano il blocco funzionale. Ora:
- nuova base **`OverlayDialogBase : UserControl`** (`Views/`): `ShowOverlayAsync(Visual)`
  inserisce il dialogo (card centrata) sopra uno scrim semitrasparente
  nell'`OverlayLayer` del TopLevel e ritorna un `Task` che si completa su
  `Chiudi()`. La root dell'overlay ha Width/Height vincolati ai `Bounds` del
  layer (è un `Canvas`, non stira i figli).
- i due dialoghi: root XAML `Window`→`UserControl` (card `Border`, sfondo chiaro
  hardcoded → tema scuro a Step 10), `Close()`→`Chiudi()`, focus default in
  `OnShown()`. Call site: `await dialog.ShowOverlayAsync(this)`, risultati letti
  come prima.

Build Debug verde. **Verifica runtime headless superata** (`Avalonia.Headless`,
harness in `%TEMP%\m2verify`): 18/18 check OK — `OverlayLayer` presente,
`PausaDialog` → Abbandona/Annulla/Salva, `ConfermaDialog` → true/false, overlay
aggiunto e rimosso a ogni ciclo. **Nota harness** (2026-06-18): il driver
originale faceva `Dispatcher.UIThread.InvokeAsync(...).GetResult()` dopo
`SetupWithoutStarting()` → deadlock sul thread UI (era la causa del "nessun esito"
della sessione precedente, NON un bug dei dialoghi). Corretto: il corpo gira sul
thread UI pompando `Dispatcher.UIThread.RunJobs()` in un loop con timeout di 30s.
Resta da verificare su **device fisico** (emulatore non eseguibile su questa VM).

**Parte 2 — layout touch-friendly: committata e verificata (build + smoke headless,
2026-06-19).** Meccanismo a **flag `AppEnv.TouchMode`** (acceso solo nel ramo
`ISingleViewApplicationLifetime`/Android di `App.OnFrameworkInitializationCompleted`,
`false` su desktop) + due `ResourceDictionary` con chiavi omonime
(`Themes/Sizes.Desktop.axaml` / `Sizes.Touch.axaml`): il dizionario touch è
merge-ato a runtime solo su Android (append; le chiavi vincono via
`TryGetResource`, che è il path di `DynamicResource` — l'indexer
`Resources["..."]` NON attraversa le MergedDictionaries in Avalonia 12). Stili
globali in `App.axaml` danno `MinHeight={DynamicResource SzTapMin}` (32 desktop /
48 touch) a Button/CheckBox/NumericUpDown/ListBoxItem/TabItem → target tap grandi
ovunque su Android senza toccare ogni view. Schermate: **QuizView/SospesiView**
solo sizing; **HomeView** impila Materie/Categorie in verticale su touch (reflow
in code-behind gated da `TouchMode`); **RiepilogoView** dispone le 5 metriche in
2×2; **StatisticheView** header `WrapPanel` + celle heatmap toccabili (chiavi
`SzCellaPad`/`SzCellaMinH` touch-only, desktop pixel-identico); **CronologiaView**
mostra **card verticali** su touch (secondo `DataTemplate` modellato su SospesiView,
tabella desktop invariata; toggle reattivo via proprietà calcolate
`MostraTabella`/`MostraCard`/`MostraHeaderColonne` legate a `NessunaSessione`).
**MainView**: no-op (i `TabItem` ereditano `MinHeight` dallo stile globale; padding
espliciti regredirebbero il desktop). Tutto a valori desktop = identici a prima →
desktop invariato per costruzione. Spec/plan in `docs/superpowers/`
(`2026-06-19-m3-touch-friendly-layout-*`). **Restava in M3**: icona/splash
definitivi.

**Parte 3 — bottom-nav + swipe + safe-area: implementata e VERIFICATA SU DEVICE
FISICO (Samsung Galaxy S21 5G, SM-G991B, density 480, 3-button nav; 2026-06-26).**
Su touch `MainView` nasconde il `TabControl` e mostra: (a) una **bottom navigation
a icone** (`BottomNav`, 4 `Path`) ancorata in basso, attiva in blu via `AggiornaNav`;
(b) **`SwipeHost`**, uno **ScrollViewer orizzontale** con le 4 pagine affiancate
(larghe/alte quanto il viewport, impostato in `DimensionaPagine` su `SizeChanged`),
`HorizontalSnapPointsType=MandatorySingle` → swipe col dito + inerzia + aggancio a
una tab per volta (`SwipePages` è uno `StackPanel` orizzontale; lo scroll verticale
è disabilitato sull'host, ogni pagina ha il proprio ScrollViewer → assi ortogonali,
nessun conflitto). Il tap su un'icona fa uno **scroll animato** (`ScorriAllaPagina`,
easeOutCubic 220ms via `DispatcherTimer` sull'`Offset`); `ScrollChanged` mantiene
sincronizzata l'icona attiva. **Safe-area edge-to-edge** (targetSdk 36 → forzato):
la `MainActivity` (`View.IOnApplyWindowInsetsListener` sulla decor view) legge le
insets native `WindowInsets.systemBars()` e le passa a `MainView` via
`AppEnv.SystemBarInsetsPx` (px fisici); `ApplicaSafeArea` riserva header (status bar)
e padding-bottom della bottom nav convertendo px→unità con `f = ScalaTouch ×
RenderScaling`, `RenderScaling` letto a runtime. **Perché non `IInsetsManager` né
`Carousel`**: vedi Trappole 17–18. Workflow di test su device (adb, screencap+pull,
`EmbedAssembliesIntoApk` per un Debug installabile a mano): vedi la sezione
"Test su device fisico (adb)".

**Parte 4 — splash: chiusa il 2026-09-09, M3 completo.** Il punto di partenza è
che **da Android 12 (API 31) il sistema mostra comunque uno splash e non è
disattivabile**: senza intervenire usava lo sfondo di default del tema. Non
abbiamo quindi *aggiunto* uno splash, abbiamo preso il controllo di quello che
c'era già. Un solo file, `Resources/values-v31/styles.xml`, che ridefinisce
`WsaQuizTheme` con `windowSplashScreenBackground` = `@color/ic_launcher_background`
(lo stesso nero dell'icona) e `windowSplashScreenAnimatedIcon` =
`@drawable/ic_launcher_foreground` (la "Q" del rebrand, già disegnata dentro la
safe-zone e quindi compatibile con la maschera circolare del sistema).

Scelte deliberate: **niente dipendenza** `androidx.core:core-splashscreen` e
**niente codice** nel percorso di avvio, che è delicato (un `parent` sbagliato in
`values/styles.xml` causò un `IllegalStateException` all'avvio sul device). Su
**API 24–30 la cartella non viene letta** e il comportamento resta quello di
prima, nessuno splash: coprire anche quelle versioni richiederebbe un tema di
lancio con `windowBackground` più un `SetTheme()` in `OnCreate`, cioè codice non
provabile senza un telefono vecchio.

Verificato **senza device** ispezionando l'APK con `aapt2 dump resources`: lo
stile esiste in due configurazioni, `()` vuota e `(v31)` con gli attributi
`0x0101062c` / `0x0101062d`, che risolvono (via `android.R$attr` di
`android-36/android.jar`) esattamente a `windowSplashScreenBackground` e
`windowSplashScreenAnimatedIcon`, legati alle risorse giuste. Poi **verificato su
device fisico** lo stesso giorno (S21, Android 15): "Q" bianca su nero pieno a
tutto schermo, catturata con `adb exec-out screencap` ~0,4 s dopo il lancio.

### ✅ Rebrand "Quaestio" (2026-07-18)
Brand da "WSA Quiz" a **Quaestio** senza toccare gli identificatori interni
(vedi nota in testa al documento). Modificate le 5 stringhe user-facing:
`MainView.axaml` (header), `App.axaml.cs` (Title finestra desktop),
`MainApplication.cs` + `MainActivity.cs` (Label Android), `AndroidManifest.xml`
(`android:label`). Icona: monogramma **"Q" bianco su sfondo nero** (era una spunta
verde). File reali dell'icona Android: `Resources/drawable/ic_launcher_foreground.xml`
(vector "Q", adaptive API 26+), `Resources/values/ic_launcher_background.xml`
(`#000000`), `mipmap-anydpi-v26/ic_launcher.xml` (composizione); i 10 PNG legacy
`mipmap-*/ic_launcher(.round).png` (Android 7.x / API 24-25) rigenerati con un
tool SkiaSharp usa-e-getta (nessun rasterizzatore installato). Le `branding/*.svg`
sono solo sorgenti di design, NON wired nel build. Verificato: build desktop verde,
APK Release verde e ispezionato con `aapt2` (`application-label='Quaestio'`,
`ic_launcher_background=#ff000000`). Verde brand `#1F7A4D` invariato nel resto
dell'app (risposte corrette ecc.).

### ✅ Step 10 — Dark mode (2026-07-15, insieme a Step 13)
Toggle Chiaro/Scuro nella **6ª sezione "Impostazioni"** (non esisteva prima
alcun controllo tema in UI), scelta persistita in `preferenze.json` — vedi
Step 13. Risolta anche la nota che questo step si portava dietro: il cronometro
non a tempo ha `Foreground` bindato a `TempoBrush`, il cui default
`QuizColors.TempoNeutro` deriva dalla const `QuizColors.TestoNeutro`: era
hard-coded `#1F1F1F`, quasi invisibile su fondo scuro → portata a `#767676`
(`wsa.quiz.app/QuizColors.cs:29`), leggibile in entrambi i temi.

### ✅ Step 11 — Esportazione e filtri cronologia (2026-09-09)
Barra filtri nella tab Cronologia (materia, periodo, percentuale, stato) e
bottone "Esporta (N)". **L'export rispetta i filtri** — era la domanda lasciata
aperta: senza filtri attivi esporta tutto, quindi il caso "backup" resta coperto
senza un secondo comando.

Logica pura in Core, testabile headless: `Models/FiltroCronologia` (criteri in
AND + `MaterieDisponibili`) e `Services/EsportazioneService` (`VersoCsv` /
`VersoJson`, stringa in uscita, nessun I/O). Il CSV è tarato su Excel italiano:
separatore `;`, virgola decimale, quoting RFC4180, CRLF, BOM aggiunto in
scrittura. Una riga per partita; il dettaglio domanda-per-domanda resta fuori
(consultabile nell'app).

**Il filtro per materia legge i `Dettagli`, non `MateriaNome`**: per il
multi-materia quel campo vale la label `"Quizzone"` (`HomeView.axaml.cs`), quindi
da solo non basta — una partita Quizzone che contiene SQL deve passare il filtro
"SQL". Fallback sulla label per i record senza dettagli (abbandoni immediati,
record vecchi). Stessa regola in `EsportazioneService.MaterieCoinvolte`.

Due dettagli non ovvi emersi in implementazione:
1. Il binding TwoWay del `ComboBox` rimanda `SelectedItem = null` appena la
   collezione viene svuotata: la scelta va salvata **prima** della `Clear()`, o
   dopo ogni ricarica la tendina resta in bianco.
2. Se sparisce l'ultima partita di una materia filtrata, il filtro **si azzera
   da solo** invece di lasciare una lista vuota da sbloccare a mano.

Il formato si sceglie da un **menu sul bottone Esporta** (CSV o JSON). In
origine lo deduceva dall'estensione scelta nel file picker, ma **su Linux quel
selettore non compare affatto** (verificato a mano il 2026-09-09: l'utente non
aveva modo di scegliere, né di sapere che poteva scrivere `.json` a mano). La
scelta di formato deve stare **nell'app**, dove si vede su tutte le piattaforme;
il picker riceve un solo tipo, coerente con la voce scelta. Verificato con due harness
headless (27/27 sui servizi Core, 20/20 sulla vera `CronologiaView`).
**Non ancora provato a mano sulla GUI desktop.**

**Regressione trovata su device il 2026-09-09 e corretta**: il terzo bottone
nell'header (Esporta) faceva traboccare la riga, e su un 1080px i tre bottoni
finivano **sopra il titolo**, coprendolo. Non si vedeva né a desktop né headless:
solo il telefono l'ha mostrata. Ora i bottoni stanno in un `WrapPanel` ancorato a
destra su desktop che il code-behind sposta **sotto il titolo** quando
`AppEnv.TouchMode` è attivo. Regola generale: **una riga di header con più di due
bottoni va verificata sul layout touch**, dove la larghezza disponibile è un
terzo di quella desktop.

### ✅ Step 12 — Import domande/materie da GUI (2026-07-04)
5ª tab **"Gestione contenuti"** (desktop e Android): due flussi — crea nuova
materia da `.json`, oppure aggiungi domande a una materia esistente (built-in
o già importata) — entrambi con validazione strutturale (`ValidatoreImportDomande`,
un solo errore rifiuta l'intero file), anteprima prima di confermare, e dedup
per testo esatto sui duplicati (saltati e conteggiati, non è un errore). I
contenuti importati vivono **solo** in `CartellaUtenteDefault()/ContenutiUtente/`
(mai nei file built-in del repo/APK): risolve l'implicazione storage originale
di questo step senza toccare `<None Include="...">`. Nuovo
`ContenutiUtenteService` (Core) + `IStorageProvider` di Avalonia per i file
picker (prima introduzione nel repo). La stessa tab include anche un riquadro
che rende scaricabili (`SaveFilePickerAsync`) la guida e il motore per
generare domande bilanciate con un'IA (`generazione-domande/`, incorporati
come `AvaloniaResource`). Verificato: harness headless che invoca i veri
handler della view (bypassando solo il file picker nativo, identico su
Task 9/10), più verifica live sull'app desktop reale (creazione/rimozione/
persistenza dopo riavvio via UI Automation, log reali catturati). Bug reale
scoperto e risolto durante la verifica: vedi Trappola 19 (RadioButton
GroupName cross-instance in `StatisticheView`, esposto dal primo reload
completo dei dati mai eseguito dopo il boot). Spec/plan in `docs/superpowers/`
(`2026-07-02-import-domande-json-*`). **Non verificato su device Android
fisico** in questa sessione (nessun device/adb disponibile): APK Release
comunque prodotto e firmato senza errori.

### ✅ Gestione contenuti — eliminazione per sezione + fix visibilità (2026-07-20)
Tre interventi, tutti **verificati su device fisico** (Samsung Galaxy S21 5G,
Android, dark mode):
- **Fix bug visibilità bottoni accent** (Trappola 20): su questo device
  `SystemAccentColorBrush` si risolveva invisibile → *tutti* i bottoni accent
  abilitati (Avvia quiz, Prossima domanda, Conferma, import) erano bianco-su-bianco.
  Era la vera causa del "non vedo il pulsante per importare". Sfondi accent ora
  in hex esplicito (`#0078D4`/`#2B90DE`/`#005FB0`) in `App.axaml`.
- **Import più chiaro** in `GestioneContenutiView`: testo guida sui due passaggi,
  label+icona "📁 Importa domande da file JSON", bottone import disabilitato
  finché manca nome (nuova materia) / materia scelta (aggiungi). Verificato anche
  che il **file picker Android (SAF) si apre** end-to-end (mai testato prima).
- **Eliminazione per sezione (nascondi/ripristina)**: ogni materia è un `Expander`
  con le sue sezioni (categorie); "Nascondi" (conferma inline) esclude la sezione
  dal pool quiz, "Ripristina" la riattiva. Meccanismo **unico "nascondi"** per
  built-in e importate (le built-in restano nell'APK, si filtrano al caricamento
  → reversibile). Persistenza: `SezioneEsclusa {MateriaId, Categoria}` in
  `ContenutiUtente/sezioni-escluse.json` via `ContenutiUtenteService`
  (`CaricaSezioniEscluse`/`NascondiSezione`/`RipristinaSezione`, idempotenti).
  `MainView.Caricamento` filtra `domandeAttive` (Home + header) tenendo
  `_tutteDomande` completo per Gestione e per riprendere i sospesi. Verifica:
  harness headless Core 8/8 OK + device (nascondi → conteggio 2080→2078 e sezione
  sbiadita con pill "nascosta"+Ripristina; ripristina → 2078→2080). Spec:
  `docs/superpowers/specs/2026-07-19-eliminazione-sezione-design.md`.

**Bug bottom-nav dopo cold start — NON riproducibile al 2026-09-09.** Osservato
il 20/07 (tap sulla bottom-nav che non commutano tab dopo un cold start, mentre
lo swipe funzionava), con sospetta radice `RenderScaling` = 1 al boot / 3 a
regime (cfr. Trappola 17).

Ritentato il **2026-09-09** sullo stesso telefono (S21, ora **Android 15 / API
35**) con la build del giorno: **due cold start** (`force-stop` + `monkey`) e
**quattro tap sintetici** su icone diverse della bottom-nav, tutti andati a
segno, sia alla primissima apertura senza `preferenze.json` sia con una tab
salvata diversa da zero (il caso in cui Step 13 anima lo scroll di ripristino,
che era l'ipotesi più promettente). Non si sa se sia stato risolto per effetto
collaterale di qualche modifica o se dipendesse dalla versione di Android che il
telefono aveva a luglio. **Prima di rimetterci mano, riprodurlo a mano**: se non
si riproduce, non c'è niente da correggere.

### ✅ Step 13 — Preferenze utente persistite (2026-07-15)
File **`preferenze.json`** (non `settings.json` come previsto in origine) nella
cartella utente, modello `Wsa.Quiz.Core.Models.PreferenzeUtente`, letto/scritto
da `StorageService`. Ricorda: **ultima sezione aperta** (`TabIniziale`), **tema**
(`TemaScuro`, vedi Step 10) e le **ultime opzioni quiz** (randomizza, rotazione,
cronometro, limite domande + N, modalità a tempo + minuti). I default del modello
coincidono con quelli hardcoded in `HomeView`, così senza il file il
comportamento resta identico a prima. Aggiunta la **6ª sezione "Impostazioni"**
(bottom-nav/tab) che ospita il toggle tema.

**Due bug reali emersi in implementazione, entrambi da tenere a mente:**
1. Il piano prescriveva di assegnare le opzioni quiz ai **campi privati** di
   `HomeView` (per evitare salvataggi spuri), ma i binding TwoWay sono già live
   al momento di `Inizializza()` → la UI restava sui default hardcoded. Fix:
   usare i **setter pubblici**; il salvataggio spurio resta comunque evitato
   perché `MainView` si iscrive a `PropertyChanged` solo *dopo* che
   `Inizializza()` è ritornato.
2. Su Android/touch il guard anti-doppio-salvataggio della tab attiva non
   teneva: `IndiceSezione` anima sempre lo scroll, generando più `ScrollChanged`
   con **indici intermedi** durante la transizione (rischio di persistere la tab
   sbagliata se il processo muore a metà animazione). Fix: salvataggio
   **debounced 300ms** sul path touch (`MainView.axaml.cs`).

Piano: `docs/superpowers/plans/2026-07-14-preferenze-utente.md`. Verificato su
desktop via UI Automation + lettura diretta di `preferenze.json`; **non
verificato su device Android fisico**.

### ✅ Step 14 — Rifiniture distribuzione (2026-09-09, senza installer)
**Installer escluso per scelta**: per un progetto didattico l'eseguibile
portable basta, e la toolchain (Inno Setup/MSIX) è Windows-only. Fatto il resto:

- **Versione unica** in `Directory.Build.props` (`<Version>1.0.0</Version>`),
  ereditata da tutti e 5 i progetti e letta a runtime dall'assembly, così non c'è
  un numero da allineare a mano. Non tocca l'APK: l'head Android continua a usare
  i suoi `ApplicationVersion` / `ApplicationDisplayVersion` (verificato:
  `versionName="1.0"`, `versionCode="1"` invariati).
  *(2026-11-11: passati a **2.0.0**; ora anche Android e iOS mostrano
  `$(Version)` tramite `ApplicationDisplayVersion`, e `ApplicationVersion` = 2.)*
- **About dentro Impostazioni**, non come 7ª sezione: la bottom-nav Android ha
  già 6 icone. Quattro riquadri — app+versione, contenuti caricati (stessi
  conteggi dell'header, quindi al netto delle sezioni nascoste), cartella dati
  con bottone "Apri cartella" (`ILauncher`, nascosto in TouchMode perché su
  Android non c'è un file manager a cui passarla), crediti + link al repo.
  Serviva esporre `StorageService.CartellaUtente`, prima privato.
- **Icona della finestra desktop**: `branding/icon.svg` rasterizzato con
  ImageMagick in `wsa.quiz.app/Assets/Branding/quaestio-icon.png` (risorsa
  Avalonia, usata da `App.OnFrameworkInitializationCompleted` per `Window.Icon`)
  e in `wsa.quiz.desktop/quaestio.ico` multi-risoluzione per `<ApplicationIcon>`
  (icona del file `.exe`). Prima la finestra non aveva alcuna icona.
- **Publish verificati** per win-x64, osx-arm64 e linux-x64 — e la sezione
  "Distribuzione" corretta: `PublishSingleFile` da solo non produce un file
  singolo (vedi il riquadro là). Il singolo eseguibile linux-x64, da solo in una
  cartella vuota, è stato **avviato davvero** su questo PC.

Verifica: harness headless sulla vera `ImpostazioniView`, 12/12 (versione,
percorso dati, conteggi, icona caricabile, e 10 materie / 2163 domande lette
dalle risorse incorporate). **Non provato a mano** né su Android.

### 🔍 Verifiche a mano sulla GUI desktop (2026-09-09, parziali)

Prima sessione con mani e tastiera vere, su Fedora. Esiti:

- **Step 14 — About**: corretto in tutte le voci (versione 1.0.0, 10 materie ·
  2163 domande, percorso, crediti).
- **Step 11 — filtri ed export**: provati su 17 partite di prova. Il CSV
  esportato è stato aperto e ispezionato: BOM, separatore `;`, decimali con
  virgola, e conteneva esattamente le righe filtrate. **Difetto trovato**: il
  file picker di sistema su Linux **non mostra il selettore del tipo di file**,
  quindi dedurre il formato dall'estensione lasciava l'utente senza scelta. La
  scelta è stata spostata in un **menu sul bottone Esporta**, dentro l'app.
- **Step 13 — preferenze**: vedi sotto.
- **Step 8 — tastiera**: navigazione fra sezioni e `Invio` **funzionano**.
- **Icona finestra**: su GNOME/Wayland **non si vede**, ma *non è un bug del
  codice*: `xprop` conferma che `_NET_WM_ICON` è impostata (128×128) sulla
  finestra. GNOME non mostra icone nella barra del titolo e per dash/alt-tab
  associa la finestra a un file `.desktop` tramite `WM_CLASS`
  (`Wsa.Quiz.Desktop`), che qui non esiste. Su Windows, la piattaforma di
  riferimento, l'icona si vede. Per vederla anche su Linux servirebbe
  installare un `.desktop` con `StartupWMClass=Wsa.Quiz.Desktop` — packaging
  Linux, oggi fuori scope.
- **`Tab` nella Home + bordo di focus invisibile — BUG TROVATO E CORRETTO**.
  Segnalato come "il Tab non entra nei riquadri e seleziona sempre la prima
  opzione". Il `Tab` che salta *fra* le zone (`TabNavigation="Once"`, frecce
  *dentro* la zona) è il comportamento progettato; il difetto vero era che **il
  bordo giallo non seguiva le frecce**, quindi la navigazione funzionava ma alla
  cieca — sembrava che il focus non si muovesse affatto. Causa e fix:
  **Trappola 25**. Con il bordo visibile la navigazione a frecce è usabile e la
  questione `Once` vs `Continue` decade.
- **Step 13 — verificato, con un'anomalia singola mai riprodotta**. La
  persistenza di tema e sezione **funziona**: osservata dal vivo su una dozzina
  di cambi sezione (un watcher su `preferenze.json` mentre Luca ciclava con
  `Ctrl+Tab`), ogni indice scritto correttamente e nessuno zero spurio.

  Resta un'unica osservazione iniziale mai spiegata: `TabIniziale: 0` mentre la
  sezione aperta era Impostazioni. **Due ipotesi indagate e SCARTATE con
  evidenza** — non ripercorrerle:
  1. *Un altro punto di salvataggio sovrascrive il campo*: no. Tutti e tre
     (`SalvaTabSeCambiata`, opzioni Home, `ImpostazioniView.Salva`) fanno
     leggi-modifica-riscrivi da disco, quindi nessuno tocca i campi altrui.
  2. *La chiusura della finestra azzera `SelectedIndex` e il handler lo
     persiste*: no. Harness headless che imposta la sezione 5 e poi chiama
     `window.Close()`: il file resta a 5.

  Se ricapita, la strada buona è il watcher sul file mentre si usa l'app:
  mostra l'istante esatto della scrittura sbagliata. **Nessun fix inventato**
  per un difetto che non si riproduce.

### ✅ Localizzazione dell'interfaccia it/en (2026-10-23)

Spec `docs/superpowers/specs/2026-10-06-localizzazione-design.md`, piano
`docs/superpowers/plans/2026-10-06-localizzazione.md`. Task 1–7 fatti su Fedora
il 12–15/10, Task 8 e 10 il 23/10 su Windows. **Task 9 saltato per scelta di
Luca**: nessuna domanda è tradotta, la materia pilota Cybersecurity non esiste.

- **Stringhe**: `wsa.quiz.core/Risorse/Strings.resx` (italiano, base) e
  `Strings.en.resx`, ~250 chiavi. La classe `Strings` è generata da MSBuild in
  `obj/` (niente `Designer.cs` nel repo). Uso: `{x:Static r:Strings.Chiave}` negli
  `.axaml` (con `xmlns:r="clr-namespace:Wsa.Quiz.Core.Risorse;assembly=Wsa.Quiz.Core"`),
  `Strings.Chiave` / `string.Format(Strings.Chiave, …)` nel codice.
  Convenzione `<Area>_<Descrizione>`, testi condivisi con prefisso `Comune_`.
- **Lingua**: è la `CultureInfo` del processo (`Lingue.Applica`). All'avvio
  `App.LinguaIniziale()` legge `PreferenzeUtente.Lingua`; vuota = lingua di sistema
  se supportata, altrimenti inglese. Il cambio da Impostazioni **ricrea la
  `MainView`**, che si riapre su Impostazioni grazie a `TabIniziale`. Il selettore
  è disabilitato durante un quiz. La CLI forza l'italiano.
- **Aggiungere una lingua**: un `Strings.<codice>.resx` con tutte le chiavi + una
  riga in `Lingue.Supportate`. Il test di parità delle chiavi segnala quelle
  mancanti.
- **Domande**: restano in italiano. Il meccanismo per tradurle **c'è già** (Task
  2): file `traduzioni/<lingua>/<materiaId>.json` sovrapposti al caricamento,
  indicizzati per `Domanda.Id`. Manca solo il contenuto, e la cartella
  `traduzioni/` non è ancora inclusa nei csproj (era il Task 9).
- **Regola**: `Materia.Nome`, `Domanda.MateriaNome` e `Domanda.Categoria` sono
  **chiavi canoniche italiane** (statistiche e filtri raggruppano su quelle): si
  traducono solo in visualizzazione, via `EtichetteContenuti.Correnti`. La label
  `"Quizzone"` è anch'essa una chiave.
- **Formati**: date e numeri seguono la cultura; il CSV dell'export usa `;` e
  `,` in italiano, `,` e `.` in inglese.

**Harness su Windows**: `StorageService.CartellaUtenteDefault()` usa
`Environment.GetFolderPath`, che su Windows **ignora le variabili d'ambiente**: il
trucco `XDG_CONFIG_HOME` del piano funziona solo su Linux. Un harness che istanzia
`MainView` su Windows scrive nella `%APPDATA%\WsaQuiz` vera: va lanciato da uno
script che la salva prima e la ripristina dopo.

**Non verificato a mano** su desktop né su Android: harness headless verdi (Core
con 247 chiavi allineate, App 19/19) e satellite `en` presente nell'APK.

### ✅ Bottom-nav Android ridisegnata: pillola flottante (2026-10-23)

Le icone inattive erano bianche al 45% (pensate per il tema scuro) e sul tema
chiaro sparivano; anche l'header era invisibile, per la Trappola 20
(`SystemAccentColorBrush`). Scelta di Luca fra tre mockup: **barra flottante**.

- **Pillola** arrotondata con ombra, staccata dai bordi, colori per tema in
  `ThemeDictionaries` di `MainView.axaml` (hex, mai colori di sistema). Icone
  stile Lucide come tracciati `Path` (nessuna libreria), `Stretch="None"` sul
  viewBox 24. L'attiva sta in una pillola blu `#0078D4`, guidata dalla classe
  `attiva` che `AggiornaNav` accende. Header ora `#0078D4` fisso (anche desktop).
- **Il contenuto scorre sotto la pillola**: `BottomNav` non è più agganciata al
  `DockPanel` ma sta sopra `SwipeHost` nella stessa cella. La sua altezza reale
  (inset Android compreso) diventa la risorsa **`SzRiservaNav`** (+12), impostata
  in `BottomNav.SizeChanged`. Le pagine la usano così:
  - fondo a scroll (Home, Gestione, Statistiche, Sospesi, Cronologia card,
    Impostazioni, drill-down): `Margin`/`Padding` sul **contenuto** dello scroll;
  - riga fissa in fondo (Quiz, Riepilogo, Dettaglio cronologia): `Padding` sulla
    `UserControl` intera, così la riga resta sopra la pillola;
  - l'hint "doppio click" della Cronologia su touch va in alto.
  **Una view nuova va aggiunta a una delle due categorie**, o il suo fondo resta
  coperto.
- Verifica: harness headless a dimensioni S21 (360×800 logici, insets 27/48 px)
  che scorre ogni pagina fino in fondo e confronta il fondo del contenuto con la
  cima della pillola, tutte OK; poi su device (S21, chiaro e scuro, Home in fondo,
  partita con feedback). Trovata e corretta la Trappola 26.
- Osservato una volta: l'app avviata col telefono **bloccato** ha mostrato il
  layout più alto dello schermo (header fuori in alto, barra sotto i tasti);
  dopo un riavvio a telefono sbloccato non si è più visto. Non indagato.
- In release: allegato di `apk-2026-10-23` **sostituito** la sera stessa con
  questa versione (stessa firma, si installa sopra la prima di oggi).

### 💡 Idea aperta — traduzione delle domande

Resta da fare la traduzione dei **contenuti**: 11 materie e 2383 domande. Il
formato e il caricamento sono pronti (vedi sopra). La domanda vera è di prodotto:
tradurre **raddoppia il materiale da mantenere**, perché ogni correzione va fatta
due volte. In più, modificare il testo italiano di una domanda già tradotta ne
cambia l'`Id` (SHA256 del testo), e la traduzione va riallineata: l'avviso
`[ATTENZIONE] ... senza domanda corrispondente` su `Console.Error` lo segnala.

### ✔️ Ex "Da riprendere — chiudere Step 8"
Chiuso il 2026-07-28: vedi la sezione Step 8 sopra e la **Trappola 21**. La
raccomandazione che era rimasta inevasa per due tentativi ("prima la
diagnostica, poi il codice") si è rivelata giusta — ma l'evidenza da stampare
non era solo `e.Source`/`e.Route`/`e.Handled`: era **l'elemento con focus**.

### 🗺️ In roadmap — versione web (WebAssembly) (chiesta il 2026-11-06)

Perché: l'app iOS nativa senza Apple Developer Program (99 $/anno) gira solo
sui device di Luca, con profili da rinnovare ogni 7 giorni. Per farla usare
**ad altri, ovunque** (iPhone, iPad, PC di scuola) la strada gratuita è un head
browser di Avalonia (`Avalonia.Browser`, `net10.0-browser`), ospitato come sito
statico (es. GitHub Pages) e aggiungibile alla schermata Home come una PWA.
Da verificare prima di pianificare:
- **persistenza**: in WASM il file system di .NET è in memoria; cronologia,
  sospesi e preferenze (`StorageService`) vanno salvati altrove (localStorage /
  IndexedDB del browser), quindi i dati restano **in quel browser**;
- dimensione del download iniziale e tempi di avvio su telefono;
- UI touch (`AppEnv.TouchMode`) e safe area su Safari iOS;
- import di contenuti (Gestione contenuti) senza accesso al file system.
L'iOS nativo (spec `2026-11-06-ios-simulatore-design.md`) resta, per studio.

### ✔️ Codice leggibile nelle domande + guida/motore (roadmap 11/11, punto 3) — 2026-11-14

Spec `docs/superpowers/specs/2026-11-12-codice-leggibile-design.md`, piano
`docs/superpowers/plans/2026-11-12-codice-leggibile.md`, branch `feature/codice-leggibile`.
- Parser condiviso `Wsa.Quiz.Core.Services.TestoConCodice` (console + GUI), primo
  progetto di test `wsa.quiz.core.tests` (parser + integrità dati).
- Controllo `Wsa.Quiz.App.Controls.TestoConCodice` in Quiz/Riepilogo/Cronologia/
  Statistiche; font JetBrains Mono NL incluso; colori del codice per tema.
- Dati: Python e C# a blocchi ```lang, backtick in SQL/Frontend/Version Control/REST,
  traduzioni ri-chiavate (`generazione-domande/strumenti/`).
- Motore: `--audit`, controlli sul codice, avvisi "riferimento al corso"; guida riscritta.
- **Verificato**: test automatici (15 .NET + 11 node); controllo disegnato headless
  (Avalonia.Headless + Skia, 400 px) in tema chiaro/scuro e con cambio tema a schermo;
  app iOS avviata nel simulatore (Home ok).
- **Da verificare a mano**: una partita Python su telefono (scroll verticale che parte
  sopra un blocco, scroll orizzontale della riga lunga), domande convertite in inglese,
  Riepilogo/Cronologia/Statistiche con codice. Su questo Mac l'head desktop non parte
  (Avalonia.Native RenderTimer -6661, anche su main) e la build iOS con l'SDK attuale
  chiede `-p:ValidateSceneManifest=false` (MT7199, scene manifest iOS 27).

### ✔️ Repo pubblico ordinato (roadmap 11/11, punto 6) — 2026-11-14/15

Spec `docs/superpowers/specs/2026-11-14-repo-pubblico-design.md`, piano
`docs/superpowers/plans/2026-11-14-repo-pubblico.md`.
- README riscritto in inglese (storia, vibe-coding dichiarato, architettura con
  diagramma Mermaid, generazione domande, materie proprie, build, obiettivi futuri),
  licenza MIT, screenshot in `docs/images/` (web a 1280 px + simulatore iPhone),
  descrizione/topics/homepage su GitHub.
- Documenti di lavoro con l'IA **restano pubblici** (scelta di Luca): ripuliti da
  percorsi personali e identità di firma, rimossi i due LEGGIMI sulle macchine,
  `docs/README.md` in inglese. Storico git non riscritto: lì restano email edu,
  percorsi e l'identità Apple (commit `5027f8f`), accettato.
- Trovato e corretto il **sito web rotto** dal 14/11 (trappola 28).
- **Resta (solo su ok di Luca)**: `.gitignore` per `.remember/ .revisione/
  .superpowers/ .claude/` e `gh repo edit --visibility public`.
- Annotato: con UI in inglese il nome della modalità multi-materia resta
  "Quizzone" (Home/Cronologia/Riepilogo) — da tradurre.
