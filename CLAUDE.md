# Quaestio — Riferimento di progetto

> **Rebrand (2026-07-18):** l'app si chiama ora **Quaestio** (dal latino
> "questione/indagine"). Cambiate SOLO le stringhe user-facing (titolo finestra
> desktop, header, label Android) e l'icona (monogramma "Q" bianco su sfondo
> nero, era una spunta verde). **Tutto ciò che è interno resta invariato**:
> namespace/progetti `Wsa.Quiz.*`, `ApplicationId it.eduits.wsaquiz`, path/nomi
> file, e la cartella dati utente `WsaQuiz` (rinominarla farebbe perdere
> cronologia/preferenze). "WSA" nel resto del documento resta come nome del
> corso (Web Solution Architect), non del brand.

> Documento unico di contesto. Sostituisce e unifica `README_STEP1/2/3.md`,
> `WSA_QUIZ_HANDOFF.md` e le spec/plan in `docs/superpowers/`.

## Cos'è

App di quiz per il corso WsA. Esisteva una versione **console** (C#, .NET 10) che
funziona e che Luca usa per studiare. È stata aggiunta un'app con **interfaccia
grafica** (Avalonia) che condivide la stessa logica e gli stessi dati. Lungo
termine: anche Android/iOS.

Luca sta imparando C# in parallelo a un corso. Il progetto è anche un esercizio
didattico, quindi le scelte privilegiano chiarezza e separazione dei concetti.

## Stato attuale (2026-11-24)

Roadmap Step 1–14 chiusa; app desktop + Android (M1–M3) su `main`; interfaccia
e domande it/en; **12 materie / 2483 domande**. Versione corrente su `main`:
**`2.3.2`** (Android `versionCode 8`, iOS `ApplicationVersion 6`; Laboratorio di
codice C# e Python). Ultima GitHub Release pubblicata: `v2.3.2` (27/11:
margini di sistema con avvio a telefono bloccato; `v2.3.1` stesso giorno: correzioni
al riepilogo ed etichette del Laboratorio in inglese; `v2.3.0`: Laboratorio di codice; APK + desktop Windows/macOS/Linux; prima `v2.2.0` del 26/11:
punti deboli, Statistiche, revisione grafica, fix swipe). Exe desktop portabile in
`%LOCALAPPDATA%\Programs\Quaestio` + collegamento sul desktop. **iOS** su `main`
(head `wsa.quiz.ios`): gira nel simulatore sul Mac, iPhone 17 e iPad 9ª gen.,
provato il 2026-11-09; **iPad fisico** (Apple ID gratuito, Debug e Release
AOT) provato il 2026-11-09; **WebAssembly** su `main` (head `wsa.quiz.browser`,
dati in `localStorage`, deploy automatico su GitHub Pages), provato nel
browser il 2026-11-10.
Restano da verificare a mano: una partita intera, Gestione contenuti,
Statistiche.
**Revisione grafica (25–26/11, in `v2.2.0`):** tavolozza dei 16
inchiostri, font Archivo, angoli vivi, marchio e icone a6-2, barra C2 su telefono,
tab coi colori di sezione su desktop, colore utente su intestazione e titoli.
**Laboratorio di codice (27/11):** 3 livelli × 3 schede per C# e per Python (18 varianti it/en ciascuno,
prevedi l'output → trova il bug → rompi la funzione, review completa al livello
alta), pause separate (`laboratorio_in_pausa.json`) e interruttore globale
Quiz | Laboratorio.
Dettagli e storia: `docs/claude/storia-e-roadmap.md`.

## Stack

- **.NET 10** (target framework — `net10.0` su tutti e tre i csproj)
- **Avalonia 12.0.2** (release del 28 aprile 2026, mobile improvement notevoli
  — buono per il futuro Android/iOS)
- **Tema Fluent** + font **Archivo** (OFL; **JetBrains Mono NL** per il codice, invariato — spec: `docs/superpowers/specs/2026-11-25-revisione-grafica-design.md`)
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
├── domande/                    ← 12 materie (2483 domande)
│   ├── cloudservices/, cpp/, cs/, cybersecurity/, frontend/, infra/,
│   │   nosql/, python/, restfulapi/, sql/, ux/, versioncontrol/
├── laboratorio/                ← schede del Laboratorio di codice (cs/, 3 livelli × 3 schede)
├── traduzioni/                 ← traduzioni overlay di domande e schede (en/, en/laboratorio/cs/)
├── generazione-domande/        ← guida IA, quiz-engine.js e strumenti di supporto
├── generazione-laboratorio/    ← guida IA e motore (esegue il codice, scrive le schede del Laboratorio)
├── wsa.quiz.core/              (class library di logica)
│   ├── Wsa.Quiz.Core.csproj
│   ├── Models/   (RisultatoQuiz, DettaglioRisposta, SessionePausa,
│   │              Materia, Domanda, DomandaPreparata, OpzioniQuiz,
│   │              Statistiche/, Laboratorio/, PreferenzeUtente, SezioneEsclusa)
│   ├── Services/ (QuizService, StorageService, StatisticheService, LaboratorioService,
│   │              ContenutiUtenteService, TestoConCodice, EsportazioneService)
│   └── Risorse/  (Strings.resx, Strings.en.resx)
├── wsa.quiz.core.tests/        (unit test xUnit, parser e integrità dati)
├── wsa.quiz.app/               (libreria UI condivisa Avalonia)
│   ├── Wsa.Quiz.App.csproj
│   ├── App.axaml + App.axaml.cs   ← stili globali, selezione lifetime
│   ├── QuizColors.cs              ← colori condivisi (verde/ambra/rosso)
│   ├── AppEnv.cs                  ← flag TouchMode e safe area
│   ├── Controls/ (TestoConCodice)
│   ├── State/    (SessioneQuiz, SessioneLaboratorio, wrapper observable)
│   ├── Views/    (Home, Quiz, Laboratorio, Riepilogo, Cronologia, Sospesi, Statistiche,
│   │              GestioneContenuti, Impostazioni, overlay dialoghi)
│   └── Assets/   (Fonts monospazio, icone)
├── wsa.quiz.desktop/           (head desktop Avalonia, Windows/Mac/Linux)
│   ├── Wsa.Quiz.Desktop.csproj
│   └── Program.cs
├── wsa.quiz.android/           (head Android, APK)
│   ├── Wsa.Quiz.Android.csproj
│   ├── MainActivity.cs + MainApplication.cs
│   └── Properties/AndroidManifest.xml
├── wsa.quiz.ios/               (head iOS, simulatore Mac / device fisico)
│   ├── Wsa.Quiz.iOS.csproj
│   ├── AppDelegate.cs + Main.cs
│   └── Info.plist
├── wsa.quiz.browser/           (head WebAssembly, deploy GitHub Pages)
│   ├── Wsa.Quiz.Browser.csproj
│   ├── Program.cs
│   └── wwwroot/
└── wsa.quiz.cli/               (head console)
    ├── Wsa.Quiz.Cli.csproj
    ├── Program.cs
    └── Services/ConsoleUI.cs
