# Codice leggibile nelle domande — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Blocchi di codice e codice inline leggibili in tutte le schermate e piattaforme, dati con una convenzione unica, guida e motore di generazione aggiornati (con audit dei banchi esistenti).

**Architecture:** Un parser puro in `Wsa.Quiz.Core` trasforma una stringa in segmenti (testo / inline / blocco). La console e un nuovo controllo Avalonia `TestoConCodice` (code-behind, `ContentControl`) usano lo stesso parser. I dati Python vengono convertiti una volta sola con uno script che ricalcola gli ID e ri-chiava le traduzioni; un test C# custodisce l'integrità dei dati. Il motore JS espone funzioni testabili con `node --test` e aggiunge `--audit`.

**Tech Stack:** .NET 10, Avalonia 12.0.2, xUnit, JetBrains Mono (OFL), Node.js (`node --test`), Python 3 (script one-shot), Antigravity CLI `agy` per il lavoro di volume.

**Spec:** `docs/superpowers/specs/2026-11-12-codice-leggibile-design.md`

## Global Constraints

- Convenzione dati: blocco = ```` ```<linguaggio> ```` a capo, codice, a capo ```` ``` ````; inline = `` `codice` ``. Linguaggi ammessi: `python`, `csharp`, `cpp`, `c`, `sql`, `html`, `css`, `js`, `bash`, `json`, `http`.
- Blocco: font monospazio, **mai a capo**, scroll orizzontale, **numeri di riga in entrambi i temi**, colori da `ThemeDictionaries` Light/Dark (chiaro = grigio chiaro tipo mockup A; scuro = editor scuro tipo mockup B).
- Inline: font monospazio + sfondo, backtick non visibili, in domanda, risposte e spiegazione.
- Niente colorazione della sintassi. Niente migrazione cronologia/statistiche.
- Schema JSON delle domande invariato (`id, categoria, domanda, risposte, rispostaCorretta, spiegazione`).
- Code-behind con `INotifyPropertyChanged`, niente MVVM. Namespace `Wsa.Quiz.*`.
- Trappole da rispettare: 5 (`x:DataType`), 7 (niente `DynamicResource` di tipo `Color` nei Setter), 11 (scroll innestati), 12 (`Foreground` sui bottoni accent), 15 (niente solution folder), 20 (niente `SystemAccentColorBrush`).
- Testo malformato (backtick non chiuso) → mostrato com'è, mai eccezioni.
- Commit **e push** alla fine di ogni task (preferenza di Luca).
- Lavoro di volume (Task 4, 5, 6b, 7, 8) passa da `agy` (skill `antigravity`); Claude rivede il diff, compila, esegue i test.

## Review Focus

1. Scroll verticale con il dito che parte sopra un blocco di codice (Android/iOS/web su touch): la pagina deve scorrere, il blocco scorre solo in orizzontale → verifica manuale Task 9.
2. Cambio tema chiaro↔scuro con una domanda a schermo: riquadro, numeri e chip cambiano colore senza riaprire la domanda → Task 4 (binding a risorse dinamiche) + verifica Task 9.
3. Chip inline dentro un bottone risposta verde/rosso (dopo la risposta): resta leggibile → Task 4 (sfondo chip semitrasparente) + verifica Task 9.
4. Riga lunghissima e blocco di 30+ righe su schermo stretto: la card non si allarga oltre lo schermo, compare lo scroll orizzontale solo nel blocco → Task 4 (`MaxWidth` implicito dal genitore, `ScrollViewer` orizzontale) + verifica Task 9.
5. Testo con backtick singolo, fence non chiuso, `\r\n` (contenuti importati da Gestione contenuti) → test `Analizza_*Malformato*` e `Analizza_CrLf` nel Task 1.

---

## File Structure

