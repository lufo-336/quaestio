# Design — Pausa che congela il timer + Modalità a tempo

> Data: 2026-06-03
> Stato: approvato in brainstorming, in attesa di review utente prima del piano.

## Obiettivo

Due funzionalità richieste da Luca:

1. **La pausa congela il cronometro.** Oggi mettere in pausa e poi riprendere
   (ramo "Annulla" della modale) lascia che il tempo passato con la modale
   aperta continui a contare. Deve invece fermarsi e ripartire alla ripresa.
2. **Modalità a tempo.** L'utente sceglie per quanti minuti gioca; allo scadere
   la partita termina. Combinabile con tutte le altre opzioni (rotazione,
   limite domande, categorie, ecc.).

Scope: **solo app GUI (`Wsa.Quiz.App`) + modello condiviso (`Wsa.Quiz.Core`)**.
Il console (`Wsa.Quiz.Cli`) resta invariato — i nuovi campi hanno default
neutri che non lo toccano.

## Decisioni prese in brainstorming

- **Allo scadere: termina subito** (anche a metà di una domanda non risposta).
- **Display: countdown alla rovescia con colore di allerta** — verde normale,
  ambra sotto 1 minuto, rosso sotto 10 secondi. Forzato visibile quando la
  modalità a tempo è attiva, anche se "Mostra cronometro" è spento.
- **Esito timeout: completata** (non abbandono), con nota/badge "⏱ tempo
  scaduto" in riepilogo e cronologia. Percentuale e punteggio calcolati
  normalmente sulle domande affrontate.
- **Domanda in corso non risposta allo scadere: ignorata** — non finisce in
  `Dettagli`, non viene conteggiata. Percentuale/totale solo sulle domande
  effettivamente risposte. (Nessun codice aggiuntivo: la domanda corrente non
  è ancora in `Dettagli` al momento della scadenza.)
- **Colore di allerta (opzione A):** `SessioneQuiz` espone direttamente un
  `IBrush TempoBrush`, bindato a `Foreground`. Frizione minima, riusa gli hex
  di `QuizColors`.

## Feature 1 — La pausa congela il cronometro

### Stato attuale

`_cron` (`Stopwatch`) e `_timer` (`DispatcherTimer`) vengono fermati solo in
`EsportaPausa()` (Salva e esci) e `Concludi()` (Abbandona/fine). Sul ramo
"Annulla" di `QuizView.ApriMenuPausaAsync` nulla li ferma: il tempo con la
modale aperta conta.

### Modifica

`SessioneQuiz` — due metodi pubblici nuovi:

- `SospendiCronometro()` → `_cron.Stop(); _timer.Stop();`
- `RiprendiCronometro()` → `_cron.Start();` e `_timer.Start()` se
  `MostraTempo` (cioè se cronometro o modalità a tempo attivi).

`QuizView.ApriMenuPausaAsync`:

- Chiama `_sessione.SospendiCronometro()` **prima** di mostrare la modale.
- Sul ramo `default` (Annulla) chiama `_sessione.RiprendiCronometro()`.
- Rami `Abbandona` / `Salva`: nessuna ripresa (la sessione finisce o viene
  esportata; le `Stop()` esistenti restano idempotenti).

Poiché `Stopwatch.Stop()/Start()` accumula il tempo, `_cron.Elapsed` esclude
automaticamente l'intervallo in pausa: **nessuna aritmetica di offset** per
questa feature.

## Feature 2 — Modalità a tempo

### Modello (Core)

- `OpzioniQuiz.LimiteTempoMinuti` (int, default `0` = nessun limite).
- `RisultatoQuiz.TempoScaduto` (bool, default `false`). Campo additivo:
  i record JSON esistenti restano deserializzabili (retrocompatibile).

Nessun nuovo campo in `SessionePausa`: l'elapsed è già persistito in
`TempoTrascorso` (→ `_offsetCronometro` alla ripresa) e il limite vive in
`pausa.Opzioni.LimiteTempoMinuti`.

### `SessioneQuiz`

Stato derivato:

- `bool HaLimiteTempo => Opzioni.LimiteTempoMinuti > 0;`
- `MostraTempo => Opzioni.Cronometro || HaLimiteTempo;` (oggi è solo
  `Opzioni.Cronometro`).

Avvio del timer (`Avvia`): `if (MostraTempo) _timer.Start();` (oggi
`if (Opzioni.Cronometro)`).

`AggiornaTempo()` (tick ogni secondo):

```
elapsed = _cron.Elapsed + _offsetCronometro;
if (HaLimiteTempo)
{
    var rimanente = TimeSpan.FromMinutes(Opzioni.LimiteTempoMinuti) - elapsed;
    if (rimanente <= TimeSpan.Zero)
    {
        Tempo = "00:00";
        AggiornaLivelloTempo(TimeSpan.Zero);
        Concludi(abbandonato: false, tempoScaduto: true);
        return;
    }
    Tempo = QuizService.FormattaDurata(rimanente);
    AggiornaLivelloTempo(rimanente);
}
else
{
    Tempo = QuizService.FormattaDurata(elapsed);   // comportamento attuale
}
```

