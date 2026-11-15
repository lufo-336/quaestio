# Quaestio — Riferimento di progetto

> **Rebrand (2026-07-18):** l'app si chiama ora **Quaestio** (dal latino
> "questione/indagine"). Cambiate SOLO le stringhe user-facing (titolo finestra
> desktop, header, label Android) e l'icona (monogramma "Q" bianco su sfondo
> nero, era una spunta verde). **Tutto ciò che è interno resta invariato**:
> namespace/progetti `Wsa.Quiz.*`, `ApplicationId it.eduits.wsaquiz`, path/nomi
> file, e la cartella dati utente `WsaQuiz` (rinominarla farebbe perdere
> cronologia/preferenze). "WSA" nel resto del documento resta come nome del
> corso (Web Solutions Architect), non del brand.

> Documento unico di contesto. Sostituisce e unifica `README_STEP1/2/3.md`,
> `WSA_QUIZ_HANDOFF.md` e le spec/plan in `docs/superpowers/`.

## Cos'è

App di quiz per il corso WsA. Esisteva una versione **console** (C#, .NET 10) che
funziona e che Luca usa per studiare. È stata aggiunta un'app con **interfaccia
grafica** (Avalonia) che condivide la stessa logica e gli stessi dati. Lungo
termine: anche Android/iOS.

Luca sta imparando C# in parallelo a un corso. Il progetto è anche un esercizio
didattico, quindi le scelte privilegiano chiarezza e separazione dei concetti.

## Stato attuale (2026-11-11)

Roadmap Step 1–14 chiusa; app desktop + Android (M1–M3) su `main`; interfaccia
e domande it/en; **11 materie / 2383 domande**. Ultima release APK:
`apk-2026-11-06` (domande in inglese). Exe desktop portabile in
`%LOCALAPPDATA%\Programs\Quaestio` + collegamento sul desktop. **iOS** su `main`
(head `wsa.quiz.ios`): gira nel simulatore sul Mac, iPhone 17 e iPad 9ª gen.,
provato il 2026-11-09; **iPad fisico** (Apple ID gratuito, Debug e Release
AOT) provato il 2026-11-09; **WebAssembly** su `main` (head `wsa.quiz.browser`,
dati in `localStorage`, deploy automatico su GitHub Pages), provato nel
browser il 2026-11-10.
Restano da verificare a mano: una partita intera, Gestione contenuti,
Statistiche. Dettagli e storia: `docs/claude/storia-e-roadmap.md`.

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
├── domande/                    ← 11 materie (2383 domande)
│   ├── cloudservices/, cpp/, cs/, cybersecurity/, frontend/, infra/,
│   │   python/, restfulapi/, sql/, ux/, versioncontrol/
├── traduzioni/                 ← traduzioni overlay delle domande (en/)
├── generazione-domande/        ← guida IA, quiz-engine.js e strumenti di supporto
├── wsa.quiz.core/              (class library di logica)
│   ├── Wsa.Quiz.Core.csproj
│   ├── Models/   (RisultatoQuiz, DettaglioRisposta, SessionePausa,
│   │              Materia, Domanda, DomandaPreparata, OpzioniQuiz,
│   │              Statistiche/, PreferenzeUtente, SezioneEsclusa)
│   ├── Services/ (QuizService, StorageService, StatisticheService,
│   │              ContenutiUtenteService, TestoConCodice, EsportazioneService)
│   └── Risorse/  (Strings.resx, Strings.en.resx)
├── wsa.quiz.core.tests/        (unit test xUnit, parser e integrità dati)
├── wsa.quiz.app/               (libreria UI condivisa Avalonia)
│   ├── Wsa.Quiz.App.csproj
│   ├── App.axaml + App.axaml.cs   ← stili globali, selezione lifetime
│   ├── QuizColors.cs              ← colori condivisi (verde/ambra/rosso)
│   ├── AppEnv.cs                  ← flag TouchMode e safe area
│   ├── Controls/ (TestoConCodice)
│   ├── State/    (SessioneQuiz state machine, wrapper observable)
│   ├── Views/    (Home, Quiz, Riepilogo, Cronologia, Sospesi, Statistiche,
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
| `docs/claude/contenuti-e-dati.md` | aggiungere materie/domande, storage condiviso console↔GUI, API, modello dati |
| `docs/claude/architettura-gui.md` | lavorare sulla GUI: SessioneQuiz, pausa/ripresa, navigazione, stili, colori, decisioni UX |
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