| File | Responsabilità |
|---|---|
| `wsa.quiz.core/Services/TestoConCodice.cs` (new) | parser puro: stringa → `IReadOnlyList<SegmentoTesto>` |
| `wsa.quiz.core.tests/` (new, xUnit) | test parser + integrità dati |
| `wsa.quiz.cli/Services/ConsoleUI.cs` (mod) | usa il parser di Core, rimuove `Tokenizza`/`Segmento` |
| `wsa.quiz.app/Assets/Fonts/` (new) | `JetBrainsMono-Regular.ttf`, `JetBrainsMono-Bold.ttf`, `OFL.txt` |
| `wsa.quiz.app/App.axaml` (mod) | risorse `FontCodice` + brush Light/Dark del codice |
| `wsa.quiz.app/Controls/TestoConCodice.cs` (new) | controllo che disegna i segmenti |
| `wsa.quiz.app/Views/{Quiz,Riepilogo,CronologiaDettaglio,CategoriaDettaglio}View.axaml` (mod) | sostituzione dei `TextBlock` di testo/risposte/spiegazione |
| `generazione-domande/strumenti/converti_codice.py` (new) | conversione one-shot Python + C# + traduzioni |
| `domande/python/python_domande.json`, `domande/cs/csharp_domande_unificate.json`, `traduzioni/en/python.json`, `traduzioni/en/cs.json` (mod) | dati convertiti (Task 6) |
| `domande/{sql,frontend,versioncontrol,restfulapi}/*.json` + traduzioni en (mod) | backtick sul codice "nudo" (Task 6b) |
| `generazione-domande/quiz-engine.js` (mod), `generazione-domande/quiz-engine.test.js` (new) | motore + test |
| `generazione-domande/GUIDA_GENERAZIONE_DOMANDE.md` (mod) | guida riscritta |
| `docs/claude/contenuti-e-dati.md`, `docs/claude/architettura-gui.md` (mod) | documentazione |

---

### Task 1: Parser `TestoConCodice` in Core + progetto di test

**Files:**
- Create: `wsa.quiz.core/Services/TestoConCodice.cs`
- Create: `wsa.quiz.core.tests/Wsa.Quiz.Core.Tests.csproj`, `wsa.quiz.core.tests/TestoConCodiceTests.cs`
- Modify: `Wsa.Quiz.sln` (`dotnet sln add wsa.quiz.core.tests/Wsa.Quiz.Core.Tests.csproj` — **senza** solution folder, trappola 15), `wsa.quiz.core/Wsa.Quiz.Core.csproj` (`<InternalsVisibleTo Include="Wsa.Quiz.Core.Tests" />`)

**Interfaces:**
- Produces:
  ```csharp
  namespace Wsa.Quiz.Core.Services;
  public enum TipoSegmento { Testo, Inline, Blocco }
  public sealed record SegmentoTesto(TipoSegmento Tipo, string Testo, string? Linguaggio = null);
  public static class TestoConCodice
  {
      public static IReadOnlyList<SegmentoTesto> Analizza(string? testo);
      public static string SenzaMarcatori(string? testo); // testo visibile: backtick e riga ```lang tolti
  }
  ```
  Per `Blocco`: `Testo` = righe del codice unite da `\n`, senza la riga del fence, senza `\r`, senza `\n` iniziale/finale; `Linguaggio` = parola dopo ```` ``` ```` o `null`. I `\n` immediatamente prima e dopo un blocco non finiscono nei segmenti `Testo` adiacenti (il blocco "va a capo da solo").

