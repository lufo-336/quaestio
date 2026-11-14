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
