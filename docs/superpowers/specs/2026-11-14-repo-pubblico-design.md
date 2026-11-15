# Punto 6 — Repo pubblico ordinato (design)

Data: 2026-11-14. Roadmap: punto 6 delle idee dell'11/11.

## Intento

Rendere pubblico `lufo-336/quaestio` (oggi privato). Il repo deve apparire
**pulito e ordinato come se fosse di un professionista**, ma raccontare in modo
onesto la storia vera: uno studente che ha iniziato il progetto mentre
cominciava a studiare C#, con un'app scritta in gran parte con l'IA
("vibe-coded"). Lo storico git resta com'è: è la testimonianza, con pregi e
difetti, del percorso di un programmatore alle prime armi. Chi guarda il repo
deve poter vedere il cammino dall'inizio a oggi.

## Obiettivi

1. **README nuovo, in inglese**, che sostituisce quello attuale (italiano, 160 righe).
2. **Screenshot** dell'app nel README.
3. **Diagramma dell'architettura** nel README.
4. **Licenza MIT** (`LICENSE`, copyright "2026 Luca Foglia").
5. **Metadati GitHub**: descrizione, topics, homepage = sito web.
6. **Revisione dei dati sensibili su tutta la storia** (non solo sui file attuali).
7. **Documenti di lavoro con l'IA restano pubblici** (decisione di Luca, che
   ha cambiato idea rispetto a toglierli): `CLAUDE.md`, `docs/claude/`,
   `docs/superpowers/`. Il README li segnala e vengono ripuliti (vedi sotto).
8. **Ultimo passo, solo su via libera esplicito di Luca**: aggiornare
   `.gitignore`, rendere pubblico.

## Non-obiettivi

- **Niente riscrittura della storia.** L'email `luca.foglia@edu-its.it` nei
  commit resta (deciso da Luca). Se la revisione trova un segreto vero (chiave,
  token, password), ci si ferma e si decide con Luca; non si riscrive in autonomia.
- Niente rinomina del codice: namespace `Wsa.Quiz.*`, cartelle `wsa.quiz.*`,
  `ApplicationId`, cartella dati `WsaQuiz` restano.
- Non si chiude qui il buco del campo `tipo` (guida e motore non lo conoscono):
  va nel README come obiettivo futuro.
- Non si traduce `GUIDA_GENERAZIONE_DOMANDE.md` (resta in italiano, il README lo dice).
- Niente CONTRIBUTING, Code of Conduct, template di issue: non è un progetto che
  cerca contributori (scelta di Claude).
- Niente ringraziamento nominale a Facundo Fanti (intervento minimo, deciso da Luca).

## Anti-obiettivi (sarebbe un fallimento anche se "funziona")

