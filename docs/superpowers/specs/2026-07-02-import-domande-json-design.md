# Import materie/domande da JSON — "Gestione contenuti" — Design

Data: 2026-07-02
Branch: `feature/android`
Contesto: Step 12 della roadmap (CLAUDE.md, "Import domande/materie da GUI"),
finora solo abbozzato. Prosecuzione naturale dopo M1/M2/M3 del port Android.

## Obiettivo

Una schermata in-app per aggiungere contenuti (materie e domande) caricando
file `.json`, senza dover editare a mano i file nella root del repo — utile
soprattutto su Android, dove il repo non è raggiungibile. La schermata offre
anche accesso alla guida/motore usati per generare domande bilanciate con
un'IA. I contenuti importati sono **personali al dispositivo** (non finiscono
nel repo/APK ufficiale finché non li si aggiunge manualmente lì).

## Decisioni (da brainstorming)

- Persistenza: **solo locale al dispositivo** (cartella utente scrivibile),
  non un flusso che produce contenuti da committare nel repo.
- Copertura: **entrambi i flussi**, creazione nuova materia e aggiunta di
  domande a una materia già esistente (built-in o già importata).
- Guida: **non** si replica in C# l'audit di bilanciamento lunghezze
  (L/S/M/Q) del motore — si espone/spiega il motore esistente e lo si rende
  scaricabile dalla schermata.
- Posizione UI: **5ª tab dedicata** "Gestione contenuti", sempre visibile,
  stesso livello di Home/Cronologia/Sospesi/Statistiche.
- Errori di validazione strutturale: **rifiuta tutto il file** e mostra
  l'elenco errori (nessun import parziale in presenza di errori).
- Anteprima: **sì**, elenco domande + conteggi prima della conferma.
- Duplicati (append a materia esistente, stesso testo `domanda`): **si
  saltano silenziosamente**, si importa il resto, riepilogo finale.
- Rimozione: la schermata permette anche di **rimuovere una materia
  importata** (solo le importate, non le built-in).

## Architettura

### 1. Storage dei contenuti importati

Nuova sottocartella dentro la cartella utente già esistente
(`StorageService.CartellaUtenteDefault()`, `ApplicationData/WsaQuiz`,
scrivibile identicamente su desktop e Android):

```
WsaQuiz/
└── ContenutiUtente/
    ├── materie.json                       # indice materie importate
    └── domande/
        └── <materiaId>/
            ├── import-<timestamp>.json     # domande della prima importazione
            └── import-<timestamp>.json     # eventuali aggiunte successive
```

- `ContenutiUtente/materie.json`: stesso schema di `materie.json` built-in
  (`id`, `nome`, `cartella`, `formato:"standard"`).
- **`id` materia**: slug del nome inserito (minuscolo, spazi → `-`, accenti
  rimossi). Se collide con un `id` built-in o già importato si aggiunge un
  suffisso numerico (`-2`, `-3`, …), mostrato all'utente prima della conferma.
- Non si scrive mai nei file built-in/embedded (sola lettura).

### 2. Caricamento — merge built-in + utente

`StorageService.CaricaMaterie()` continua a leggere le materie built-in via
`IFonteDati` come oggi (`FileSystemFonteDati` su desktop, `AvaloniaResourceFonteDati`
su Android), poi legge — se presente — `ContenutiUtente/materie.json` da
filesystem reale (sempre disponibile, indipendentemente dalla piattaforma) e
unisce le due liste. Ogni materia viene marcata con un flag `Importata: bool`
usato in UI per abilitare/disabilitare la rimozione.

Per il caricamento domande di una materia, si uniscono: i file built-in della
sua cartella (via `IFonteDati`, come oggi) + tutti i file dentro
`ContenutiUtente/domande/<materiaId>/` (filesystem reale), stesso meccanismo
già esistente di "più file per materia, il loader li unisce".

Nessuna modifica al meccanismo `IFonteDati`/embedded esistente: il merge è
additivo, a valle, indipendente dalla piattaforma.

### 3. Flusso schermata "Gestione contenuti" (5ª tab)

- Nuova tab: icona dedicata in bottom-nav touch (aggiornare `BottomNav`,
  `SwipePages`, `DimensionaPagine`, `AggiornaNav` in `MainView`, oggi cablati
  su 4 sezioni) + `TabItem` nel `TabControl` desktop.
- **Elenco materie** in cima: nome, conteggio domande, badge "built-in" /
  "importata"; solo le importate hanno pulsante di rimozione (con conferma).
- **Azione "Nuova materia"**: campo nome → file picker (`IStorageProvider` di
  Avalonia — dialog nativo su desktop, Storage Access Framework su Android) →
  validazione (sezione 4) → anteprima (elenco domande categoria/testo +
  conteggio totale, eventuale avviso su id rinominato per collisione) →
  Conferma scrive i file.
