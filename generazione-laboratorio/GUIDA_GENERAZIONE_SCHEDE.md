# Guida alla generazione delle schede del Laboratorio

> **A cosa serve.** È un **prompt riutilizzabile**: dallo a un'IA (o seguilo tu) per scrivere
> nuove schede del Laboratorio di codice. Insieme c'è il **motore** (`motore/`), che **esegue
> davvero il codice**, calcola `output`, `atteso` ed `effettivo`, controlla i vincoli più facili da
> sbagliare e scrive il JSON in `laboratorio/<linguaggio>/`. Tu (o l'IA) scrivi codice, difetti
> e testi; i valori stampati non si calcolano mai a mente.
>
> Riferimento completo del formato: `docs/claude/contenuti-e-dati.md`, sezione "Schede del Laboratorio".

---

## 1. Come usarlo (istruzioni per l'IA)

1. **Leggi i parametri** della sezione 2. Se manca qualcosa di essenziale (linguaggio, livello,
   quante schede), chiedi.
2. **Guarda le schede esistenti** dello stesso linguaggio e livello in `laboratorio/<ling>/`, come
   modello di formato e difficoltà, e per **non ripetere i temi**.
3. **Scrivi una bozza per scheda** in `generazione-laboratorio/bozze/<ling>/<id>.json` (cartella
   ignorata da git). **Mai** dentro `laboratorio/`: ogni `.json` lì viene caricato dall'app.
   Puoi omettere `output`, `atteso` ed `effettivo`: li calcola il motore.
4. **Lancia il motore in dry run** dalla radice del repo:
   ```powershell
   dotnet run --project generazione-laboratorio/motore -- generazione-laboratorio/bozze/py/<id>.json py
   ```
   Stampa per ogni variante l'output reale e, per ogni input, `atteso`/`effettivo` e se **ESPONE** il
   difetto. Se trova `ERRORE` esce con codice 1 e non scrive niente.
5. **Leggi il risultato come un revisore**: l'output è quello che volevi? Gli input che espongono il
   difetto sono quelli giusti? Le esche sono ancora credibili ora che conosci l'output vero?
   Correggi la bozza e rilancia finché non ci sono errori e gli `AVVISO` sono spiegati.
6. **Scrivi la scheda** aggiungendo `--write`: crea `laboratorio/<ling>/<id>.json`.
   Lo stesso comando su un file già in `laboratorio/` lo ricalcola in posto (utile dopo una modifica al codice).
7. **Scrivi la traduzione** `traduzioni/en/laboratorio/<ling>/<id>.json` (sezione 8).
8. **Valida tutto**:
   ```powershell
   dotnet test wsa.quiz.core.tests --filter Laboratorio
   ```
   I test controllano tutta la struttura (id, conteggi, righe dei difetti, traduzioni) e rieseguono il codice.

Per Python serve l'interprete (`py -3` su Windows, `python3` altrove), per C# niente di esterno (Roslyn).

---

## 2. Parametri (da compilare prima di iniziare)

| Parametro | Valore | Note |
|---|---|---|
| **Linguaggio** | `cs` (C#) o `py` (Python) | un linguaggio nuovo richiede codice: `contenuti-e-dati.md`, "Aggiungere un linguaggio" |
| **Livello** | `base`, `media` o `alta` | sezione 4 |
| **Numero di schede** | _(es. 3)_ | ogni scheda ha 2 varianti (fino a 4) |
| **Temi** | _(facoltativo)_ | credibili per il lavoro, diversi da quelli già presenti |

---

## 3. Che cos'è una scheda

Una scheda è una funzione con un **requisito** e 2–4 **varianti**: lo stesso codice con un difetto
diverso. A ogni sessione esce una variante a caso e l'utente fa tre passi sulla stessa funzione:

1. **Prevedi l'output**: legge `righe` + `chiamata` e sceglie cosa stampa fra `output` e le `esche`.
2. **Trova il bug**: indica la riga difettosa e il tipo di difetto (al livello `media` anche la
   correzione fra `correzione` e 2 `trappole`; al livello `alta` segna tutti i difetti, punteggio parziale).
3. **Rompi la funzione**: fra gli `input` sceglie quello che fa sbagliare il codice
   (`atteso` ≠ `effettivo`).

---

## 4. Livelli

| Livello | Righe | Difetti per variante | In più |
|---|---|---|---|
| `base` | 8–12 | esattamente 1 | – |
| `media` | 15–25 | esattamente 1 | `correzione` + esattamente 2 `trappole` |
| `alta` | 30+ | 2–3, **dal più grave al meno grave**, tipi diversi | almeno un input che espone il più grave |

Le 2 varianti di una scheda devono avere difetti di **tipo diverso**.

---

## 5. Formato della bozza

È il formato della scheda finale (`SchedaLab`), con i campi calcolati facoltativi:

```json
{
  "id": "filtra-finestra",
  "titolo": "Filtro finestra temporale",
  "livello": "base",
  "requisito": "Data una lista di minuti e un limite, restituisce i soli valori da 0 al limite incluso.",
  "chiamata": "print(filtra_finestra([10, 30, 45, 50], 30))",
  "codiceSano": [ "def filtra_finestra(minuti, limite):", "    ..." ],
  "varianti": [
    {
      "id": "v1",
      "righe": [ "def filtra_finestra(minuti, limite):", "    ..." ],
      "esche": [ "[10, 30]", "[10, 30, 45]" ],
      "difetti": [
        {
          "riga": 5,
          "tipo": "MutazioneIterazione",
          "indizio": "Guarda su quale sequenza itera il ciclo for.",
          "spiegazione": "Il ciclo rimuove elementi dalla lista che sta scorrendo, quindi salta quello successivo."
        }
      ],
      "input": [
        { "chiamata": "print(filtra_finestra([5, 40, 15], 30))" },
        { "chiamata": "print(filtra_finestra([40, 50], 30))" },
        { "chiamata": "print(filtra_finestra([], 30))" }
      ]
    }
  ]
}
```

- `id`: kebab-case, unico nel linguaggio, senza `:`; varianti `v1`, `v2`…
- `codiceSano`: il codice corretto, mai mostrato all'utente: è il riferimento per `atteso`.
- `righe`: il codice mostrato, con il difetto; `riga` dei difetti è 1-based su `righe`.
- `chiamata` (della scheda): l'esempio del passo 1; può avere più istruzioni separate da `\n`,
  ogni riga stampata diventa una riga di `output`.
- `input`: 3–4, con `chiamata` diverse fra loro; **almeno uno espone** il difetto e **almeno uno no**.
- `esche`: almeno 2, diverse fra loro e da ogni riga di `output`.

---

## 6. Regole per linguaggio

**Python (`py`)**
- Modulo Python 3 con funzioni a livello superiore (`def`), indentazione a 4 spazi, solo libreria
  standard; niente file, rete, input da tastiera, casualità o orari.
- La `chiamata` stampa con `print(...)`. L'output è quello vero di `print`: `True`, `None`, `90.0`
  (un float intero ha `.0`), `[1, 2]`, stringhe senza virgolette (dentro le liste con gli apici).
- Eccezione non gestita: righe stampate fino a lì, poi `!NomeEccezione` (es. `!ZeroDivisionError`).
- `SyntaxError`/`IndentationError` = "non compila": ammesso solo nelle trappole.

**C# (`cs`)**
- Script C# top-level (funzioni senza `class Program`/`Main`), using impliciti `System`,
  `System.Linq`, `System.Collections.Generic`, `System.Text`.
- La `chiamata` stampa con `Console.WriteLine`. Cultura invariante (decimali col punto).
- Eccezione non gestita: righe stampate fino a lì, poi `!NomeEccezione` (es. `!DivideByZeroException`).
- Errori di compilazione ammessi solo nelle trappole.

**Per tutti**: righe ≤ ~60 caratteri (leggibili sul telefono), nomi e commenti in italiano, nessun
riferimento a corsi, lezioni o docenti.

---

## 7. Come scrivere una buona scheda

**Il difetto**
- Usa uno dei 10 tipi di `TipiDifetto`: `Confine`, `DivisioneIntera`, `NullVuoto`,
  `CondizioneInvertita`, `Overflow`, `OperandoSbagliato`, `ConfrontoStringhe`,
  `MutazioneIterazione`, `EccezioneInghiottita`, `RisorsaNonChiusa`.
- Sfrutta gli errori tipici del linguaggio, non tradurre una scheda da un altro. In Python per
  esempio: rimuovere da una lista mentre la si scorre, argomento di default mutabile, `is` al
  posto di `==`, `//` al posto di `/`, `except` che inghiotte l'errore. `Overflow` in Python non
  esiste (gli int sono illimitati); `RisorsaNonChiusa` solo se dimostrabile senza file veri.
- La riga del difetto non può essere vuota, una sola parentesi `{`/`}`, né identica (dopo trim) a
  una riga di `codiceSano`.
- **Ogni riga che cambia rispetto a `codiceSano` deve far parte di un difetto dichiarato.** Al
  livello `alta` segnare una riga sana costa un punto: una differenza non dichiarata è un bug nascosto
  che punisce l'utente. Il motore lo segnala con un `AVVISO` (se il numero di righe cambia, controlla a mano).

**I testi**
- `indizio`: dice **dove** guardare, non **cosa** c'è ("Guarda il confronto nell'elif", non "il `>=` dovrebbe essere `>`").
- `spiegazione`: perché è sbagliato e quale effetto ha, con un caso concreto.

**Le esche** (passo 1): sbagli plausibili di chi legge in fretta: l'output del codice sano, quello
con un elemento in meno, il valore senza arrotondamento. Scrivile **dopo** aver visto l'output vero
dal motore.

**Gli input** (passo 3): uno o due che espongono il difetto in modo diverso, e almeno uno innocuo che
sembri pericoloso (lista vuota, valore al limite gestito bene).

**Livello `media`, correzione e trappole**: la `correzione` sostituisce la riga del difetto e deve
dare `atteso` su tutti gli input; ogni `trappola` deve sembrare una correzione ma sbagliare (o non
compilare) su almeno un input. Il motore verifica entrambe le cose.

**Livello `alta`**: le righe sane devono sembrare sospette quanto basta da non rendere ovvi i difetti,
ma essere davvero corrette. Difetti di gravità diversa (uno che rompe il risultato, uno di confine,
uno più sottile), elencati dal più grave.

**Valori**: preferisci numeri esatti (interi, .5, .25) a calcoli con arrotondamenti incerti.

---

## 8. Traduzione inglese

Ogni scheda ha `traduzioni/en/laboratorio/<ling>/<id>.json` con **solo** i testi didattici:

```json
{
  "titolo": "Time window filter",
  "requisito": "Given a list of minutes and a limit, returns only the values from 0 up to the limit included.",
  "varianti": {
    "v1": { "difetti": [ { "indizio": "Look at which sequence the for loop iterates over.", "spiegazione": "..." } ] },
    "v2": { "difetti": [ { "indizio": "...", "spiegazione": "..." } ] }
  }
}
```

Stesse varianti e stesso numero e ordine di difetti della scheda. Il codice non si traduce.

---

## 9. Checklist finale

- [ ] Il motore dà 0 errori e ogni `AVVISO` è spiegato.
- [ ] `output`, `atteso`, `effettivo` vengono dal motore (`--write`), non da calcoli a mano.
- [ ] Le 2 varianti hanno difetti di tipo diverso; ad `alta` sono in ordine di gravità.
- [ ] Gli indizi non svelano il difetto.
- [ ] La traduzione inglese c'è ed è allineata.
- [ ] `dotnet test wsa.quiz.core.tests --filter Laboratorio` è verde.
- [ ] Nessun file temporaneo in `laboratorio/` o in `traduzioni/`.