```

### Naming

- Namespace radice: `Wsa.Quiz.*` (sostituisce il vecchio `QuizFdP.*`)
- Librerie: `Wsa.Quiz.Core` (logica), `Wsa.Quiz.App` (UI condivisa Avalonia)
- Teste per piattaforma: `wsa.quiz.desktop`, `wsa.quiz.android`, `wsa.quiz.ios`, `wsa.quiz.browser`, `wsa.quiz.cli`
- Test: `wsa.quiz.core.tests`
- **Importante**: il nome del progetto console NON è `Wsa.Quiz.Console` —
  collide con `System.Console` e rompe la build. (Trappola 1)
- L'head Android usa namespace `Wsa.Quiz.Droid` (non `Android`, per Trappola 1).

## Build & run

```powershell
dotnet build Wsa.Quiz.sln
dotnet run --project wsa.quiz.cli
dotnet run --project wsa.quiz.desktop
```

`dotnet run` è il comando del **ciclo di sviluppo**: ricompila e riavvia ogni
volta, non è ciò che si consegna. Per produrre eseguibili autonomi o pacchetti per le altre piattaforme (Android, iOS, WebAssembly) vedi `docs/claude/piattaforme-e-distribuzione.md`.

## Dove sono i dettagli (leggere SOLO quando servono)

| File | Leggerlo quando |
|---|---|
| `docs/claude/trappole.md` | prima di toccare la zona di una trappola (indice sotto) |
| `docs/claude/piattaforme-e-distribuzione.md` | build su Fedora/Mac, publish desktop, APK e release, test su device con adb |
| `docs/claude/contenuti-e-dati.md` | aggiungere materie/domande, schede del Laboratorio, storage condiviso console↔GUI, API, modello dati |
| `docs/claude/architettura-gui.md` | lavorare sulla GUI: SessioneQuiz, SessioneLaboratorio, pausa/ripresa, navigazione, stili, colori, decisioni UX |
| `docs/claude/storia-e-roadmap.md` | capire perché una cosa è fatta così (step chiusi, verifiche fatte) |

### Indice delle trappole (dettagli in `docs/claude/trappole.md`)

1. `System.Console` shadowing
2. `Avalonia.Diagnostics` rimosso in v12
3. `RefreshBinding` con `DataContext = null/this`
4. Avalonia desktop = `OutputType=WinExe`
5. Compiled bindings richiedono `x:DataType` ovunque
6. Bottone Fluent disabled + accent color = "scomparso"
7. `DynamicResource` di tipo `Color` nei `Setter` di Style
8. `Key.Enter` == `Key.Return`
10. `BoxShadow` su sticky bar adiacente a `ScrollViewer`
11. Doppio scroll innestato (pagina + pannello con `MaxHeight`)
12. `Foreground` diretto su `Button Classes="accent"` non vince
14. Avalonia 12 — bootstrap Android NON è `AvaloniaMainActivity<App>`
15. `dotnet sln` + solution folder omonima al progetto = `MSB5004`
16. APK Android non prodotto / `XABAA7024` `ZipIOException` — vere cause
18. Swipe fra tab: la gesture manuale "non funziona quasi mai"
20. `DynamicResource SystemAccentColorBrush` INVISIBILE su device Android
21. `ListBox.Focus()` non fa nulla: la ListBox NON è focusable
22. `PublishSingleFile=true` non produce un file singolo
24. La debug key Android e' diversa su ogni macchina
25. `Focus()` senza `NavigationMethod` = focus invisibile
26. Il `Padding` di uno `ScrollViewer` NON entra nell'altezza scorrevole
27. WebAssembly: JsonSerializerIsReflectionDisabled, assembly satellite .resx (loadAllSatelliteResources) e cartella _framework su GitHub Pages (.nojekyll)
28. Deploy web: runner GitHub con .NET 11 → serve `global.json` in CI, altrimenti SkiaSharp non viene ricollegato (sito nero)
29. APK Release da build incrementale = crash all'avvio (`UnsatisfiedLinkError`): pulire `bin`/`obj`
30. Swipe fra sezioni: scroll verticale sposta la pagina di lato → blocco d'asse manuale (`BloccaDerivaOrizzontale`), prova con `adb shell input swipe`