- [ ] **Step 1: crea il progetto xUnit** (`dotnet new xunit -o wsa.quiz.core.tests -n Wsa.Quiz.Core.Tests -f net10.0`, riferimento a `wsa.quiz.core`, aggiunto alla sln). Run: `dotnet test wsa.quiz.core.tests` → PASS (test di template, poi cancellato).
- [ ] **Step 2: scrivi i test che falliscono** in `TestoConCodiceTests.cs`:
  - `Analizza_SoloTesto` — `"Ciao"` → `[Testo "Ciao"]`
  - `Analizza_Inline` — ``"Cosa fa `int.Parse(\"42\")` in C#?"`` → `[Testo "Cosa fa ", Inline "int.Parse(\"42\")", Testo " in C#?"]`
  - `Analizza_BloccoConLinguaggio` — `"Che cosa stampa?\n\n```python\ndef f():\n    return 1\n```"` → `[Testo "Che cosa stampa?", Blocco "def f():\n    return 1" lang "python"]` (indentazione preservata)
  - `Analizza_BloccoSenzaLinguaggio` — Linguaggio `null`
  - `Analizza_TestoDopoBlocco` — `"A\n```c\nx;\n```\nB"` → `[Testo "A", Blocco "x;" "c", Testo "B"]`
  - `Analizza_BacktickSingoloMalformato` — ``"costo `5"`` → `[Testo "costo `5"]`
  - `Analizza_FenceNonChiusoMalformato` — `"A\n```python\nx = 1"` → `[Testo "A", Blocco "x = 1" "python"]` (fino a fine testo, niente eccezione)
  - `Analizza_CrLf` — stesso input di `BloccoConLinguaggio` con `\r\n` → stesso risultato
  - `Analizza_NullOVuoto` — `null` e `""` → lista vuota
  - `SenzaMarcatori_TogliBacktick` — ``"usa `x` e\n```python\ny\n```"`` → `"usa x e\ny"`
- [ ] **Step 3:** Run `dotnet test wsa.quiz.core.tests` → FAIL (tipo `TestoConCodice` non esiste).
- [ ] **Step 4: implementa** `TestoConCodice` partendo dall'algoritmo di `ConsoleUI.Tokenizza` (scansione lineare; prima ```` ``` ````, poi `` ` ``), normalizzando `\r\n`→`\n` all'inizio e togliendo i `\n` di bordo attorno ai blocchi.
- [ ] **Step 5:** Run `dotnet test wsa.quiz.core.tests` → PASS (10 test).
- [ ] **Step 6: commit + push** — `feat(core): parser TestoConCodice per codice inline e a blocchi`

### Task 2: Console sul parser di Core

**Files:**
- Modify: `wsa.quiz.cli/Services/ConsoleUI.cs:67-160` (`ScriviTestoConCodice` usa `TestoConCodice.Analizza`; cancellare `Segmento` e `Tokenizza`)

**Interfaces:**
- Consumes: `TestoConCodice.Analizza`, `SegmentoTesto`, `TipoSegmento` (Task 1)

- [ ] **Step 1:** sostituisci il tokenizzatore locale; per `Blocco` stampa ogni riga come `prefisso + "{n,2} │ " + riga` (numero in `C_Soft`, codice in `C_Codice`), una riga vuota prima del blocco; `Testo` → `StampaConWrap`, `Inline` → `Scrivi(..., C_Codice)` come oggi.
- [ ] **Step 2:** Run `dotnet build Wsa.Quiz.sln` → 0 errori. Run `dotnet run --project wsa.quiz.cli`, avvia un quiz C# e trova una domanda con ```` ``` ```` (la console mostra il blocco numerato e niente backtick).
- [ ] **Step 3: commit + push** — `refactor(cli): usa il parser di Core per il codice`

### Task 3: Font monospazio e risorse colore

**Files:**
- Create: `wsa.quiz.app/Assets/Fonts/JetBrainsMono-Regular.ttf`, `JetBrainsMono-Bold.ttf`, `OFL.txt` (da release ufficiale GitHub JetBrains/JetBrainsMono, cartella `fonts/ttf`)
- Modify: `wsa.quiz.app/Wsa.Quiz.App.csproj` (`<AvaloniaResource Include="Assets\Fonts\**" />` se non già coperto), `wsa.quiz.app/App.axaml`

