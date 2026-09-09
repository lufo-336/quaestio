# Quaestio — Riferimento di progetto

> **Rebrand (2026-07-18):** l'app si chiama ora **Quaestio** (dal latino
> "questione/indagine"). Cambiate SOLO le stringhe user-facing (titolo finestra
> desktop, header, label Android) e l'icona (monogramma "Q" bianco su sfondo
> nero, era una spunta verde). **Tutto ciò che è interno resta invariato**:
> namespace/progetti `Wsa.Quiz.*`, `ApplicationId it.eduits.wsaquiz`, path/nomi
> file, e la cartella dati utente `WsaQuiz` (rinominarla farebbe perdere
> cronologia/preferenze). "WSA" nel resto del documento resta come nome del
> corso (Web & Software Architect), non del brand.

> Documento unico di contesto. Sostituisce e unifica `README_STEP1/2/3.md`,
> `WSA_QUIZ_HANDOFF.md` e le spec/plan in `docs/superpowers/`.

## Cos'è

App di quiz per il corso WsA. Esisteva una versione **console** (C#, .NET 10) che
funziona e che Luca usa per studiare. È stata aggiunta un'app con **interfaccia
grafica** (Avalonia) che condivide la stessa logica e gli stessi dati. Lungo
termine: anche Android/iOS.

Luca sta imparando C# in parallelo a un corso. Il progetto è anche un esercizio
didattico, quindi le scelte privilegiano chiarezza e separazione dei concetti.

## Stato attuale

> **Aggiornato al 2026-09-09.** In sintesi: **tutta la roadmap Step 1–14 è
> chiusa** (Step 11 ed export/filtri e Step 14 About+icona+publish chiusi oggi;
> l'installer di Step 14 è escluso per scelta). App desktop + Android su `main`,
> nessun branch feature aperto, **10 materie / 2163 domande**.
>
> Resta una coda di **verifiche a mano mai fatte sulla GUI desktop**: Step 8 con
> tastiera vera, Step 11, Step 13 e Step 14. Il **bug della bottom-nav dopo cold
> start non si riproduce più** su Android 15 (sessione device del 2026-09-09):
> vedi la nota in fondo a "Gestione contenuti — eliminazione per sezione".
>
> Dal 2026-09-09 si sviluppa anche su **Fedora**, dove la solution compila per
> intero (Android incluso) e c'è `adb` — vedi "Sul PC Linux (Fedora)".
>
> Nota: l'APK pubblicato (release `apk-2026-07-21`) è del 21/07 e **non
> contiene** le +23 domande RESTful del commit `c9c1b8e`; va rigenerato alla
> prossima occasione con un device a disposizione.

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

## Stack

- **.NET 10** (target framework — `net10.0` su tutti e tre i csproj)
- **Avalonia 12.0.2** (release del 28 aprile 2026, mobile improvement notevoli
  — buono per il futuro Android/iOS)
- **Tema Fluent** + font **Inter**
- **Nessuna libreria di grafici**: la heatmap statistiche di Step 9 è fatta
  con `ItemsControl`/`WrapPanel`/`Border` nativi. LiveCharts2 era previsto in
  origine, scartato per YAGNI. Se in futuro servirà un line chart vero
  (es. andamento nel tempo) valutarla allora.
- **MVVM no**: code-behind con `INotifyPropertyChanged`. È la strada
  intrapresa, non rifattorizziamo.

## Layout repo

```
wsa.quiz/
├── Wsa.Quiz.sln
├── materie.json                ← editabile, copiato nei bin a ogni build
├── domande/                    ← editabile, idem
│   ├── cpp/, cs/, cybersecurity/, frontend/, infra/, sql/
├── wsa.quiz.core/              (class library)
│   ├── Wsa.Quiz.Core.csproj
│   ├── Models/   (RisultatoQuiz, DettaglioRisposta, SessionePausa,
│   │              Materia, Domanda, DomandaPreparata, OpzioniQuiz,
│   │              Statistiche/{CategoriaStat, DomandaSbagliata, StatistichePerMateria})
│   └── Services/ (QuizService, StorageService, StatisticheService)
├── wsa.quiz.cli/               (console)
│   ├── Wsa.Quiz.Cli.csproj
│   ├── Program.cs
│   └── Services/ConsoleUI.cs
└── wsa.quiz.app/               (Avalonia, tre tab popolate)
    ├── Wsa.Quiz.App.csproj
    ├── Program.cs (Avalonia bootstrap)
    ├── App.axaml + App.axaml.cs   ← style Button.accent / Button.danger /
    │                                Focus adorner globale
    ├── QuizColors.cs              ← colori condivisi (verde/ambra/rosso)
    ├── MainWindow.axaml(.cs)      ← shell con TabControl Home/Cronologia/Sospesi
    ├── State/
    │   ├── ObservableObject.cs
    │   ├── MateriaSelezionabile.cs
    │   ├── CategoriaSelezionabile.cs
    │   ├── RispostaItem.cs
    │   ├── SessioneQuiz.cs               ← state machine event-driven
    │   ├── RisultatoCronologiaItem.cs
    │   ├── SessioneSospesaItem.cs
    │   ├── DettaglioRispostaItem.cs
    │   ├── CategoriaCellaItem.cs         ← cella heatmap statistiche
    │   └── MateriaRigaItem.cs            ← riga heatmap statistiche
    └── Views/
        ├── HomeView.axaml(.cs)
        ├── QuizView.axaml(.cs)
        ├── RiepilogoView.axaml(.cs)
        ├── PlaceholderView.axaml(.cs)    ← ramo "impossibile avviare"
        ├── PausaDialog.axaml(.cs)        ← modale pausa (estratta da QuizView)
        ├── ConfermaDialog.axaml(.cs)     ← dialog conferma generico
        ├── TabellaViewBase.axaml.cs      ← base astratta per Cronologia/Sospesi
        ├── CronologiaView.axaml(.cs)
        ├── CronologiaDettaglioView.axaml(.cs)
        ├── SospesiView.axaml(.cs)
        ├── StatisticheView.axaml(.cs)         ← step 9: tab Statistiche heatmap
        └── CategoriaDettaglioView.axaml(.cs)  ← step 9: drill-down domande sbagliate
```

### Naming

- Namespace radice: `Wsa.Quiz.*` (sostituisce il vecchio `QuizFdP.*`)
- Tre progetti: `Wsa.Quiz.Core` (libreria), `Wsa.Quiz.Cli` (console),
  `Wsa.Quiz.App` (Avalonia)
- **Importante**: il nome del progetto console NON è `Wsa.Quiz.Console` —
  collide con `System.Console` e rompe la build. (Trappola 1)

## Build & run

```powershell
dotnet build Wsa.Quiz.sln
dotnet run --project wsa.quiz.cli
dotnet run --project wsa.quiz.app
```

`dotnet run` è il comando del **ciclo di sviluppo**: ricompila e riavvia ogni
volta, non è ciò che si consegna. Per produrre eseguibili autonomi vedi sotto.
(`dotnet run --project wsa.quiz.app` è stale: dopo M1 l'head desktop è
`wsa.quiz.desktop` — `Wsa.Quiz.App` è una libreria e non si avvia.)

### Sul PC Linux (Fedora)

Dal 2026-09-09 il progetto si compila **interamente** anche su Fedora, head
Android compreso. Niente è nel PATH di sistema: le variabili stanno in
`~/.bashrc`, ma **le shell non interattive non lo leggono**, quindi negli script
vanno esportate a mano.

```bash
export PATH="$HOME/.dotnet:$PATH"            # SDK 10.0.401, installazione utente
export DOTNET_ROOT="$HOME/.dotnet"
export JAVA_HOME="$HOME/.local/jdk/jdk-17.0.20.1+1"   # serve solo all'head Android
export ANDROID_HOME="$HOME/Android/Sdk"
```

Come è stato messo in piedi (da rifare solo su una macchina nuova):
`dotnet workload install android` — se fallisce con "Downloading
microsoft.android.sdk.linux ... failed" è **transitorio**, basta riprovare; poi
il JDK 17 (di sistema c'è solo la JRE, senza `javac`) e infine l'SDK Android con
`dotnet build wsa.quiz.android -t:InstallAndroidDependencies
-p:AndroidSdkDirectory=$HOME/Android/Sdk -p:AcceptAndroidSDKLicenses=True`.
Quest'ultimo porta con sé **`adb`** (`~/Android/Sdk/platform-tools/adb`), quindi
il workflow su device della sezione apposita funziona anche da qui, senza `sudo`.

> **Insidia da orologio**: se l'ora di sistema salta all'indietro (NTP, RTC in
> local time), i binari in `bin/` risultano *più recenti* dei sorgenti appena
> scritti e **MSBuild salta la ricompilazione** — il codice nuovo sembra non
> esistere e si perde tempo a cercare l'errore nel posto sbagliato. Sintomo:
> `CS0246` su un tipo che il file contiene di sicuro. Rimedio: `--no-incremental`
> su `dotnet build` (`dotnet publish` lo rifiuta con `MSB1001: Unknown switch`),
> oppure `touch` sui sorgenti.

## Distribuzione (publish desktop)

L'head desktop è **`wsa.quiz.desktop`** (`net10.0`, `OutputType=WinExe` — vedi
Trappola 4). Per consegnare un eseguibile che parta con doppio click si usa
`dotnet publish`, **non** `dotnet run`.

Tre opzioni, tre concetti distinti:

| Flag | Effetto |
|---|---|
| `-r <RID>` | sceglie piattaforma + CPU di destinazione (Runtime Identifier) |
| `--self-contained` | impacchetta **anche il runtime .NET** → gira su PC senza .NET installato (output più grande, ~70–80 MB) |
| `-p:PublishSingleFile=true` | un solo file invece di una cartella di DLL |

**Importante**: un binario è **specifico per OS+CPU**. Non esiste un unico file
che giri sia su Windows sia su Mac: si pubblica una volta per ogni target. Stesso
sorgente, pacchetti finali diversi. RID utili: `win-x64`, `win-arm64`,
`osx-arm64` (Mac Apple Silicon), `osx-x64` (Mac Intel), `linux-x64`.

> **`PublishSingleFile=true` da solo NON basta** (scoperto 2026-09-09, Step 14).
> Lascia accanto all'eseguibile le librerie native (`libSkiaSharp`,
> `libHarfBuzzSharp`, `av_libglesv2`) e i `.pdb` — su win-x64 sono 101 MB di soli
> simboli. Copiando il solo `.exe` l'app **non parte**. Servono altre due
> proprietà, usate in tutti i comandi qui sotto:
> `-p:IncludeNativeLibrariesForSelfExtract=true` (impacchetta le native) e
> `-p:DebugType=none` (niente `.pdb`).

### Windows (`.exe` singolo autonomo)

```powershell
dotnet publish wsa.quiz.desktop -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none
```

Output: `wsa.quiz.desktop\bin\Release\net10.0\win-x64\publish\Wsa.Quiz.Desktop.exe`
— copiabile e avviabile su qualsiasi Windows x64, senza installare .NET.
(Per i PC ARM, es. Surface recenti, ripetere con `-r win-arm64`.)

Accanto all'eseguibile il publish copia anche `materie.json` e `domande/`, ma
**non servono**: se non li trova nel bin, `MainView.Caricamento` ripiega sulle
risorse incorporate (la stessa strada di Android, vedi M2). Verificato il
2026-09-09 su linux-x64 copiando il **solo** eseguibile in una cartella vuota:
l'app parte, e un harness headless conferma che dalle risorse incorporate
escono 10 materie / 2163 domande.

Dimensioni reali misurate il 2026-09-09 (Release, self-contained, single file
con native incluse): **~101 MB** l'eseguibile linux-x64.

### Mac (eseguibile autonomo)

```powershell
dotnet publish wsa.quiz.desktop -c Release -r osx-arm64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none
```

Output: `...\bin\Release\net10.0\osx-arm64\publish\Wsa.Quiz.Desktop` (Mach-O,
niente `.exe`). Per Mac Intel usare `-r osx-x64`.

**Caveat Mac (importante, dato che si compila da una VM Windows):**
- Il `publish` da Windows **produce** il binario Mac (cross-compile ok), ma quel
  binario **non si può eseguire né firmare da Windows**.
- Su Mac va reso eseguibile (`chmod +x Wsa.Quiz.Desktop`) e di norma **firmato**
  (`codesign`), altrimenti Gatekeeper lo blocca al primo avvio.
- Quello prodotto è un **eseguibile nudo**, non un bundle `.app` cliccabile dal
  Finder: il `.app` (con `Info.plist`, icona) e la notarizzazione vanno fatti
  **su un Mac**. Per il progetto didattico l'eseguibile da terminale è
  sufficiente; il packaging `.app` completo è materiale di Step 14.

> Riduzione dimensioni: `-p:PublishTrimmed=true` rimuove il codice non usato, ma
> il trimming può rompere il caricamento via reflection (Avalonia/`AssetLoader`).
> Da provare e **verificare a runtime** prima di distribuire, non darlo per
> scontato. Vedi anche Step 14 (icona, About, installer).

### Android (APK su GitHub Releases)

L'APK consegnabile **non sta nel repo**: dall'8/09/2026 vive come allegato di una
release su GitHub. Prima era committato in `dist/`, e sei versioni da ~30 MB nella
storia pesavano 75 dei 76 MB che ogni clone si portava dietro; la storia è stata
riscritta per toglierle e `dist/` è ora nel `.gitignore`.

Per pubblicare una versione nuova: `dotnet build wsa.quiz.android -c Release`, poi

```powershell
gh release create apk-AAAA-MM-GG `
  wsa.quiz.android/bin/Release/net10.0-android/it.eduits.wsaquiz-Signed.apk `
  --title "APK Android AAAA-MM-GG" --notes "cosa cambia"
```

Nessun commit: l'APK è un artefatto, non sorgente. Le release stanno su
<https://github.com/lufo-336/quaestio/releases>.

### Test su device fisico (adb)

> Ricostruito dai comandi realmente usati nelle sessioni su device (Samsung
> Galaxy S21 5G, SM-G991B, density 480, nav a 3 tasti). Sostituisce la vecchia
> memory `android-device-testing`, che non esiste più: queste note vivono qui
> perché il repo le porta su ogni macchina, la memory no.

`adb` non è nel PATH: sta in
`C:\Users\<utente>\AppData\Local\Android\Sdk\platform-tools\adb.exe`. Negli
script PowerShell si mette in `$adb` e si invoca con l'operatore di chiamata
(`& $adb ...`), altrimenti il path con spazi non viene interpretato.

**Ciclo tipico** (build → installa → avvia → guarda):

```powershell
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
$apk = "wsa.quiz.android\bin\Release\net10.0-android\it.eduits.wsaquiz-Signed.apk"

& $adb devices -l                       # se vuoto: kill-server + start-server
& $adb shell am force-stop it.eduits.wsaquiz
& $adb install -r $apk                  # -r = reinstalla conservando i dati
& $adb logcat -c                        # pulisci PRIMA di avviare
& $adb shell monkey -p it.eduits.wsaquiz -c android.intent.category.LAUNCHER 1
```

`monkey` è il modo affidabile di lanciare l'app senza conoscere il nome esatto
dell'activity. Per un **cold start vero** (necessario per il bug della
bottom-nav) serve `force-stop` prima, altrimenti si riattacca al processo vivo.

