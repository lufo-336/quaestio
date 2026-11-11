# Rifiniture della versione web (WebAssembly) — piano

> Data: 2026-11-11. Contesto: giro di prova sul sito pubblicato
> (`https://lufo-336.github.io/quaestio/`). Il bug "dettaglio partita e Statistiche
> vuoti" è già corretto (commit `2efc519`, trappola 27): **non fa parte di questo piano**.
> Approvato da Luca in chat.

Regole generali:
- Leggere `CLAUDE.md` e `docs/claude/trappole.md` (in particolare 3, 5, 6, 12, 26, 27).
- Code-behind + `INotifyPropertyChanged`, niente MVVM. Commenti in italiano, come il resto del codice.
- Ogni testo visibile va in **entrambe** le risorse: `wsa.quiz.core/Risorse/Strings.resx` (it)
  e `wsa.quiz.core/Risorse/Strings.en.resx` (en). La classe `Strings` è generata in `obj/`
  a ogni build: **non** creare né modificare un `Strings.Designer.cs`.
- Per sapere se si è nel browser usare `OperatingSystem.IsBrowser()`.
- Modifiche minime: non toccare il comportamento di desktop, Android e iOS se non
  dove il task lo dice.
- Non fare commit.

## Task 1 — Impostazioni: niente "Apri cartella" e percorso `/WsaQuiz` nel browser

File: `wsa.quiz.app/Views/ImpostazioniView.axaml`, `wsa.quiz.app/Views/ImpostazioniView.axaml.cs`.

Oggi il riquadro "Dati" mostra il percorso (`SelectableTextBlock Text="{Binding PercorsoDati}"`)
e il bottone "Apri cartella" (`IsVisible="{Binding MostraApriCartella}"`, che vale
`!AppEnv.TouchMode`). Nel browser da PC il bottone compare ma non fa nulla e il percorso
`/WsaQuiz` non significa niente per l'utente.

- `MostraApriCartella` → `!AppEnv.TouchMode && !OperatingSystem.IsBrowser()`.
- Nuova proprietà `MostraPercorsoDati` (`!OperatingSystem.IsBrowser()`) sul
  `SelectableTextBlock` del percorso.
- Nuova proprietà `DatiDescrizione` (string) usata al posto di
  `{x:Static r:Strings.Impostazioni_DatiDescrizione}` nel TextBlock descrittivo:
  nel browser restituisce `Strings.Impostazioni_DatiDescrizioneBrowser`, altrove
  `Strings.Impostazioni_DatiDescrizione` (invariato).
- Nuova risorsa `Impostazioni_DatiDescrizioneBrowser`:
  - it: `Cronologia, sessioni sospese, preferenze e contenuti importati sono salvati nel browser, solo su questo dispositivo. Se cancelli i dati di navigazione del sito, si perdono: per un backup usa Esporta → JSON in Cronologia.`
  - en: `History, paused sessions, preferences and imported content are saved in the browser, on this device only. Clearing the site's browsing data deletes them: for a backup use Export → JSON in History.`
- Le proprietà sono calcolate una volta (non cambiano a runtime): basta `{ get; }` o
  un getter, nessun `Raise` necessario. Ricordare `x:DataType` già presente (trappola 5).

## Task 2 — Footer diagnostico nascosto nel browser

File: `wsa.quiz.app/MainView.axaml.cs`.

Il footer `FooterBar` ("Dati read-only: risorse embedded (avares://) · Cartella utente: /WsaQuiz")
è un'informazione per sviluppatori. Oggi viene nascosto solo in touch mode
(`FooterBar.IsVisible = false;` nel ramo touch). Nasconderlo anche quando
`OperatingSystem.IsBrowser()` è vero, qualunque sia la modalità. Sul desktop resta com'è.

## Task 3 — Gestione contenuti: l'anteprima dell'import deve vedersi

File: `wsa.quiz.app/Views/GestioneContenutiView.axaml.cs`.

Dopo aver scelto il file JSON, `MostraAnteprima(...)` e `MostraErrori(...)` rendono
visibile `PannelloAnteprima`, che però è in fondo alla pagina (dentro lo `ScrollViewer`
esterno della view) e resta fuori dallo schermo: l'utente crede che non sia successo nulla.

- Dopo `PannelloAnteprima.IsVisible = true;` in **entrambi** i metodi, portare il
  pannello in vista: `Dispatcher.UIThread.Post(() => PannelloAnteprima.BringIntoView(), DispatcherPriority.Background);`
  (il Post serve perché il layout del pannello appena reso visibile non è ancora calcolato).
- Vale su tutte le piattaforme (il problema è lo stesso anche su desktop con finestra bassa).

## Task 4 — Cronologia: colonne della tabella allineate all'intestazione

File: `wsa.quiz.app/Views/CronologiaView.axaml`.