**Interfaces:**
- Produces (risorse in `App.axaml`):
  - `FontFamily x:Key="FontCodice"` = `avares://Wsa.Quiz.App/Assets/Fonts#JetBrains Mono`
  - In `ThemeDictionaries`, `SolidColorBrush` con chiavi e valori di partenza:

  | Chiave | Light | Dark |
  |---|---|---|
  | `CodiceSfondo` | `#F3F4F6` | `#1E1F24` |
  | `CodiceBordo` | `#DADDE2` | `#2E3038` |
  | `CodiceTesto` | `#1F1F1F` | `#E6E6E6` |
  | `CodiceNumeri` | `#8A8F99` | `#6C7080` |
  | `ChipSfondo` | `#1A000000` | `#26FFFFFF` |

- [ ] **Step 1:** aggiungi file e risorse.
- [ ] **Step 2:** Run `dotnet build Wsa.Quiz.sln` → 0 errori; avvio `dotnet run --project wsa.quiz.desktop` senza eccezioni di risorsa all'avvio.
- [ ] **Step 3: commit + push** — `feat(app): font JetBrains Mono e colori del codice per tema`

### Task 4: Controllo `TestoConCodice` (agy)

**Files:**
- Create: `wsa.quiz.app/Controls/TestoConCodice.cs`

**Interfaces:**
- Consumes: `TestoConCodice.Analizza` (Task 1), risorse del Task 3.
- Produces:
  ```csharp
  namespace Wsa.Quiz.App.Controls;
  public class TestoConCodice : ContentControl
  {
      public static readonly StyledProperty<string?> TestoProperty;   // "Testo"
      public static readonly StyledProperty<double> InterlineaProperty; // "Interlinea", default double.NaN
      public string? Testo { get; set; }
      public double Interlinea { get; set; }
  }
  ```
  Uso in XAML: `xmlns:c="using:Wsa.Quiz.App.Controls"` → `<c:TestoConCodice Testo="{Binding DomandaTesto}" FontSize="..." Foreground="..." FontWeight="..."/>`. `FontSize`, `Foreground`, `FontWeight`, `FontStyle` del controllo si propagano al testo normale per ereditarietà.

- [ ] **Step 1: implementa.** Al cambio di `Testo` ricostruisci `Content` = `StackPanel` (Spacing 8):
  - sequenza di `Testo`/`Inline` consecutivi → un `TextBlock` (`TextWrapping=Wrap`, `LineHeight=Interlinea`) con `Inlines`: `Run` per il testo; per l'inline `Run` con `FontFamily=FontCodice`, `FontSize = FontSize * 0.9`, `Background` legato a `ChipSfondo`;
  - `Blocco` → `Border` (`Background=CodiceSfondo`, `BorderBrush=CodiceBordo`, `BorderThickness=1`, `CornerRadius=6`, `Padding=10,8`) → `ScrollViewer` (`HorizontalScrollBarVisibility=Auto`, `VerticalScrollBarVisibility=Disabled`) → `Grid` `Auto,*`: colonna 0 `TextBlock` numeri `1..n` uniti da `\n`, `TextAlignment=Right`, `Foreground=CodiceNumeri`, `Margin=0,0,10,0`; colonna 1 `TextBlock` codice `TextWrapping=NoWrap`, `Foreground=CodiceTesto`; entrambi `FontFamily=FontCodice`, `FontSize = FontSize * 0.87`, stessa `LineHeight` (`FontSize * 1.45`) per allinearli.
  - Le brush vanno legate con `GetResourceObservable(chiave)` (non lette una volta), così il cambio tema aggiorna i colori (Review Focus 2).
- [ ] **Step 2:** Run `dotnet build Wsa.Quiz.sln` → 0 errori, 0 nuovi warning di binding.
- [ ] **Step 3: commit + push** — `feat(app): controllo TestoConCodice`

### Task 5: Sostituzione nelle view (agy)