**Screenshot** — due varianti, la prima evita di scrivere sul device:

```powershell
& $adb exec-out screencap -p > shot.png                      # preferita
& $adb shell screencap -p /sdcard/q.png; & $adb pull /sdcard/q.png shot.png
& $adb shell rm /sdcard/q.png                                # pulizia
```

**Input sintetico** (utile per riprodurre il bug bottom-nav senza toccare il
telefono): `& $adb shell input tap <x> <y>` e
`& $adb shell input swipe <x1> <y1> <x2> <y2> <ms>`. Le coordinate sono in **px
fisici**: leggerle con `& $adb shell wm size` e `& $adb shell wm density`.
Lasciare ~1s di `Start-Sleep` prima dello screenshot, o si cattura il frame
di transizione. Se lo schermo è spento: `& $adb shell input keyevent KEYCODE_WAKEUP`.

**Log**: `& $adb logcat -d | Select-String -Pattern "wsaquiz|AndroidRuntime|mono"`.
`-d` scarica e termina (senza, resta in streaming e blocca la sessione).

**Ispezione utile in debug**:

```powershell
& $adb shell dumpsys window                            # insets, RenderScaling
& $adb shell settings get secure navigation_mode       # 0=3 tasti, 2=gesture
& $adb shell run-as it.eduits.wsaquiz ls -R /data/data/it.eduits.wsaquiz/files
```