- **Azione "Aggiungi domande a materia esistente"**: selezione materia
  (built-in o importata) → stesso file picker → validazione → anteprima con
  conteggio "N nuove, M duplicate saltate" → Conferma.
- Stile coerente con le altre schermate già adattate in M3 (dimensioni da
  `Sizes.Touch.axaml`/`Sizes.Desktop.axaml`, code-behind +
  `INotifyPropertyChanged`, no MVVM).

### 4. Validazione (porting mirato da `quiz-engine.js`)

Solo controlli strutturali — **non** l'audit di bilanciamento lunghezze
L/S/M/Q (quello resta uno strumento per chi scrive le domande con la guida,
non un gate d'import):

- `domanda` presente e non vuota
- `risposte` array con almeno 2 elementi
- `rispostaCorretta` indice numerico valido dentro `risposte`
- nessuna risposta duplicata all'interno della stessa domanda
- `spiegazione` presente
- nessuna `domanda` duplicata **all'interno del file caricato**

Un solo elemento non valido → si rifiuta l'intero file, si elenca ogni
errore con l'indice della domanda incriminata, nessuna scrittura su disco.
(I duplicati *contro le domande già esistenti* in caso di append sono
gestiti a parte, sezione "Decisioni".)

### 5. Guida e motore, esposti dalla schermata

- Si copiano nel repo, invariati: `docs/generazione-domande/GUIDA_GENERAZIONE_DOMANDE.md`
  (da `MODELLO - Generatore Domande Quiz.md`) e
  `docs/generazione-domande/quiz-engine.js` (da `MODELLO - quiz-engine.js`),
  entrambi oggi in `G:\Il mio Drive\ITS Corso` — percorso personale
  dell'utente, non raggiungibile dall'app (specialmente su Android).
- Diventano risorse incorporate nell'app (`AvaloniaResource`, stesso
  meccanismo già usato per `materie.json`/`domande/`), non letti dal
  filesystem a runtime.
- Nella schermata "Gestione contenuti", un riquadro spiega a cosa serve il
  motore (genera JSON bilanciato per lunghezza/posizione a partire da un file
  dati, evita il bias "la risposta lunga è quella giusta") con due pulsanti
  **"Scarica guida"** / **"Scarica motore"** (save-file dialog via
  `IStorageProvider`).

### 6. README

- Sezione "Aggiungere materie, categorie e domande" riscritta in due parti:
  (a) dall'app (nuovo flusso "Gestione contenuti", consigliato), (b) a mano
  editando i JSON nella root (resta documentato per chi lavora offline sul
  repo).
- Blocco "Generare nuove domande con un'IA" ampliato per rimandare a guida +
  motore ora bundlati nel repo (cosa fanno, come lanciarli da riga di
  comando, che sono scaricabili anche dall'app).
- Allineamento delle parti disallineate: port Android (oggi il README parla
  solo di CLI + GUI desktop, .NET 8), 5 progetti invece di 3, 5 tab correnti
  invece di "Tre tab".

## Fuori scope

- Replica in C# dell'audit L/S/M/Q del motore.
- Modifica/rimozione di singole domande importate (solo rimozione dell'intera
  materia importata).
- Editing dei contenuti built-in dalla UI.
- Produzione di un JSON "pronto da committare nel repo" — i contenuti
  importati restano locali al dispositivo.
- Rebuild/pubblicazione dell'APK con i nuovi contenuti (passo successivo,
  esplicitamente rimandato dall'utente).

## Verifica

1. Creazione nuova materia con JSON valido → compare in elenco, badge
   "importata", domande selezionabili nel resto dell'app (Home, Quiz).
2. Aggiunta domande a materia built-in esistente → domande unite, duplicati
   (stesso testo) saltati e conteggiati nel riepilogo.
3. JSON con errori strutturali → import rifiutato, elenco errori mostrato,
   nessun file scritto.
4. Rimozione materia importata → sparisce da elenco e dai filtri; le
   built-in restano intatte e non rimovibili.
5. Persistenza tra riavvii dell'app (desktop e — quando possibile — device
   Android).
6. Scarica guida / Scarica motore → file salvati leggibili e identici ai
   sorgenti in `docs/generazione-domande/`.
7. Desktop invariato per le altre tab; nuova tab coerente con lo stile
   touch/desktop esistente.

## Rischi

- File picker Android (SAF) tramite `IStorageProvider`: non ancora provato
  in questo progetto — verificare presto su device reale (rischio simile
  alle Trappole 14-18 già incontrate nel port Android).
- Estendere bottom-nav/swipe da 4 a 5 sezioni tocca codice già delicato
  (Trappole 17-18): verificare che swipe e safe-area restino corretti con
  una pagina in più.
- Slug id materia: possibili collisioni con nomi molto simili — mitigato dal
  suffisso numerico automatico e dall'anteprima che lo mostra prima di
  confermare.
