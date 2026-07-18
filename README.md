# Quaestio

> Rebrand di *WSA Quiz* (2026-07-18). Il nome mostrato dall'app è ora **Quaestio** (dal latino "questione, indagine"); gli identificatori tecnici — progetti `Wsa.Quiz.*`, `ApplicationId` `it.eduits.wsaquiz`, cartella dati `WsaQuiz` — restano invariati per non rompere dati e build.

App di quiz multi-materia pensata per ripassare il programma del corso **Web & Software Architect (WSA)**. Tre eseguibili condividono lo stesso Core, gli stessi dati e la stessa cronologia:

- **`wsa.quiz.cli`** — versione console. Pratica per ripassare senza distrazioni o da terminale.
- **`wsa.quiz.desktop`** — versione grafica desktop (Avalonia 12).
- **`wsa.quiz.android`** — stessa GUI, impacchettata come APK Android (layout touch-friendly, bottom navigation a icone con swipe, safe-area edge-to-edge).

La GUI (desktop e Android) ha 5 sezioni: Home (configurazione + Avvia), Cronologia (sessioni passate con drill-down), Sospesi (sessioni in pausa, riprendibili), Statistiche (heatmap per materia/categoria), Gestione contenuti (import di materie/domande da JSON, guida per generarle con un'IA).

Una sessione di quiz può essere **classica** (ogni domanda chiesta una volta) o in **modalità rotazione** (le sbagliate tornano in coda finché non vengono indovinate). Si possono filtrare le domande per materia e categoria, limitare a N domande, randomizzare l'ordine, attivare/disattivare il cronometro. Si può mettere in pausa e riprendere dopo. La cronologia mostra punteggio, durata, % corrette e — al doppio click — il dettaglio domanda per domanda.

## Avvio rapido

Requisiti: **.NET 10 SDK**.

```bash
# Console
dotnet run --project wsa.quiz.cli

# GUI desktop
dotnet run --project wsa.quiz.desktop
```

`wsa.quiz.app` è la libreria UI condivisa (usata da desktop e Android): non si esegue direttamente. Per Android vedi `dotnet build wsa.quiz.android -c Release` più sotto in Architettura.

La cronologia viene salvata in una cartella utente condivisa fra console e GUI:

- Windows: `%APPDATA%\WsaQuiz`
- macOS:   `~/Library/Application Support/WsaQuiz`
- Linux:   `~/.config/WsaQuiz`

## Aggiungere materie, categorie e domande

Due modi, complementari:

### 1. Dall'app — "Gestione contenuti" (consigliato)

Nella GUI (desktop o Android), la 5ª tab **"Gestione contenuti"** permette di:

- **Creare una nuova materia**: nome + upload di un file `.json` con le sue domande (schema sotto). Validato prima dell'import: se anche una sola domanda non è conforme, l'intero file viene rifiutato e gli errori sono elencati.
- **Aggiungere domande a una materia esistente** (built-in o già importata): stesso upload, con anteprima prima di confermare. Le domande già presenti (stesso testo) vengono saltate automaticamente.
- **Rimuovere** una materia importata (le materie built-in non sono rimovibili da qui).

I contenuti importati restano **locali al dispositivo** (cartella utente, vedi sotto): non modificano i file del repo. Per farli entrare nel repo/APK ufficiale vanno copiati a mano nella cartella `domande/` (metodo 2).

### 2. A mano, editando i JSON nella root del repo

I dati si modificano via file JSON nella root del progetto. A ogni build i file vengono copiati negli output `bin/` (desktop/CLI) o incorporati nell'APK (Android); basta rilanciare l'eseguibile per vedere le modifiche.

#### Materie — `materie.json`

Elenco di oggetti, uno per materia. La `cartella` è relativa alla root e contiene tutti i `.json` con le domande di quella materia.

```json
[
  {
    "id": "cs",
    "nome": "C#",
    "cartella": "domande/cs",
    "formato": "standard"
  }
]
```

| Campo      | Tipo   | Note                                                                 |
|------------|--------|----------------------------------------------------------------------|
| `id`       | string | Identificatore breve, usato internamente. Univoco.                   |
| `nome`     | string | Etichetta mostrata in Home e nella cronologia.                       |
| `cartella` | string | Path relativo alla root. La cartella deve esistere.                  |
| `formato`  | string | Per ora sempre `"standard"`. Riservato per formati alternativi.      |

#### Domande — `domande/<materia>/<qualsiasi>.json`

Ogni file è un array di domande. Si possono avere più file per materia (es. `csharp_domande.json`, `csharp_domande_2.json`): il loader li unisce tutti. La **categoria** è un campo libero su ciascuna domanda — non va dichiarata altrove. La Home offre i filtri per materia e categoria automaticamente.

```json
[
  {
    "id": 302,
    "categoria": "Basi del linguaggio",
    "domanda": "Quale tipo di dato si usa in C# per rappresentare numeri interi?",
    "risposte": [
      "int",
      "char",
      "double",
      "float"
    ],
    "rispostaCorretta": 0,
    "spiegazione": "Il tipo `int` rappresenta numeri interi in C# (es. 42, -10, 0). `double` è per numeri decimali, `float` è un decimale a precisione singola, `char` rappresenta un singolo carattere."
  }
]
```

| Campo              | Tipo     | Note                                                                                  |
|--------------------|----------|---------------------------------------------------------------------------------------|
| `id`               | int      | Ignorato: l'app calcola un id stabile a partire dal contenuto della domanda.          |
| `categoria`        | string   | Etichetta libera. Stessa stringa = stessa categoria nei filtri.                       |
| `domanda`          | string   | Testo della domanda.                                                                  |
| `risposte`         | string[] | Esattamente **4** opzioni (l'import da app accetta anche solo 2, ma lo standard è 4). |
| `rispostaCorretta` | int      | Indice 0..3 della risposta giusta in `risposte`.                                      |
| `spiegazione`      | string   | Mostrata dopo aver risposto. Markdown leggero (apici, backtick, a-capo) consentito.   |

Internamente le 4 risposte vengono **rimescolate ad ogni partita** (in modo che A/B/C/D non siano sempre nello stesso ordine), e l'indice corretto viene ricalcolato di conseguenza.

## Generare nuove domande con un'IA

Le risposte multiple sono soggette a due bias tipici quando vengono generate da modelli di linguaggio:

1. **Bias della risposta più lunga**: l'IA tende a produrre la risposta corretta più dettagliata e i distrattori più brevi. Chi conosce il trucco indovina senza sapere la materia.
2. **Bias di posizione**: la risposta corretta finisce statisticamente più spesso in alcuni indici (tipicamente A o l'ultimo).

Il repo include una **guida** (`generazione-domande/GUIDA_GENERAZIONE_DOMANDE.md`) e un **motore** (`generazione-domande/quiz-engine.js`) pensati apposta per evitarli:

- La guida è un prompt riutilizzabile da dare a un'IA (o da seguire a mano): spiega come scrivere le domande, come bilanciare le lunghezze delle risposte in 4 categorie (più lunga / più corta / intermedia / simile, ~25% ciascuna) e gli errori tipici in cui un'IA cade sistematicamente.
- Il motore (`node`) prende il file dati scritto seguendo la guida, **valida** (campi mancanti, indici fuori range, duplicati), **misura** la distribuzione delle lunghezze e, solo se ok, **scrive** il JSON finale nello schema sopra, mescolando posizione delle risposte e ordine delle domande.

Uso da riga di comando:

```bash
# DRY RUN — controlla le quote senza scrivere
node "generazione-domande/quiz-engine.js" miamateria.data.js out.json

# Genera il JSON definitivo
node "generazione-domande/quiz-engine.js" miamateria.data.js miamateria_domande.json --write
```

Entrambi i file sono anche **scaricabili direttamente dall'app**, dalla tab "Gestione contenuti" (vedi sopra) — utile su Android, dove il repo non è raggiungibile. Il JSON generato si importa poi dalla stessa schermata (nuova materia, o aggiunta a una esistente).

## Architettura

Cinque progetti .NET 10 nella stessa solution:

```
Wsa.Quiz.sln
├── wsa.quiz.core/      Libreria. Modelli (Domanda, Materia, RisultatoQuiz, ...) e servizi
│                        (QuizService per shuffle/punteggio, StorageService per cronologia/sospesi,
│                        ContenutiUtenteService per l'import da "Gestione contenuti").
├── wsa.quiz.cli/       Console. Tutta l'interazione utente in Program.cs e Services/ConsoleUI.cs.
├── wsa.quiz.app/       Libreria UI condivisa Avalonia. State machine event-driven in
│                        State/SessioneQuiz.cs, viste in Views/. Usata sia da Desktop che Android.
├── wsa.quiz.desktop/   Head desktop (Window, ApplicationLifetime classico).
└── wsa.quiz.android/   Head Android (single-view, APK).
```

Storage condiviso: `StorageService(IFonteDati, cartellaUtente)`. La cartella utente contiene `cronologia.json`, `quiz_in_pausa.json` e — da "Gestione contenuti" — `ContenutiUtente/` (materie e domande importate dall'utente, mai scritte nei file built-in del repo). `IFonteDati` astrae i dati read-only built-in: file system (desktop/CLI) o risorse Avalonia incorporate (Android).

L'app GUI usa **code-behind + INotifyPropertyChanged**, no MVVM. Stile Fluent + font Inter.

Maggiori dettagli architetturali, decisioni prese, trappole incontrate e roadmap futura sono in [`CLAUDE.md`](./CLAUDE.md).

## Stato

Lavoro in corso, sviluppato per step incrementali documentati in `docs/superpowers/`. Funziona end-to-end su desktop Windows e su device Android fisico (vedi `CLAUDE.md` per il dettaglio delle verifiche). La tab "Gestione contenuti" (import da JSON) è verificata su desktop; su Android la build è verde e produce un APK firmato, ma il collaudo pratico su device/emulatore è ancora da fare. macOS/Linux non sono stati provati di recente ma il Core e Avalonia 12 sono cross-platform.

## Licenza

Uso personale didattico. Le domande sono pensate per il corso WSA — alcune categorie potrebbero non essere generalizzabili.
