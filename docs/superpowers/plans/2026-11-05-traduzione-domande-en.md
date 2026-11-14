# Traduzione in inglese delle domande — Implementation Plan

> **Per chi esegue:** i Task 1 e 2 sono codice, pensati per il modello locale
> (`esegui-piano.sh` di `<cartella llm-locale>`). Il Task 3 è
> traduzione: lo fa lo script `traduci_domande.py` chiamando direttamente il
> modello locale, una domanda alla volta, non un agente. I passi usano le
> caselle (`- [ ]`).

**Goal:** tutte le 2383 domande delle 11 materie disponibili in inglese quando
l'interfaccia è in inglese, su desktop e su Android.

**Architecture:** il meccanismo c'è già (piano `2026-10-06-localizzazione.md`,
Task 2): `StorageService` con `LinguaContenuti = "en"` sovrappone
`traduzioni/en/<materiaId>.json` alle domande italiane, indicizzando per
`Domanda.Id` (hash del testo italiano). Questo piano riprende il Task 9 di
quel piano (saltato a ottobre), lo adatta a Windows e lo estende da una materia
pilota a tutte: includere `traduzioni/` nei build, un harness che verifica i
file di traduzione, la traduzione stessa.

**Tech Stack:** .NET 10, Avalonia 12, i JSON della banca domande; per la
traduzione llama-server con Qwen3.6-35B-A3B in locale.

**Revisione rispetto al Task 9 originale** (2026-11-05, Opus):

- percorsi Linux (`~/...`) e trucco `XDG_CONFIG_HOME` → l'harness
  usa la cartella di lavoro e una cartella utente temporanea, così funziona
  anche su Windows senza toccare `%APPDATA%\WsaQuiz`;
- l'harness esterno `$H/core` col suo `Verifica.Check` non esiste più: Task 1
  ne crea uno piccolo e autonomo in `_verify/` (non va committato);
- lo scheletro e la traduzione non li scrive un agente (un JSON di centinaia di
  voci gli riempirebbe il contesto): li fanno due strumenti di
  `<cartella llm-locale>\scripts` (`scheletro/` e
  `traduci_domande.py`), con controlli automatici su ogni domanda;
- da una materia pilota a tutte le 11, Cybersecurity per prima.

## Global Constraints

- **Le chiavi non si traducono**: `Domanda.Id`, i nomi canonici italiani delle
  materie e delle categorie (chiavi in `categorie`), `RispostaCorretta`.
- **Stesso numero e stesso ordine delle risposte** dell'originale.
- **Codice invariato**: testo tra backtick, blocchi di codice, nomi di
  funzioni/classi/comandi restano identici.
- **Le sigle restano sigle** (GDPR, NIS2, DORA, XSS, VPN, SQL, HTTP…). Nelle
  spiegazioni i riferimenti alle lettere delle risposte (A, B, C, D) non
  cambiano.
- JSON in UTF-8 senza escape degli accenti, indentato di 2 spazi.
- La CLI resta in italiano (non include `traduzioni/`).
- `_verify/` è un harness di lavoro: non va committato.

---

### Task 1: Harness di verifica delle traduzioni

**Files:**
- Create: `_verify/_verify.csproj`
- Create: `_verify/Program.cs`

**Interfaces:**
- Consumes: `StorageService(string cartellaDati, string cartellaUtente)`, `StorageService.LinguaContenuti`, `StorageService.Etichette`, `TraduzioneMateria`.
- Produces: `dotnet run --project _verify` (dalla radice del repo) stampa una riga per materia ed esce con 0 se le traduzioni presenti sono coerenti; con `-- --tutte` esce con 0 solo se **ogni** domanda di ogni materia ha la sua traduzione.

- [ ] **Step 1: Il progetto**

`_verify/_verify.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\wsa.quiz.core\Wsa.Quiz.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Il programma**

`_verify/Program.cs`:

```csharp
using System.Text.Json;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Services;

// Verifica delle traduzioni in inglese: per ogni materia confronta il caricamento
// italiano con quello inglese. Con --tutte esige che ogni domanda sia tradotta.
bool tutte = args.Contains("--tutte");
string repo = Directory.GetCurrentDirectory();
if (!File.Exists(Path.Combine(repo, "materie.json")))
{
    Console.WriteLine("Lanciare dalla radice del repo (dove sta materie.json).");
    return 2;
}

// Gli avvisi di StorageService vanno su Console.Error: li raccolgo per contarli.
var avvisi = new StringWriter();
var erroreOriginale = Console.Error;
Console.SetError(avvisi);

string utente = Path.Combine(Path.GetTempPath(), "quaestio-verifica-utente");
var it = new StorageService(repo, utente);
var en = new StorageService(repo, utente) { LinguaContenuti = "en" };

int problemi = 0, totale = 0, totaleTradotte = 0;
foreach (var m in it.CaricaMaterie())
{
    var domIt = it.CaricaDomandeMateria(m);
    var domEn = en.CaricaDomandeMateria(en.CaricaMaterie().Single(x => x.Id == m.Id));
    string percorso = Path.Combine(repo, "traduzioni", "en", m.Id + ".json");

    var tr = File.Exists(percorso)
        ? JsonSerializer.Deserialize<TraduzioneMateria>(File.ReadAllText(percorso))
        : null;
    var idItaliani = domIt.Select(d => d.Id).ToHashSet();
    int nelFile = tr?.Domande.Keys.Count(idItaliani.Contains) ?? 0;
    int orfane = tr?.Domande.Keys.Count(k => !idItaliani.Contains(k)) ?? 0;

    bool idUguali = domEn.Select(d => d.Id).SequenceEqual(domIt.Select(d => d.Id));
    bool risposteCoerenti = domEn.Zip(domIt).All(p =>
        p.First.Risposte.Count == p.Second.Risposte.Count
        && p.First.RispostaCorretta == p.Second.RispostaCorretta);
    string nome = en.Etichette.Materia(m.Nome);

    bool ok = idUguali && risposteCoerenti && orfane == 0 && (!tutte || nelFile == domIt.Count);
    if (!ok) problemi++;
    totale += domIt.Count;
    totaleTradotte += nelFile;
    Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {m.Id,-15} {nelFile,4}/{domIt.Count,-4} tradotte  orfane {orfane}  nome \"{nome}\"");
}

