# Contenuti (materie/domande), storage e modello dati

<!-- Spostato da CLAUDE.md il 2026-11-05 per alleggerire il contesto: testo invariato. -->

## Aggiungere materie/domande

Editi `materie.json` e/o aggiungi file in `domande/<materia>/`. I csproj fanno
`<None Include="..\materie.json">` con `Link` per copiarli nei bin a ogni build,
preservando la struttura di sottocartelle. Funziona per console e GUI insieme.

Stato 2026-07-22: **10 materie / 2163 domande** (Cloud Services estesa
100→190 il 19/07, ~70% scenari architetturali, categorie normalizzate a 10;
RESTful API estesa 84→144 il 21/07, taglio misto pratico/teorico, con tre
categorie nuove per l'esercitazione finale TaskFlow: "Autenticazione e JWT",
"Query, paginazione e caching", "Frontend, fetch e CORS"; poi 144→167 il 22/07
con domande di ripasso). **Dal 2026-10-23: 11 materie / 2383 domande**, con
l'aggiunta di **Python** (220 domande, 11 categorie, da Tipi di dati a ORM e
SQLAlchemy). Ripartizione per file: cs 652, sql 248, versioncontrol 236, python
220, frontend 210, cpp 200, cloud 190, restfulapi 167, ux 100, infra 50+50,
cybersecurity 60.
**Dal 2026-11-18: 12 materie / 2483 domande**, con l'aggiunta di **NoSQL**
(100 domande, 13 categorie: MongoDB — query, CRUD, indici, validazione,
aggregation, modellazione e design pattern, import/backup — ed Elasticsearch;
48 teoria / 31 codice / 21 architettura; stile ripreso da una simulazione di
verifica; blocchi `js` per `mongosh` e `http` per le richieste Elasticsearch).

**Attenzione**: i csproj includono `..\domande\**\*.json` (AvaloniaResource +
None). **Qualsiasi** `.json` lasciato in `domande/` — anche temporaneo o
intermedio — finisce nei bin e nell'APK e viene enumerato da `ElencaJson` come
file di domande (schema sbagliato → caricamento rotto). Script e file di
lavoro vanno nello scratchpad, mai in `domande/`.

Nota: dall'8/09/2026 la cartella `docs/` **è nel repo**. Spec, piani e note di
lavoro viaggiano tracciati con git come tutto il resto del progetto.

### Convenzione codice nelle domande

- **Blocchi multiriga**: racchiusi tra triple backtick con identificatore di linguaggio
  obbligatorio (```lang ... ```). Linguaggi ammessi: `python`, `csharp`, `cpp`, `c`, `sql`,
  `html`, `css`, `js`, `bash`, `json`, `http`.
- **Codice inline**: racchiuso tra singoli backtick (`` `inline` ``) in domanda, risposte
  e spiegazione.
- **Risposte**: se sono interamente codice/output vanno tra backtick (es. `` `TypeError` ``, `` `(7, 12)` ``);
  se sono miste (testo + codice), solo la parte di codice va tra backtick (es. ``Solleva `KeyError` ``);
  termini usati come concetto in linguaggio naturale restano testo normale (es. `'Nessuna'`).
- **Righe brevi**: consigliato un massimo di **~60 caratteri** per riga, per garantire la leggibilità
  su smartphone senza obbligare a scrollare in orizzontale.
- **Universalità**: nessun riferimento al contesto del corso (lezioni, slide, docente, dispense, compiti),
  nemmeno dentro i commenti o i nomi di variabile nel codice.
- Per generare o validare banchi vedi `generazione-domande/GUIDA_GENERAZIONE_DOMANDE.md` e il motore
  `quiz-engine.js` (`--audit`).

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
- `storage.CaricaPauseLab()` → `List<PausaLaboratorio>` (file separato `laboratorio_in_pausa.json`)
- `storage.SalvaPausaLab(PausaLaboratorio)` — upsert per `SessioneId`
- `storage.EliminaPausaLab(string sessioneId)`
- `storage.EliminaRisultato(string id)`
- `storage.SvuotaCronologia()`

### Modello dati — note