L'ultimo mostra la cartella utente dell'app (cronologia, sospesi,
`preferenze.json`, `ContenutiUtente/`) — funziona solo su build **Debug**.

**APK Debug installabile a mano** (per iterare senza Release, vedi Trappola 16):

```powershell
dotnet build wsa.quiz.android -c Debug -t:Build `
  -p:EmbedAssembliesIntoApk=true -p:AndroidUseFastDeployment=false
```

Senza queste due proprietà il Debug usa il fast-deployment e l'APK installato
via `adb install` crasha con `monodroid: No assemblies found ... Exiting`.

## Aggiungere materie/domande

Editi `materie.json` e/o aggiungi file in `domande/<materia>/`. I csproj fanno
`<None Include="..\materie.json">` con `Link` per copiarli nei bin a ogni build,
preservando la struttura di sottocartelle. Funziona per console e GUI insieme.

Stato 2026-07-22: **10 materie / 2163 domande** (Cloud Services estesa
100→190 il 19/07, ~70% scenari architetturali, categorie normalizzate a 10;
RESTful API estesa 84→144 il 21/07, taglio misto pratico/teorico, con tre
categorie nuove per l'esercitazione finale TaskFlow: "Autenticazione e JWT",
"Query, paginazione e caching", "Frontend, fetch e CORS"; poi 144→167 il 22/07
con domande di ripasso). Ripartizione per file: cs 652, sql 248, versioncontrol
236, frontend 210, cpp 200, cloud 190, restfulapi 167, ux 100, infra 50+50,
cybersecurity 60.

**Attenzione**: i csproj includono `..\domande\**\*.json` (AvaloniaResource +
None). **Qualsiasi** `.json` lasciato in `domande/` — anche temporaneo o
intermedio — finisce nei bin e nell'APK e viene enumerato da `ElencaJson` come
file di domande (schema sbagliato → caricamento rotto). Script e file di
lavoro vanno nello scratchpad, mai in `domande/`.

Nota: dall'8/09/2026 `docs/` **è nel repo**. Spec, piani e note di lavoro
viaggiano con git come tutto il resto; la copia su `G:\Il mio Drive\_claude\quaestio\docs`
è un residuo dell'assetto precedente e va rimossa. Il progetto vive in
`<home>\dev\quaestio` — fuori da Drive — e l'unica cosa che resta sul Drive è la
cartella `memory` di Claude, agganciata da una junction. Vedi `_claude\LEGGIMI.md`
sul Drive per l'assetto completo.

## Storage condiviso console ↔ GUI

`StorageService` ha due costruttori:
- `StorageService(string cartella)` — legacy
- `StorageService(string cartellaDati, string cartellaUtente)` — quello in uso

`cartellaDati` = output del bin (dove ci sono i JSON copiati). `cartellaUtente`
= cartella per cronologia + sospesi, condivisa fra tutti gli eseguibili.
Default cross-platform via helper statico `StorageService.CartellaUtenteDefault()`:

- Windows: `%APPDATA%\WsaQuiz`
- macOS: `~/Library/Application Support/WsaQuiz`
- Linux: `~/.config/WsaQuiz`

### API

- `storage.CaricaCronologia()` → `List<RisultatoQuiz>` (migrazione lazy: i
  record vecchi senza `Id` ne ricevono uno e il file viene riscritto, una
  volta sola — idempotente)
- `storage.CaricaPause()` → `List<SessionePausa>`
- `storage.SalvaRisultato(RisultatoQuiz)` — append; genera `Guid` se `Id` vuoto
- `storage.SalvaPausa(SessionePausa)` — upsert per `SessioneId`
- `storage.EliminaPausa(string sessioneId)`
- `storage.EliminaRisultato(string id)`
- `storage.SvuotaCronologia()`

### Modello dati — note

- `Domanda.Id` è uno **SHA256 troncato a 12 char hex** calcolato su
  `testo|risposte|indice_corretta`. Stabile a riordini, cambia se cambia il
  contenuto. Utile per matching cronologia/sospesi.
- `RisultatoQuiz.Id` è un **Guid stringa** generato in `SalvaRisultato`.
- `OpzioniQuiz.Categorie` usa la chiave `"NomeMateria/Categoria"` per
  supportare multi-materia.
- `QuizService.PreparaDomanda` mescola le risposte e ricalcola l'indice della
  corretta — usare sempre questo, non leggere `Domanda.RispostaCorretta`
  direttamente.
- `RisultatoQuiz.Dettagli` contiene la sequenza completa di `DettaglioRisposta`
  (id, testo, risposta data, corretta, spiegazione, tentativi). Da step 7 ha
  anche 3 campi additivi (`RisposteShufflate`, `IndiceCorrettoShufflato`,
  `IndiceDataShufflato`) che il `CronologiaDettaglioView` ignora ma che
  servono al `QuizView` in view-mode per ricostruire i 4 bottoni colorati
  anche dopo pausa/ripresa. Default vuoto/-1 → backward compatible coi record
  JSON vecchi.

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

### 🚧 Port Android — M3 — UX mobile (in corso, 2026-06-18)
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

Formato del file scelto dall'**estensione nel file picker** (`FileTypeChoices`
CSV/JSON), così non serve un controllo dedicato. Verificato con due harness
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
- **Step 8 — tastiera**: navigazione fra sezioni e `Invio` **funzionano**.
- **Icona finestra**: su GNOME/Wayland **non si vede**, ma *non è un bug del
  codice*: `xprop` conferma che `_NET_WM_ICON` è impostata (128×128) sulla
  finestra. GNOME non mostra icone nella barra del titolo e per dash/alt-tab
  associa la finestra a un file `.desktop` tramite `WM_CLASS`
  (`Wsa.Quiz.Desktop`), che qui non esiste. Su Windows, la piattaforma di
  riferimento, l'icona si vede. Per vederla anche su Linux servirebbe
  installare un `.desktop` con `StartupWMClass=Wsa.Quiz.Desktop` — packaging
  Linux, oggi fuori scope.
- **`Tab` nella Home — comportamento progettato, ma contestato dall'utente**:
  `Tab` salta *fra* le 4 zone (`KeyboardNavigation.TabNavigation="Once"`) e le
  frecce si muovono *dentro* la zona. Luca si aspettava che `Tab` scorresse le
  singole materie e categorie e lo ha segnalato come difetto. **Decisione
  aperta**: lasciare `Once` (Tab veloce, frecce per i dettagli) o passare a
  `Continue` (Tab attraversa ogni checkbox: più atteso, ma con 10 materie e
  decine di categorie diventa lungo). Da chiudere provando le frecce.
- **Step 13 — anomalia non spiegata**: a fine sessione `preferenze.json` aveva
  `TemaScuro: true` (giusto) ma `TabIniziale: 0` mentre la sezione aperta era
  Impostazioni (indice 5). **Non riproducibile**: un harness headless che
  imposta `Tabs.SelectedIndex = 5` salva correttamente `TabIniziale: 5`, e tutti
  e tre i punti di salvataggio (`SalvaTabSeCambiata`, opzioni Home,
  `ImpostazioniView.Salva`) fanno leggi-modifica-riscrivi da disco, quindi
  nessuno può azzerare i campi degli altri. Causa **ignota**: da osservare dal
  vivo alla prossima occasione, prima di toccare il codice.

### 💡 Idea aperta — app in inglese e traduzione delle domande

**Non è mai stata pianificata**: al 2026-09-09 non esiste in nessuno step, spec o
piano; l'italiano è monolingua per default implicito, non per decisione presa.
Annotata qui il 2026-09-09 perché Luca l'ha chiesta a voce e non c'era traccia
da nessuna parte.

Due problemi distinti, da non confondere:
- **UI**: ~poche centinaia di stringhe hard-coded negli `.axaml` e nei
  code-behind. Richiederebbe risorse localizzate (`.resx` o dizionari Avalonia) e
  un giro su ogni view — meccanico ma esteso.
- **Contenuti**: 2163 domande con testo, 4 risposte e spiegazione ciascuna. Qui
  la vera domanda non è tecnica ma di prodotto: tradurre significa **raddoppiare
  il materiale da mantenere** (una correzione va fatta due volte), e servirebbe
  decidere se la lingua è una proprietà della materia, della singola domanda, o
  un file parallelo. Da valutare anche l'impatto su `Domanda.Id`, che è uno
  SHA256 del testo: tradurre cambia l'id e quindi rompe il matching con
  cronologia e sospesi esistenti.

Se si affronta, è **architetturale**: merita spec propria, non un ritocco.

### ✔️ Ex "Da riprendere — chiudere Step 8"
Chiuso il 2026-07-28: vedi la sezione Step 8 sopra e la **Trappola 21**. La
raccomandazione che era rimasta inevasa per due tentativi ("prima la
diagnostica, poi il codice") si è rivelata giusta — ma l'evidenza da stampare
non era solo `e.Source`/`e.Route`/`e.Handled`: era **l'elemento con focus**.

## Trappole già scoperte (non rifare)

1. **`System.Console` shadowing**: usare un namespace `Wsa.Quiz.Console`
   rompe tutte le chiamate `Console.WriteLine`. Il compilatore risolve
   `Console` come sotto-namespace di se stesso prima di guardare in
   `System`. Soluzione: `Wsa.Quiz.Cli`.
2. **`Avalonia.Diagnostics` rimosso in v12**: il package non esiste più. Il
   rimpiazzo è `AvaloniaUI.DiagnosticsSupport` (separato), opzionale. Non
   includerlo finché non serve.
3. **`RefreshBinding` con `DataContext = null/this`**: anti-pattern usato
   nella WPF iniziale. Cancellava la selezione delle ListView a ogni
   interazione. Soluzione: INPC corretto su tutto, `DataContext` settato solo
   nel costruttore.
4. **Avalonia desktop = `OutputType=WinExe`** anche su Mac/Linux. Sembra
   Windows-specifico ma è il flag standard.
5. **Compiled bindings richiedono `x:DataType` ovunque**
   (`<AvaloniaUseCompiledBindingsByDefault>true</...>`). Sui `UserControl`
   root e su ogni `<DataTemplate>`. Senza, il build dà errori cripti tipo
   "could not resolve property X on type System.Object".
6. **Bottone Fluent disabled + accent color = "scomparso"** *(risolto step 3)*:
   in Fluent, un bottone con `Background="{DynamicResource SystemAccentColorBrush}"`
   quando va in `IsEnabled=false` diventa molto chiaro su sfondo chiaro e
   sembra assente. Risolto con style globale `Button.accent` in `App.axaml`
   con i 4 stati definiti sul template
   (`/template/ ContentPresenter#PART_ContentPresenter`).