- Tono da marketing, o un README che nasconde che l'app è fatta con l'IA.
- Un README che sembra un prodotto commerciale e nega l'origine studentesca.
- Un segreto o un dato personale (oltre all'email dei commit, accettata) che
  finisce pubblico.
- Contenuti inutili o stantii: file che a un visitatore non servono,
  informazioni superate o contraddittorie nei documenti di lavoro.

## Contenuto del README (deciso)

Lingua: inglese. Struttura in quest'ordine:

1. **Titolo + una riga** su cos'è Quaestio, poi due link "Try it": versione web
   `https://lufo-336.github.io/quaestio/` e APK dall'ultima Release
   (`https://github.com/lufo-336/quaestio/releases/latest`).
2. **Screenshot** (vedi sotto).
3. **La storia**: nata come quiz da console per ripassare il corso Web Solution
   Architect (WsA) di Luca; poi GUI Avalonia, Android, iOS, web;
   11 materie / 2383 domande; interfaccia e domande in italiano e inglese.
   Va detto chiaramente che l'app è stata **vibe-coded** con l'IA e che lo storico
   git mostra davvero il percorso di un programmatore dilettante, pregi e difetti.
4. **Obiettivo attuale**: uno strumento di studio a quiz multi-materia, non
   più solo per il corso WsA.
5. **Funzionalità** (breve): modalità classica e a rotazione, filtri per
   materia/categoria/tipo (codice, teoria, architettura), pausa e ripresa,
   cronologia, statistiche heatmap, gestione contenuti, colore dell'app,
   it/en, codice formattato nelle domande.
6. **Architettura**: Core condiviso + una "testa" per piattaforma, con diagramma.
7. **Generazione delle domande**: la guida (da dare a un'IA) e il motore
   `quiz-engine.js` (mescolamento, bilanciamento lunghezze, `--audit`).
8. **Aggiungere le proprie materie**: da "Gestione contenuti" nell'app, oppure
   `materie.json` + `domande/<materia>/`; formato JSON di una domanda.
9. **Build**: prerequisiti (.NET 10; workload `android`, `ios`, `wasm-tools`
   per le rispettive teste) e comandi per ogni testa (cli, desktop, android,
   ios, browser).
10. **Nota sui nomi**: il brand è Quaestio, il codice si chiama ancora
    `Wsa.Quiz.*` perché rinominare avrebbe rotto dati e build: fa parte della storia.
11. **Traduzioni**: le domande in inglese sono tradotte con l'IA.
12. **Obiettivi futuri**: chiudere il buco del `tipo` (guida e motore che lo
    assegnano); quiz per lo studio delle lingue; una modalità in cui dopo una
    risposta sbagliata si può riprovare; un manuale didattico di teoria per le
    materie pre-installate.
13. **Ringraziamenti**: generico, ai compagni di corso che hanno provato l'app.
14. **Licenza**: MIT.

15. **Come è stato costruito / documenti di lavoro con l'IA**: una sezione che
    spiega che `CLAUDE.md` e `docs/claude/` sono il contesto che Claude Code
    legge a ogni sessione (trappole, architettura, piattaforme, storia) e che
    `docs/superpowers/` contiene spec e piani scritti con l'IA prima di ogni
    lavoro; sono in italiano e restano pubblici come parte del percorso.
    `docs/claude/trappole.md` va citato come la raccolta delle lezioni imparate.

Le informazioni tecniche giuste stanno nel README attuale e in `docs/claude/`.

## Screenshot (deciso)

Cinque immagini: **Home desktop**, **Home mobile**, **Statistiche**,
**Cronologia**, **una domanda dentro un quiz**.

Scelte di Claude (Luca può correggerle leggendo):
- Fatti sul Mac. Su questo Mac la testa desktop non parte (Avalonia.Native
  RenderTimer -6661), quindi Home desktop/Statistiche/Cronologia/domanda si
  catturano dalla **testa browser** (stessa UI Avalonia) servita in locale, in
  una finestra larga ~1280 px; Home mobile dal simulatore iOS (iPhone).
  Android non si compila sul Mac.
- Interfaccia **in inglese**, coerente con il README.
- La domanda mostrata contiene un blocco di codice (fa vedere la formattazione).
- Statistiche e Cronologia devono avere dati: prima si giocano alcune partite
  brevi su più materie.
- File PNG in `docs/images/` (questa cartella resta pubblica), dimensioni
  ragionevoli (larghezza ~1200 px desktop; mobile più stretto), nomi in inglese
  (`home-desktop.png`, `home-mobile.png`, `statistics.png`, `history.png`, `quiz.png`).

## Diagramma dell'architettura

Scelta di Claude: **Mermaid** dentro il README (GitHub lo disegna, è testo e si
aggiorna facilmente). Mostra: `Wsa.Quiz.Core` (modelli, QuizService,
StorageService, StatisticheService) → usato da `wsa.quiz.cli` e da
`Wsa.Quiz.App` (UI Avalonia condivisa) → teste `desktop`, `android`, `ios`,
`browser`; dati `materie.json` / `domande/` / `traduzioni/en/`; storage su
cartella utente (desktop/mobile) o `localStorage` (web); test in `wsa.quiz.core.tests`.

## Revisione dati sensibili

Su tutta la storia (`git log -p --all`) **e sulle Release GitHub esistenti**
(note di rilascio; gli APK sono build normali e non vanno toccati): chiavi/token/password, keystore o file
di firma, percorsi personali e nomi utente di macchina, email diverse da
quelle note, file grandi o binari inutili. Strumento: `gitleaks` se disponibile
(gratuito), altrimenti ricerca mirata con `git log -p | grep`. Output: un
rapporto per Luca. Percorsi personali e dati privati nei file attuali (doc IA
compresi, che restano pubblici) vanno tolti; nello storico restano (niente riscrittura).

## Pulizia dei documenti di lavoro (decisa: "diamo una pulita anche a loro")

Scelte di Claude su cosa significa "pulita" (Luca può correggerle):
- **Restano in italiano**: sono la lingua in cui il progetto è stato fatto.
- **`CLAUDE.md` e `docs/claude/*` (documenti vivi)**: togliere informazioni
  superate o contraddittorie (es. il layout repo di `CLAUDE.md` fermo a 6
  materie e senza le teste android/ios/browser/desktop; la nota su
  `dotnet run --project wsa.quiz.app` stantia), dati personali (percorsi con
  nomi utente di macchina, riferimenti all'esperimento in `llm-locale`), note
  che hanno senso solo sulle macchine di Luca (es. `LEGGIMI-memoria-junction.md`,
  `LEGGIMI-multimacchina.md`: tenere solo se servono a capire il progetto,
  altrimenti rimuovere). Il contenuto tecnico (trappole, architettura) non si taglia.
- **`docs/superpowers/` (spec e piani)**: sono documenti storici, non si
  riscrivono; si tolgono solo dati personali/sensibili trovati dalla revisione.
- **Un `docs/README.md` in inglese**, breve, che spiega cosa c'è in `docs/`
  e che sono documenti di lavoro con l'IA, in italiano.

## Ultimo passo (solo su via libera di Luca)

Luca deve riallineare i file locali sulle altre piattaforme: quindi questo
passo non parte senza il suo ok esplicito.

1. `.gitignore`: aggiungere `.remember/`, `.revisione/`, `.superpowers/`, `.claude/`.
2. Commit, push, `gh repo edit --visibility public`.

## Lasciato al costruttore

- Formulazione e lunghezza delle sezioni del README (tono: sobrio, onesto, in prima persona va bene).
- Badge (es. workflow di deploy web, licenza): facoltativi, pochi.
- Topics GitHub precisi.
- Come popolare i dati per gli screenshot.

## Lavoro con Antigravity

Luca vuole usare `agy` quando possibile: README (stesura e revisione), e
controlli meccanici vanno ad `agy_run.py`, invocato da Claude via shell (nessun
passaggio manuale di Luca). Screenshot, revisione dei dati sensibili,
metadati GitHub e ultimo passo restano a Claude (servono Mac, simulatore, `gh`).
