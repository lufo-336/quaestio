# Trappole già scoperte (non rifare)

<!-- Spostato da CLAUDE.md il 2026-11-05 per alleggerire il contesto: testo invariato. -->

1. **`System.Console` shadowing**: usare un namespace `Wsa.Quiz.Console`
   rompe tutte le chiamate `Console.WriteLine`. Il compilatore risolve
   `Console` come sotto-namespace di se stesso prima di guardare in
   `System`. Soluzione: `Wsa.Quiz.Cli`.
2. **`Avalonia.Diagnostics` rimosso in v12**: il package non esiste più. Il
   rimpiazzo è `AvaloniaUI.DiagnosticsSupport` (separato), opzionale. Non
   includerlo finché non serve.
3. **`RefreshBinding` con `DataContext = null/this`**: anti-pattern usato
   nella WPF iniziale. Cancellava la selezione delle ListView a ogni
   interazione. Soluzione: INPC corretto su tutto, `DataContext` settato solo
   nel costruttore.
4. **Avalonia desktop = `OutputType=WinExe`** anche su Mac/Linux. Sembra
   Windows-specifico ma è il flag standard.
5. **Compiled bindings richiedono `x:DataType` ovunque**
   (`<AvaloniaUseCompiledBindingsByDefault>true</...>`). Sui `UserControl`
   root e su ogni `<DataTemplate>`. Senza, il build dà errori cripti tipo
   "could not resolve property X on type System.Object".
6. **Bottone Fluent disabled + accent color = "scomparso"** *(risolto step 3)*:
   in Fluent, un bottone con `Background="{DynamicResource SystemAccentColorBrush}"`
   quando va in `IsEnabled=false` diventa molto chiaro su sfondo chiaro e
   sembra assente. Risolto con style globale `Button.accent` in `App.axaml`
   con i 4 stati definiti sul template
   (`/template/ ContentPresenter#PART_ContentPresenter`).
7. **`DynamicResource` di tipo `Color` nei `Setter` di Style**: nella XAML
   "diretta" Avalonia converte automaticamente `Color → SolidColorBrush`
   quando assegna a `Background`/`Foreground`/`BorderBrush`, ma nei `Setter`
   di Style la conversione è meno affidabile e può dare errori a runtime.
   Per i Setter cromatici, usare `DynamicResource SystemAccentColorBrush`
   (versione `Brush`) o un valore esplicito hex. Gli stati `:disabled` di
   `Button.accent`/`Button.danger` usano hex literali (`#D6D6D6` su `#7A7A7A`).
8. **`Key.Enter` == `Key.Return`** *(scoperta step 4)*: in `Avalonia.Input.Key`
   sono lo stesso enum constant. Metterli su due `case` distinti dello stesso
   `switch` è un errore `CS0152`. Tenerne **uno solo** (per coerenza usiamo
   `Key.Enter`).
9. **`SystemBaseHighColor` come `Foreground` su background hardcoded chiaro
   = illeggibile in dark mode** *(scoperta step 6)*: i brush "Color" di
   sistema si invertono a seconda del tema. Se il `Background` del
   contenitore è hardcoded chiaro (es. feedback box `#E5F4EC` / `#F9E7E6`),
   in dark mode il foreground diventa quasi-bianco. Soluzione: `Foreground`
   hex esplicito (es. `#1F1F1F`) quando il background è anch'esso hex.
10. **`BoxShadow` su sticky bar adiacente a `ScrollViewer`** *(scoperta step 6)*:
    aggiungere `BoxShadow="0 -2 8 0 #28000000"` su una `Border` adiacente al
    viewport del `ScrollViewer` proietta l'ombra di 8px che copre
    visivamente il bordo del viewport. Effetto "ultimo elemento tagliato".
    Soluzione: niente shadow, basta `BorderThickness` di 1px sul lato che separa.
11. **Doppio scroll innestato (pagina + pannello con `MaxHeight`)** *(scoperta
    step 6)*: avere un `ScrollViewer` esterno alla pagina + un `ScrollViewer
    MaxHeight=240` su un pannello interno è confondente. Regola: un solo
    scroll dove possibile; se serve un cap interno, deve essere giustificato
    da una vera differenza di volume di dati.