7. **`DynamicResource` di tipo `Color` nei `Setter` di Style**: nella XAML
   "diretta" Avalonia converte automaticamente `Color → SolidColorBrush`
   quando assegna a `Background`/`Foreground`/`BorderBrush`, ma nei `Setter`
   di Style la conversione è meno affidabile e può dare errori a runtime.
   Per i Setter cromatici, usare `DynamicResource SystemAccentColorBrush`
   (versione `Brush`) o un valore esplicito hex. Gli stati `:disabled` di
   `Button.accent`/`Button.danger` usano hex literali (`#D6D6D6` su `#7A7A7A`).
8. **`Key.Enter` == `Key.Return`** *(scoperta step 4)*: in `Avalonia.Input.Key`
   sono lo stesso enum constant. Metterli su due `case` distinti dello stesso
   `switch` è un errore `CS0152`. Tenerne **uno solo** (per coerenza usiamo
   `Key.Enter`).
9. **`SystemBaseHighColor` come `Foreground` su background hardcoded chiaro
   = illeggibile in dark mode** *(scoperta step 6)*: i brush "Color" di
   sistema si invertono a seconda del tema. Se il `Background` del
   contenitore è hardcoded chiaro (es. feedback box `#E5F4EC` / `#F9E7E6`),
   in dark mode il foreground diventa quasi-bianco. Soluzione: `Foreground`
   hex esplicito (es. `#1F1F1F`) quando il background è anch'esso hex.
