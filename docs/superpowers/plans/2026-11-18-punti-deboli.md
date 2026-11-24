# Punti deboli e "Ripassa" — piano di implementazione (descrittivo)

Spec: `docs/superpowers/specs/2026-11-18-punti-deboli-design.md` (leggerla prima).

## Obiettivo e architettura
1. In Home un riquadro **Punti deboli** con quattro pulsanti (Rosse / Ambra / Verdi / Nuove) che
   filtra le categorie in base al livello di errore calcolato dalla cronologia.
2. Nel dettaglio categoria delle Statistiche un pulsante **Ripassa** che avvia subito una partita
   con le sole domande sbagliate di quella categoria.
La logica pura sta nel core (`QuizService`, nuova classe costanti, test); la UI copia il riquadro
"Tipo di domande" (`TipoSelezionabile`, `ZonaTipo`, `OnTipoPropertyChanged`).

## Vincoli globali
- Nessuna migrazione: campi nuovi con default; JSON esistenti si caricano.
- Non toccare `wsa.quiz.cli`, né le teste android/ios/browser. Nessun commit/push.
- Stile: commenti in italiano, code-behind con `INotifyPropertyChanged`, niente MVVM.
- Stringhe in **entrambi** `Strings.resx` (it) e `Strings.en.resx` (en).
- Le chiavi di materia/categoria usate per confrontare statistiche e domande sono quelle
  canoniche: `CategoriaStat.Materia`/`.Categoria` coincidono con `Domanda.MateriaNome`/`.Categoria`.
- Leggi sempre i file prima di modificarli e segui lo stile vicino.

## File
Modifica: `wsa.quiz.core/Models/PreferenzeUtente.cs`, `OpzioniQuiz.cs`,
`wsa.quiz.core/Services/QuizService.cs`, `wsa.quiz.core/Risorse/Strings.resx`, `Strings.en.resx`,
`wsa.quiz.app/State/SessioneQuiz.cs`, `wsa.quiz.app/Views/HomeView.axaml`, `HomeView.axaml.cs`,
`CategoriaDettaglioView.axaml`, `CategoriaDettaglioView.axaml.cs`, `StatisticheView.axaml.cs`,
`wsa.quiz.app/MainView.axaml.cs`.
Nuovi: `wsa.quiz.core/Models/LivelliDebolezza.cs`, `wsa.quiz.app/State/LivelloSelezionabile.cs`,
`wsa.quiz.core.tests/PuntiDeboliTests.cs`.

