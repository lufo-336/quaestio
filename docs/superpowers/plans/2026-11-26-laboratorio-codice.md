# Laboratorio di codice — piano descrittivo

**Spec:** `docs/superpowers/specs/2026-11-26-laboratorio-codice-design.md` (approvata il 2026-11-26, esecuzione a fette).
Mockup: `docs/superpowers/specs/assets/2026-11-26-laboratorio/` (`ingresso.html`, `prevedi-output.html`, `trova-bug.html`).

**Obiettivo:** nuova modalità "Laboratorio" accanto ai quiz: schede di codice C# attraversate in tre passaggi
(prevedi l'output → trova il bug → rompi la funzione), su tre livelli, con i risultati nella cronologia e nelle
statistiche esistenti.

**Architettura.** Formato, caricamento, selezione e punteggio stanno in `Wsa.Quiz.Core` (testabili con xUnit). La
sessione è una piccola macchina a stati nuova in `wsa.quiz.app/State`, separata da `SessioneQuiz`. La vista è nuova,
mentre l'archivio è quello che c'è già: ogni sessione produce un normale `RisultatoQuiz` con un `DettaglioRisposta` per
passaggio, che Cronologia, Statistiche e Riepilogo mostrano senza essere riscritti. Le chiavi di risposta sono
precalcolate nel JSON e verificate da un test che compila ed esegue ogni variante con Roslyn.

## Esecuzione

Il codice e le schede li scrive **agy**, non sotto-agenti Claude. Claude (Opus) prepara i brief, lancia build e test
(0 token), giudica solo le segnalazioni ALTA/MEDIA della review e prepara la prova a mano per Luca.

**Ciclo di ogni fetta:**
1. Claude scrive il brief in `<artefatti>/brief-fetta-N.md`: "leggi il piano, esegui SOLO i Task a–b nel worktree,
   leggi solo dentro il repository, segna i task fatti, se qualcosa non è chiaro lascialo ed elencalo sotto NON FATTO /
   DUBBI". Strumenti: `view_file`, `grep_search`, `find_by_name`, `replace_file_content`, `write_to_file`. Nessun
   comando shell, salvo dove la tabella indica `dotnet test`.
2. `agy_run.py --role write --workdir <worktree> --plan-file docs/superpowers/plans/2026-11-26-laboratorio-codice.md
   --prompt-file <brief> --model <modello> --timeout 2400s`, in background. Un solo agy alla volta.
3. Claude lancia in un solo comando: `git status --short` (file fuori perimetro?), i build e i test (sotto). Se è rosso:
   `--role fix` con l'errore preciso (file, riga, messaggio). Se resta rosso, corregge Claude.
4. `agy_run.py --role review` con un brief limitato ai task della fetta e `--report-file <artefatti>/review-fetta-N.md`.
   Claude legge solo i punti ALTA/MEDIA e le righe che citano. Se agy ha toccato file esistenti di `wsa.quiz.core` o
   `wsa.quiz.app`, Claude legge il `git diff` di quei soli file.
5. Prova a mano di Luca (punto di controllo a fine fetta). Con esito positivo: commit sul branch, push del branch,
   merge in `main`, push.

**Modelli** (agy ha quote sue, separate dal Pro; l'effort si sceglie dal suffisso del modello):

| Task | Lavoro | Modello agy | Perché |
|---|---|---|---|
| 1, 2, 5, 8b, 14 | logica core/stato, JSON, storage, traduzioni | `gemini-4-hydrogen-high` | fedele al brief, compila al primo colpo |
| 3 | test di coerenza + runner Roslyn | `gemini-4-hydrogen-high` | permesso in più: `dotnet test` |
| 4 | schede base | `gemini-4-hydrogen-high` | il test d'esecuzione corregge i valori; permesso `dotnet test --filter Laboratorio` |
| 10, 12 | schede media / alta | `gemini-4-hydrogen-xhigh` | difetti sottili o multipli: serve più ragionamento per esche e trappole plausibili |
| 6, 7, 9, 11, 13 | viste Avalonia (XAML + code-behind) | `gemini-4-hydrogen-high`; se dopo un `fix` resta rosso per trappole Avalonia → `--pool claude --model claude-sonnet-5-5-high` | Hydrogen ha retto la revisione grafica; Sonnet su agy è la riserva per l'XAML difficile |
| 8, 15 | ritocchi a viste esistenti | `gemini-4-hydrogen-medium` | modifiche piccole e circoscritte |
| 16 | documentazione | `--role doc` (`gemini-4-argon-high`) | |
| review di fetta | | `gemini-4-argon-high` | 7 veri su 8 nella prova del 23/11 |
| review finale del branch (prima dell'ultimo merge) | | `gemini-4-hydrogen-xhigh` | 13 su 13 senza falsi positivi; costa il triplo, una volta sola |

**Percorsi:**
- worktree: `C:/Users/sonit/llm/worktrees/quaestio-laboratorio`, branch `feature/laboratorio`, creato da `main` dopo
  il commit di questo piano;
- artefatti: `C:/Users/sonit/llm/worktrees/quaestio-laboratorio.artefatti/`.

**Build e test** (tutti a 0 errori):
`dotnet build wsa.quiz.desktop` · `dotnet build wsa.quiz.browser` · `dotnet build wsa.quiz.cli` ·
`dotnet test wsa.quiz.core.tests`. A fine Fetta 2 e a fine piano anche `dotnet build wsa.quiz.android`, per verificare
che le schede arrivino nelle risorse incorporate.

## Vincoli globali

- Code-behind con `INotifyPropertyChanged`, niente MVVM; `x:DataType` su ogni template (trappola 5); angoli vivi,
  token di `Themes/Tokens.axaml`, inchiostri e brush applicativi, mai `SystemAccentColorBrush` (trappole 6/12/20);
  `DynamicResource` di tipo Brush nei Setter (7). Leggere `docs/claude/trappole.md` prima di toccare stili, scroll
  (10, 11, 26) e focus (21, 25).
- **Nessun record esistente deve cambiare**: solo campi additivi in `PreferenzeUtente` e `FiltroCronologia`; le pause
  del Laboratorio vanno in un **file separato** (`laboratorio_in_pausa.json`), così le pause dei quiz e la CLI restano
  intatte.
- Testi dell'interfaccia in `wsa.quiz.core/Risorse/Strings.resx` + `Strings.en.resx`, chiavi con prefisso `Lab_`. I
  contenuti delle schede si traducono con la sovrapposizione del Task 8b (`traduzioni/en/laboratorio/…`): ogni scheda
  nuova nasce con la sua traduzione.
- Nessuna esecuzione di codice sul dispositivo: l'app confronta soltanto con valori presenti nel JSON.
- I modelli JSON stanno in `Wsa.Quiz.Core` (già radice del trimmer per WebAssembly, trappola 27): nessun cambio al
  csproj del browser.

## File

| File | Nuovo/modifica | Task |
|---|---|---|
| `wsa.quiz.core/Models/Laboratorio/SchedaLab.cs` (+ `VarianteLab`, `DifettoLab`, `InputLab` nello stesso file) | nuovo | 1 |
| `wsa.quiz.core/Models/Laboratorio/TipiDifetto.cs` | nuovo | 1 |
| `wsa.quiz.core/Models/Laboratorio/PausaLaboratorio.cs` | nuovo | 14 |
| `wsa.quiz.core/Services/LaboratorioService.cs` | nuovo | 2, 9, 11 |
| `wsa.quiz.core/Services/StorageService.cs` | modifica | 14 |
| `wsa.quiz.core/Models/PreferenzeUtente.cs`, `FiltroCronologia.cs` | modifica | 7, 13 |
| `wsa.quiz.core/Risorse/Strings.resx`, `Strings.en.resx` | modifica | 1, 6, 7, 9, 11, 13, 14 |
| `laboratorio/cs/*.json` | nuovo | 4, 10, 12 |
| `wsa.quiz.core/Models/Laboratorio/TraduzioneScheda.cs` | nuovo | 8b |
| `traduzioni/en/laboratorio/cs/*.json` | nuovo | 8b, 10, 12 |
| `wsa.quiz.app/Wsa.Quiz.App.csproj`, `wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj` | modifica | 2 |
| `wsa.quiz.core.tests/Wsa.Quiz.Core.Tests.csproj` | modifica (Roslyn) | 3 |
| `wsa.quiz.core.tests/LaboratorioServiceTests.cs`, `LaboratorioCoerenzaTests.cs`, `LaboratorioEsecuzioneTests.cs` | nuovi | 2, 3, 9, 11, 14 |
| `wsa.quiz.app/State/SessioneLaboratorio.cs` | nuovo | 5, 9, 11, 14 |
| `wsa.quiz.app/Views/LaboratorioView.axaml(.cs)` | nuovo | 6, 9, 11, 14 |
| `wsa.quiz.app/Views/HomeView.axaml(.cs)`, `wsa.quiz.app/MainView.axaml.cs` | modifica | 7, 13, 14, 15 |
| `CronologiaView`, `CronologiaDettaglioView`, `DettaglioRispostaItem`, `StatisticheView`, `SospesiView`, `SessioneSospesaItem` | modifica | 8, 13, 14, 15 |
| `CLAUDE.md`, `docs/claude/architettura-gui.md`, `docs/claude/contenuti-e-dati.md` | modifica | 16 |

---

# Fetta 1 — Fondamenta

## Task 1: Modello della scheda e vocabolario dei difetti

- [x] Fatto 2026-11-26 (agy, Hydrogen high): `wsa.quiz.core/Models/Laboratorio/`, `Strings*.resx` — verifica (Claude): build desktop/browser/cli verdi, `dotnet test` 82/82

In `wsa.quiz.core/Models/Laboratorio/`, namespace `Wsa.Quiz.Core.Models.Laboratorio`, classi POCO con proprietà
`{ get; set; }` e default non nulli, nello stile di `Domanda.cs`. Il JSON usa chiavi camelCase (la lettura in
`StorageService` è già case-insensitive: riusare le stesse `JsonSerializerOptions`).

**`SchedaLab`**

| Proprietà | Tipo | Significato |
|---|---|---|
| `Id` | string | kebab-case, unico nel linguaggio (es. `media-voti`) |
| `Titolo` | string | breve, mostrato in testa alla scheda |
| `Livello` | string | `base` / `media` / `alta` |
| `Requisito` | string | cosa deve fare il codice, 1–3 frasi |
| `Chiamata` | string | istruzioni C# che usano il codice e stampano con `Console.WriteLine` (la chiamata di esempio del passo 1) |
| `CodiceSano` | List\<string\> | il codice corretto, una riga per elemento; non mostrato, serve al test |
| `Varianti` | List\<VarianteLab\> | 2–4 |
| `Linguaggio` | string | **non nel JSON**: lo valorizza il caricamento dalla cartella (`cs`) |

**`VarianteLab`:** `Id` (`v1`, `v2`…), `Righe` (List\<string\>, il codice col difetto: è quello che si mostra),
`Output` (List\<string\>, ciò che stampano `Righe` + `Chiamata`, riga per riga), `Esche` (List\<string\>, righe
plausibili ma false, per il menu e le tessere), `Difetti` (List\<DifettoLab\>), `Input` (List\<InputLab\>, 3–4).

**`DifettoLab`:** `Riga` (int, 1-based in `Righe`), `Tipo` (id di `TipiDifetto`), `Indizio` (frase per la seconda
possibilità: dice dove guardare, non cosa c'è), `Spiegazione`, `Correzione` (string, la riga corretta che sostituisce
`Riga`; obbligatoria a livello media), `Trappole` (List\<string\>, 2 righe sbagliate plausibili; obbligatorie a livello
media).

**`InputLab`:** `Chiamata` (istruzioni C# come quella della scheda, con altri valori), `Atteso` (output del codice
sano), `Effettivo` (output della variante). Righe separate da `\n`. Proprietà calcolata, non serializzata
(`[JsonIgnore]`): `Espone` = `Atteso != Effettivo`.

**Convenzione eccezioni:** se il codice lancia un'eccezione non gestita, l'output è quello stampato fino a quel punto
seguito da una riga `!NomeEccezione` (es. `!DivideByZeroException`), sia in `Output` sia in `Effettivo`/`Atteso`. Va
scritta nel commento XML di `VarianteLab.Output`.

**Codice come script:** `Righe` e `CodiceSano` sono codice C# valido come *script* (funzioni a livello superiore, niente
`class Program`/`Main`); usings impliciti: `System`, `System.Linq`, `System.Collections.Generic`, `System.Text`.

**`TipiDifetto`** (classe statica): l'elenco fisso degli id, nell'ordine in cui si mostrano, e un metodo `Etichetta(id)`
che restituisce la stringa localizzata da `Strings` (chiave `Lab_Difetto_<Id>`) e un metodo `EtichettaCanonica(id)` che
la restituisce sempre in italiano (`Strings.ResourceManager.GetString(chiave, CultureInfo("it"))`), usata come
`Categoria` nei `DettaglioRisposta`. Le categorie dei quiz sono canoniche in italiano allo stesso modo.

| Id | it | en |
|---|---|---|
| `Confine` | Off-by-one / confine | Off-by-one / boundary |
| `DivisioneIntera` | Divisione intera | Integer division |
| `NullVuoto` | Null o vuoto non gestito | Unhandled null or empty |
| `CondizioneInvertita` | Condizione invertita | Inverted condition |
| `Overflow` | Overflow | Overflow |
| `OperandoSbagliato` | Variabile o operando sbagliato | Wrong variable or operand |
| `ConfrontoStringhe` | Confronto di stringhe | String comparison |
| `MutazioneIterazione` | Collezione modificata durante il ciclo | Collection modified while iterating |
| `EccezioneInghiottita` | Eccezione inghiottita | Swallowed exception |
| `RisorsaNonChiusa` | Risorsa non chiusa | Resource not disposed |

## Task 2: `LaboratorioService` e cartella dati

- [x] Fatto 2026-11-26 (agy, Hydrogen high): `LaboratorioService.cs`, csproj app/desktop, `StorageService.OpzioniLettura` → internal, `LaboratorioServiceTests.cs` — verifica (Claude): build desktop/browser/cli verdi, `dotnet test` 82/82

**Cartella dati:** `laboratorio/cs/` nella radice del repo, accanto a `domande/`, un JSON per scheda (un oggetto
`SchedaLab`, non una lista). Collegarla come `domande/`: in `Wsa.Quiz.App.csproj` una riga `AvaloniaResource` subito
dopo quella di `traduzioni` (Link `Assets\laboratorio\…`); in `Wsa.Quiz.Desktop.csproj` un `None` con
`CopyToOutputDirectory` subito dopo quello di `traduzioni`. La CLI non la usa.

**`wsa.quiz.core/Services/LaboratorioService.cs`**, classe con metodi statici dove non serve stato:

| Membro | Cosa fa |
|---|---|
| `const string Modalita = "Laboratorio"` | valore di `RisultatoQuiz.Modalita` (non localizzato: riconosce i record) |
| `const string SuffissoMateria = " · Laboratorio"` | `MateriaNome` = nome del linguaggio + suffisso, es. `C# · Laboratorio` |
| `static IReadOnlyList<(string Id, string Nome)> Linguaggi` | v1: solo `("cs", "C#")` |
| `static List<SchedaLab> CaricaSchede(IFonteDati fonte)` | per ogni linguaggio: `fonte.ElencaJson("laboratorio/<id>")`, ordine ordinale, deserializza, valorizza `Linguaggio`. Un file illeggibile si salta (come per le domande: niente crash all'avvio) |
| `static string IdPasso(SchedaLab s, VarianteLab v, string passo)` | `lab:<linguaggio>:<scheda>:<variante>:<passo>`, con `passo` = `output` / `bug` / `input` |
| `static bool EDettaglioLab(DettaglioRisposta d)` / `static bool ERisultatoLab(RisultatoQuiz r)` | prefisso `lab:` / `Modalita == Modalita` |
| `static List<(SchedaLab, VarianteLab)> Prepara(schede, livello, bool ripassa, int quante, IEnumerable<RisultatoQuiz> cronologia, Random rnd)` | vedi sotto |

**Regole di `Prepara`** (spec §5.5). Si ricava dalla cronologia, per ogni coppia (scheda, variante), la data dell'ultimo
tentativo e se quel tentativo ha almeno un passo con `PesoErrore() > 0` (gli id hanno il prefisso
`lab:<ling>:<scheda>:<variante>:`).
- *Non ripassa* ("Solo non ancora viste"): per ogni scheda del livello si sceglie una variante mai vista, a caso; se
  sono tutte viste, quella vista meno di recente. Prima le schede con almeno una variante nuova (in ordine casuale), poi
  le altre dalla meno recente. Si prendono le prime `quante`.
- *Ripassa*: le coppie del livello con l'ultimo tentativo sbagliato, dalla meno recente; al massimo `quante`.
- Un solo elemento per scheda in una sessione.

Serve anche `static (int Nuove, int Totale, int DaRipassare) Conteggi(schede, livello, cronologia)` per la barra della
Home.

**Test** `LaboratorioServiceTests.cs`, con schede e cronologie costruite in memoria (stile di `PuntiDeboliTests`): id
del passo; una cronologia vuota dà solo varianti nuove; esaurite le varianti si prende la meno recente; ripassa prende
solo gli ultimi tentativi sbagliati (una variante sbagliata e poi rifatta giusta non compare); mai due varianti della
stessa scheda; `quante` rispettato.

## Task 3: Test di coerenza ed esecuzione reale

- [x] Fatto 2026-11-26 (agy, Hydrogen high): `LaboratorioCoerenzaTests.cs`, `LaboratorioEsecuzioneTests.cs` (+ fix Claude: `StringWriter` con InvariantCulture) — verifica (Claude): build desktop/browser/cli verdi, `dotnet test` 82/82

**`LaboratorioCoerenzaTests.cs`**: legge `laboratorio/*/*.json` dalla radice del repo (riusare l'idea di `Radice()` di
`IntegritaDatiTests`), un caso per file (`[Theory]` + `MemberData`, così il nome della scheda compare nel fallimento).
Controlli:
- id unici nel linguaggio; `Livello` valido; 2–4 varianti con id unici nella scheda;
- ogni `Difetto.Riga` dentro `Righe`; ogni `Tipo` in `TipiDifetto`; base e media esattamente 1 difetto, alta 2–3;
- media: `Correzione` presente, esattamente 2 `Trappole`, diverse fra loro, dalla correzione e dalla riga originale;
- `Esche` non vuote (almeno 2) e nessuna uguale a una riga di `Output`;
- 3–4 `Input`, almeno uno con `Espone` e almeno uno senza;
- testi non vuoti (`Titolo`, `Requisito`, `Indizio`, `Spiegazione`).

**`LaboratorioEsecuzioneTests.cs`**: aggiungere al solo progetto di test il pacchetto
`Microsoft.CodeAnalysis.CSharp.Scripting` (ultima versione stabile che compila con `net10.0`). Un aiutante
`Esegui(righe, chiamata)` esegue lo script `righe + chiamata` con gli usings impliciti del Task 1, cattura l'output
redirigendo `Console.Out` su uno `StringWriter`, applica la convenzione `!NomeEccezione` e restituisce le righe.
`Console.SetOut` è globale: i test che lo usano stanno tutti in questa classe (xUnit non esegue in parallelo i test di
una stessa classe), e l'output va ripristinato in un `finally`. Controlli, per variante:
- `Righe` + `Chiamata` producono esattamente `Output`;
- ogni `Input`: `CodiceSano` produce `Atteso` e `Righe` producono `Effettivo`;
- media: `Righe` con la riga del difetto sostituita da `Correzione` producono `Atteso` su tutti gli input; con ciascuna
  `Trappola` al posto di quella riga almeno un input dà un risultato diverso da `Atteso` (o lo script non compila).

**Messaggio d'errore utile:** ogni asserzione fallita stampa scheda, variante, input e **il valore vero** (es. "scheda
media-voti v2, input 3: la scheda dice `7`, il codice stampa `7,25`"), così chi corregge la scheda copia il valore.
Cultura: eseguire con `CultureInfo.InvariantCulture` impostata sul thread (decimali col punto, identici su ogni
macchina) e scriverlo nel commento XML di `VarianteLab.Output`.

## Task 4: Prime schede base

- [x] Fatto 2026-11-26 (agy, Hydrogen high): `laboratorio/cs/{media-voti,conta-vocali,massimo-lista}.json` — verifica (Claude): build desktop/browser/cli verdi, `dotnet test` 82/82

- [x] Corretto 2026-11-26 (agy): review fetta 1 — file `wsa.quiz.core.tests/LaboratorioCoerenzaTests.cs`, `wsa.quiz.core/Services/LaboratorioService.cs`, `laboratorio/cs/media-voti.json`, `laboratorio/cs/massimo-lista.json`, `laboratorio/cs/conta-vocali.json`

Tre schede in `laboratorio/cs/`, livello `base`, 2 varianti ciascuna, 8–12 righe, un difetto evidente per variante, con
tipi diversi fra le varianti. Temi d'esempio, liberi: media di un array, conteggio delle vocali in una stringa, massimo di
una lista. `Esche` credibili: l'errore che farebbe chi legge di fretta (es. il risultato del codice sano). Gli `Input`
coprono il caso normale e il caso limite che fa scattare il difetto.

Per questo task agy può lanciare `dotnet test wsa.quiz.core.tests --filter Laboratorio` e deve correggere le schede
finché passa, copiando i valori veri dai messaggi del test. Il test non va modificato per farlo passare.

**Punto di controllo Fetta 1:** test verdi; Luca legge una scheda JSON e dice se il formato è comodo da scrivere a mano.

---

# Fetta 2 — Una scheda base dall'inizio alla fine

## Task 5: `SessioneLaboratorio`

- [x] Fatto 2026-11-26 (agy): `wsa.quiz.app/State/SessioneLaboratorio.cs`, `wsa.quiz.core/Risorse/Strings*.resx` — verifica (Claude): build desktop/browser/cli/android verdi, `dotnet test` 82/82
- [x] Corretto 2026-11-26 (Claude): review fetta 2 (Argon, 2 ALTA veri) — ordine degli assegnamenti in `CaricaScheda`/`Avanti` (righe non cliccabili al passo 2; codice della scheda precedente dalla scheda 2); clamp di `LabSchede`; blocco doppione in `HomeView.OnMateriaPropertyChanged` tolto

`wsa.quiz.app/State/SessioneLaboratorio.cs`, eredita `ObservableObject` (come gli altri stati). Riceve la lista di
coppie da `LaboratorioService.Prepara`, il nome del linguaggio e il livello.

- Stato pubblico: `SchedaCorrente`, `VarianteCorrente`, `IndiceScheda`/`TotaleSchede`, `Passo` (enum `PassoLab`:
  `PrevediOutput`, `TrovaBug`, `RompiFunzione`, `Concluso`), `InAttesaDiAvanti` (risposta data, si mostra l'esito),
  `UltimoEsitoCorretto`.
- Metodi di risposta, uno per forma: `RispondiOutput(IReadOnlyList<string> righe)`,
  `RispondiBugTipo(int riga, string tipo)`, `RispondiInput(int indice)`; `Avanti()` passa al passo o alla scheda
  successivi; `Abbandona()`.
- Correzione: output giusto se tutte le righe coincidono, nell'ordine; bug giusto se riga **e** tipo coincidono con il
  difetto; input giusto se `Espone`.
- Ogni risposta aggiunge un `DettaglioRisposta`: `IdDomanda` da `IdPasso`, `MateriaNome` = nome + `SuffissoMateria`,
  `Categoria` = `TipiDifetto.EtichettaCanonica` del (primo) difetto, `TestoDomanda` = "titolo — nome del passo",
  `RispostaData`/`RispostaCorretta` testuali (l'output a righe; "riga 5 · Divisione intera"; la chiamata scelta),
  `Spiegazione` del difetto, `Corretta`, `Tentativi = 1`. `RisposteShufflate` resta vuota.
- `Risultato` (`RisultatoQuiz`): costruito a fine sessione o all'abbandono, con lo stesso calcolo dei totali di
  `SessioneQuiz` (`TotaleDomande` = passi risposti, corrette/errate, percentuale, durata da uno `Stopwatch`,
  `Punteggio` con `QuizService.CalcolaPunteggio`), `Modalita = LaboratorioService.Modalita`, `MateriaNome` come i
  dettagli, `CategorieSelezionate` vuota, `Abbandonato` se uscito prima della fine.

In questa fetta esistono solo le forme base (menu per riga, riga + tipo, un tentativo). Le altre arrivano nelle Fette
3–4: lasciare lo spazio senza scriverle prima.

## Task 6: `LaboratorioView`

- [x] Fatto 2026-11-26 (agy): `wsa.quiz.app/Views/LaboratorioView.axaml`, `wsa.quiz.app/Views/LaboratorioView.axaml.cs`, `wsa.quiz.app/State/LaboratorioItems.cs`, `wsa.quiz.core/Risorse/Strings*.resx` — verifica (Claude): build desktop/browser/cli/android verdi, `dotnet test` 82/82

`wsa.quiz.app/Views/LaboratorioView.axaml(.cs)`, costruita come `QuizView` (costruttore con la sessione, eventi
`LaboratorioConcluso` e, dalla Fetta 5, `LaboratorioMessoInPausa`). Riferimento visivo: i mockup
`prevedi-output.html` (variante C per il livello base) e `trova-bug.html` (variante A).

- **Testa:** titolo della scheda, "Scheda 2 di 5", nome del passo, requisito (sempre visibile, compresso su telefono).
- **Codice:** righe numerate in JetBrains Mono NL (stesso font di `TestoConCodice`), una riga per elemento di un
  `ItemsControl`. Scroll orizzontale per le righe lunghe, mai a capo (il numero di riga deve restare vero). Al passo
  "trova il bug" ogni riga è toccabile e quella scelta si evidenzia; dopo la risposta la riga del difetto si colora con
  i colori degli esiti di `QuizColors`.
- **Passo 1 (base):** la `Chiamata` in un riquadro, poi un menu a tendina per ogni riga di `Output`; voci = le righe di
  `Output` più le `Esche`, mescolate una volta per scheda. *Conferma* abilitato quando ogni menu ha una scelta. Esito:
  righe giuste/sbagliate evidenziate, output vero accanto.
- **Passo 2 (base):** tocco della riga, poi scelta del tipo da un elenco con le etichette di `TipiDifetto`. *Conferma*.
  Esito: riga giusta illuminata, tipo giusto, spiegazione.
- **Passo 3:** gli `Input` come opzioni da toccare (mostrare la `Chiamata`). Esito: per ogni input, *atteso* ed
  *effettivo* affiancati, quelli che espongono evidenziati.
- **Barra in basso** (come la `ZonaAvvia` della Home): *Conferma* / *Avanti* / *Fine*, più *Esci* (con `ConfermaDialog`
  esistente).
- Desktop/tablet: stessa colonna con `MaxWidth` come la Home; nessun layout a due colonne nella v1.
- Testi in `Strings` con prefisso `Lab_`.

## Task 7: Home, preferenze e collegamento

- [x] Fatto 2026-11-26 (agy): `wsa.quiz.core/Models/PreferenzeUtente.cs`, `wsa.quiz.app/Views/HomeView.axaml`, `wsa.quiz.app/Views/HomeView.axaml.cs`, `wsa.quiz.app/MainView.axaml.cs`, `wsa.quiz.core/Risorse/Strings*.resx` — verifica (Claude): build desktop/browser/cli/android verdi, `dotnet test` 82/82

**`PreferenzeUtente`**, campi additivi in fondo: `Laboratorio` (bool, false: l'interruttore globale), `LabLivello`
(string, `base`), `LabRipassa` (bool, false), `LabSchede` (int, 5).

**`HomeView`** (riferimento: mockup `ingresso.html`, variante A):
- In cima al contenuto scorrevole, un interruttore a due voci **Quiz | Laboratorio** (due `ToggleButton` o
  `RadioButton` nello stile dei controlli della Home, angoli vivi, la voce attiva col colore utente).
- Con *Laboratorio* le zone dei quiz (`GrigliaMaterieCategorie`, `ZonaOpzioni`, `ZonaTipo`, `ZonaLivelli`) si
  nascondono e compare `ZonaLaboratorio`, con tre riquadri con lo stesso trattino dei titoli: **Linguaggio** (i
  linguaggi con almeno una scheda), **Livello** (Base/Media/Alta; un livello senza schede è disabilitato), **Schede**
  (Solo non ancora viste / Ripassa quelle sbagliate, più il numero di schede per sessione, come `LimiteN`).
- `ZonaAvvia`: con *Laboratorio* il riepilogo mostra i conteggi di `LaboratorioService.Conteggi` (es. "4 nuove su 6",
  "Nessuna nuova: si rifanno le meno recenti", "2 da ripassare") e *Inizia* è disabilitato quando la selezione è vuota.
- `Inizializza` riceve anche le schede caricate; nuovo evento `LaboratorioAvviato` (argomento `SessioneLaboratorio`).
  Le nuove proprietà sollevano `PropertyChanged` come le opzioni quiz esistenti.

**`MainView.axaml.cs`:**
- In `Caricamento`, dopo le domande, `_schedeLab = LaboratorioService.CaricaSchede(fonte)`; passarle alla Home.
- Aggiungere le nuove proprietà della Home a `_proprietaOpzioniQuiz` e salvarle in `OnHomeOpzioniCambiate`.
- `AvviaLaboratorioView` sul modello di `AvviaQuizView`; `OnLaboratorioConcluso` sul modello di `OnQuizConcluso`
  (salva se c'è almeno un passo, ricarica cronologia/statistiche/storico, apre `RiepilogoView`).
- `RitornaAllaHome` ricostruisce la Home con le schede, come già fa con le domande.

## Task 8: Cronologia, Riepilogo e CLI con i record del Laboratorio

- [x] Fatto 2026-11-26 (agy): nessuna modifica necessaria — verifica (ispezione codice, nessuna shell eseguita): `CronologiaDettaglioView.axaml(.cs)` e `DettaglioRispostaItem.cs` non leggono `RisposteShufflate` né mostrano bottoni A/B/C/D; `RiepilogoView.axaml(.cs)` mostra `Modalita` e `Sbagliate` tramite `DettaglioRispostaItem` senza errori; `StatisticheView.axaml.cs` e `StatisticheService.cs` raggruppano `C# · Laboratorio` come materia; `wsa.quiz.cli/Program.cs` e `ConsoleUI.cs` mostrano cronologia e breakdown per categoria senza risolvere `IdDomanda` né ripassare sbagliate.

- `CronologiaDettaglioView` / `DettaglioRispostaItem`: un dettaglio con `RisposteShufflate` vuota mostra testo, risposta
  data, risposta corretta e spiegazione, senza i quattro bottoni A/B/C/D e senza errori. Se lo fa già, non toccare
  niente.
- `RiepilogoView`: verificare che regga un `RisultatoQuiz` di Laboratorio (titolo dalla `Modalita`, sbagliate elencate).
- `StatisticheView`: la voce `C# · Laboratorio` compare come una materia. *Ripassa* su quella voce oggi non fa niente
  (`OnRipassaRichiesto` non trova gli id `lab:` e ritorna): va bene fino al Task 15.
- CLI (`wsa.quiz.cli/Program.cs`): i punti che leggono la cronologia (righe ~93 e ~515) non devono provare ad aprire
  come domande gli id `lab:`; dove si ripassano le sbagliate, filtrare con `LaboratorioService.EDettaglioLab`. Le
  statistiche della CLI possono mostrarli.

**Punto di controllo Fetta 2:** `dotnet run --project <worktree>/wsa.quiz.desktop`: Home → Laboratorio → Base →
Inizia; i tre passi di una scheda giusti e sbagliati; Esci a metà; il Riepilogo; la sessione in Cronologia (dettaglio
leggibile) e in Statistiche come "C# · Laboratorio"; un quiz normale funziona come prima. Poi l'APK sul telefono, per la
leggibilità del codice su 360 dp.

---

# Fetta 2b — Schede in inglese

Decisione di Luca (2026-11-26): anche i **contenuti** delle schede si traducono, non solo l'interfaccia. Va fatto
prima della Fetta 3, così le schede media e alta nascono già con la traduzione.

## Task 8b: Sovrapposizione inglese delle schede

- [x] Fatto 2026-11-26 (agy): file `wsa.quiz.core/Models/Laboratorio/TraduzioneScheda.cs`, `wsa.quiz.core/Services/LaboratorioService.cs`, `wsa.quiz.app/MainView.axaml.cs`, `wsa.quiz.core.tests/LaboratorioServiceTests.cs`, `wsa.quiz.core.tests/LaboratorioCoerenzaTests.cs`, `traduzioni/en/laboratorio/cs/{media-voti,conta-vocali,massimo-lista}.json` — verifica: non eseguita (sessione headless senza shell; riletti tutti i file creati e modificati)

Stesso principio delle domande (`StorageService.ApplicaTraduzione`, `Models/TraduzioneMateria.cs`): un file per
scheda che sovrappone **solo i testi**, mai bloccante. Le schede hanno id fissi (kebab-case), quindi a differenza
delle domande una traduzione non diventa orfana se cambia il testo italiano.

**File:** `traduzioni/<lingua>/laboratorio/<linguaggio>/<schedaId>.json`, es. `traduzioni/en/laboratorio/cs/media-voti.json`.
I csproj di app e desktop includono già `traduzioni/**` in modo ricorsivo: nessun cambio. Il test esistente
`IntegritaDatiTests.Traduzioni_NessunaChiaveOrfana` legge solo i file in cima a `traduzioni/en/`: non va toccato.

**Modello** `wsa.quiz.core/Models/Laboratorio/TraduzioneScheda.cs` (stesso namespace di `SchedaLab`, tutti i campi
opzionali come in `TraduzioneMateria`):
- `TraduzioneScheda`: `Titolo`, `Requisito` (string?), `Varianti` (Dictionary\<string, TraduzioneVariante\>, chiave = id
  della variante);
- `TraduzioneVariante`: `Difetti` (List\<TraduzioneDifetto\>, **stesso ordine e stesso numero** dei `Difetti` della
  variante);
- `TraduzioneDifetto`: `Indizio`, `Spiegazione` (string?).

Si traducono solo `Titolo`, `Requisito`, `Indizio`, `Spiegazione`. Restano identici: `Chiamata`, `CodiceSano`, `Righe`,
`Output`, `Esche`, `Input`, `Correzione`, `Trappole` (sono codice o il suo output, verificati da Roslyn), `Livello`,
`Tipo`, gli id. Le stringhe dentro il codice (es. `Console.WriteLine("Media: ...")`) restano come sono.

**`LaboratorioService.CaricaSchede(IFonteDati fonte, string lingua = Lingue.Base)`**: dopo aver validato una scheda, se
`lingua` non è quella base e il file di traduzione esiste, lo sovrappone (path costruito da linguaggio e `scheda.Id`).
Regole come `ApplicaTraduzione`: file illeggibile → scheda in italiano con avviso su `Console.Error`; campo nullo o
vuoto → resta l'italiano; variante non trovata → avviso e salto; numero di difetti diverso → avviso e quella variante
resta in italiano. In `MainView.Caricamento` passare la stessa lingua che riceve `_storage.LinguaContenuti`.

**Test:**
- `LaboratorioServiceTests`: la sovrapposizione cambia i quattro testi e nient'altro; con un numero di difetti diverso
  la variante resta in italiano; con `lingua` base non si legge niente. Per la fonte, riusare quella già usata nei test
  di `CaricaSchede` se c'è, altrimenti una cartella temporanea.
- `LaboratorioCoerenzaTests`: per ogni scheda di `laboratorio/*/*.json` esiste `traduzioni/en/laboratorio/<ling>/<id>.json`,
  con `Titolo` e `Requisito` non vuoti, le stesse varianti (né in più né in meno), per ogni variante lo stesso numero
  di difetti, `Indizio` e `Spiegazione` non vuoti; nessun file di traduzione senza la scheda corrispondente.

**Traduzione delle tre schede base** (`media-voti`, `conta-vocali`, `massimo-lista`): inglese naturale, stesso tono
didattico; gli indizi continuano a dire dove guardare, non cosa c'è.

**Punto di controllo:** lingua inglese nelle Impostazioni → una scheda base mostra titolo, requisito, indizio e
spiegazione in inglese; codice e output identici all'italiano.

---

# Fetta 3 — Livello media

## Task 9: Tessere, correzione fra tre righe, seconda possibilità

- [x] Fatto 2026-11-26 (agy): file `wsa.quiz.core/Services/LaboratorioService.cs`, `wsa.quiz.core/Risorse/Strings.resx`, `wsa.quiz.core/Risorse/Strings.en.resx`, `wsa.quiz.core.tests/LaboratorioServiceTests.cs`, `wsa.quiz.app/State/SessioneLaboratorio.cs`, `wsa.quiz.app/State/LaboratorioItems.cs`, `wsa.quiz.app/Views/LaboratorioView.axaml`, `wsa.quiz.app/Views/LaboratorioView.axaml.cs` — verifica: non eseguita (sessione headless senza shell; riletti i file modificati)

Riferimento: mockup `prevedi-output.html` variante B, `trova-bug.html` variante B.

- **Passo 1 media/alta, tessere:** le righe di `Output` più le `Esche`, mescolate, come tessere da toccare in ordine;
  una tessera toccata va nella zona "output composto" e un secondo tocco la rimette fra le tessere. *Conferma* attivo con
  almeno una tessera. Correzione esatta (stesse righe, stesso ordine, nessuna in più). Metodo di sessione:
  `RispondiOutput` (lo stesso: cambia solo la vista).
- **Passo 2 media:** tocco della riga, poi scelta fra tre righe (`Correzione` + 2 `Trappole`, mescolate). Giusto se
  riga e correzione sono giuste. Nuovo metodo `RispondiBugCorrezione(int riga, string correzione)`.
- **Seconda possibilità (base e media, passo 2):** se la riga è sbagliata si mostra l'`Indizio` e si riprova una volta
  (il tipo o la correzione si chiedono dopo una riga giusta). Al secondo tentativo riuscito: `Corretta = true`,
  `SecondoTentativo = true` (peso ½, come la seconda possibilità dei quiz). Se fallisce anche il secondo, la riga giusta
  si illumina e si prosegue. Se la riga è giusta ma tipo o correzione sono sbagliati: sbagliato, senza secondo
  tentativo.
- Test in `LaboratorioServiceTests`: spostare in `LaboratorioService` (metodi statici) le regole di correzione di output,
  bug e input, e provarle lì, compreso il peso ½.

## Task 10: Schede media

- [x] Fatto 2026-11-26 (agy): file `laboratorio/cs/{sconto-ordine,verifica-accesso,verifica-buffer}.json`, `traduzioni/en/laboratorio/cs/{sconto-ordine,verifica-accesso,verifica-buffer}.json` — verifica: non eseguita (sessione headless senza shell; verifica manuale di struttura, coerenza, traduzioni ed esecuzione riga per riga)

Tre schede `media`, 2 varianti, 15–25 righe, un difetto sottile per variante (es. un `<=` al posto di `<`, una
divisione intera dentro un'espressione, un confronto di stringhe sensibile alle maiuscole), `Correzione` + 2 `Trappole`
che compilano e sembrano giuste. Stessa regola del Task 4 per `dotnet test --filter Laboratorio`. Ogni scheda con la
sua traduzione in `traduzioni/en/laboratorio/cs/` (Task 8b).

**Punto di controllo Fetta 3:** una sessione Media sul desktop e sul telefono (le tessere al tocco), seconda possibilità
riuscita e fallita, peso ½ visibile in Statistiche.

---

# Fetta 4 — Livello alta

## Task 11: Review completa

- [x] Fatto 2026-11-27 (agy): file `wsa.quiz.core/Services/LaboratorioService.cs`, `wsa.quiz.core/Risorse/Strings.resx`, `wsa.quiz.core/Risorse/Strings.en.resx`, `wsa.quiz.app/State/SessioneLaboratorio.cs`, `wsa.quiz.app/State/LaboratorioItems.cs`, `wsa.quiz.app/Views/LaboratorioView.axaml`, `wsa.quiz.app/Views/LaboratorioView.axaml.cs`, `wsa.quiz.core.tests/LaboratorioServiceTests.cs` — verifica: non eseguita (sessione headless senza shell; riletti i file modificati; `SchedaLab.cs` e `LaboratorioCoerenzaTests.cs` già contenevano commento e regole strutturali)

Riferimento: mockup `trova-bug.html` variante C.

- **Passo 2 alta:** si segnano più righe, a ognuna si dà un tipo (lo stesso elenco del base); un contatore "difetti
  segnati: 2 di 3"; una riga segnata si può togliere. *Conferma* quando c'è almeno un segno. Nessuna seconda
  possibilità.
- **Punteggio** (`LaboratorioService.PunteggioReview`): per ogni difetto, 1 se c'è un segno sulla sua riga col tipo
  giusto, ½ se c'è un segno sulla riga col tipo sbagliato; meno 1 per ogni segno su una riga senza difetti; diviso per il
  numero di difetti, limitato a 0..1. Per l'archivio: punteggio 1 → giusto; da ½ incluso a 1 escluso → giusto al secondo
  colpo (`SecondoTentativo`, peso ½); sotto ½ → sbagliato. `RispostaData`/`RispostaCorretta` elencano righe e tipi;
  `Categoria` = il tipo del difetto più grave, cioè il primo della lista `Difetti` (scriverlo nel commento del formato:
  i difetti si elencano dal più grave).
- **Passo 3 alta:** giusto se l'input espone almeno un difetto (`Espone`); nell'esito, per ogni input, atteso ed
  effettivo come al base.
- Test: segnare ogni riga vale 0; tutti i difetti col tipo giusto valgono 1; un tipo sbagliato vale ½ per quel difetto;
  un falso allarme toglie.

## Task 12: Schede alta

- [x] Fatto 2026-11-27 (agy): file `laboratorio/cs/{paginazione-catalogo,parser-righe-csv,pulizia-cache-ttl}.json`, `traduzioni/en/laboratorio/cs/{paginazione-catalogo,parser-righe-csv,pulizia-cache-ttl}.json` — verifica: non eseguita (sessione headless senza shell; verifica manuale di struttura, coerenza, traduzioni ed esecuzione riga per riga; test Roslyn demandato al chiamante)

Tre schede `alta`, 2 varianti, 30+ righe, 2–3 difetti di gravità diversa per variante, elencati dal più grave, con
almeno un input che espone il più grave. Stessa regola del Task 4 per i test. Ogni scheda con la sua traduzione
(Task 8b).

**Punto di controllo Fetta 4:** una sessione Alta; provare che "segna tutto" non conviene.

---

# Fetta 5 — Il resto dell'app

## Task 13: Interruttore globale in Cronologia, Statistiche e Sospesi

- [x] Fatto 2026-11-27 (agy): file `wsa.quiz.core/Models/FiltroCronologia.cs`, `wsa.quiz.core/Risorse/Strings.resx`, `wsa.quiz.core/Risorse/Strings.en.resx`, `wsa.quiz.core.tests/FiltroCronologiaTests.cs`, `wsa.quiz.app/Views/HomeView.axaml.cs`, `wsa.quiz.app/Views/CronologiaView.axaml(.cs)`, `wsa.quiz.app/Views/StatisticheView.axaml(.cs)`, `wsa.quiz.app/Views/SospesiView.axaml(.cs)`, `wsa.quiz.app/MainView.axaml.cs` — verifica: non eseguita (sessione headless senza shell; riletti i file modificati e il nuovo test)

- Lo stesso interruttore **Quiz | Laboratorio** della Home in cima a `CronologiaView`, `StatisticheView` e
  `SospesiView`. È un'unica scelta (`PreferenzeUtente.Laboratorio`): ogni vista espone una proprietà `Laboratorio` e un
  evento `LaboratorioCambiato`; `MainView` lo propaga alle altre quattro viste e salva la preferenza una volta sola.
  All'avvio tutte partono dal valore salvato.
- `FiltroCronologia`: campo additivo `Laboratorio` (bool, false); `Applica` tiene solo i record con
  `LaboratorioService.ERisultatoLab(r) == Laboratorio`. **Non** entra in `Attivo` (non è un filtro del pannello: non
  deve accendere l'indicatore "filtri attivi"). `MaterieDisponibili` riceve la cronologia già filtrata per tipo.
- `StatisticheView`: con *Quiz* solo le materie senza `SuffissoMateria`, con *Laboratorio* solo quelle col suffisso.
- `SospesiView`: con *Laboratorio* le pause del Task 14, con *Quiz* quelle di oggi.
- Con *Quiz* tutto deve apparire esattamente come prima del Laboratorio.

## Task 14: Pausa e ripresa per scheda

- [x] Fatto 2026-11-27 (agy): file `wsa.quiz.core/Models/Laboratorio/PausaLaboratorio.cs`, `wsa.quiz.core/Services/StorageService.cs`, `wsa.quiz.core/Risorse/Strings.resx`, `wsa.quiz.core/Risorse/Strings.en.resx`, `wsa.quiz.core.tests/LaboratorioServiceTests.cs`, `wsa.quiz.app/State/SessioneLaboratorio.cs`, `wsa.quiz.app/Views/PausaDialog.axaml(.cs)`, `wsa.quiz.app/Views/LaboratorioView.axaml(.cs)`, `wsa.quiz.app/State/SessioneSospesaItem.cs`, `wsa.quiz.app/Views/SospesiView.axaml(.cs)`, `wsa.quiz.app/MainView.axaml.cs` — verifica: non eseguita (sessione headless senza shell; riletti i file creati e modificati)

- `PausaLaboratorio` (core, Models/Laboratorio): `SessioneId` (Guid "N"), `DataOraPausa`, `Linguaggio`, `Livello`,
  `Coppie` (List\<string\>, `schedaId/varianteId` in ordine), `IndiceScheda` (la scheda da cui ripartire),
  `Dettagli` (solo quelli delle schede concluse), `TempoTrascorso`.
- `StorageService`: file `laboratorio_in_pausa.json` nella cartella utente, con la stessa validazione dei percorsi del
  costruttore; `CaricaPauseLab`, `SalvaPausaLab`, `EliminaPausaLab`, copiando la forma di
  `CaricaPause`/`SalvaPausa`/`EliminaPausa` (file corrotto → lista vuota, scrittura atomica).
- `SessioneLaboratorio`: `CreaPausa()` (scarta i dettagli della scheda in corso) e `static RiprendiDa(pausa, schede)`;
  una scheda o variante non più presente → `InvalidOperationException`, come `SessioneQuiz.RiprendiDa`, gestita in
  `MainView` allo stesso modo (messaggio, pausa eliminata).
- `LaboratorioView`: bottone *Pausa* accanto a *Esci* (con `PausaDialog` se adatto, altrimenti `ConfermaDialog`);
  evento `LaboratorioMessoInPausa`.
- `SospesiView`/`SessioneSospesaItem`: le pause del Laboratorio come righe nello stesso stile (titolo "C# ·
  Laboratorio · Media", "scheda 3 di 5", data); *Riprendi* ed *Elimina*. Evento di ripresa distinto
  (`RiprendiLabRichiesto`).
- `OnLaboratorioConcluso` elimina la pausa d'origine, come fa `OnQuizConcluso`.
- Test: andata e ritorno di `PausaLaboratorio` su una cartella temporanea; un `quiz_in_pausa.json` esistente non cambia.

## Task 15: Ripassa da Statistiche

- [x] Fatto 2026-11-27 (agy): file `wsa.quiz.core/Services/LaboratorioService.cs`, `wsa.quiz.core.tests/LaboratorioServiceTests.cs`, `wsa.quiz.app/MainView.axaml.cs` — verifica: non eseguita (sessione headless senza shell; riletti i file modificati)

In `MainView.OnRipassaRichiesto`: se la materia ha `SuffissoMateria`, invece del quiz si ricavano dagli id sbagliati le
coppie (scheda, variante) ancora esistenti, una per scheda, e si avvia una `SessioneLaboratorio` con quelle (il livello
della sessione è quello delle schede; con livelli misti va bene, ogni scheda usa le forme del proprio livello). Nessuna
coppia trovata: niente, come oggi.

## Task 16: Documentazione

- [x] Fatto 2026-11-27 (agy): file `CLAUDE.md`, `docs/claude/contenuti-e-dati.md`, `docs/claude/architettura-gui.md` — verifica: riletti i file modificati e controllata la coerenza con il codice reale (`docs/claude/trappole.md` invariato: nessuna nuova trappola emersa oltre alle note documentate in `contenuti-e-dati.md` e `architettura-gui.md`)

- `CLAUDE.md`: cartella `laboratorio/` nel layout del repo; riga di stato; nessuna trappola nuova se non ne sono emerse.
- `docs/claude/contenuti-e-dati.md`: sezione "Schede del Laboratorio" (formato, convenzioni script/eccezioni/cultura,
  come si verifica con `dotnet test --filter Laboratorio`, la traduzione in `traduzioni/en/laboratorio/`, come si
  aggiunge un linguaggio).
- `docs/claude/architettura-gui.md`: `SessioneLaboratorio`, `LaboratorioView`, interruttore globale, pause separate.
- `docs/claude/trappole.md`: solo le trappole davvero incontrate in questo piano.

**Punto di controllo Fetta 5 (finale):** interruttore cambiato in Statistiche → anche Home, Cronologia e Sospesi
cambiano, e al riavvio resta; pausa a metà sessione, ripresa dalla scheda in corso; Ripassa da Statistiche su
"C# · Laboratorio"; quiz, pause dei quiz e CLI come prima. Review finale del branch con `gemini-4-hydrogen-xhigh`, poi
APK Release da build pulita (trappola 29) e prova sul telefono.