12. **`Foreground` diretto su `Button Classes="accent"` non vince**
    *(scoperta step 7)*: assegnare `Foreground="#1F1F1F"` come attributo non
    sovrascrive il colore del testo se il bottone ha `Classes="accent"`. Lo
    style globale colpisce il `ContentPresenter#PART_ContentPresenter`, che
    ha priorità sul `Foreground` dell'attributo. Soluzione: classe locale
    aggiuntiva (es. `Classes="accent prossima"`) e style con selector
    `Button.prossima /template/ ContentPresenter` che setti
    `TextBlock.Foreground`. Stesso pattern già usato per
    `Button.risposta.corretta/sbagliata` in `QuizView.axaml`.
13. **`OnKeyDown` override su `UserControl` non basta per intercettare
    frecce/Invio quando ci sono `ScrollViewer` o `ListBox` sopra**
    *(scoperta step 8; **la vera causa dello Step 8 era però un'altra: vedi
    Trappola 21**)*: l'override
    viene chiamato in fase di **bubbling** ed è registrato dalle class-handler
    interne con `handledEventsToo=false`. Se un controllo intermedio (es.
    `ScrollViewer` per `↑/↓`, o `ListBoxItem` per `Enter`) marca l'evento come
    `Handled` durante il bubble, l'override non riceve mai la notifica.
    Tentativo originale step 8: convertire ad `AddHandler(KeyDownEvent, ...,
    RoutingStrategies.Tunnel)` per intercettare in fase di capture (top-down)
    — non ha sbloccato niente. Secondo tentativo (PR #2, Facundo Fanti): stesso
    `AddHandler` ma con `handledEventsToo: true` per ricevere anche gli eventi
    già marcati `Handled`. Verifica pratica: **ancora non funzionava
    perfettamente**. **Epilogo (2026-07-28)**: il pattern `Tunnel +
    handledEventsToo` è corretto e funziona (la diagnostica headless lo mostra
    intercettare Invio prima della ListBox); ciò che mancava era il **focus**
    sulla riga — Trappola 21. Quindi questa trappola resta valida come nozione
    sul routing, ma non era la causa dello Step 8.
14. **Avalonia 12 — bootstrap Android NON è `AvaloniaMainActivity<App>`**
    *(scoperta porting M1, 2026-06-18)*: il plan/spec del porting erano scritti
    per l'API Avalonia 11.x dove `MainActivity : AvaloniaMainActivity<App>` con
    `CustomizeAppBuilder`. In **Avalonia 12.0.2** `AvaloniaMainActivity` è
    **non generico** → `error CS0308`. La scelta dell'`App` e
    `CustomizeAppBuilder` sono migrate in una sottoclasse di
    `Android.App.Application`: `MainApplication : AvaloniaAndroidApplication<TApp>`
    (con `[Application]`, ctor `(IntPtr, JniHandleOwnership)`, `WithInterFont()`
    dentro `CustomizeAppBuilder`). `MainActivity` resta nudo
    (`: AvaloniaMainActivity`). Vedi `wsa.quiz.android/MainApplication.cs`.
15. **`dotnet sln` + solution folder omonima al progetto = `MSB5004`**
    *(scoperta porting M1)*: lo schema "solution folder per head" introdotto nel
    refactor creava folder lowercase (`wsa.quiz.app`/`wsa.quiz.android`/
    `wsa.quiz.desktop`) con lo stesso nome **case-insensitive** dei progetti
    reali (`Wsa.Quiz.App`...). `dotnet build Wsa.Quiz.sln` falliva con
    "due progetti denominati wsa.quiz.app". Le solution folder sono solo
    cosmetiche (organizzazione VS): rimosse, solution flat a 5 progetti.
16. **APK Android non prodotto / `XABAA7024` `ZipIOException` — vere cause**
    *(porting M1, **risolto** 2026-06-18)*: tre cose concatenate, e un grosso
    depistaggio. **Causa risolutiva**: mancava **`<OutputType>Exe</OutputType>`**
    nel csproj dell'head → il progetto era trattato come **libreria** Android
    (emetteva `Wsa.Quiz.Android.aar`) e NON impacchettava alcun APK. Il template
    Avalonia/Android la include sempre; il piano M1 l'aveva omessa. Aggiunta →
    APK prodotto. Altri due punti correlati:
    - **Debug = fast-deployment** (`EmbedAssembliesIntoApk=false`): non produce
      un APK autonomo senza un device. Installato a mano con `adb install` crasha
      con `monodroid: No assemblies found ... Exiting`. Per un APK installabile su
      dispositivo: **`dotnet build -c Release`** (embed + sign in un colpo solo) →
      `bin/Release/net10.0-android/it.eduits.wsaquiz-Signed.apk` (~29 MB,
      firmato con la Android Debug key, verificato con `apksigner`). Installa
      con `adb install <file>-Signed.apk`. Per testare modifiche **in Debug**
      installandolo via adb (senza IDE): `dotnet build -c Debug -t:Build
      -p:EmbedAssembliesIntoApk=true -p:AndroidUseFastDeployment=false`.
    - **NON invocare `-t:SignAndroidPackage` come target standalone**: dà
      `XACAF7009 "Assembly compression info not found"` perché
      `__CompressedAssembliesInfo` è registrato in-memory da un target a monte e
      vive solo nella **stessa** invocazione MSBuild. Produrre l'APK nel chain
      normale di build, non con un target isolato.
    - **Depistaggio**: il log *"Falling back to LibZipSharp because we are not
      running on .NET 6+"* è **innocuo** — il check del SDK è
      `FrameworkDescription != ".NET"`, sempre vero (la descrizione contiene la
      versione), quindi LibZipSharp è il path zip normale **per tutti**. Non era
      quello il problema; il `ZipIOException "errore lettura ...apk"` era solo il
      sintomo dell'APK base mai creato (per i punti sopra). Non sprecare tempo su
      `dotnet workload update` o su `_AndroidUseLibZipSharp`.
17. **Safe-area Android: `IInsetsManager.SafeAreaPadding` di Avalonia riporta
    `bottom=0` su device reale** *(M3 parte 3, **risolto** 2026-06-26 su Galaxy
    S21)*: con edge-to-edge forzato (targetSdk 36) la bottom nav finiva **sotto i
    tasti Android** perché Avalonia non vede l'inset della nav bar (`SafeAreaPadding`
    → top=80, **bottom=0**). In più `TopLevel.RenderScaling` **varia** (1 al boot,
    3 a regime), quindi un fattore di conversione costante oscillava tra barra
    coperta e barra gigante. Soluzione: leggere le insets **native** nella
    `MainActivity` (`View.IOnApplyWindowInsetsListener` sulla decor view →
    `WindowInsets.GetInsetsIgnoringVisibility(SystemBars())`, in **px fisici**:
    status 80, nav 224) e passarle a `MainView` via `AppEnv.SystemBarInsetsPx`.
    Conversione px→unità di layout = `px / (ScalaTouch × RenderScaling)`, con
    `RenderScaling` letto **a runtime a ogni apply** (robusto al variare). NON
    consumare le insets: `return v.OnApplyWindowInsets(insets)`.
