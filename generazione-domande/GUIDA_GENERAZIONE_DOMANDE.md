# MODELLO — Generatore di Domande per Quiz

> **A cosa serve.** Questo file è un **prompt riutilizzabile**: dallo a un'IA (o seguilo tu)
> ogni volta che vuoi generare un banco di domande a risposta multipla per una materia.
> Insieme c'è il motore `MODELLO - quiz-engine.js`, che **verifica le quote e scrive il JSON**
> nel formato giusto. Tu/l'IA scrivete solo l'elenco di domande in un file dati; al resto
> (bilanciamento lunghezze, mescolamento, validazione) pensa il motore.

---

## 1. Come usarlo (istruzioni per l'IA)

1. **Leggi i parametri** della sezione 2 (li compila l'utente). Se mancano informazioni
   essenziali (materia, fonti da cui attingere, numero di domande), **chiedi all'utente**.
2. **Studia le fonti indicate** (PDF, manuali, dispense). Le domande devono basarsi **solo**
   su quei contenuti, non su conoscenza generica non verificata.
3. **Scrivi un file dati** `NOMEMATERIA.data.js` con l'array di domande nel formato della
   sezione 4. Per ciascuna domanda **decidi a tavolino** in quale "bucket di lunghezza" deve
   cadere la risposta corretta (sezione 5) e costruisci le 4 opzioni di conseguenza.
4. **Lancia il motore in DRY RUN** per controllare le quote:
   `node "MODELLO - quiz-engine.js" NOMEMATERIA.data.js out.json`
5. **Correggi gli scarti**: se una categoria è sopra/sotto la quota, riscrivi le opzioni
   delle domande segnalate finché la distribuzione è ~equilibrata (sezione 5).
6. **Genera il JSON**: aggiungi `--write`:
   `node "MODELLO - quiz-engine.js" NOMEMATERIA.data.js NOMEMATERIA_domande.json --write`
7. **Valida** che il JSON sia parsabile e abbia tutti i campi.

> Il motore **mescola da solo** la posizione della risposta corretta (così non è sempre la "B")
> e l'ordine delle domande (così bucket e categorie non sono raggruppati). Tu pensa solo a
> scrivere domande corrette e a centrare i bucket di lunghezza.

---

## 2. Parametri (da compilare prima di iniziare)

| Parametro | Valore | Note |
|-----------|--------|------|
| **Materia** | _(es. UX e Accessibilità)_ | |
| **Numero di domande (N)** | _(es. 100, 60, 30…)_ | N è libero |
| **Risposte per domanda** | **4** (default) | una sola corretta |
| **Fonti** | _(percorsi di PDF/manuali/dispense)_ | la fonte di verità |
| **Argomenti/categorie** | _(elenco aree da coprire)_ | servono a bilanciare |
| **Lingua** | Italiano (termini tecnici in inglese) | |
| **Distribuzione lunghezze** | **~25/25/25/25** (default) | vedi sezione 5 |

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

## 5. La regola d'oro: distribuzione delle LUNGHEZZE

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

### Come il motore classifica (per sapere cosa ti aspetta)

Su una domanda, dette le lunghezze in caratteri delle 4 opzioni:
`spread = max − min`. Se `spread ≤ 6` → **Q** (simili). Altrimenti: corretta = max → **L**;
corretta = min → **S**; altrimenti → **M**.

---

## 6. ⚠️ Suggerimenti operativi (la parte che fa risparmiare tempo a te/all'IA)

Questi sono gli errori in cui un'IA cade **sistematicamente**. Leggili prima di scrivere.

- **Trappola n.1 — la corretta viene sempre lunga.** Per le domande "definizione" l'IA scrive
  d'istinto la risposta giusta come una frase lunga e i distrattori corti → escono **tutte L**.
  **Devi invertire di proposito**:
  - per un bucket **S**: scrivi la corretta **breve** (un termine, una sigla) e gonfia i
    **distrattori** rendendoli frasi lunghe e plausibili;
  - per un bucket **M**: corretta di lunghezza media, con **un distrattore più lungo** e
    **uno più corto** di lei;
  - per un bucket **Q**: tutte e quattro le opzioni di lunghezza ravvicinata (spesso termini
    o frasi brevi simili).
- **Trappola n.2 — la posizione.** Non mettere la corretta sempre nello stesso posto. (Il motore
  ri-mescola le posizioni, ma scrivi comunque con `k` vario per chiarezza.)
- **Distrattori plausibili, non assurdi.** Devono essere errori credibili (concetti vicini,
  confusioni tipiche), non opzioni palesemente ridicole: un buon distrattore "insegna" anche lui.
- **Una sola risposta inequivocabilmente corretta.** Evita ambiguità e doppie verità.
- **Vietati** "Tutte le precedenti" / "Nessuna delle precedenti" / "A e B": rompono la logica
  delle lunghezze e dei distrattori.
- **Distrattori coerenti per tipo:** se la corretta è un numero, anche i distrattori siano numeri;
  se è una data, date; ecc. (evita che il tipo riveli la risposta).
- **Niente ripetizioni:** non usare la stessa domanda due volte; varia il fraseggio.
- **La spiegazione spiega davvero:** una frase sul perché la corretta è giusta e, dove utile,
  perché gli altri sono distrattori. È materiale di studio, non un riempitivo.
- **Fedeltà alle fonti:** ogni fatto deve essere verificabile nel materiale indicato. Se non sei
  sicuro di un dato (una cifra, un criterio, una sigla), **non inventarlo**.
- **Copri tutte le categorie:** distribuisci le N domande sugli argomenti in modo uniforme.
- **Accenti e apostrofi:** preferisci il testo italiano naturale con accenti (UTF-8: è, à, ù…).
  Se l'ambiente dà problemi di codifica, in alternativa usa la forma ASCII (es. `e'`, `perche'`)
  ma **sii coerente** in tutto il file.
- **Calibrare i bucket è iterativo:** è normale che il primo giro sia sbilanciato. Lancia il
  DRY RUN, guarda quali categorie sforano, riscrivi quelle opzioni, ripeti. Due-tre giri bastano.

---

## 7. Esempi pronti (uno per bucket)

Conteggi di caratteri indicativi tra parentesi.

```js
// L — corretta = la più lunga
{ c:"Esempio", d:"Che cos'è il reflow?",
  r:["Un suono",                                                              // (8)
     "La capacità del layout di riorganizzarsi senza perdere contenuto",      // (62)  <- corretta
     "Un font",                                                               // (7)
     "Un colore"],                                                            // (9)
  k:1, s:"Il reflow riorganizza il layout quando il viewport si stringe." }

// S — corretta = la più corta
{ c:"Esempio", d:"Quale attività appartiene alla UI e non alla UX?",
  r:["Lo studio dei bisogni reali tramite user research e interviste",        // (60)
     "Il visual design",                                                      // (17)  <- corretta
     "La progettazione dell'interazione e dei percorsi di navigazione",       // (62)
     "La valutazione dell'usabilità con i test con gli utenti"],              // (54)
  k:1, s:"Il visual design è UI; ricerca, interazione e usabilità sono UX." }

// M — corretta = intermedia (un distrattore più lungo, uno più corto)
{ c:"Esempio", d:"Su cosa si concentra l'aspetto 'How'?",
  r:["Sul prezzo",                                                            // (10)
     "Sull'accessibilità e sull'estetica dell'interfaccia",                   // (51)  <- corretta
     "Sulla strategia commerciale e sulle modalità di vendita del prodotto",  // (68)
     "Sui ricavi"],                                                           // (11)
  k:1, s:"L'How riguarda accessibilità ed estetica dell'interfaccia." }

// Q — quattro opzioni di lunghezza simile (spread ≤ 6)
{ c:"Esempio", d:"Quante fasi ha il Double Diamond?",
  r:["Due", "Quattro", "Cinque", "Sette"],                                    // (3/7/6/5)
  k:1, s:"Quattro: Discover, Define, Develop, Deliver." }
```

---

## 8. Comandi del motore (riepilogo)

```bash
# 1) DRY RUN — controlla le quote senza scrivere
node "MODELLO - quiz-engine.js"  miamateria.data.js  out.json

# 2) Genera il JSON definitivo
node "MODELLO - quiz-engine.js"  miamateria.data.js  miamateria_domande.json  --write

# Opzioni:
#   --seed=2026     mescolamento riproducibile (cambia per un nuovo ordine)
#   --simile=8      allarga/stringe la soglia del bucket "simile"
```

Il DRY RUN stampa: distribuzione L/S/M/Q in %, posizione della risposta corretta (A/B/C/D)
e numero di domande per categoria. Il motore **valida** anche: domande/risposte mancanti,
indice corretto fuori range, risposte o domande duplicate, spiegazione mancante.

---

## 9. Checklist finale prima di consegnare

- [ ] N domande generate, tutte basate sulle fonti indicate
- [ ] Distribuzione lunghezze ~equilibrata (DRY RUN ok)
- [ ] Posizione della risposta corretta distribuita (non sempre la stessa lettera)
- [ ] Categorie coperte in modo uniforme
- [ ] Distrattori plausibili; una sola risposta corretta; niente "tutte/nessuna delle precedenti"
- [ ] Spiegazioni utili e corrette
- [ ] JSON valido e con i campi: `id, categoria, domanda, risposte, rispostaCorretta, spiegazione`

> *Nota storica:* questo modello nasce dal banco "UX e Accessibilità" (100 domande,
> distribuzione 25/25/25/25). Vedi `UX e Accessabilità/ux/` per un esempio reale completo
> (`_genera.js` = versione specifica, `ux_domande_unificate.json` = output).