Console.SetError(erroreOriginale);
var righeAvviso = avvisi.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
    .Where(r => r.Contains("[ATTENZIONE]")).ToList();
foreach (var r in righeAvviso) Console.WriteLine("AVVISO " + r.Trim());
if (righeAvviso.Count > 0) problemi++;

Console.WriteLine($"\nTotale: {totaleTradotte}/{totale} domande tradotte, {righeAvviso.Count} avvisi.");
Console.WriteLine(problemi == 0 ? "TUTTO OK" : $"{problemi} PROBLEMI");
return problemi == 0 ? 0 : 1;
```

- [ ] **Step 3: Esegui**

Run: `dotnet run --project _verify`
Expected: 11 righe `OK` con `0/N tradotte` (non c'è ancora nessuna traduzione), `TUTTO OK`, uscita 0.

---

### Task 2: Cartella `traduzioni/` nei build

**Files:**
- Modify: `wsa.quiz.app/Wsa.Quiz.App.csproj`
- Modify: `wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj`

**Interfaces:**
- Consumes: `AvaloniaResourceFonteDati` (Android: risorse incorporate), `FileSystemFonteDati` (desktop: cartella accanto all'eseguibile).
- Produces: `traduzioni/**/*.json` incorporata nell'app (Android) e copiata nell'output del desktop.

- [ ] **Step 1: App (risorse incorporate, il percorso di Android)**

In `wsa.quiz.app/Wsa.Quiz.App.csproj`, subito dopo la riga

```xml
    <AvaloniaResource Include="..\domande\**\*.json" Link="Assets\domande\%(RecursiveDir)%(Filename)%(Extension)" />
```

aggiungi:

```xml
    <AvaloniaResource Include="..\traduzioni\**\*.json" Link="Assets\traduzioni\%(RecursiveDir)%(Filename)%(Extension)" />
```

- [ ] **Step 2: Desktop (file accanto all'eseguibile)**

In `wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj`, subito dopo il blocco

```xml
    <None Include="..\domande\**\*.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <Link>domande\%(RecursiveDir)%(Filename)%(Extension)</Link>
    </None>
```

aggiungi:

```xml
    <None Include="..\traduzioni\**\*.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <Link>traduzioni\%(RecursiveDir)%(Filename)%(Extension)</Link>
    </None>
```

La CLI **non** la include (resta in italiano).

- [ ] **Step 3: Build**

Run: `dotnet build wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj -c Debug`
Expected: `Errori: 0` (con `traduzioni/` ancora vuota il glob non trova niente, ed è normale).

---

### Task 3: Traduzione (strumenti, non agente)

**Files:**
- Create: `traduzioni/en/<materiaId>.json` per le 11 materie

- [ ] **Step 1: Scheletri con gli Id veri**

```sh
dotnet run --project <cartella llm-locale>/scripts/scheletro -p:Core=<worktree>/wsa.quiz.core/Wsa.Quiz.Core.csproj -- <worktree> <cartella scheletri>
```

Expected: 11 righe e `totale: 2383`.

- [ ] **Step 2: Traduci, materia per materia (Cybersecurity per prima)**

Server llama-server con `-np 3 -c 49152` (tre richieste in parallelo), poi per
ogni materia:

```sh
PARALLELI=3 python -I <cartella llm-locale>/scripts/traduci_domande.py materia <scheletri>/<id>.json <worktree>/traduzioni/en/<id>.json
```

Lo script traduce nome e categorie in una chiamata, poi ogni domanda con lo
schema JSON imposto (stesso numero di risposte) e la scarta se: un campo è
vuoto, il codice tra backtick è cambiato, o il testo sembra ancora italiano
(fino a 3 tentativi; se fallisce la domanda resta in italiano e viene
segnalata). Salva dopo ogni domanda e, se interrotto, riprende da dove era.

- [ ] **Step 3: Verifica**

Run: `dotnet run --project _verify -- --tutte`
Expected: 11 righe `OK` con `N/N tradotte`, nessun avviso, `TUTTO OK`.

---

### Task 4: Revisione e verifica finale

- [ ] **Step 1: Revisione di Opus a campione**: per ogni materia alcune domande
  scelte a caso (e tutte quelle con codice o segnalate dallo script), lette in
  italiano e in inglese; correzioni puntuali nei file di traduzione.
- [ ] **Step 2: Prova manuale (Luca)**: app desktop in inglese, qualche quiz in
  più materie (testo, risposte, spiegazione, filtri per categoria con i nomi
  tradotti); poi in italiano, per verificare che non sia cambiato niente.
- [ ] **Step 3: Android**: `dotnet build wsa.quiz.android -c Release` e prova
  sul telefono in inglese.
- [ ] **Step 4: Commit** (non `_verify/`):

```bash
git add traduzioni wsa.quiz.app/Wsa.Quiz.App.csproj wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj docs/superpowers/plans/2026-11-05-traduzione-domande-en.md
git commit -m "feat(l10n): domande in inglese per tutte le materie"
```