18. **Swipe fra tab: la gesture manuale "non funziona quasi mai"** *(M3 parte 3,
    **risolto** 2026-06-26)*: intercettare `PointerMoved` in tunnel per cambiare
    pagina è inaffidabile — il `ScrollGestureRecognizer` dello ScrollViewer interno
    cattura il tocco e i move non arrivano (lo ammetteva il commento del vecchio
    codice). Soluzione: niente gesture manuale, niente `Carousel`. Uno **ScrollViewer
    orizzontale** (`SwipeHost`) con le pagine affiancate e
    `HorizontalSnapPointsType=MandatorySingle`: lo scroll nativo dà trascinamento col
    dito + inerzia + snap (feeling pager), affidabile perché è il gesto nativo. Assi
    ortogonali (host solo orizzontale, pagine solo verticale) → nessun conflitto.
    Tap-nav animato via `ScrollViewer.Offset`.
19. **`RadioButton.GroupName` condiviso = esclusività a livello di PROCESSO, non
    solo dentro il singolo controllo/finestra** *(scoperta Step 12/import JSON,
    **risolto** 2026-07-04)*: `StatisticheView` aveva due `RadioButton` con
    `GroupName="FiltroPeriodo"` + `IsChecked` bindato a due proprietà
    mutuamente inverse (`ModalitaTutto` / `!ModalitaTutto`). Innocuo finché
    esisteva **una sola istanza** della view. Dopo aver aggiunto in Task 9 un
    reload completo dei dati (`MainView.Caricamento()` ricostruisce tutte le
    tab, compresa una **nuova** `StatisticheView`, ogni volta che cambia
    qualcosa in "Gestione contenuti"), costruire una seconda istanza registra i
    suoi due `RadioButton` nello **stesso gruppo globale** di quelli della
    prima istanza (i gruppi non sono scoped alla finestra/UserControl per
    controlli non ancora attaccati all'albero visivo). Con `DataContext`
    diversi e binding inverse, le due coppie entrano in un loop infinito di
    check/uncheck reciproco: CPU al 100%, "non risponde", poi crash dopo
    circa 50s — sintomo scambiato inizialmente per un bug di rientranza nel
    `Dispatcher.UIThread.Post` del reload (che invece era corretto). Riprodotto
    deterministicamente headless con due `new StatisticheView()` di fila
    (nessun dato/dispatcher coinvolto): la seconda si blocca. **Soluzione**:
    rimuovere `GroupName`, le due binding inverse bastano da sole a garantire
    l'esclusività. **Regola generale**: qualunque `GroupName` di `RadioButton`
    in una view che può essere istanziata più di una volta nel processo
    (ricreata a ogni reload, aperta in più tab/finestre, ecc.) è a rischio;
    preferire binding inverse espliciti (`{Binding !Prop}`) senza `GroupName`,
    oppure un `GroupName` reso univoco per istanza a runtime.

20. **`DynamicResource SystemAccentColorBrush` INVISIBILE su device Android**
    *(scoperto 2026-07-19, verificato su Samsung Galaxy S21 5G)*: lo stile
    `Button.accent` in `App.axaml` usava `{DynamicResource SystemAccentColorBrush}`
    (+ `...Light1Brush`/`...Dark1Brush`) per lo sfondo dello stato **abilitato**.
    Su questo device il resource si risolve in un colore **non visibile**
    (bianco/trasparente): risultato → **ogni bottone accent abilitato era
    invisibile** (testo bianco su sfondo bianco), incluso "Avvia quiz",
    "Prossima domanda", "Conferma" e gli import di "Gestione contenuti". Era la
    vera causa del "non vedo il pulsante per importare" (il bottone c'era ed era
    cliccabile, solo invisibile). Lo stato `:disabled` si vedeva perché già in
    hex esplicito (`#D6D6D6`/`#7A7A7A`) — stesso motivo per cui i `danger`
    (hex) non hanno mai avuto il problema. **Soluzione**: sfondi accent in hex
    esplicito (`#0078D4` default, `#2B90DE` hover, `#005FB0` pressed), come già
    fanno `danger` e lo stato disabled. Vale sia desktop sia Android. **Regola**:
    per i colori che DEVONO essere visibili in una UI cross-platform, non
    affidarsi a `SystemAccentColor*` (dipende da piattaforma/tema di sistema);
    usare hex espliciti. **Verificato su device**: fix + rebuild Release +
    `adb install -r`, "Avvia quiz" e import ora blu pieni; file picker Android
    (SAF) confermato apribile end-to-end (mai testato prima su device fisico).

21. **`ListBox.Focus()` non fa nulla: la ListBox NON è focusable** *(causa radice
    dello Step 8, scoperta 2026-07-28 con harness headless)*: in Avalonia il
    focus di una lista appartiene ai suoi **`ListBoxItem`** (come al click del
    mouse), non al contenitore. `ListaSessioni.Focus()` / `ListaPause.Focus()`
    restituivano `false` e lasciavano il focus sullo `UserControl`, che è un
    **antenato** della lista: dato che il routing di `KeyDown` va dalla radice
    all'elemento con focus, gli eventi **non passavano mai dalla ListBox** e le
    frecce non potevano muovere la selezione. Sintomo ingannevole: Invio/Canc
    sembravano funzionare (il handler di view usa `SelectedItem`, forzato a 0 dal
    fallback nel `GotFocus`) ma agivano **sempre sulla riga 0**. **Soluzione**:
    `TabellaViewBase.FocusRigaSelezionata(ListBox)` — seleziona la prima riga se
    serve e chiama `Focus()` sul container `ContainerFromIndex(SelectedIndex)`,
    con retry `Dispatcher.Post(..., DispatcherPriority.Loaded)` se il container
    non è ancora realizzato. **Regola generale**: prima di dare la colpa al
    routing di un evento tastiera, **stampare l'elemento con focus** — due
    tentativi (Tunnel, poi `handledEventsToo: true`) sono falliti proprio perché
    cercavano nel routing un problema che era di focus. Corollario: `Focus()`
    restituisce `bool`, **controllarlo**.

22. **`PublishSingleFile=true` non produce un file singolo** *(scoperta Step 14,
    2026-09-09)*: le librerie native (`libSkiaSharp`, `libHarfBuzzSharp`,
    `av_libglesv2`) restano accanto all'eseguibile, più i `.pdb` — su win-x64
    erano 101 MB di soli simboli su 204 MB di cartella. Copiando il solo `.exe`
    l'app **non parte**, e CLAUDE.md diceva il contrario da mesi. Servono
    `-p:IncludeNativeLibrariesForSelfExtract=true` e `-p:DebugType=none`. Vedi il
    riquadro in "Distribuzione (publish desktop)".

23. **Binding TwoWay su `ComboBox.SelectedItem` + `ItemsSource.Clear()` = scelta
    persa** *(scoperta Step 11, 2026-09-09)*: svuotare la collezione fa scattare
    il binding all'indietro con `null`, quindi il campo dietro la property viene
    azzerato **prima** che si possa rimetterci il valore. Chi ripopola la lista
    deve salvarsi la selezione *prima* della `Clear()` e ripristinarla dopo,
    altrimenti la tendina resta in bianco a ogni ricarica. Trovato da un harness
    headless, non a occhio: da UI sembrava solo "a volte la tendina è vuota".

24. **La debug key Android e' diversa su ogni macchina** *(scoperta 2026-09-09
    passando a Linux)*: l'APK Release e' firmato con la Android Debug key
    **locale**, quindi un APK costruito su una macchina **non si installa sopra**
    a uno costruito su un'altra — `adb install -r` fallisce con
    `INSTALL_FAILED_UPDATE_INCOMPATIBLE: signatures do not match`. Il rifiuto e'
    innocuo (non tocca i dati), ma l'unico modo di procedere e' **disinstallare**,
    e con l'app se ne vanno cronologia, sospesi, preferenze e contenuti importati
    del telefono. Successo il 09/09 passando da Windows a Fedora: dati sul device
    persi per scelta consapevole. **Se un giorno l'APK va distribuito sul serio**,
    generare una keystore stabile tenuta fuori dal repo e firmarci tutte le build,
    da qualunque macchina.