- `Domanda.Id` è uno **SHA256 troncato a 12 char hex** calcolato su
  `testo|risposte|indice_corretta` (`StorageService.GeneraId`). Stabile a riordini,
  cambia se cambia il contenuto. Utile per matching cronologia/sospesi.
  **Nota critica sulle traduzioni**: cambiare il testo (anche solo aggiungendo backtick
  o formattando un blocco di codice) **cambia l'ID della domanda**. Poiché le traduzioni
  in `traduzioni/en/<materia>.json` sono indicizzate per ID, qualsiasi modifica al testo
  richiede di **ri-chiavare le traduzioni** per evitare che diventino orfane e smettano
  di comparire nell'app. Strumenti a supporto in `generazione-domande/strumenti/`:
  - `converti_codice.py --richiava <materiaId> <banco_vecchio.json>`: aggiorna i file di
    traduzione rimappando le vecchie chiavi sui nuovi ID.
  - `coppie_it_en.py`: permette di esportare ed importare domande it/en affiancate (`esporta` / `importa`)
    eseguendo la ri-chiavatura automatica.
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
- **Record del Laboratorio in `cronologia.json`**: una sessione di Laboratorio salva un normale
  `RisultatoQuiz` con `Modalita = "Laboratorio"` (`LaboratorioService.Modalita`),
  `MateriaNome = "<Linguaggio> · Laboratorio"` (`LaboratorioService.SuffissoMateria`, es. `"C# · Laboratorio"`)
  e un `DettaglioRisposta` per passaggio con `IdDomanda = "lab:<ling>:<scheda>:<variante>:<passo>"`
  (`passo` = `output` | `bug` | `input`), `Categoria` = `TipiDifetto.EtichettaCanonica(tipo)` (sempre in italiano,
  dal primo difetto della variante) e `RisposteShufflate` vuota. Vedi `docs/claude/architettura-gui.md` per
  la macchina a stati e l'integrazione con la UI.

## Schede del Laboratorio

Le schede del Laboratorio di codice vivono in `laboratorio/<linguaggio>/` (nella radice del repo, accanto a
`domande/`), un file `.json` per scheda (un singolo oggetto `SchedaLab`, non una lista). Come `domande/`, la
cartella è inclusa con wildcard in `Wsa.Quiz.App.csproj` (`AvaloniaResource` sotto `Assets\laboratorio\…`) e
in `Wsa.Quiz.Desktop.csproj` (`None` con `CopyToOutputDirectory`): **non lasciare file `.json` temporanei**
dentro `laboratorio/`.

Stato v1 (2026-11-27): linguaggio **C# (`cs`)**, **9 schede / 18 varianti** (2 varianti per scheda, tutte con
traduzione inglese):
- `base` (8–12 righe, 1 difetto): `media-voti`, `conta-vocali`, `massimo-lista`;
- `media` (15–25 righe, 1 difetto + correzione e 2 trappole): `sconto-ordine`, `verifica-accesso`, `verifica-buffer`;
- `alta` (30+ righe, 2–3 difetti per la review completa): `paginazione-catalogo`, `parser-righe-csv`, `pulizia-cache-ttl`.

Dal 2026-11-27 anche **Python (`py`)**, stessa struttura (9 schede / 18 varianti, traduzione inglese):
- `base`: `normalizza-email`, `filtra-finestra`, `lotti-spedizione`;
- `media`: `budget-categorie`, `parametri-query`, `merge-intervalli`;
- `alta`: `prenotazione-magazzino`, `verifica-sla-servizi`, `coda-retry-job`.

### Formato JSON (`wsa.quiz.core/Models/Laboratorio/`)

- **`SchedaLab` (`SchedaLab.cs`)**:
  - `id`: kebab-case unico nel linguaggio (es. `"media-voti"`), senza `:`;
  - `titolo`: titolo breve mostrato in testa;
  - `livello`: `"base"`, `"media"` o `"alta"` (`LivelliLab`);
  - `requisito`: 1–3 frasi su cosa deve fare il codice;
  - `chiamata`: istruzioni C# per il passo 1 (*Prevedi l'output*) che invocano il codice e stampano con `Console.WriteLine`;
  - `codiceSano`: `List<string>` (una riga per elemento) del codice corretto; non mostrato all'utente, usato dai test per verificare `atteso`;
  - `varianti`: 2–4 oggetti `VarianteLab`;
  - `Linguaggio` (`[JsonIgnore]`): valorizzato al caricamento da `LaboratorioService.CaricaSchede` in base alla cartella (`"cs"`).
