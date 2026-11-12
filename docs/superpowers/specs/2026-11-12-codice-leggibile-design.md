# Codice leggibile nelle domande + guida e motore di generazione — design

> Roadmap idee 11/11, punto 3. Data: 2026-11-12.

## Perché

Oggi il codice dentro le domande ha **lo stesso font e lo stesso colore** del
testo, e sul telefono le righe lunghe vanno a capo **perdendo l'indentazione**
(grave in Python, dove l'indentazione è sintassi). Luca studia anche da
telefono: il codice deve essere leggibile su desktop, Android, iOS e web.

Nello stesso progetto si rivedono la guida e il motore di generazione, così
le domande nuove nascono già nel formato giusto e la revisione delle 2383
domande it+en (punto 4) ha gli strumenti per misurare cosa correggere.

## Stato di partenza (rilevato il 2026-11-12)

- **Tre convenzioni diverse nei JSON**:
  - Python (111/220 domande): codice dopo `\n\n`, senza marcatori; 44 con righe indentate.
  - C#: 8 domande con blocchi ```` ``` ```` (senza linguaggio); ~400 con `` `inline` `` nel testo.
  - C++, REST, Cloud, Infra: solo `` `inline` ``, anche in risposte e spiegazioni
    (C#: 165 risposte, 515 spiegazioni; C++: 53 risposte, 171 spiegazioni).
- **La GUI non interpreta nulla**: `TextBlock` semplici con font Inter e `TextWrapping="Wrap"`
  in QuizView (domanda + risposte), RiepilogoView, CronologiaDettaglioView,
  CategoriaDettaglioView (testo + spiegazione). Backtick visibili tali e quali.
- **La console già interpreta** ```` ``` ```` e `` ` `` (`ConsoleUI.Tokenizza` /
  `ScriviTestoConCodice`, salta l'identificatore di linguaggio dopo ```` ``` ````).
- **ID domanda** = SHA256 troncato di testo + risposte + indice corretto
  (`StorageService`). Cambiare il testo cambia l'ID. Le traduzioni
  `traduzioni/en/<materia>.json` sono indicizzate per ID.
- **Bias lunghezze** (risposta corretta = la più lunga; ideale ~25%):
  C++ 57% (S solo 3%), C# 39%; le materie recenti stanno fra 20 e 33%.
- **Riferimenti al corso** ("corso", "lezione", "slide", "docente", "dispensa"…):
  ~55 domande in tutto, di più in Version Control (12) e Python (11).

## Obiettivi

1. **Riquadro codice** per i blocchi su più righe, in tutte le schermate che
   mostrano testo di domande/risposte/spiegazioni, su tutte le piattaforme.
2. **Codice inline** riconoscibile ovunque (domanda, risposte, spiegazione).
3. **Una sola convenzione** nei dati, applicata subito a Python (+ le 8 C#).
4. **Guida** riscritta con le regole: formato del codice, universalità, lunghezze.
5. **Motore** che valida il nuovo formato e sa fare l'**audit di un banco esistente**.

## Non obiettivi

- Colorazione della sintassi (parole chiave colorate): esclusa per ora.
- Migrazione di cronologia/statistiche per gli ID cambiati: **Luca cancellerà
  cronologia e statistiche**, nessuna migrazione.
- Riscrivere le domande vecchie (lunghezze C++/C#, riferimenti al corso): è il
  **punto 4**. Qui si preparano solo gli strumenti (audit).
- Cambiare lo schema JSON delle domande (campi): resta identico.

## Anti-obiettivi (sarebbe un fallimento anche se "funziona")

- L'indentazione si rompe ancora su telefono (righe che vanno a capo dentro il blocco).
- Il riquadro è illeggibile in uno dei due temi.
- Su web il codice esce con un font proporzionale (fallback).
- Le traduzioni inglesi delle domande convertite smettono di comparire.

## Decisioni prese con Luca

### UI (mockup `.superpowers/brainstorm/mockup-codice-v1.html`, varianti A+B)

- **Blocco su più righe**: riquadro separato dentro la card della domanda,
  font a **larghezza fissa**, **le righe non vanno mai a capo**, **scorrimento
  orizzontale** del riquadro quando una riga non entra.
- **Il riquadro segue il tema**: tema chiaro → sfondo grigio chiaro con bordo
  sottile (variante A); tema scuro → sfondo scuro tipo editor (variante B).
- **Numeri di riga** a sinistra, in **entrambi i temi**, in colore attenuato
  e non selezionabili se possibile.
- **Codice inline**: stesso font a larghezza fissa su un piccolo sfondo
  ("chip") che segue il tema; i backtick non si vedono.

### Dati

- Convenzione unica in tutte le materie:
  - blocco: ```` ```python ```` a capo, codice, a capo ```` ``` ````
    (identificatore di linguaggio obbligatorio nei dati nuovi: `python`,
    `csharp`, `cpp`, `c`, `sql`, `html`, `css`, `js`, `bash`, `json`, `http`…);
  - inline: `` `codice` `` in domanda, risposte e spiegazione.
- Conversione subito di: domande Python con codice dopo `\n\n` → blocco
  ```` ```python ````; le 8 C# → aggiungere `csharp`. Stessa conversione
  sulle corrispondenti traduzioni inglesi e **ri-chiavatura** delle
  traduzioni con i nuovi ID.

### Guida e motore

- **Universalità**: le domande devono avere senso per chiunque studi la
  materia, senza presupporre il corso WSA, le sue dispense o il docente.
  Vale **anche dentro il codice** (nomi di esercizi, commenti tipo
  `# esercizio 3`, nomi di file/variabili presi da esercitazioni del corso).
- La regola delle lunghezze (L/S/M/Q ~25% ciascuno) resta; la guida aggiunge
  esempi con codice e il motore misura la lunghezza **senza i backtick**.
- Il motore deve poter fare l'audit di un **banco JSON già esistente**
  (stesse statistiche del dry run) per il punto 4.

## Come (parti decise da me — da controllare)

- **Parser condiviso in Core**: spostare/estrarre il tokenizzatore della console
  in `Wsa.Quiz.Core` (es. `Services/TestoConCodice.cs`) con segmenti
  `Normale / InlineCode / CodeBlock(linguaggio, righe)`. Console e GUI usano lo
  stesso. Comportamento su testo malformato (backtick non chiuso): il backtick
  si mostra com'è, niente crash.
- **Controllo GUI riusabile**: un controllo in `wsa.quiz.app` (es.
  `Controls/TestoConCodice`) che riceve una stringa e costruisce: testo
  normale + inline (`TextBlock` con `Inlines`: `Run` + chip per l'inline,
  es. `InlineUIContainer` con `Border`) e blocchi (`Border` +
  `ScrollViewer` orizzontale + griglia numeri/codice). Sostituisce i
  `TextBlock` di domanda, risposte e spiegazione nelle 4 view.
  Code-behind, niente MVVM (scelta del progetto).
- **Font**: includere un font monospazio libero come risorsa (proposta:
  JetBrains Mono, licenza OFL), caricato come Inter, così è uguale su tutte
  le piattaforme e soprattutto su WASM (nessun font di sistema).
- **Colori del riquadro e del chip** in `ThemeDictionaries` (Light/Dark),
  non `SystemAccentColorBrush` (trappola 20) e non `DynamicResource Color`
  nei Setter (trappola 7).
- **Scroll**: lo scroll orizzontale del blocco non deve rubare lo scroll
  verticale della pagina (trappola 11, attenzione su touch Android/iOS).
- **Risposte**: il chip inline vale anche nelle risposte dei bottoni A/B/C/D;
  i colori corretta/sbagliata del bottone non devono rendere il chip illeggibile
  (trappola 12: `Foreground` sui bottoni).
- **Console**: invariata nel comportamento; usa il parser di Core. Opzionale
  mostrare i numeri di riga.
- **Python, risposte**: nelle domande "Che cosa stampa", le risposte che sono
  *interamente* output/codice (`(7, 12)`, `'uno\n'`, `TypeError`) vanno tra
  backtick; le risposte in prosa no. Conversione assistita, rivista a mano.
- **Motore** (`generazione-domande/quiz-engine.js`):
  - validazione: ```` ``` ```` non chiusi, backtick dispari, blocco senza linguaggio (avviso);
  - lunghezza delle risposte calcolata sul testo senza backtick;
  - avviso "riferimento al corso" con elenco di parole sospette (corso,
    lezione, slide, docente, prof, dispensa, esercitazione, esercizio, es1…)
    cercate in domanda, risposte, spiegazione e codice;
  - nuova modalità `--audit <banco.json>`: legge lo schema JSON finale
    (`domanda/risposte/rispostaCorretta`) e stampa L/S/M/Q, posizione,
    categorie, avvisi. Nessuna scrittura.
- **Guida** (`generazione-domande/GUIDA_GENERAZIONE_DOMANDE.md`): nuove
  sezioni "Codice nelle domande", "Universalità", esempi L/S/M/Q con codice,
  checklist aggiornata, sezione audit; correggere i nomi file obsoleti
  (`"MODELLO - quiz-engine.js"` → `quiz-engine.js`) e il riferimento a una
  cartella UX che non esiste più nel repo.
- **Lavoro di volume** (conversione Python, riscrittura guida, motore,
  controllo GUI da piano descrittivo) via pipeline **Antigravity**, con
  revisione e build/verifica da parte di Claude.
- **Documentazione**: aggiornare `docs/claude/contenuti-e-dati.md` (convenzione
  codice) e `docs/claude/architettura-gui.md` (controllo TestoConCodice).

- **Copertura** (mia scelta): il controllo vale anche per la spiegazione
  nell'area feedback di QuizView e per le domande importate da Gestione
  contenuti (stesso testo, stesso controllo: nessun lavoro in più).
- **Conversione Python** (mia scelta): il blocco è tutto ciò che segue il
  primo `\n\n` fino alla fine del testo; se dopo il codice c'è altro testo
  (raro), si chiude il blocco a mano. Script one-shot, risultato rivisto.
- **Audit** (mia scelta): solo sui banchi italiani in `domande/`; le
  traduzioni si controllano solo per chiavi orfane.

## Verifica

- Test unitari del parser (Core): blocchi con/senza linguaggio, inline,
  malformati, `\r\n`, blocco a inizio/fine testo.
- Script di controllo dati: nessuna domanda Python con codice fuori dai
  blocchi; ogni traduzione inglese convertita trova la sua domanda (nessuna
  chiave orfana); motore `--audit` pulito su Python e C#.
- Test a mano: una domanda Python indentata con riga lunga, in tema chiaro e
  scuro, su desktop, Android (emulatore), iOS (simulatore) e web.

## Lasciato a chi costruisce

Dimensioni esatte di font/padding/colori (partire dal mockup), nome esatto
dei file nuovi, struttura interna del motore, come gestire la ri-chiavatura
delle traduzioni (script one-shot va bene).