**Files:**
- Modify: `wsa.quiz.app/Views/QuizView.axaml:158-161` (domanda, `Interlinea="22"`), `:177-179` (testo risposta nel bottone), `:204-209` (`SpiegazioneTesto`, mantieni `IsVisible`)
- Modify: `wsa.quiz.app/Views/RiepilogoView.axaml:127-156` (`TestoDomanda`, `RispostaData`, `RispostaCorretta`, `Spiegazione`)
- Modify: `wsa.quiz.app/Views/CronologiaDettaglioView.axaml:145-184` (stessi campi)
- Modify: `wsa.quiz.app/Views/CategoriaDettaglioView.axaml:52-66` (`Testo`, riga "corretta", `Spiegazione`)

**Interfaces:**
- Consumes: `Wsa.Quiz.App.Controls.TestoConCodice` (Task 4).

- [ ] **Step 1:** sostituisci ogni `TextBlock` con quei binding con `c:TestoConCodice`, mantenendo `FontSize`, `FontWeight`, `FontStyle`, `Foreground`, `Margin`, `IsVisible`. Dove il `TextBlock` usa `<Run>` con un prefisso fisso (es. `Statistiche_CorrettaPrefisso`), tieni il prefisso in un `TextBlock` a parte in uno `StackPanel` orizzontale/`Grid Auto,*` e metti il valore nel controllo.
- [ ] **Step 2:** Run `dotnet build Wsa.Quiz.sln` → 0 errori. Avvia desktop, gioca un quiz C# di 5 domande: domande con ```` ``` ```` mostrano il riquadro numerato; inline senza backtick in domanda/risposte/spiegazione.
- [ ] **Step 3: commit + push** — `feat(app): codice leggibile in quiz, riepilogo, cronologia e statistiche`

### Task 6: Conversione dati + test di integrità

**Files:**
- Create: `generazione-domande/strumenti/converti_codice.py`
- Create: `wsa.quiz.core.tests/IntegritaDatiTests.cs`
- Modify: `domande/python/python_domande.json`, `domande/cs/csharp_domande_unificate.json`, `traduzioni/en/python.json`, `traduzioni/en/cs.json`

**Interfaces:**
- Consumes: `TestoConCodice.Analizza` (Task 1), `StorageService.GeneraId(Domanda)` (internal, già esistente: SHA256 di `testo|risposte unite da \u0001|indice`, primi 6 byte hex minuscolo).