- **`VarianteLab`**:
  - `id`: `"v1"`, `"v2"`… (unico nella scheda, senza `:`);
  - `righe`: `List<string>` del codice col difetto mostrato all'utente;
  - `output`: `List<string>` delle righe stampate da `righe` + `chiamata`;
  - `esche`: almeno 2 righe plausibili ma false, tutte distinte fra loro e assenti da `output` (usate nei menu del livello `base` e nelle tessere di `media`/`alta`);
  - `difetti`: `List<DifettoLab>`, esattamente 1 per `base` e `media`, 2–3 per `alta` **elencati dal più grave al meno grave** (il primo fornisce la `Categoria` del `DettaglioRisposta`);
  - `input`: 3–4 oggetti `InputLab` per il passo 3 (*Rompi la funzione*), con `chiamata` distinte, `atteso` (output di `codiceSano`) ed `effettivo` (output di `righe`). `Espone` (`[JsonIgnore]`, `Atteso != Effettivo`) deve essere vero per almeno un input e falso per almeno uno.
- **`DifettoLab` e `TipiDifetto` (`TipiDifetto.cs`)**:
  - `riga`: indice 1-based in `righe` (un solo difetto per riga; la riga non può essere vuota, una sola parentesi `{`/`}` né comparire identica in `codiceSano`);
  - `tipo`: uno dei 10 id fissi di `TipiDifetto.Tutti` (`Confine`, `DivisioneIntera`, `NullVuoto`, `CondizioneInvertita`, `Overflow`, `OperandoSbagliato`, `ConfrontoStringhe`, `MutazioneIterazione`, `EccezioneInghiottita`, `RisorsaNonChiusa`), localizzati in `Strings*.resx` con prefisso `Lab_Difetto_`;
  - `indizio`: indica dove guardare senza svelare il difetto (usato nella seconda possibilità di `base`/`media`);
  - `spiegazione`: spiegazione didattica mostrata all'esito e salvata nel dettaglio;
  - `correzione` e `trappole`: obbligatori a livello `media` (`correzione` sostituisce `riga`; `trappole` contiene esattamente 2 righe sbagliate ma plausibili, diverse fra loro, dalla `correzione` e dalla riga difettosa).

### Convenzioni: script, eccezioni, cultura

- **Script C#**: `codiceSano` e `righe` sono codice C# valido come script top-level (funzioni a livello superiore, senza `class Program` o `Main`), eseguito con gli using impliciti `System`, `System.Linq`, `System.Collections.Generic`, `System.Text`.
- **Eccezioni non gestite**: se l'esecuzione solleva un'eccezione, l'output registrato in `output`, `atteso` o `effettivo` consiste nelle eventuali righe stampate fino a quel punto seguite da una riga `!NomeEccezione` (es. `!NullReferenceException`, `!DivideByZeroException`, `!ArgumentOutOfRangeException`). Nelle stringhe multilinea (`chiamata`, `atteso`, `effettivo`) le righe sono separate da `\n`.
- **Script Python** (`laboratorio/py/`): modulo Python 3 con funzioni a livello superiore, la `chiamata` stampa con `print(...)`. Gli output sono quelli reali di `print` (`True`, `None`, `90.0`, `[1, 2]`); un'eccezione non gestita dà `!NomeEccezione` (es. `!ZeroDivisionError`), un `SyntaxError`/`IndentationError` conta come "non compila" (ammesso solo nelle trappole). Il test lancia `py -3` su Windows e `python3` altrove: per `dotnet test` serve Python installato.
- **Cultura invariante**: tutti gli output sono valutati con `CultureInfo.InvariantCulture` (decimali col punto, es. `7.5`). Nel runner di test (`LaboratorioEsecuzioneTests.Esegui`), oltre a impostare `CultureInfo.CurrentCulture = CultureInfo.InvariantCulture`, lo `StringWriter` passato a `Console.SetOut` va costruito esplicitamente con `new StringWriter(CultureInfo.InvariantCulture)`: `TextWriter` fissa infatti il proprio `FormatProvider` al momento della costruzione, perciò un `new StringWriter()` senza argomenti cattura la cultura del sistema operativo (formattando `7,5` su macchine italiane).

