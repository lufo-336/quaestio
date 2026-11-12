# Guida alla Generazione di Domande per Quiz

> **A cosa serve.** Questo file è un **prompt riutilizzabile**: dallo a un'IA (o seguilo tu)
> ogni volta che vuoi generare un banco di domande a risposta multipla per una materia.
> Insieme c'è il motore `quiz-engine.js`, che **verifica le quote e scrive il JSON**
> nel formato giusto. Tu/l'IA scrivete solo l'elenco di domande in un file dati; al resto
> (bilanciamento lunghezze, mescolamento, validazione) pensa il motore.

---

## 1. Come usarlo (istruzioni per l'IA)

1. **Leggi i parametri** della sezione 2 (li compila l'utente). Se mancano informazioni
   essenziali (materia, fonti da cui attingere, numero di domande), **chiedi all'utente**.
2. **Studia le fonti indicate** (PDF, manuali, dispense). Le domande devono basarsi **solo**
   su quei contenuti, non su conoscenza generica non verificata.
3. **Scrivi un file dati** `NOMEMATERIA.data.js` con l'array di domande nel formato della
   sezione 4, rispettando le convenzioni per il **codice** (sezione 5) e l'**universalità**
   (sezione 6). Per ciascuna domanda **decidi a tavolino** in quale "bucket di lunghezza" deve
   cadere la risposta corretta (sezione 7) e costruisci le 4 opzioni di conseguenza.
4. **Lancia il motore in DRY RUN** per controllare le quote e i controlli formali:
   `node quiz-engine.js NOMEMATERIA.data.js out.json`
   (oppure, dalla radice del repo: `node generazione-domande/quiz-engine.js NOMEMATERIA.data.js out.json`)
5. **Correggi gli scarti**: se una categoria di lunghezza è sopra/sotto la quota o se il motore
   segnala problemi di codice o riferimenti al corso, correggi le domande segnalate.
6. **Genera il JSON**: aggiungi `--write`:
   `node quiz-engine.js NOMEMATERIA.data.js NOMEMATERIA_domande.json --write`
7. **Valida** che il JSON sia parsabile e abbia tutti i campi previsti.

> Il motore **mescola da solo** la posizione della risposta corretta (così non è sempre la "B")
> e l'ordine delle domande (così bucket e categorie non sono raggruppati). Tu pensa solo a
> scrivere domande corrette, con codice ben formattato e centrando i bucket di lunghezza.

---

## 2. Parametri (da compilare prima di iniziare)

| Parametro | Valore | Note |
|-----------|--------|------|
| **Materia** | _(es. Python, C#, SQL, Cloud... )_ | |
| **Numero di domande (N)** | _(es. 100, 60, 30…)_ | N è libero |
| **Risposte per domanda** | **4** (default) | una sola corretta |
| **Fonti** | _(percorsi di PDF/manuali/dispense)_ | la fonte di verità |
| **Argomenti/categorie** | _(elenco aree da coprire)_ | servono a bilanciare |
| **Lingua** | Italiano (termini tecnici in lingua originale) | |
| **Distribuzione lunghezze** | **~25/25/25/25** (default) | vedi sezione 7 |

---

## 3. Formato di output (schema JSON — NON modificarlo)

Lo stesso schema dei banchi esistenti. Ogni domanda è un oggetto:

```json
{
    "id": 1,
    "categoria": "Nome area",
    "domanda": "Testo della domanda?",
    "risposte": ["opzione A", "opzione B", "opzione C", "opzione D"],
    "rispostaCorretta": 1,
    "spiegazione": "Perché la corretta è giusta e, se utile, perché le altre sono sbagliate."
}
```

- `rispostaCorretta` è l'**indice 0-based** dentro `risposte` (0 = prima opzione).
- L'`id` lo assegna il motore (rinumera dopo il mescolamento).

---

## 4. Formato del file dati (quello che scrivi tu/l'IA)

File `NOMEMATERIA.data.js`. Forma compatta: `c`=categoria, `d`=domanda, `r`=risposte,
`k`=indice 0-based della corretta, `s`=spiegazione.

```js
module.exports = [
  { c:"Basi", d:"Cos'è X?",
    r:["Definizione lunga e corretta di X che spiega bene il concetto","Un breve termine errato","Un altro errato","Un terzo errato"],
    k:0, s:"X è… ; le altre sono sbagliate perché…" },

  { c:"Basi", d:"Quale di questi è Y?",
    r:["Una definizione errata ma molto lunga e articolata che sembra plausibile","Y","Un'altra definizione errata altrettanto lunga","Una terza errata e lunga"],
    k:1, s:"Y è il termine corretto; le altre sono distrattori lunghi." },
];
```

---

## 5. Codice nelle domande

L'applicazione Quaestio gira su desktop, tablet e smartphone (Android/iOS) e visualizza il codice
tramite il controllo dedicato `TestoConCodice`, con font monospazio senza legature (`JetBrains Mono NL`),
numeri di riga e scorrimento orizzontale. Perché il rendering funzioni in modo impeccabile ovunque,
è indispensabile attenersi alle seguenti regole:

### Convenzione di formattazione

- **Blocchi di codice multiriga**: racchiusi tra triple backtick con identificatore di linguaggio
  obbligatorio:
  ````markdown
  ```linguaggio
  codice qui
  ```
  ````
  L'identificatore di linguaggio è **obbligatorio** (il motore segnala avviso se manca).
- **Linguaggi ammessi**:
  `python`, `csharp`, `cpp`, `c`, `sql`, `html`, `css`, `js`, `bash`, `json`, `http`.
- **Codice inline**: racchiuso tra singoli backtick (`` `codice` ``) sia nel testo della domanda,
  sia nelle risposte, sia nella spiegazione.

### Quando inline vs quando blocco

- Usa il **blocco** per:
  - Funzioni, classi, metodi o frammenti di codice su più righe.
  - Codice con indentazione significativa (es. Python).
  - Query SQL articolate o strutture JSON/HTML/CSS da analizzare.
- Usa **inline** per:
  - Identificatori singoli, nomi di variabili, funzioni, classi o proprietà (`x`, `len()`, `Console.WriteLine`).
  - Parole chiave di linguaggio (`async`, `yield`, `SELECT`, `def`).
  - Tipi di dato (`int`, `str`, `List<T>`).
  - Percorsi di file o comandi rapidi citati nel testo discorsivo.

### Risposte che contengono codice

- **Risposte di puro output / codice**: se una risposta è interamente un valore, un output a video,
  una tupla, una lista o un errore (es. nelle domande *"Cosa stampa questo codice?"*), va racchiusa
  interamente tra backtick:
  - `` `(7, 12)` ``
  - `` `'uno\n'` ``
  - `` `TypeError` ``
  - `` `[1, 2, 3]` ``
- **Risposte miste (testo + codice)**: racchiudi tra backtick **solo** la porzione di codice, lasciando
  il resto in testo naturale:
  - ``Solleva `KeyError` ``
  - ``Restituisce `None` ``
  - ``Stampa `0` ``
  - ``Una tupla vuota `()` ``
- **Parole usate come concetto**: rimangono in testo normale senza backtick quando sono espressioni
  in linguaggio naturale e non codice letterale (es. `'Due'`, `'Nessuna'`, `'Viene sollevata un'eccezione'`).

### Righe di codice brevi (max ~60 caratteri)

Sugli schermi degli smartphone lo spazio orizzontale è limitato. Anche se il riquadro di codice
supporta lo scorrimento orizzontale, una riga troppo lunga costringe l'utente a scorrere per capire
il quiz.
- Mantieni ogni riga di codice entro **circa 60 caratteri consigliati**.
- Se un'istruzione o una riga è troppo lunga, spezzala su più righe con indentazione coerente.

---

## 6. Universalità dei contenuti

Quaestio è progettata per essere utilizzata da chiunque studi o ripassi la materia, indipendentemente
da corsi specifici o percorsi accademici.

- **Nessun riferimento al contesto d'aula**: evita qualsiasi menzione a corsi, lezioni, numeri di slide,
  dispense, docenti/professori, esami, compiti in classe o esercitazioni d'aula
  (es. *"Come spiegato a lezione"*, *"Nella slide 14"*, *"Il docente suggerisce"*).
- **Universalità anche nel codice**:
  - Nessun commento nel codice che richiami esercitazioni (vietati `# esercizio 3`, `// lezione 4`, `# compito`).
  - Nomi di file, variabili e classi devono essere neutri e contestuali al problema (es. `utente`, `calcola_totale()`,
    non `es1_slide.py` o `consegna_wsa`).

Il motore rileva automaticamente parole sospette (`corso`, `lezion`, `slide`, `docente`, `prof`,
`dispens`, `esercitazion`, `eserciz`, `es1..es99`, `laboratorio`, `compito`) e le segnala in fase di audit.

---

## 7. La regola d'oro: distribuzione delle LUNGHEZZE

**Perché esiste.** In molti quiz "la risposta più lunga è quella giusta". Studiare su domande
con questo bias insegna una **scorciatoia**, non la materia. Per questo distribuiamo di proposito
la lunghezza della risposta corretta su **quattro categorie**, così la lunghezza **non dà
nessun indizio**.

### Le quattro categorie (bucket)

| Bucket | La risposta corretta è… | Quota default |
|--------|-------------------------|---------------|
| **L** | la **più lunga** delle 4 | ~25% |
| **S** | la **più corta** delle 4 | ~25% |
| **M** | **intermedia** (né la più lunga né la più corta) | ~25% |
| **Q** | di **lunghezza simile** agli altri (le 4 opzioni stanno entro ~6 caratteri l'una dall'altra) | ~25% |

> Variante a 3 categorie (se l'utente preferisce ~33/33/33): usa solo **L, S, M** e ometti Q.
> La soglia di "simile" (default 6 caratteri) è regolabile col flag `--simile=N` del motore.

### Come il motore calcola la lunghezza visibile

Il motore misura la lunghezza di ogni risposta tramite la funzione `lunghezzaVisibile(s)`:
1. Rimuove le righe di delimitazione dei blocchi (```` ```linguaggio ```` e ```` ``` ````).
2. Rimuove tutti i singoli backtick (`` ` ``).
3. Conta i caratteri effettivi che l'utente vede a schermo.

In questo modo, la presenza di backtick per formattare il codice **non altera né falsa**
l'assegnazione ai bucket.

Dette le lunghezze visibili delle 4 opzioni: `spread = max − min`.
Se `spread ≤ 6` → **Q** (simili). Altrimenti: corretta = max → **L**; corretta = min → **S**;
altrimenti → **M**.

---

## 8. ⚠️ Suggerimenti operativi (la parte che fa risparmiare tempo a te/all'IA)

Questi sono gli errori in cui un'IA cade **sistematicamente**. Leggili prima di scrivere.

- **Trappola n.1 — la corretta viene sempre lunga.** Per le domande "definizione" l'IA scrive
  d'istinto la risposta giusta come una frase lunga e i distrattori corti → escono **tutte L**.
  **Devi invertire di proposito**:
  - per un bucket **S**: scrivi la corretta **breve** (un termine, un valore tra backtick, una sigla)
    e gonfia i **distrattori** rendendoli frasi lunghe e plausibili;
  - per un bucket **M**: corretta di lunghezza media, con **un distrattore più lungo** e
    **uno più corto** di lei;
  - per un bucket **Q**: tutte e quattro le opzioni di lunghezza ravvicinata (es. 4 output brevi
    o 4 termini tecnici).
- **Trappola n.2 — la posizione.** Non mettere la corretta sempre nello stesso posto. (Il motore
  ri-mescola le posizioni, ma scrivi comunque con `k` vario per chiarezza.)
- **Distrattori plausibili, non assurdi.** Devono essere errori credibili (concetti vicini,
  confusioni tipiche di sintassi o logica), non opzioni palesemente ridicole: un buon distrattore "insegna" anche lui.
- **Una sola risposta inequivocabilmente corretta.** Evita ambiguità e doppie verità.
- **Vietati** "Tutte le precedenti" / "Nessuna delle precedenti" / "A e B": rompono la logica
  delle lunghezze e dei distrattori.
- **Distrattori coerenti per tipo:** se la corretta è un'eccezione o un valore di ritorno, anche i distrattori
  siano eccezioni o valori omogenei (evita che il tipo riveli la risposta).
- **Niente ripetizioni:** non usare la stessa domanda due volte; varia il fraseggio.
- **La spiegazione spiega davvero:** una frase sul perché la corretta è giusta e, dove utile,
  perché gli altri sono distrattori. È materiale di studio, non un riempitivo.
- **Fedeltà alle fonti:** ogni fatto deve essere verificabile nel materiale indicato. Se non sei
  sicuro di un dato o comportamento del codice, verificalo con cura.
- **Copri tutte le categorie:** distribuisci le N domande sugli argomenti in modo uniforme.
- **Accenti e apostrofi:** preferisci il testo italiano naturale con accenti (UTF-8: è, à, ù…).
  Se l'ambiente dà problemi di codifica, in alternativa usa la forma ASCII (es. `e'`, `perche'`)
  ma **sii coerente** in tutto il file.
- **Calibrare i bucket è iterativo:** è normale che il primo giro sia sbilanciato. Lancia il
  DRY RUN, guarda quali categorie sforano, riscrivi quelle opzioni, ripeti. Due-tre giri bastano.

---

## 9. Esempi pronti con codice (uno per bucket)

I conteggi mostrano i caratteri visibili effettivi (senza backtick).

```js
// L — corretta = la più lunga
{ c:"Controllo di flusso",
  d:"Che cosa stampa il seguente codice Python?\n\n```python\nx = [1, 2, 3]\nprint(x[10:])\n```",
  r:[
    "`IndexError`",                                                          // (10)
    "Una lista vuota `[]`, perché lo slicing tollera indici oltre il limite",  // (66) <- corretta
    "`None`",                                                                // (4)
    "`[3]`"                                                                  // (3)
  ],
  k:1,
  s:"Lo slicing in Python non solleva `IndexError` se l'indice iniziale supera la lunghezza della lista, ma restituisce una lista vuota." }

// S — corretta = la più corta
{ c:"Eccezioni",
  d:"Quale eccezione viene sollevata eseguendo questo codice?\n\n```python\nd = {'chiave': 42}\nprint(d['mancante'])\n```",
  r:[
    "Solleva `ValueError` perché il valore associato non esiste",            // (56)
    "`KeyError`",                                                            // (8)  <- corretta
    "Solleva `IndexError` interpretando il dizionario come sequenza",        // (61)
    "Solleva `AttributeError` per accesso a membro non definito"             // (57)
  ],
  k:1,
  s:"L'accesso con parentesi quadre a una chiave inesistente solleva `KeyError`; `get()` avrebbe invece restituito `None`." }

// M — corretta = intermedia (un distrattore più lungo, uno più corto)
{ c:"Funzioni",
  d:"Cosa restituisce la funzione `somma` se invocata come `somma(2)`?\n\n```python\ndef somma(a, b=3):\n    return a + b\n```",
  r:[
    "`TypeError`",                                                           // (9)
    "`5`, usando il valore predefinito per il secondo argomento",            // (56) <- corretta
    "`None`, poiché la funzione richiede obbligatoriamente tutti gli argomenti espliciti", // (82)
    "`2`"                                                                    // (1)
  ],
  k:1,
  s:"Il parametro `b` ha valore di default `3`, quindi `somma(2)` calcola `2 + 3 = 5`." }

// Q — quattro opzioni di lunghezza simile (spread ≤ 6)
{ c:"Operatori",
  d:"Qual è il valore di `risultato` al termine dell'operazione?\n\n```python\nrisultato = 7 // 2\n```",
  r:[
    "`3.5`",                                                                 // (3)
    "`3`",                                                                   // (1)  <- corretta
    "`4`",                                                                   // (1)
    "`2`"                                                                    // (1)
  ],
  k:1,
  s:"L'operatore `//` esegue la divisione intera (floor division), troncando a 3." }
```

---

## 10. Comandi del motore e audit (riepilogo)

Dalla cartella `generazione-domande/`:

```bash
# 1) DRY RUN — controlla quote, codice e universalità senza scrivere
node quiz-engine.js  miamateria.data.js  out.json

# 2) Genera il JSON definitivo
node quiz-engine.js  miamateria.data.js  miamateria_domande.json  --write

# Opzioni per generazione:
#   --seed=2026     mescolamento riproducibile (cambia per un nuovo ordine)
#   --simile=8      allarga/stringe la soglia del bucket "simile"
```

Dalla radice del repository:

```bash
# 3) AUDIT di un banco JSON già esistente
node generazione-domande/quiz-engine.js --audit domande/ux/ux_domande_unificate.json
node generazione-domande/quiz-engine.js --audit domande/python/python_domande.json --simile=6
```

### Cosa controlla l'audit (`--audit`)

Il comando `--audit`:
- Calcola e stampa la distribuzione delle lunghezze **L / S / M / Q** e delle posizioni della risposta corretta (**A / B / C / D**).
- Mostra il conteggio delle domande per categoria.
- **Analizza il codice** (`problemiCodice`): segnala fence ```` ``` ```` non chiusi, backtick dispari, blocchi di codice privi di identificatore di linguaggio.
- **Cerca riferimenti al corso** (`riferimentiCorso`): individua parole sospette legate al contesto d'aula (`corso`, `lezion`, `slide`, `docente`, `prof`, `dispens`, `esercitazion`, `eserciz`, `es1..es99`, `laboratorio`, `compito`).

---

## 11. Revisione di banchi esistenti e gestione traduzioni

In Quaestio l'ID di ogni domanda (`Domanda.Id`) viene calcolato a runtime da `StorageService.GeneraId`
come hash **SHA256 troncato a 12 caratteri esadecimali** di:
`testoDomanda | opzione0\u0001opzione1\u0001opzione2\u0001opzione3 | rispostaCorretta`.

> ⚠️ **Attenzione**: **qualsiasi modifica al testo** (anche solo aggiungere o rimuovere backtick,
> o racchiudere uno snippet in un blocco di codice) **cambia l'ID della domanda**.
> Poiché i file di traduzione inglese (`traduzioni/en/<materia>.json`) sono dizionari
> indicizzati per ID domanda, se l'ID cambia nel banco italiano la traduzione corrispondente
> diventa **orfana** e non compare più nell'app.

Per gestire in sicurezza la revisione dei banchi esistenti, sono disponibili due strumenti in
`generazione-domande/strumenti/`:

### 1. `converti_codice.py`
Automatizza la conversione del codice a blocchi e il riallineamento delle chiavi di traduzione:
```bash
# Dalla radice del repo:

# Conversione automatica nei banchi supportati
python3 -I generazione-domande/strumenti/converti_codice.py

# Ri-chiavatura dopo modifica a mano di un banco italiano:
# Confronta il vecchio banco con il nuovo e aggiorna traduzioni/en/<materiaId>.json
python3 -I generazione-domande/strumenti/converti_codice.py --richiava <materiaId> <banco_vecchio.json>
```

### 2. `coppie_it_en.py`
Consente la revisione affiancata di italiano e inglese senza rischiare disallineamenti:
```bash
# Esporta domande italiane e inglesi affiancate in un unico JSON di lavoro:
python3 -I generazione-domande/strumenti/coppie_it_en.py esporta <materiaId> coppie_lavoro.json

# (si modificano i testi 'it' ed 'en' mantenendo intatte struttura e risposte)

# Re-importa le modifiche, aggiorna il banco italiano e ri-chiava le traduzioni inglesi:
python3 -I generazione-domande/strumenti/coppie_it_en.py importa <materiaId> coppie_lavoro.json
```

---

## 12. Checklist finale prima di consegnare

- [ ] N domande generate, tutte basate sulle fonti indicate.
- [ ] Distribuzione lunghezze equilibrata (~25% L, ~25% S, ~25% M, ~25% Q nel DRY RUN / AUDIT).
- [ ] Posizione della risposta corretta distribuita tra A, B, C, D (non concentrata su una sola lettera).
- [ ] Categorie tematiche coperte in modo uniforme.
- [ ] Distrattori plausibili; una sola risposta inequivocabilmente corretta; niente "tutte/nessuna delle precedenti".
- [ ] Codice formattato secondo le convenzioni: blocchi con linguaggio specificato tra quelli ammessi, backtick pari, inline per simboli.
- [ ] Risposte con codice: output tra backtick, risposte miste solo per la parte di codice, concetti in testo semplice.
- [ ] Righe di codice brevi (consigliato max ~60 caratteri per leggibilità su smartphone).
- [ ] Universalità verificata: nessun riferimento a lezioni, docenti, slide o compiti (anche nei commenti del codice).
- [ ] Spiegazioni chiare, corrette ed esplicative.
- [ ] Motore in DRY RUN / `--audit` eseguito senza avvisi di errore o riferimenti al corso.
- [ ] Se sono stati modificati testi in un banco esistente: traduzioni inglesi ri-chiavate con successo.
- [ ] File JSON valido e conforme allo schema (`id, categoria, domanda, risposte, rispostaCorretta, spiegazione`).

> *Nota storica:* questo modello nasce dal banco "UX e Accessibilità" (100 domande,
> distribuzione 25/25/25/25). Vedi `domande/ux/ux_domande_unificate.json` per un esempio reale completo.