## Task 1 — Core: livelli, filtro, priorità
- [x] Fatto 2026-11-18 (agy): file `wsa.quiz.core/Models/LivelliDebolezza.cs`, `wsa.quiz.core/Models/PreferenzeUtente.cs`, `wsa.quiz.core/Models/OpzioniQuiz.cs`, `wsa.quiz.core/Services/QuizService.cs`, `wsa.quiz.core/Risorse/Strings.resx`, `wsa.quiz.core/Risorse/Strings.en.resx` — verifica: non eseguita (richiesta utente: compilazione eseguita da processo esterno al termine)
- `LivelliDebolezza` (static class, modello `TipiDomanda`): costanti stringa `Rosse = "rosse"`,
  `Ambra = "ambra"`, `Verdi = "verdi"`, `Nuove = "nuove"` e `Tutti` (array in quest'ordine).
- `QuizService` (metodi statici, nello stile del file):
  - `string LivelloDi(double pctErrore)`: stesse soglie di `QuizColors.HeatmapBordo`
    (< 0,20 verdi; < 0,60 ambra; altrimenti rosse). Commento che rimanda a quella.
  - `Dictionary<(string Materia, string Categoria), string> LivelliPerCategoria(IEnumerable<StatistichePerMateria> stat)`:
    una voce per ogni `CategoriaStat` (usa `LivelloDi(PctErrore)`), chiavi senza distinguere maiuscole.
  - `List<Domanda> FiltraPerLivello(IEnumerable<Domanda> sorgente, ICollection<string> livelliEsclusi, IReadOnlyDictionary<(string Materia, string Categoria), string> livelli)`:
    se `livelliEsclusi` è nullo o vuoto restituisce tutto; altrimenti tiene le domande il cui
    livello (cercato per `(MateriaNome, Categoria)`; assente = `Nuove`) non è escluso.
  - `HashSet<string> IdSbagliate(IEnumerable<StatistichePerMateria> stat)`: gli `IdDomanda` di tutte
    le `DomandaSbagliata` di tutte le categorie.
- `PreferenzeUtente.LivelliEsclusi` (`List<string>`, default vuoto), accanto a `TipiEsclusi`.
- `OpzioniQuiz.IdPrioritari` (`List<string>`, default vuota): id di domande da prendere per prime
  quando c'è un limite di domande.
- Stringhe (it / en): `Home_PuntiDeboliTitolo` ("Punti deboli" / "Weak spots"; chiave
  `Home_PuntiDeboliTitolo`), `Home_LivelloRosse` ("Rosse" / "Red"), `Home_LivelloAmbra` ("Ambra" /
  "Amber"), `Home_LivelloVerdi` ("Verdi" / "Green"), `Home_LivelloNuove` ("Nuove" / "New"),
  `Home_PuntiDeboliSuggerimento` (spiega: filtra le categorie in base a come le hai giocate; accesi
  tutti = nessun filtro; con un limite di domande prima le sbagliate), `Home_SoloLivelli` (formato
  "solo categorie {0}"), `Statistiche_Ripassa` ("Ripassa" / "Review"), `Statistiche_RipassaModalita`
  (formato "{0} — Ripasso: {1}" / "{0} — Review: {1}").

## Task 2 — Sessione: priorità col limite
- [x] Fatto 2026-11-18 (agy): file `wsa.quiz.app/State/SessioneQuiz.cs` — verifica: non eseguita (richiesta utente: compilazione eseguita da processo esterno al termine)
In `SessioneQuiz.Avvia`, nel ramo "sessione non ripresa": se `Opzioni.LimiteDomande > 0`,
`Opzioni.IdPrioritari` non è vuota e il pool supera il limite, invece di `Take(limite)` sul
rimescolato prendi **prima** le domande del pool con id in `IdPrioritari` (in ordine casuale),
poi riempi con le altre (in ordine casuale) fino al limite. Se `IdPrioritari` è vuota o il limite
non c'è, il comportamento resta identico a oggi. (Metti la logica in un metodo statico di
`QuizService`, es. `ApplicaLimiteConPriorita`, per poterla testare.)

## Task 3 — Home: riquadro Punti deboli
- [x] Fatto 2026-11-18 (agy): file `wsa.quiz.app/State/LivelloSelezionabile.cs`, `wsa.quiz.app/Views/HomeView.axaml`, `wsa.quiz.app/Views/HomeView.axaml.cs`, `wsa.quiz.app/MainView.axaml.cs` — verifica: non eseguita (richiesta utente: compilazione eseguita da processo esterno al termine)
- `LivelloSelezionabile` (in `wsa.quiz.app/State`, modello `TipoSelezionabile`): `Id`, `Nome`
  (da `Strings.Home_Livello*`), `NumDomande`, `Etichetta` ("Nome · N"), `IsSelected` (default true).