- [ ] **Step 1: test che falliscono** in `IntegritaDatiTests.cs` (leggono i JSON dal repo, risalendo da `AppContext.BaseDirectory` fino a `Wsa.Quiz.sln`):
  - `NessunFenceNonChiuso` — per ogni domanda di ogni banco in `domande/`, il numero di ```` ``` ```` in domanda/risposte/spiegazione è pari.
  - `Python_CodiceSempreInBlocco` — ogni domanda Python che contiene `"\n\n"` ha almeno un segmento `Blocco` con `Linguaggio == "python"`.
  - `Blocchi_HannoLinguaggio` — ogni `Blocco` in `domande/` ha `Linguaggio` fra quelli dei Global Constraints.
  - `Traduzioni_NessunaChiaveOrfana` — per ogni `traduzioni/en/<id>.json`, ogni chiave di `domande` è l'ID (`GeneraId`) di una domanda del banco italiano corrispondente (`materie.json` → cartella).
  Run `dotnet test wsa.quiz.core.tests` → FAIL su `Python_CodiceSempreInBlocco` e `Blocchi_HannoLinguaggio`; `Traduzioni_NessunaChiaveOrfana` deve **già passare** (conferma che la formula ID dello script è giusta prima di cambiare nulla).
- [ ] **Step 2: scrivi lo script** `converti_codice.py` (Python 3, solo libreria standard, idempotente):
  - Python: se `domanda` contiene `"\n\n"` e non contiene ```` ``` ````, `domanda = testa + "\n\n```python\n" + codice + "\n```"` dove `testa` = prima del primo `\n\n`, `codice` = il resto. Stessa trasformazione sul `domanda` inglese.
  - Python, risposte delle domande convertite: avvolgi in backtick le risposte che **non** combaciano con `^[A-ZÀ-Ý][a-zà-ÿ]+ [a-zà-ÿ]` (prosa) e non contengono già `` ` ``; stessa scelta (stessi indici) sulle risposte inglesi. Lo script stampa l'elenco completo delle risposte avvolte e non avvolte per revisione.
  - C#: ogni riga che è esattamente ```` ``` ```` e apre un blocco diventa ```` ```csharp ````, sia in italiano sia in inglese.
  - Ri-chiavatura: calcola `id_vecchio` sulla domanda prima della modifica e `id_nuovo` dopo, con la stessa formula di `GeneraId`; sposta la voce di traduzione da `id_vecchio` a `id_nuovo`. Errore e uscita se un `id_vecchio` non è trovato o se due domande producono lo stesso `id_nuovo`.
  - Scrive i JSON con `ensure_ascii=False`, indentazione uguale ai file originali (4 spazi domande; controlla la traduzione e mantieni la sua).
- [ ] **Step 3:** Run `python3 -I generazione-domande/strumenti/converti_codice.py`; rivedi l'elenco delle risposte e correggi a mano nei JSON i casi sbagliati (prosa avvolta o codice non avvolto), sempre in coppia it/en. Se correggi a mano una risposta, la chiave di traduzione va ricalcolata: rilancia lo script (idempotente) dopo aver corretto solo il file italiano e l'inglese con gli stessi indici.
- [ ] **Step 4:** Run `dotnet test wsa.quiz.core.tests` → PASS (tutti). Run `git diff --stat` → toccati solo i 4 JSON + script.
- [ ] **Step 5:** avvia desktop in inglese (Impostazioni → lingua) e apri un quiz Python: le domande convertite sono in inglese (nessuna ricaduta in italiano).
- [ ] **Step 6: commit + push** — `data: codice Python e C# nel formato a blocchi, traduzioni ri-chiavate`

### Task 6b: Backtick nelle materie con codice "nudo" (agy)

SQL, Frontend, Version Control (e 1 domanda REST) hanno codice scritto come testo normale o tra apici (misura 2026-11-12, solo testo domanda: SQL ~54, Frontend ~23, VC ~34, REST 1). Lavoro di giudizio, non meccanico.

**Files:**
- Modify: `generazione-domande/strumenti/converti_codice.py` (nuova modalità `--richiava`)
- Modify: `wsa.quiz.core.tests/IntegritaDatiTests.cs`
- Modify: `domande/{sql,frontend,versioncontrol,restfulapi}/*.json`, `traduzioni/en/{sql,frontend,versioncontrol,restfulapi}.json`

**Interfaces:**
- Consumes: formula ID e ri-chiavatura del Task 6; `TestoConCodice.Analizza` (Task 1).
- Produces: `python3 -I generazione-domande/strumenti/converti_codice.py --richiava <materiaId> <banco_vecchio.json>` — confronta banco vecchio (es. estratto con `git show HEAD:<percorso>`) e attuale **per posizione** (stesso numero e ordine di domande, errore altrimenti), sposta ogni chiave di `traduzioni/en/<materiaId>.json` da ID vecchio a ID nuovo.

- [ ] **Step 1: test che fallisce** `NessunCodiceNudo` in `IntegritaDatiTests.cs`: per domanda e risposte di queste materie, considerando **solo i segmenti `Testo`** (codice già tra backtick escluso), nessuna corrispondenza con:
  - sql: `\b(SELECT|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|TRUNCATE|JOIN|WHERE|GROUP BY|ORDER BY)\b` (maiuscolo, case-sensitive)
  - frontend: `<[a-z][a-z0-9]*[ >/]|=>|document\.|addEventListener|console\.`
  - versioncontrol: `\bgit [a-z-]+`
  - restfulapi: `\b(GET|POST|PUT|DELETE|PATCH) /`
  Run `dotnet test wsa.quiz.core.tests` → FAIL (elenca le domande).