10. **`BoxShadow` su sticky bar adiacente a `ScrollViewer`** *(scoperta step 6)*:
    aggiungere `BoxShadow="0 -2 8 0 #28000000"` su una `Border` adiacente al
    viewport del `ScrollViewer` proietta l'ombra di 8px che copre
    visivamente il bordo del viewport. Effetto "ultimo elemento tagliato".
    Soluzione: niente shadow, basta `BorderThickness` di 1px sul lato che separa.
11. **Doppio scroll innestato (pagina + pannello con `MaxHeight`)** *(scoperta
    step 6)*: avere un `ScrollViewer` esterno alla pagina + un `ScrollViewer
    MaxHeight=240` su un pannello interno è confondente. Regola: un solo
    scroll dove possibile; se serve un cap interno, deve essere giustificato
    da una vera differenza di volume di dati.
12. **`Foreground` diretto su `Button Classes="accent"` non vince**
    *(scoperta step 7)*: assegnare `Foreground="#1F1F1F"` come attributo non
    sovrascrive il colore del testo se il bottone ha `Classes="accent"`. Lo
    style globale colpisce il `ContentPresenter#PART_ContentPresenter`, che
    ha priorità sul `Foreground` dell'attributo. Soluzione: classe locale
    aggiuntiva (es. `Classes="accent prossima"`) e style con selector
    `Button.prossima /template/ ContentPresenter` che setti
    `TextBlock.Foreground`. Stesso pattern già usato per
    `Button.risposta.corretta/sbagliata` in `QuizView.axaml`.