- `HomeView.axaml.cs`:
  - `ObservableCollection<LivelloSelezionabile> Livelli` (uno per `LivelliDebolezza.Tutti`),
    `List<string> LivelliEsclusi` (come `TipiEsclusi`), handler `OnLivelloPropertyChanged` copia di
    `OnTipoPropertyChanged` (almeno uno acceso; richiama `AggiornaRiepilogo()` e solleva
    `LivelliEsclusi`).
  - Dati dello storico: campo privato con i livelli (`Dictionary` di `LivelliPerCategoria`) e
    l'insieme `IdSbagliate`; metodo pubblico `AggiornaStorico(IReadOnlyList<StatistichePerMateria> stat)`
    che li ricalcola e chiama `AggiornaRiepilogo()`. Prima della prima chiamata il dizionario è vuoto
    (tutte le categorie sono "nuove": col filtro tutto acceso nessun effetto).
  - Pipeline del pool (`CalcolaPoolCorrente`): materie/categorie → **livello** → tipo. I conteggi dei
    pulsanti livello si calcolano sul pool materie+categorie (come i tipi, indipendenti fra loro).
    Il testo `Riepilogo` aggiunge `TestoLivelli` (come `TestoTipi`) solo se qualche livello è spento.
  - Applica le preferenze (`LivelliEsclusi`) all'inizializzazione, come i tipi (stessa gestione del
    caso "tutti esclusi" → riaccendi tutti).
  - In `OnAvviaClick`: aggiungi `TestoLivelli` a `nomeMod` e, **solo se qualche livello è spento**,
    imposta `IdPrioritari` = `IdSbagliate` (l'insieme) nelle `OpzioniQuiz`.
- `HomeView.axaml`: nuovo `Border` identico a `ZonaTipo` (stessa grafica, titolo
  `Home_PuntiDeboliTitolo`, suggerimento) subito dopo di esso, con `ItemsControl` su `Livelli` e
  `ToggleButton Classes="tipo"`. Prima del testo di ogni pulsante un pallino colorato 10 px
  (verde / ambra / rosso / grigio neutro) usando i colori di `QuizColors` (se mancano proprietà
  pubbliche per i tre colori, aggiungile in modo minimo). Dai al `Border` un `x:Name="ZonaLivelli"` e
  aggiungilo alla lista di nomi dove compare `ZonaTipo` in `HomeView.axaml.cs` (navigazione tastiera).
- `MainView.axaml.cs`: aggiungi `nameof(HomeView.LivelliEsclusi)` all'elenco delle proprietà che
  salvano le preferenze e `prefs.LivelliEsclusi = _homeView.LivelliEsclusi;`. Dopo
  `_homeView.Inizializza(...)` (due punti) e ogni volta che si ricarica la cronologia dopo una
  partita (dove oggi c'è `_statisticheView?.Ricarica()`), calcola le statistiche con
  `new StatisticheService().Calcola(_storage.CaricaCronologia())` (try/catch come in
  `StatisticheView.Ricarica`) e chiama `_homeView.AggiornaStorico(...)`.

## Task 4 — Ripassa nelle Statistiche
- [x] Fatto 2026-11-18 (agy): file `wsa.quiz.app/Views/CategoriaDettaglioView.axaml`, `wsa.quiz.app/Views/CategoriaDettaglioView.axaml.cs`, `wsa.quiz.app/Views/StatisticheView.axaml.cs`, `wsa.quiz.app/MainView.axaml.cs` — verifica: non eseguita (richiesta utente: compilazione eseguita da processo esterno al termine)
- `CategoriaDettaglioView`: pulsante "Ripassa" (`Strings.Statistiche_Ripassa`) accanto a "Indietro",
  visibile/attivo solo se `stat.Sbagliate.Count > 0`; evento pubblico
  `event EventHandler<CategoriaStat>? RipassaRichiesto` sollevato al click con la `CategoriaStat`
  mostrata (tienila in un campo in `Mostra`).
- `StatisticheView`: inoltra l'evento con un proprio `event EventHandler<CategoriaStat>? RipassaRichiesto`
  (collega il dettaglio dove viene creato in `ApriDettaglio`).
- `MainView`: sottoscrivi `_statisticheView.RipassaRichiesto` dove la vista è creata. Handler: prende
  gli `IdDomanda` di `stat.Sbagliate`, ricava le `Domanda` corrispondenti dalle domande attive (le
  stesse usate da Home) saltando quelle che non esistono più; se non ne resta nessuna esce senza
  fare nulla. Crea `OpzioniQuiz` partendo dalle preferenze salvate (Randomizza, Rotazione,
  SecondaPossibilita, Cronometro; nessun limite domande/tempo): `NomeModalita` =
  `Statistiche_RipassaModalita` con materia e categoria, `MateriaNomeLabel` = materia canonica,
  `Materie` = id della materia delle domande, `Categorie` = `"{Materia}/{Categoria}"` (stesso formato
  di `CategoriaSelezionabile.ChiaveCombinata`). Poi `new SessioneQuiz(domande, opzioni)` e
  `AvviaQuizView(sessione)`.

## Task 5 — Test (xUnit, `PuntiDeboliTests.cs`, stile degli altri test)
- [x] Fatto 2026-11-18 (agy): file `wsa.quiz.core.tests/PuntiDeboliTests.cs` — verifica: non eseguita (richiesta utente: compilazione eseguita da processo esterno al termine)
`LivelloDi` ai bordi (0,19 verdi; 0,20 ambra; 0,59 ambra; 0,60 rosse); `FiltraPerLivello` (esclusi
vuoti = tutto; escludendo `verdi` restano rosse+ambra+nuove; categoria assente dal dizionario =
nuova); `IdSbagliate`; `ApplicaLimiteConPriorita` (priorità prima, riempimento, limite rispettato,
`IdPrioritari` vuota = comportamento attuale).

## Build da lanciare dopo ogni task
`dotnet build wsa.quiz.desktop` e `dotnet test wsa.quiz.core.tests`.
