# Partita sui punti deboli e "Ripassa" nelle Statistiche — design

Data: 2026-11-18. Richiesta da Luca il 15/11 (roadmap), decisioni prese in conversazione il 18/11.
Si appoggia sulla seconda possibilità (mezzo errore = peso 0,5 nelle statistiche), già in `main`.

## Perché
Luca vuole ripassare dove sbaglia di più, senza scegliere a mano le categorie guardando la
heatmap. Due ingressi: un filtro in Home e un pulsante "Ripassa" nelle Statistiche.

## Comportamento (deciso da Luca)
### 1. Riquadro "Punti deboli" in Home
- Quattro pulsanti a scelta multipla, stesso stile e logica di "Tipo di domande":
  **Rosse** (errore ≥ 60%), **Ambra** (20–59%), **Verdi** (< 20%), **Nuove** (categoria senza
  storico). Soglie e colori sono quelli della heatmap (`QuizColors.HeatmapBordo`).
- Tutti e quattro accesi = nessun filtro (default). Almeno uno resta acceso. Ogni pulsante mostra
  il numero di domande che porterebbe, come i pulsanti del tipo.
- Il filtro agisce **sulle categorie** già scelte con materie/categorie: il pool finale è
  l'intersezione. Si somma anche al filtro per tipo.
- Il livello di una categoria viene da `StatisticheService.Calcola` su tutta la cronologia
  (partite concluse **e abbandonate**; sospesi esclusi), per coppia (materia, categoria).
- Il mix: senza limite di domande entrano **tutte** le domande delle categorie scelte; con un
  limite (`LimiteDomande > 0`) la partita prende **prima le domande sbagliate** (anche a metà,
  peso > 0) di quelle categorie e riempie col resto della stessa selezione. Nessuna opzione nuova.
- Se il filtro lascia 0 domande: messaggio nel riepilogo di Home (come oggi per pool vuoto),
  pulsante Avvia disattivato.
- Preferenza persistita come i tipi: `PreferenzeUtente.LivelliEsclusi` (lista di chiavi
  `rosse`/`ambra`/`verdi`/`nuove`, vuoto = tutti), default vuoto.

### 2. Pulsante "Ripassa" nella pagina dettaglio categoria (Statistiche)
- Nella `CategoriaDettaglioView`, accanto a "Indietro". Avvia **subito** una partita con **solo
  le domande dell'elenco "sbagliate"** di quella categoria (quelle con peso errore > 0).
- Opzioni della partita: quelle salvate nelle preferenze (Rotazione, Seconda possibilità,
  Cronometro, Randomizza), nessun limite. Nome modalità: "Ripasso" + categoria.
- Se la categoria non ha domande sbagliate il pulsante non c'è (o è disattivato).

## Non obiettivi
- Console non toccata. Nessuna nuova scala di colori. Nessun limite di tempo minimo di storico
  né soglia di domande viste per classificare una categoria (1 su 1 sbagliata = 100%): noto, si
  rivede se darà fastidio.

## Approccio
Una sola strada sensata: riusare `StatisticheService`, `QuizService.FiltraPerTipo` come modello
del nuovo filtro e `OpzioniQuiz`/`SessioneQuiz` così come sono. Scartato: salvare per ogni
domanda un livello di debolezza (duplica i dati della cronologia) e un modo di gioco dedicato.

Scelte mie, da controllare:
- La cronologia viene letta una volta quando Home si mostra o cambia (non a ogni click); serve un
  modo per passarla a `HomeView` da `MainView`, che ha già lo `StorageService`.
- Funzione pura nel core (nuovo metodo statico in `QuizService`, es. `FiltraPerLivello`) che
  prende pool, statistiche e livelli esclusi; `OrdinaSbagliatePerPrime` per il limite. Test sul core.
- "Ripassa" costruisce la lista di domande dagli `IdDomanda` di `CategoriaStat.Sbagliate` e
  la mette nel `SessioneQuiz` come fa Home oggi (lo stesso punto di MainView che avvia le partite).
- Una domanda nelle statistiche può non esistere più (contenuto cancellato/modificato, id cambia):
  viene saltata; se non ne resta nessuna, messaggio invece di partita vuota.
- Posizione in Home: riquadro proprio sotto "Tipo di domande", stessa grafica. Alternativa
  scartata: pallini colorati dentro il riquadro Categorie (più compatto, meno chiaro). Mockup da
  confermare con Luca prima del codice (feedback "mockup prima della UI").

## Lascio al costruttore
Nomi esatti di stringhe it/en, stile dei pulsanti (classe già usata dai tipi, con i tre colori
della heatmap), test di `FiltraPerLivello`, prova a mano su desktop e Android.

## Aggiornamento post-prova a mano (2026-11-24)
Durante il test manuale su desktop sono emersi due miglioramenti UX applicati subito:
1. **Home (*Tipo di domande* e *Punti deboli*):**
   - Logica invertita: **tutti spenti di default = nessun filtro** (tutto incluso), eliminato il vincolo "almeno uno resta acceso" che bloccava lo spegnimento dell'ultimo filtro. Compatibile senza migrazione con `PreferenzeUtente.TipiEsclusi` / `LivelliEsclusi` (lista vuota = nessun filtro).
   - **Conteggi incrociati:** i numeri sui pulsanti *Tipo* tengono conto del filtro *Punti deboli* attivo e viceversa, così eventuali incroci a 0 domande sono evidenti subito sul pulsante.
2. **Statistiche + *Ripassa*:**
   - La heatmap piatta nascondeva il pulsante *Ripassa* dentro la singola cella e risultava disordinata. Sostituita (da mockup scelto da Luca: opzione B su desktop, A su mobile) con una vista **Master-Detail adattiva** (`AppEnv.TouchMode`):
     - **Desktop:** lista materie cliccabili a sinistra (ordinate dalla più debole, con barra 3-colori `BarraLivelli` e conteggi Rosse/Ambra/Verdi) + dettaglio materia a destra con **Ripassa materia (N)**, chip filtro per livello (`Tutte / Rosse / Ambra / Verdi`), ordinamento (`Più deboli / A–Z / Più viste`), righe categoria con barra errore + **Ripassa (N)** sempre visibile e domande sbagliate **espandibili inline**.
     - **Mobile (`TouchMode`):** stesso codice in 2 step (card materie a tutta larghezza con *Ripassa* rapido → dettaglio materia con `← Materie` ed `Esc`).