25. **`Focus()` senza `NavigationMethod` = focus invisibile** *(scoperta
    2026-09-09 dalla verifica a mano di Luca)*: in Avalonia il `FocusAdorner` (il
    bordo giallo `#FFD500` di `App.axaml`) viene disegnato solo quando l'elemento
    ha lo pseudo-stato **`:focus-visible`**, che Avalonia accende in base al
    **metodo di navigazione** con cui il focus e' stato impostato. `Focus()`
    senza argomenti non lo accende: il focus si sposta davvero, ma non si vede.
    Sintomo ingannevole: "le frecce non funzionano, il riquadro giallo resta
    sulla prima opzione" — il bordo compariva solo sulla voce raggiunta con
    `Tab` (che il metodo lo imposta) e spariva alla prima freccia. **Fix**:
    `Focus(NavigationMethod.Directional)` ovunque si sposti il focus da tastiera
    — `HomeView.OnKeyDownTunnel`, `TabellaViewBase.FocusRigaSelezionata` (righe
    di Cronologia/Sospesi, stesso difetto) e le frecce di `PausaDialog`.
    **Attenzione**: ri-chiamare `Focus(metodo)` su un elemento *gia'* a fuoco NON
    aggiorna lo stato (Avalonia esce subito), quindi il bordo non si puo'
    riaccendere a posteriori: il metodo va passato nel momento in cui si sposta
    il focus. **Regola**: verificarlo asserendo `Classes.Contains(":focus-visible")`,
    **non** `IsFocused` — quello e' vero in entrambi i casi e nasconde il difetto.

