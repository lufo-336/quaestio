# Seconda possibilità — piano di implementazione (descrittivo)

Spec: `docs/superpowers/specs/2026-11-18-seconda-possibilita-design.md` (leggerla prima).

## Obiettivo e architettura
Nuova opzione **Seconda possibilità**: dopo la prima risposta sbagliata la stessa domanda si
può riprovare subito, senza svelare la soluzione. Azzeccata al secondo colpo = *mezzo errore*;
sbagliata di nuovo = errore pieno. Si estende la state machine `SessioneQuiz` (come la
Rotazione). Una comparsa della domanda produce **un solo** `DettaglioRisposta`, con un nuovo
campo `SecondoTentativo`. Le statistiche pesano gli errori (0 / 0,5 / 1).

## Vincoli globali
- Nessuna migrazione: ogni campo nuovo ha un default che fa caricare i JSON esistenti
  (cronologia, sessioni sospese, preferenze).
- Non toccare `wsa.quiz.cli` (la console non conosce la funzione) né `wsa.quiz.android/ios/browser`.
- Stile del progetto: commenti in italiano, code-behind con `INotifyPropertyChanged` (niente MVVM).
- Nessun commit, nessun push. Non toccare file fuori dall'elenco.
- Stringhe utente: sempre in **entrambi** `Strings.resx` (it) e `Strings.en.resx` (en); leggi i
  file per vedere come sono scritte le chiavi esistenti (apostrofo al posto delle lettere accentate
  nel testo italiano in `Home_*`; negli altri casi segui la riga vicina).

## File
Modifica: `wsa.quiz.core/Models/DettaglioRisposta.cs`, `OpzioniQuiz.cs`, `PreferenzeUtente.cs`,
`SessionePausa.cs`, `wsa.quiz.core/Models/Statistiche/CategoriaStat.cs`,
`wsa.quiz.core/Services/StatisticheService.cs`, `QuizService.cs`,
`wsa.quiz.core/Risorse/Strings.resx`, `Strings.en.resx`,
`wsa.quiz.app/State/SessioneQuiz.cs`, `DettaglioRispostaItem.cs`,
`wsa.quiz.app/Views/HomeView.axaml`, `HomeView.axaml.cs`, `QuizView.axaml`,
`CronologiaDettaglioView.axaml`, `wsa.quiz.app/MainView.axaml.cs`.
Nuovo: `wsa.quiz.core.tests/SecondaPossibilitaTests.cs`.

## Task 1 — Modello e preferenze (core)
- `DettaglioRisposta`: aggiungi `bool SecondoTentativo { get; set; }` (default false) con commento:
  vero se la comparsa ha richiesto il secondo colpo, riuscito o no. Aggiungi anche un **metodo**
  (non una proprietà: le proprietà finirebbero nel JSON) `double PesoErrore()`: se
  `Corretta && Tentativi == 1` restituisce 0,5 se `SecondoTentativo` altrimenti 0; in ogni altro caso 1.
- `OpzioniQuiz.SecondaPossibilita` (bool, default false), commento in stile degli altri campi.
- `PreferenzeUtente.SecondaPossibilita` (bool, default false), accanto a `Rotazione`.
- `SessionePausa.SecondaUsataIds` (`List<string>`, default vuota): id delle domande per cui la
  seconda possibilità è già stata usata nella partita.
- Stringhe (it / en), chiavi: `Home_SecondaPossibilita` ("Seconda possibilità (riprova subito se sbagli)" /
  "Second chance (retry right away if you miss)"), `Home_SecondaPossibilitaTip` (spiega: la prima risposta
  sbagliata non svela la soluzione; azzeccata al secondo colpo conta come mezzo errore),
  `Quiz_UltimaPossibilita` ("Ultima possibilità" / "Last chance"), `Quiz_MezzoCorretto`
  ("Giusto al secondo tentativo" / "Right on the second try"), `Dettaglio_SecondoTentativo`
  ("Risposta corretta al secondo tentativo" / "Answered correctly on the second try").

## Task 2 — Statistiche e punteggio (core)
- `CategoriaStat`: il campo `Errori` passa da `int` a `double` (somma dei pesi).
- `StatisticheService.CostruisciCategoriaStat`: `errori` = somma di `PesoErrore()` sui dettagli;
  `PctErrore` = errori / visti come ora. L'elenco `sbagliate` deve includere i dettagli con
  `PesoErrore() > 0` (al posto della condizione `!(Corretta && Tentativi == 1)`). Aggiorna il commento
  del metodo `Calcola`. Se altri punti del progetto non compilano per il cambio di tipo, correggili
  in modo minimo (usano `Errori` solo in `string.Format`).
- `QuizService.CalcolaPunteggio`: per una risposta corretta con `SecondoTentativo` e `Tentativi <= 1`
  aggiungi 7 punti; le altre corrette restano 10 (primo colpo) o 4. Aggiorna il commento.

## Task 3 — Logica di sessione (`SessioneQuiz`)
Leggi per intero `RispondiA`, `CaricaProssimaDomanda`, `CreaSnapshotPausa` (o come si chiama il
metodo che costruisce `SessionePausa`), `RiprendiDa` e `Concludi`.
- Nuovi campi privati: `HashSet<string> _secondaUsataIds`, e `int? _primaRispostaErrata` (indice
  shufflato già provato nella comparsa corrente, null se nessuno). Nuova proprietà observable pubblica
  `bool SecondaPossibilitaInCorso` (vera dopo la prima risposta sbagliata finché non si risponde di nuovo).
  `CaricaProssimaDomanda` azzera `_primaRispostaErrata` e `SecondaPossibilitaInCorso`.