### Verifica con i test

L'app non compila né esegue codice sul dispositivo: confronta solo le scelte dell'utente con i valori del JSON.
La validazione avviene in sviluppo con:

```powershell
dotnet test wsa.quiz.core.tests --filter Laboratorio
```

- **`LaboratorioCoerenzaTests`**: controlla struttura JSON, unicità degli id, vincoli su `righe`/`esche`/`difetti`/`input` e completezza delle traduzioni inglesi.
- **`LaboratorioEsecuzioneTests`**: esegue ogni variante: C# con Roslyn (`Microsoft.CodeAnalysis.CSharp.Scripting`, dipendenza del solo progetto di test), Python con l'interprete di sistema (`EseguiPython`). Verifica che `righe + chiamata` stampi `output`, che su ogni `input` `codiceSano` stampi `atteso` e `righe` stampi `effettivo`, e — per il livello `media` — che la riga `correzione` produca `atteso` su tutti gli input mentre ciascuna `trappola` fallisca (o non compili) su almeno un input. Quando un'asserzione fallisce, il messaggio riporta scheda, variante, input e il valore reale stampato dal codice.

### Traduzioni (`traduzioni/en/laboratorio/`)

Ogni scheda ha un file di sovrapposizione in `traduzioni/<lingua>/laboratorio/<linguaggio>/<schedaId>.json`
(es. `traduzioni/en/laboratorio/cs/media-voti.json`), deserializzato in `TraduzioneScheda`
(`wsa.quiz.core/Models/Laboratorio/TraduzioneScheda.cs`).

- Si traducono **solo i testi didattici**: `titolo`, `requisito` e, per ciascuna variante in `varianti[<varId>].difetti` (stesso numero e stesso ordine di `VarianteLab.Difetti`), `indizio` e `spiegazione`.
- Codice (`codiceSano`, `righe`, `chiamata`, `correzione`, `trappole`), `output`, `esche` e `input` restano nel JSON principale: essendo verificati da Roslyn non vengono duplicati.
- Poiché l'`id` della scheda e gli id delle varianti sono fissi, modificare il testo italiano non richiede alcuna ri-chiavatura. `LaboratorioService.CaricaSchede(fonte, lingua)` applica la traduzione in modo non bloccante (file assente/corrotto o numero di difetti discorde → resta l'italiano con avviso su `Console.Error`), mentre `LaboratorioCoerenzaTests.Scheda_HaTraduzioneEnCoerente` e `TraduzioniEnLab_NessunFileSenzaScheda` verificano che ogni scheda abbia la traduzione inglese allineata e che non ci siano file orfani.

### Aggiungere un linguaggio

1. Aggiungere la coppia `(Id, Nome)` a `LaboratorioService.Linguaggi` in `wsa.quiz.core/Services/LaboratorioService.cs` (es. `("py", "Python")`).
2. Creare i file delle schede in `laboratorio/<id>/*.json` e le traduzioni in `traduzioni/en/laboratorio/<id>/*.json` (i glob `..\laboratorio\**\*.json` e `..\traduzioni\**\*.json` nei csproj li includono già).
3. La UI in `HomeView` elenca automaticamente i linguaggi di `LaboratorioService.Linguaggi` che hanno almeno una scheda valida e registra i risultati in cronologia/statistiche come `"<Nome> · Laboratorio"`.
4. Estendere `LaboratorioEsecuzioneTests.Esegui`, che smista l'esecuzione in base alla sottocartella del linguaggio (Roslyn per `cs`, `EseguiPython` per `py`), con il runner del nuovo linguaggio.