- [ ] **Step 2:** aggiungi `--richiava` allo script.
- [ ] **Step 3 (agy, una materia per sessione):** in domanda, risposte **e spiegazione** racchiudi tra backtick il codice (istruzioni SQL, nomi di tabella/colonna dentro un'istruzione, tag e attributi HTML, proprietà CSS, comandi git con le loro opzioni, metodo+percorso HTTP); togli gli apici `'…'` usati al posto dei backtick (`'git fetch'` → `` `git fetch` ``). Le parole usate come concetto restano testo ("un revert annulla…"); il comando letterale va tra backtick ("si usa `git revert`"). Stesse modifiche, agli stessi indici, nel file inglese. Nessun'altra modifica al testo (niente riscritture: è il punto 4).
- [ ] **Step 4:** per ogni materia: `git show HEAD:<banco> > /tmp/vecchio.json` poi `--richiava <materiaId> /tmp/vecchio.json`. Rivedi il diff a campione (≥15 domande per materia).
- [ ] **Step 5:** Run `dotnet test wsa.quiz.core.tests` → PASS (tutti, inclusi `Traduzioni_NessunaChiaveOrfana` e `NessunFenceNonChiuso`).
- [ ] **Step 6: commit + push** — una per materia: `data(<materia>): codice tra backtick`

### Task 7: Motore — validazione codice, lunghezze senza backtick, riferimenti al corso, `--audit` (agy)

**Files:**
- Modify: `generazione-domande/quiz-engine.js`
- Create: `generazione-domande/quiz-engine.test.js`

**Interfaces:**
- Produces (`module.exports`, l'esecuzione CLI solo sotto `if (require.main === module)`):
  ```js
  lunghezzaVisibile(s)              // -> number: lunghezza senza backtick e senza righe ```lang / ```
  bucket(risposte, k, simile = 6)   // -> 'L'|'S'|'M'|'Q', calcolato con lunghezzaVisibile
  problemiCodice(testo)             // -> string[]: "fence non chiuso", "backtick dispari", "blocco senza linguaggio"
  riferimentiCorso(testo)           // -> string[]: parole sospette trovate (case-insensitive, parola intera o prefisso):
                                    //    corso, lezion, slide, docente, prof, dispens, esercitazion, eserciz, es1..es99, laboratorio, compito
  auditBanco(domande, simile = 6)   // domande nello schema JSON finale -> { buckets, posizioni, categorie, avvisi[] }
  ```
  CLI: invariata per `<file-dati.js> <output.json> [--write]`; nuova forma `node quiz-engine.js --audit <banco.json> [--simile=N]` che stampa lo stesso report del dry run + avvisi (codice e riferimenti al corso) e non scrive nulla. Il dry run normale stampa anche gli avvisi; `problemiCodice` "fence non chiuso" e "backtick dispari" bloccano `--write`.

- [ ] **Step 0:** Node non è installato sul Mac: `brew install node` (gratuito). Run `node --version` → v2x.
- [ ] **Step 1: test che falliscono** in `quiz-engine.test.js` (`node:test` + `node:assert`):
  - `lunghezzaVisibile` — ``"`abc`"`` → 3; `"```python\nx\n```"` → 1
  - `bucket` — `["`a`","bbbbbbbbbbbb","cc","dddddddddddddddddddd"]`, k=0 → `'S'`
  - `problemiCodice` — `"```python\nx"` → contiene `"fence non chiuso"`; ``"a `b"`` → `"backtick dispari"`; `"```\nx\n```"` → `"blocco senza linguaggio"`; testo pulito → `[]`
  - `riferimentiCorso` — `"Come visto a lezione"` → `["lezion"]`; `"# esercizio 3"` → `["eserciz"]`; `"profilo utente"` → `[]` (prof solo come parola intera o `prof.`)
  - `auditBanco` — 4 domande costruite a mano una per bucket → `buckets` = `{L:1,S:1,M:1,Q:1}`
  Run `node --test generazione-domande/` → FAIL.
- [ ] **Step 2: implementa** le funzioni e la modalità `--audit`.
- [ ] **Step 3:** Run `node --test generazione-domande/` → PASS. Run `node generazione-domande/quiz-engine.js --audit domande/cpp/cpp_domande.json` → L ≈ 57%, S ≈ 3% (coerente con la misura del 2026-11-12). Run su `domande/python/python_domande.json` → nessun avviso di codice.
- [ ] **Step 4: commit + push** — `feat(motore): validazione codice, lunghezze senza backtick, riferimenti al corso, --audit`

### Task 8: Guida e documentazione (agy)

**Files:**
- Modify: `generazione-domande/GUIDA_GENERAZIONE_DOMANDE.md`, `docs/claude/contenuti-e-dati.md`, `docs/claude/architettura-gui.md`

- [ ] **Step 1:** guida — aggiungi sezioni "Codice nelle domande" (convenzione, linguaggi ammessi, quando inline vs blocco, risposte di output tra backtick, righe brevi: max ~60 caratteri consigliati per il telefono), "Universalità" (niente corso/lezioni/slide/docente/dispense, anche nel codice: nomi di esercizi, commenti, nomi file), esempi L/S/M/Q con codice, sezione "Audit di un banco esistente" (`--audit`), checklist aggiornata; correggi `"MODELLO - quiz-engine.js"` → `quiz-engine.js` ovunque e togli il riferimento alla cartella `UX e Accessabilità/` (sostituisci con `domande/ux/ux_domande_unificate.json`).
- [ ] **Step 2:** `contenuti-e-dati.md` — convenzione del codice, nota "cambiare il testo cambia l'ID → ri-chiavare le traduzioni (vedi `strumenti/converti_codice.py`)". `architettura-gui.md` — controllo `TestoConCodice`, risorse `FontCodice`/`Codice*`/`ChipSfondo`.
- [ ] **Step 3:** rileggi: ogni comando citato nella guida esiste ed è eseguibile (`node generazione-domande/quiz-engine.js --audit domande/ux/ux_domande_unificate.json` gira).
- [ ] **Step 4: commit + push** — `docs: guida di generazione rivista (codice, universalità, audit)`

### Task 9: Verifica sulle piattaforme

- [ ] **Step 1: desktop** — tema chiaro e scuro, Python: domanda con classe indentata (id italiano 3 "class Conto") e una con la riga più lunga del banco; cambiare tema con la domanda a schermo (Review Focus 2); rispondere e controllare chip nei bottoni verde/rosso (Review Focus 3).
- [ ] **Step 2: Android emulatore** (`docs/claude/piattaforme-e-distribuzione.md`) — scroll verticale partendo sopra un blocco (Review Focus 1), riga lunga con scroll orizzontale solo nel blocco (Review Focus 4).
- [ ] **Step 3: iOS simulatore** (memoria iOS: `simctl`, non `-t:Run`) — stesse verifiche dello Step 2.
- [ ] **Step 4: web** — `dotnet run` dell'head browser in locale: il codice usa JetBrains Mono (nessun fallback proporzionale) e Riepilogo/Cronologia/Statistiche mostrano i blocchi.
- [ ] **Step 5:** annota l'esito (ok/difetti) in `docs/claude/storia-e-roadmap.md`, aggiorna la memoria "Roadmap idee 11/11" (punto 3 chiuso), commit + push.