26. **Il `Padding` di uno `ScrollViewer` NON entra nell'altezza scorrevole**
    *(scoperta 2026-10-23 con la bottom-nav flottante)*: in Avalonia 12 l'extent
    è la sola dimensione del contenuto, quindi il padding in fondo (e in alto) si
    perde e l'ultimo pezzo di pagina non si raggiunge mai. Nella Home mancavano 40
    punti, che finivano sotto la pillola; un vecchio `Margin` di 28 in code-behind
    tamponava proprio questo. **Soluzione**: il margine pagina va su un `Border`
    (o sul `Margin` del figlio) **dentro** lo scroll. Si vede solo se la pagina
    scorre davvero: un harness a dimensioni desktop non lo mostra.

27. **WebAssembly: JsonSerializerIsReflectionDisabled, assembly satellite .resx (loadAllSatelliteResources) e cartella _framework su GitHub Pages (.nojekyll)**
    *(scoperta 2026-11-11 con l'head browser)*:
    - **System.Text.Json reflection**: l'SDK WebAssembly (`Microsoft.NET.Sdk.WebAssembly`)
      disabilita di default la reflection a runtime per ridurre le dimensioni dei binari,
      causando `InvalidOperationException: JsonSerializerIsReflectionDisabled`
      quando si deserializzano modelli senza source generator (es. in
      `StorageService.CaricaMaterie`). **Soluzione**: aggiungere
      `<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>`
      nel `PropertyGroup` di `Wsa.Quiz.Browser.csproj`.
    - **...e il trimming della publish Release** (scoperto 2026-11-11 sul sito
      pubblicato, NON con `dotnet run`/Debug che non fa trimming): riattivata la
      reflection, il trimmer taglia comunque da `Wsa.Quiz.Core` le parti che JSON
      usa solo via reflection. Nessun errore: la cronologia si carica ma
      `RisultatoQuiz.Dettagli` resta **vuota in silenzio** → dettaglio partita
      "Nessuna risposta registrata" e Statistiche "Nessuna sessione". **Soluzione**:
      `<TrimmerRootAssembly Include="Wsa.Quiz.Core" />` nel csproj browser.
      Verifica: `dotnet publish wsa.quiz.browser -c Release`, servire `wwwroot`
      in locale, aprire il dettaglio di una partita.
    - **Eccezione in un `async void` dopo una chiamata JavaScript = app morta**
      (2026-11-11): nel browser il runtime .NET esce ("runtime already exited") e
      la pagina va ricaricata. `Dispatcher.UIThread.UnhandledException` **non** la
      intercetta (provato). Rimedio: `try/catch` dentro i gestori che usano
      `StorageProvider` (schema `EsportaSicuro` di `CronologiaView`). Dopo un'eccezione
      a metà selezione, il selettore file di Avalonia resta inerte fino al reload.
    - **Test locale della publish e cache del browser**: `_framework/dotnet.js` non
      ha l'hash nel nome, Chrome lo riusa e carica gli assembly della build
      PRECEDENTE. Per ogni build nuova servire `wwwroot` su una **porta nuova**
      (origine nuova, niente cache) e controllare il nome di `Wsa.Quiz.App.*.wasm` in
      `performance.getEntriesByType('resource')`.
    - **Assembly satellite `.resx` (localizzazione/cambio lingua)**: in `Microsoft.NET.Sdk.WebAssembly`,
      `dotnet.js` scarica gli assembly satellite (es. `en/Wsa.Quiz.Core.resources.wasm`
      generato da `Strings.en.resx`) solo se `config.loadAllSatelliteResources === true`.
      Senza di esso, al cambio lingua le traduzioni dei contenuti (JSON `avares://traduzioni/en/*.json`)
      passano in inglese ma tutta la UI (`Strings.resx`) cade in fallback sull'italiano, producendo
      un mix linguistico. **Soluzione**: aggiungere `.withConfig({ loadAllSatelliteResources: true })`
      prima di `.create()` nella catena di bootstrap in `wsa.quiz.browser/wwwroot/main.js`.
    - **Cartella `_framework/` e Jekyll su GitHub Pages**: Jekyll per impostazione
      predefinita ignora file e directory il cui nome inizia con un carattere underscore (`_`).
      I file di bootstrap e i binari WASM di .NET risiedono in `_framework/`, perciò
      senza un file `.nojekyll` nella root distribuita, le richieste a `_framework/dotnet.js`
      e runtime falliscono con errore HTTP 404. **Soluzione**: includere un file
      vuoto `.nojekyll` in `wsa.quiz.browser/wwwroot/.nojekyll` e assicurarsi che
      venga mantenuto nell'output pubblicato (`touch release/wwwroot/.nojekyll` nel workflow CI).
