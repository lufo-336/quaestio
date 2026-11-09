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

## Stato attuale (2026-11-09)

Roadmap Step 1–14 chiusa; app desktop + Android (M1–M3) su `main`; interfaccia
e domande it/en; **11 materie / 2383 domande**. Ultima release APK:
`apk-2026-11-06` (domande in inglese). Exe desktop portabile in
`%LOCALAPPDATA%\Programs\Quaestio` + collegamento sul desktop. **iOS** su `main`
(head `wsa.quiz.ios`): gira nel simulatore sul Mac, iPhone 17 e iPad 9ª gen.,
provato il 2026-11-09; prossimo passo iPad fisico; **WebAssembly** in roadmap.
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