13. **`OnKeyDown` override su `UserControl` non basta per intercettare
    frecce/Invio quando ci sono `ScrollViewer` o `ListBox` sopra**
    *(scoperta step 8; **la vera causa dello Step 8 era però un'altra: vedi
    Trappola 21**)*: l'override
    viene chiamato in fase di **bubbling** ed è registrato dalle class-handler
    interne con `handledEventsToo=false`. Se un controllo intermedio (es.
    `ScrollViewer` per `↑/↓`, o `ListBoxItem` per `Enter`) marca l'evento come
    `Handled` durante il bubble, l'override non riceve mai la notifica.
    Tentativo originale step 8: convertire ad `AddHandler(KeyDownEvent, ...,
    RoutingStrategies.Tunnel)` per intercettare in fase di capture (top-down)
    — non ha sbloccato niente. Secondo tentativo (PR #2, Facundo Fanti): stesso
    `AddHandler` ma con `handledEventsToo: true` per ricevere anche gli eventi
    già marcati `Handled`. Verifica pratica: **ancora non funzionava
    perfettamente**. **Epilogo (2026-07-28)**: il pattern `Tunnel +
    handledEventsToo` è corretto e funziona (la diagnostica headless lo mostra
    intercettare Invio prima della ListBox); ciò che mancava era il **focus**
    sulla riga — Trappola 21. Quindi questa trappola resta valida come nozione
    sul routing, ma non era la causa dello Step 8.
14. **Avalonia 12 — bootstrap Android NON è `AvaloniaMainActivity<App>`**
    *(scoperta porting M1, 2026-06-18)*: il plan/spec del porting erano scritti
    per l'API Avalonia 11.x dove `MainActivity : AvaloniaMainActivity<App>` con
    `CustomizeAppBuilder`. In **Avalonia 12.0.2** `AvaloniaMainActivity` è
    **non generico** → `error CS0308`. La scelta dell'`App` e
    `CustomizeAppBuilder` sono migrate in una sottoclasse di
    `Android.App.Application`: `MainApplication : AvaloniaAndroidApplication<TApp>`
    (con `[Application]`, ctor `(IntPtr, JniHandleOwnership)`, `WithInterFont()`
    dentro `CustomizeAppBuilder`). `MainActivity` resta nudo
    (`: AvaloniaMainActivity`). Vedi `wsa.quiz.android/MainApplication.cs`.
15. **`dotnet sln` + solution folder omonima al progetto = `MSB5004`**
    *(scoperta porting M1)*: lo schema "solution folder per head" introdotto nel
    refactor creava folder lowercase (`wsa.quiz.app`/`wsa.quiz.android`/
    `wsa.quiz.desktop`) con lo stesso nome **case-insensitive** dei progetti
    reali (`Wsa.Quiz.App`...). `dotnet build Wsa.Quiz.sln` falliva con
    "due progetti denominati wsa.quiz.app". Le solution folder sono solo
    cosmetiche (organizzazione VS): rimosse, solution flat a 5 progetti.
16. **APK Android non prodotto / `XABAA7024` `ZipIOException` — vere cause**
    *(porting M1, **risolto** 2026-06-18)*: tre cose concatenate, e un grosso
    depistaggio. **Causa risolutiva**: mancava **`<OutputType>Exe</OutputType>`**
    nel csproj dell'head → il progetto era trattato come **libreria** Android
    (emetteva `Wsa.Quiz.Android.aar`) e NON impacchettava alcun APK. Il template
    Avalonia/Android la include sempre; il piano M1 l'aveva omessa. Aggiunta →
    APK prodotto. Altri due punti correlati:
    - **Debug = fast-deployment** (`EmbedAssembliesIntoApk=false`): non produce
      un APK autonomo senza un device. Installato a mano con `adb install` crasha
      con `monodroid: No assemblies found ... Exiting`. Per un APK installabile su
      dispositivo: **`dotnet build -c Release`** (embed + sign in un colpo solo) →
      `bin/Release/net10.0-android/it.eduits.wsaquiz-Signed.apk` (~29 MB,
      firmato con la Android Debug key, verificato con `apksigner`). Installa
      con `adb install <file>-Signed.apk`. Per testare modifiche **in Debug**
      installandolo via adb (senza IDE): `dotnet build -c Debug -t:Build
      -p:EmbedAssembliesIntoApk=true -p:AndroidUseFastDeployment=false`.
    - **NON invocare `-t:SignAndroidPackage` come target standalone**: dà
      `XACAF7009 "Assembly compression info not found"` perché
      `__CompressedAssembliesInfo` è registrato in-memory da un target a monte e
      vive solo nella **stessa** invocazione MSBuild. Produrre l'APK nel chain
      normale di build, non con un target isolato.
    - **Depistaggio**: il log *"Falling back to LibZipSharp because we are not
      running on .NET 6+"* è **innocuo** — il check del SDK è
      `FrameworkDescription != ".NET"`, sempre vero (la descrizione contiene la
      versione), quindi LibZipSharp è il path zip normale **per tutti**. Non era
      quello il problema; il `ZipIOException "errore lettura ...apk"` era solo il
      sintomo dell'APK base mai creato (per i punti sopra). Non sprecare tempo su
      `dotnet workload update` o su `_AndroidUseLibZipSharp`.
17. **Safe-area Android: `IInsetsManager.SafeAreaPadding` di Avalonia riporta
    `bottom=0` su device reale** *(M3 parte 3, **risolto** 2026-06-26 su Galaxy
    S21)*: con edge-to-edge forzato (targetSdk 36) la bottom nav finiva **sotto i
    tasti Android** perché Avalonia non vede l'inset della nav bar (`SafeAreaPadding`
    → top=80, **bottom=0**). In più `TopLevel.RenderScaling` **varia** (1 al boot,
    3 a regime), quindi un fattore di conversione costante oscillava tra barra
    coperta e barra gigante. Soluzione: leggere le insets **native** nella
    `MainActivity` (`View.IOnApplyWindowInsetsListener` sulla decor view →
    `WindowInsets.GetInsetsIgnoringVisibility(SystemBars())`, in **px fisici**:
    status 80, nav 224) e passarle a `MainView` via `AppEnv.SystemBarInsetsPx`.
    Conversione px→unità di layout = `px / (ScalaTouch × RenderScaling)`, con
    `RenderScaling` letto **a runtime a ogni apply** (robusto al variare). NON
    consumare le insets: `return v.OnApplyWindowInsets(insets)`.