- In `RispondiA`, dopo i controlli iniziali esistenti: se `_primaRispostaErrata == indiceShufflato`
  ignora il click. Se la risposta è sbagliata **e** `Opzioni.SecondaPossibilita` è attiva **e**
  l'id domanda non è in `_secondaUsataIds` **e** non è in `_tentativiPerId` **e**
  `_primaRispostaErrata` è null: segna quella risposta `StatoRisposta.Sbagliata`
  (disabilitata automaticamente, `PuoCliccare` richiede lo stato Neutra), memorizza
  `_primaRispostaErrata`, imposta `SecondaPossibilitaInCorso = true`, azzera `IndiceHighlight`
  ed esce **senza** registrare nulla (niente `DettaglioRisposta`, `RispostaInviata` resta false,
  nessun contatore cambia).
- Al secondo colpo (`_primaRispostaErrata != null`): registra **un solo** `DettaglioRisposta` con la
  risposta finale e `SecondoTentativo = true`; poi aggiungi l'id a `_secondaUsataIds`.
  Calcolo `Tentativi` in Rotazione: se l'esito è *mezzo errore* (corretto al secondo colpo) **non**
  incrementare `_tentativiPerId` e metti `Tentativi = 1`; se sbagliato due volte incrementa come
  oggi. Fuori Rotazione `Tentativi = 1`.
- Rotazione, ripresentazione: un mezzo errore deve **rientrare in coda** come una risposta sbagliata
  (stesso blocco che oggi rimette in coda e incrementa `_sbagliateContatoreRotazione`), ma **non**
  va in `_corretteIds`. Il test di rientro oggi usa `ok`: introduci una variabile (es.
  `superata`) = `ok && !mezzoErrore` e usala per la coda e per `_corretteIds`; il `Corretta` del
  dettaglio e il feedback restano basati su `ok`.
- Fuori Rotazione il mezzo errore conta come corretto nei contatori (`_correttoClassica`).
- Feedback: per il mezzo errore `FeedbackTitolo` = `Strings.Quiz_MezzoCorretto`, `UltimaRispostaCorretta`
  true; il resto (soluzione, spiegazione) come oggi. Gli stati delle risposte restano: la prima
  sbagliata rimane `Sbagliata`, la giusta diventa `Corretta`.
- `Concludi` (ramo Rotazione): `rispPrimoColpo` conta `Corretta && Tentativi == 1 && !SecondoTentativo`.
- Pausa: salva `SecondaUsataIds = _secondaUsataIds.ToList()` nello snapshot; alla ripresa ricostruisci
  l'insieme (anche per sessioni classiche: copia sempre). Una pausa a metà secondo tentativo non
  salva stato: la domanda riparte da zero (comportamento già ottenuto perché non c'è Dettaglio).
- La prima volta che `_secondaUsataIds` non contiene l'id basta a decidere "è la prima comparsa".

## Task 4 — Interfaccia
- `HomeView.axaml.cs`: proprietà `SecondaPossibilita` bool sul modello di `Rotazione` (campo,
  `Raise()`, `AggiornaRiepilogo()`), letta dalle preferenze dove si legge `Rotazione` (riga ~239) e
  passata a `OpzioniQuiz` dove si costruisce (riga ~425).
- `HomeView.axaml`: `CheckBox` subito sotto quello della Rotazione, stesso stile, testo
  `Home_SecondaPossibilita`, tooltip `Home_SecondaPossibilitaTip`.
- `MainView.axaml.cs`: dove si elenca `nameof(HomeView.Rotazione)` (riga ~489) aggiungi
  `nameof(HomeView.SecondaPossibilita)`, e dove si scrive `prefs.Rotazione` (riga ~504) salva anche
  `prefs.SecondaPossibilita`.
- `QuizView.axaml`: sotto il testo della domanda (o dove sta il feedback, scegli la posizione più
  coerente con il file) un `TextBlock` con `Strings.Quiz_UltimaPossibilita`, visibile solo quando
  `SecondaPossibilitaInCorso` è vera, con lo stile accento/avviso già usato nel file.
- `DettaglioRispostaItem`: proprietà `MostraSecondoTentativo` (`Dettaglio.SecondoTentativo && Dettaglio.Corretta`)
  ed etichetta `SecondoTentativoEtichetta` (= `Strings.Dettaglio_SecondoTentativo`). In
  `CronologiaDettaglioView.axaml` mostrala accanto alla riga dei tentativi, con lo stesso stile.

## Task 5 — Test (xUnit, `wsa.quiz.core.tests/SecondaPossibilitaTests.cs`)
Copia lo stile di `TestoConCodiceTests.cs`. Casi: `PesoErrore()` per le quattro combinazioni
(corretto primo colpo = 0; corretto con `SecondoTentativo` = 0,5; sbagliato = 1; corretto con
`Tentativi` 2 = 1); `StatisticheService.Calcola` con una categoria di 10 dettagli (7 giusti, 2 mezzi
errori, 1 errore pieno) → `Errori` 2,0 e `PctErrore` 0,2; `CalcolaPunteggio` con 10 / 7 / 4 punti;
deserializzare un `DettaglioRisposta` JSON senza il campo nuovo (`SecondoTentativo` false).

## Build da lanciare dopo ogni task
`dotnet build wsa.quiz.desktop` e `dotnet test wsa.quiz.core.tests` (dal Task 1; il Task 5 aggiunge i test).