L'intestazione è un `Grid ColumnDefinitions="Auto,180,*,90,90,90,160"`; ogni riga della
`ListBox x:Name="ListaSessioni"` usa `Grid ColumnDefinitions="6,180,*,90,90,90,160"`.
Le celle "%", "Risposte", "Durata" delle righe finiscono subito dopo la colonna Modalità,
non sotto le loro intestazioni: il `ListBoxItem` non si allarga a tutta la larghezza,
quindi la colonna `*` della riga collassa alla larghezza del contenuto.

- Aggiungere alla `ListBox ListaSessioni` uno stile locale che imposti
  `HorizontalContentAlignment="Stretch"` sui `ListBoxItem`, ad esempio:
  ```xml
  <ListBox.Styles>
      <Style Selector="ListBoxItem">
          <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
      </Style>
  </ListBox.Styles>
  ```
- Allineare anche la prima colonna dell'intestazione a quella delle righe
  (`Auto` → `6`, il TextBlock vuoto ha già `Width="6"`), e controllare che il padding
  orizzontale dell'intestazione (`Padding="14,8"` sul Border) corrisponda al padding del
  `ListBoxItem` (Fluent: 12 a sinistra/destra) più eventuali margini: se non coincide,
  correggere il `Padding` dell'intestazione, non le righe.
- Non toccare il layout touch (le card con `Grid ColumnDefinitions="6,*"`).

## Task 5 — Rete di sicurezza per gli errori non gestiti (solo browser)

File: `wsa.quiz.app/App.axaml.cs`, `wsa.quiz.app/Views/ConfermaDialog.axaml.cs`, risorse.

Nel browser un'eccezione non gestita in un handler `async void` fa uscire il runtime .NET:
l'app si blocca del tutto e va ricaricata la pagina. Serve un gestore che la intercetti,
la scriva in console e mostri un messaggio, lasciando l'app utilizzabile.

- In `App.OnFrameworkInitializationCompleted`, **solo se** `OperatingSystem.IsBrowser()`:
  `Dispatcher.UIThread.UnhandledException += OnErroreNonGestito;`
- `OnErroreNonGestito(object? sender, DispatcherUnhandledExceptionEventArgs e)`:
  - `Console.Error.WriteLine(e.Exception);`
  - `e.Handled = true;`
  - mostra un `ConfermaDialog` come overlay sulla vista principale
    (`(ApplicationLifetime as ISingleViewApplicationLifetime)?.MainView`), con messaggio
    `Strings.Errore_ImprevistoTitolo`, dettaglio
    `string.Format(Strings.Errore_ImprevistoDettaglio, e.Exception.Message)`, bottone
    `"OK"` con classe `accent`, e **senza** il bottone Annulla.
  - Se un dialog di errore è già aperto, non aprirne un secondo (flag statico).
  - Tutto il corpo del gestore in `try/catch` vuoto: il gestore non deve mai lanciare.
- `ConfermaDialog`: aggiungere una proprietà `public bool MostraAnnulla { set => AnnullaBtn.IsVisible = value; }`
  e far sì che `OnShown()` metta il focus su `ConfermaBtn` quando Annulla è nascosto.
  Nessun altro cambiamento al comportamento esistente.
- Nuove risorse:
  - `Errore_ImprevistoTitolo` — it: `Si è verificato un errore imprevisto.` / en: `An unexpected error occurred.`
  - `Errore_ImprevistoDettaglio` — it: `L'operazione non è andata a buon fine, ma puoi continuare a usare l'app. Dettaglio: {0}` / en: `The operation did not complete, but you can keep using the app. Details: {0}`

## Verifica (la fa chi orchestra, non serve eseguirla)

- `dotnet build Wsa.Quiz.sln` e `dotnet build wsa.quiz.browser` senza errori né warning nuovi.
- Publish Release del browser servito in locale: Impostazioni senza bottone/percorso,
  footer assente, anteprima import visibile dopo la scelta del file, colonne allineate,
  file finto che lancia un'eccezione → messaggio di errore e app ancora utilizzabile.

## Esito (2026-11-11)

- Task 1, 2, 3: fatti da Antigravity (pipeline write → build → review), verificati nel browser.
- Task 4 (colonne Cronologia): la diagnosi del piano era sbagliata (stile `Stretch` e
  `HorizontalScrollBarVisibility` non cambiavano nulla). Una diagnostica con le
  `Bounds` ha mostrato che era stretto `ScrollTabella` stesso: nel `DockPanel` l'ultimo
  figlio era la lista card touch (nascosta), quindi la tabella veniva agganciata a
  sinistra. Risolto mettendo le due liste in un `Grid`. Riguardava anche il desktop.
- Task 5: il gestore globale `Dispatcher.UIThread.UnhandledException` **non** intercetta
  l'eccezione (il runtime esce lo stesso): tolto, insieme a `MostraAnnulla`. Sostituito
  da `try/catch` nei 4 gestori file di `GestioneContenutiView` (errore mostrato sotto il
  bottone). Vedi trappola 27.
- La fase fix della pipeline ha tolto Android da `Wsa.Quiz.sln` per far passare la build:
  era un errore d'ambiente (workload android assente sul Mac), modifica annullata.
