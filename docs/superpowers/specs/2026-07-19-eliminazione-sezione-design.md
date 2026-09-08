# Eliminazione per sezione (nascondi/ripristina) — design

**Data**: 2026-07-19
**Contesto**: la tab "Gestione contenuti" permette di eliminare solo materie
importate *intere*. Manca l'eliminazione di una singola **sezione** (categoria)
di una materia. Le materie built-in vivono nell'APK in sola lettura, quindi non
sono cancellabili fisicamente.

## Decisioni (brainstorming con Luca)

1. **Meccanismo unico "nascondi"** per built-in e importate: la sezione viene
   esclusa dal pool dei quiz salvando un'esclusione nei dati utente. Il dato
   built-in resta nell'APK → operazione **reversibile**.
2. **Ripristino** offerto: le sezioni nascoste restano visibili in Gestione
   (grigie) con un bottone "Ripristina".
3. **UI a Expander in-linea**: ogni materia si espande mostrando le sue sezioni.

## Persistenza (Core)

- Nuovo modello `SezioneEsclusa { MateriaId, Categoria }`
  (`Wsa.Quiz.Core.Models`), con `[JsonPropertyName]`.
- Salvato in `ContenutiUtente/sezioni-escluse.json` (stessa cartella utente
  scrivibile di `materie.json` → identico su desktop e Android).
- `ContenutiUtenteService` guadagna:
  - `List<SezioneEsclusa> CaricaSezioniEscluse()`
  - `void NascondiSezione(string materiaId, string categoria)` (idempotente)
  - `void RipristinaSezione(string materiaId, string categoria)` (idempotente)
  - scritture con `ScriviAtomico`, come il resto del servizio.

## Caricamento / filtro (`MainView.Caricamento`)

- `_tutteDomande` resta **completo** (built-in + utente).
- Si costruisce `esclSet = HashSet<(materiaId, categoria)>` dalle esclusioni.
- `domandeAttive = _tutteDomande` meno le sezioni escluse.
- **Home** riceve `domandeAttive` → sezioni nascoste sparite da quiz e filtri.
- `_mappaDomande` (per **riprendere** i sospesi) resta sul set **completo** →
  nascondere una sezione non rompe una partita sospesa che la usa.
- **Statistiche** invariata (legge la cronologia passata; la storia non si tocca).
- Header conteggio → mostra le domande *attive*.
- **Gestione** riceve `_tutteDomande` (completo) + `esclSet` → mostra tutte le
  sezioni, incluse le nascoste, per il ripristino.

## UI (`GestioneContenutiView`)

- Ogni materia diventa un **`Expander`**; header = riga attuale (nome, badge,
  conteggio, + "Elimina" intera materia solo per le importate, invariato).
- Contenuto dell'expander: elenco **sezioni** (categorie) ordinate, con conteggio:
  - sezione attiva → **"Nascondi"** con conferma inline
    (*"Sicuro? [Sì, nascondi] [Annulla]"*, pattern dei sospesi).
  - sezione nascosta → riga con opacità ridotta + pill "nascosta" + **"Ripristina"**.
- Nuovo state `SezioneListItem` (observable per conferma inline + stato escluso).
  `MateriaListItem` costruisce la sua lista `Sezioni` dai propri quesiti + `esclSet`.

## Flusso reload

Nascondi/Ripristina → `ContenutiUtenteService` → `ContenutiCambiati` →
`Dispatcher.Post(Caricamento)` (pattern esistente). L'expander si richiude al
reload (accettabile).

## Casi limite (documentati)

- Materia con tutte le sezioni nascoste → 0 domande attive; resta in Gestione
  per il ripristino.
- Importare domande in una categoria già nascosta → nascoste anch'esse finché
  non si ripristina (coerente per `materiaId+categoria`).
- Cronologia/statistiche storiche invariate.

## Verifica

- Harness headless che invoca i veri handler (nascondi → filtro pool →
  ripristina), stile Step 12.
- Verifica live su device fisico con screenshot.