18. **Swipe fra tab: la gesture manuale "non funziona quasi mai"** *(M3 parte 3,
    **risolto** 2026-06-26)*: intercettare `PointerMoved` in tunnel per cambiare
    pagina è inaffidabile — il `ScrollGestureRecognizer` dello ScrollViewer interno
    cattura il tocco e i move non arrivano (lo ammetteva il commento del vecchio
    codice). Soluzione: niente gesture manuale, niente `Carousel`. Uno **ScrollViewer
    orizzontale** (`SwipeHost`) con le pagine affiancate e
    `HorizontalSnapPointsType=MandatorySingle`: lo scroll nativo dà trascinamento col
    dito + inerzia + snap (feeling pager), affidabile perché è il gesto nativo. Assi
    ortogonali (host solo orizzontale, pagine solo verticale) → nessun conflitto.
    Tap-nav animato via `ScrollViewer.Offset`.
19. **`RadioButton.GroupName` condiviso = esclusività a livello di PROCESSO, non
    solo dentro il singolo controllo/finestra** *(scoperta Step 12/import JSON,
    **risolto** 2026-07-04)*: `StatisticheView` aveva due `RadioButton` con
    `GroupName="FiltroPeriodo"` + `IsChecked` bindato a due proprietà
    mutuamente inverse (`ModalitaTutto` / `!ModalitaTutto`). Innocuo finché
    esisteva **una sola istanza** della view. Dopo aver aggiunto in Task 9 un
    reload completo dei dati (`MainView.Caricamento()` ricostruisce tutte le
    tab, compresa una **nuova** `StatisticheView`, ogni volta che cambia
    qualcosa in "Gestione contenuti"), costruire una seconda istanza registra i
    suoi due `RadioButton` nello **stesso gruppo globale** di quelli della
    prima istanza (i gruppi non sono scoped alla finestra/UserControl per
    controlli non ancora attaccati all'albero visivo). Con `DataContext`
    diversi e binding inverse, le due coppie entrano in un loop infinito di
    check/uncheck reciproco: CPU al 100%, "non risponde", poi crash dopo
    circa 50s — sintomo scambiato inizialmente per un bug di rientranza nel
    `Dispatcher.UIThread.Post` del reload (che invece era corretto). Riprodotto
    deterministicamente headless con due `new StatisticheView()` di fila
    (nessun dato/dispatcher coinvolto): la seconda si blocca. **Soluzione**:
    rimuovere `GroupName`, le due binding inverse bastano da sole a garantire
    l'esclusività. **Regola generale**: qualunque `GroupName` di `RadioButton`
    in una view che può essere istanziata più di una volta nel processo
    (ricreata a ogni reload, aperta in più tab/finestre, ecc.) è a rischio;
    preferire binding inverse espliciti (`{Binding !Prop}`) senza `GroupName`,
    oppure un `GroupName` reso univoco per istanza a runtime.

20. **`DynamicResource SystemAccentColorBrush` INVISIBILE su device Android**
    *(scoperto 2026-07-19, verificato su Samsung Galaxy S21 5G)*: lo stile
    `Button.accent` in `App.axaml` usava `{DynamicResource SystemAccentColorBrush}`
    (+ `...Light1Brush`/`...Dark1Brush`) per lo sfondo dello stato **abilitato**.
    Su questo device il resource si risolve in un colore **non visibile**
    (bianco/trasparente): risultato → **ogni bottone accent abilitato era
    invisibile** (testo bianco su sfondo bianco), incluso "Avvia quiz",
    "Prossima domanda", "Conferma" e gli import di "Gestione contenuti". Era la
    vera causa del "non vedo il pulsante per importare" (il bottone c'era ed era
    cliccabile, solo invisibile). Lo stato `:disabled` si vedeva perché già in
    hex esplicito (`#D6D6D6`/`#7A7A7A`) — stesso motivo per cui i `danger`
    (hex) non hanno mai avuto il problema. **Soluzione**: sfondi accent in hex
    esplicito (`#0078D4` default, `#2B90DE` hover, `#005FB0` pressed), come già
    fanno `danger` e lo stato disabled. Vale sia desktop sia Android. **Regola**:
    per i colori che DEVONO essere visibili in una UI cross-platform, non
    affidarsi a `SystemAccentColor*` (dipende da piattaforma/tema di sistema);
    usare hex espliciti. **Verificato su device**: fix + rebuild Release +
    `adb install -r`, "Avvia quiz" e import ora blu pieni; file picker Android
    (SAF) confermato apribile end-to-end (mai testato prima su device fisico).

21. **`ListBox.Focus()` non fa nulla: la ListBox NON è focusable** *(causa radice
    dello Step 8, scoperta 2026-07-28 con harness headless)*: in Avalonia il
    focus di una lista appartiene ai suoi **`ListBoxItem`** (come al click del
    mouse), non al contenitore. `ListaSessioni.Focus()` / `ListaPause.Focus()`
    restituivano `false` e lasciavano il focus sullo `UserControl`, che è un
    **antenato** della lista: dato che il routing di `KeyDown` va dalla radice
    all'elemento con focus, gli eventi **non passavano mai dalla ListBox** e le
    frecce non potevano muovere la selezione. Sintomo ingannevole: Invio/Canc
    sembravano funzionare (il handler di view usa `SelectedItem`, forzato a 0 dal
    fallback nel `GotFocus`) ma agivano **sempre sulla riga 0**. **Soluzione**:
    `TabellaViewBase.FocusRigaSelezionata(ListBox)` — seleziona la prima riga se
    serve e chiama `Focus()` sul container `ContainerFromIndex(SelectedIndex)`,
    con retry `Dispatcher.Post(..., DispatcherPriority.Loaded)` se il container
    non è ancora realizzato. **Regola generale**: prima di dare la colpa al
    routing di un evento tastiera, **stampare l'elemento con focus** — due
    tentativi (Tunnel, poi `handledEventsToo: true`) sono falliti proprio perché
    cercavano nel routing un problema che era di focus. Corollario: `Focus()`
    restituisce `bool`, **controllarlo**.

22. **`PublishSingleFile=true` non produce un file singolo** *(scoperta Step 14,
    2026-09-09)*: le librerie native (`libSkiaSharp`, `libHarfBuzzSharp`,
    `av_libglesv2`) restano accanto all'eseguibile, più i `.pdb` — su win-x64
    erano 101 MB di soli simboli su 204 MB di cartella. Copiando il solo `.exe`
    l'app **non parte**, e CLAUDE.md diceva il contrario da mesi. Servono
    `-p:IncludeNativeLibrariesForSelfExtract=true` e `-p:DebugType=none`. Vedi il
    riquadro in "Distribuzione (publish desktop)".

23. **Binding TwoWay su `ComboBox.SelectedItem` + `ItemsSource.Clear()` = scelta
    persa** *(scoperta Step 11, 2026-09-09)*: svuotare la collezione fa scattare
    il binding all'indietro con `null`, quindi il campo dietro la property viene
    azzerato **prima** che si possa rimetterci il valore. Chi ripopola la lista
    deve salvarsi la selezione *prima* della `Clear()` e ripristinarla dopo,
    altrimenti la tendina resta in bianco a ogni ricarica. Trovato da un harness
    headless, non a occhio: da UI sembrava solo "a volte la tendina è vuota".

24. **La debug key Android e' diversa su ogni macchina** *(scoperta 2026-09-09
    passando a Linux)*: l'APK Release e' firmato con la Android Debug key
    **locale**, quindi un APK costruito su una macchina **non si installa sopra**
    a uno costruito su un'altra — `adb install -r` fallisce con
    `INSTALL_FAILED_UPDATE_INCOMPATIBLE: signatures do not match`. Il rifiuto e'
    innocuo (non tocca i dati), ma l'unico modo di procedere e' **disinstallare**,
    e con l'app se ne vanno cronologia, sospesi, preferenze e contenuti importati
    del telefono. Successo il 09/09 passando da Windows a Fedora: dati sul device
    persi per scelta consapevole. **Se un giorno l'APK va distribuito sul serio**,
    generare una keystore stabile tenuta fuori dal repo e firmarci tutte le build,
    da qualunque macchina.

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
   - **Rigenerare e pubblicare l'APK**: quello su GitHub è di luglio e gli
     mancano 23 domande RESTful, Step 11/13/14 e i fix successivi.
4. Nota PR #2 (Facundo Fanti, 14 mag 2026): refactor che ha estratto
   `TabellaViewBase<TItem>`, `PausaDialog`, `ConfermaDialog`, fixato il bug
   timestamp UTC tornando a `DateTime.Now` ovunque, e tentato (senza
   successo) di chiudere Step 8 con `handledEventsToo: true`.