Helper puro per la soglia (testabile a occhio / unit):

- `AggiornaLivelloTempo(TimeSpan rimanente)` setta `TempoBrush`:
  - `> 60s` → verde `QuizColors` (o colore neutro di default del testo)
  - `≤ 60s` e `> 10s` → ambra
  - `≤ 10s` → rosso
- `IBrush TempoBrush` è una proprietà observable (`SetField`).
  Quando NON in modalità a tempo, resta sul colore neutro di default.

`Concludi`:

- Firma estesa: `Concludi(bool abbandonato, bool tempoScaduto = false)`.
- Setta `Risultato.TempoScaduto = tempoScaduto;`.
- `Abbandonato` resta `false` per il timeout (non è abbandono).
- Scoring invariato: classica e rotazione usano già `Risultato.Dettagli` per i
  conteggi, quindi le domande non affrontate semplicemente non entrano.

### Pausa + ripresa (Sospesi)

Funziona senza nuovi campi:

- L'elapsed è salvato in `pausa.TempoTrascorso` e ricaricato in
  `_offsetCronometro` da `RiprendiDa`.
- Il limite arriva da `pausa.Opzioni.LimiteTempoMinuti`.
- Il countdown riprende dal punto giusto.
- Con la Feature 1, durante la modale di pausa anche il countdown si congela.

Edge case (trascurabile): se una sessione a tempo viene ripresa dopo che il
limite è già stato superato, al primo tick `rimanente <= 0` → conclude subito
con `TempoScaduto`. Accettabile (in pratica non capita: la pausa ferma il
cronometro prima della scadenza).

### Home (UI)

Nuovo blocco nelle Opzioni, a specchio di "Limita a N domande":

- `CheckBox` "Modalità a tempo" → bind a nuova observable `ModalitaTempo` (bool).
- `NumericUpDown` minuti → bind a `MinutiTempo` (decimal), `Minimum=1`,
  `Maximum=180`, `Increment=5`, `Width=120`, `IsEnabled="{Binding ModalitaTempo}"`,
  default `10`.
- Label "minuti".

In `OnAvviaClick`:

```
LimiteTempoMinuti = ModalitaTempo ? (int)MinutiTempo : 0
```

`AggiornaRiepilogo`: opzionale — aggiungere "· max N min" alla stringa di
riepilogo quando attiva. `NomeModalita`: opzionale — suffisso "a tempo".
(Da rifinire in implementazione, non bloccante.)

### Riepilogo / Cronologia

- `RisultatoQuiz.TempoScaduto` guida un badge/nota "⏱ Tempo scaduto".
- `RiepilogoView`: quando `TempoScaduto`, titolo/sottotitolo evidenziano lo
  scadere del tempo (es. titolo "Tempo scaduto" invece di "Quiz concluso").
- `RisultatoCronologiaItem.StatoEtichetta`: "Tempo scaduto" quando
  `TempoScaduto` (oggi è "Abbandonato"/"Concluso"). Aggiungere proprietà
  `TempoScaduto` al wrapper.
- `CronologiaView.axaml`: badge accanto a quello "Abbandonato" esistente,
  `IsVisible="{Binding TempoScaduto}"`.

## Componenti toccati

| File | Modifica |
|---|---|
| `Core/Models/OpzioniQuiz.cs` | + `LimiteTempoMinuti` |
| `Core/Models/RisultatoQuiz.cs` | + `TempoScaduto` |
| `App/State/SessioneQuiz.cs` | Sospendi/Riprendi cronometro; `HaLimiteTempo`; `MostraTempo`; timer-start; `AggiornaTempo` countdown; `AggiornaLivelloTempo`; `TempoBrush`; `Concludi(tempoScaduto)` |
| `App/Views/QuizView.axaml.cs` | Sospendi/Riprendi attorno alla modale pausa |
| `App/Views/QuizView.axaml` | `Foreground="{Binding TempoBrush}"` sul TextBlock tempo |
| `App/Views/HomeView.axaml(.cs)` | blocco "Modalità a tempo" + observable + avvio |
| `App/State/RisultatoCronologiaItem.cs` | `TempoScaduto` + `StatoEtichetta` |
| `App/Views/CronologiaView.axaml` | badge "Tempo scaduto" |
| `App/Views/RiepilogoView.axaml.cs` | titolo/sottotitolo per timeout |

## Testing

In linea con le norme del progetto (smoke test manuali; non c'è progetto di
test). La logica di scadenza/soglia colore è tenuta in metodi semplici e puri
così è verificabile. Smoke test da fare a fine implementazione:

1. Quiz a tempo da 1 minuto → allo scadere termina, va al riepilogo con nota
   "tempo scaduto", la domanda corrente non risposta non è conteggiata.
2. Countdown diventa ambra <60s, rosso <10s.
3. Pausa durante quiz a tempo → countdown congelato, riprende corretto.
4. Quiz a tempo + rotazione + limite domande insieme.
5. Sospendi/Riprendi un quiz a tempo dai Sospesi → countdown coerente.
6. Cronometro normale (non a tempo) invariato: conta in salita.
```
