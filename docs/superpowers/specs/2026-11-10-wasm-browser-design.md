# WebAssembly — primo passo: Quaestio nel browser (design)

## Intento

Far girare Quaestio in un normale browser web tramite WebAssembly
(`Avalonia.Browser`, `net10.0-browser`), senza server di backend e a costo
zero. Chiunque (su iPhone, iPad, Android o PC di scuola) potrà aprire l'app da
un indirizzo statico, giocare, mettere in pausa e ritrovare cronologia e
preferenze nello stesso browser.

## Obiettivi

- Nuovo head `wsa.quiz.browser` (`net10.0-browser`, SDK
  `Microsoft.NET.Sdk.WebAssembly`) sopra la libreria `Wsa.Quiz.App`, affiancato
  a `wsa.quiz.desktop`, `wsa.quiz.android` e `wsa.quiz.ios`.
- Caricamento delle 11 materie / 2383 domande e delle traduzioni dalle risorse
  incorporate (`AvaloniaResourceFonteDati`), già pronte in `Wsa.Quiz.App`.
- **Persistenza nel browser (`localStorage`)**: nel browser il file system di
  .NET (`MEMFS`) vive solo in memoria e si azzera ricaricando la pagina. I file
  della cartella utente (`/WsaQuiz`: `preferenze.json`, `cronologia.json`,
  `quiz_in_pausa.json` e `ContenutiUtente/`) vengono idratati da
  `window.localStorage` all'avvio (prima di `App.LinguaIniziale()`) e
  rispecchiati in `localStorage` a ogni scrittura o cancellazione.
- **Scelta automatica UI Touch / UI Desktop in base al dispositivo**:
  - su telefono o tablet (`pointer: coarse` o viewport stretto `< 768px`),
    parte in modalità Touch (`AppEnv.TouchMode = true`, bottom nav, swipe e
    `ScalaTouch`);
  - su browser desktop (PC con mouse/tastiera e viewport largo), parte in
    modalità Desktop (`AppEnv.TouchMode = false`, tab in alto, layout a due
    colonne, scorciatoie da tastiera).

## Non obiettivi (passi successivi)

- Pubblicazione automatica su GitHub Pages / Cloudflare Pages (workflow CI/CD).
- Manifesto PWA (`manifest.json`, service worker offline, icone Apple Touch per
  «Aggiungi a schermata Home»).
- Sincronizzazione dei dati utente tra dispositivi diversi (i dati restano nel
  browser locale).

## Vincoli verificati (2026-11-10)

- **SDK e build**: `Microsoft.NET.Sdk.WebAssembly` è incluso nell'SDK .NET 10
  (`10.0.403`). `dotnet build wsa.quiz.browser` compila il C# in ~3 secondi
  anche senza workload aggiuntivi; per linkare gli archivi nativi SkiaSharp /
  HarfBuzzSharp in `dotnet.native.wasm` ed eseguire con `dotnet run` (o
  `dotnet publish`) serve il workload `wasm-tools` (`dotnet workload install
  wasm-tools`).
- **Fuori da `Wsa.Quiz.sln`**: come `wsa.quiz.ios` e `wsa.quiz.android`, l'head
  browser non entra nella solution, così `dotnet build Wsa.Quiz.sln` resta
  veloce e senza dipendenze da workload su ogni macchina.
- **Avalonia 12.0.2**: il pacchetto `Avalonia.Browser` 12.0.2 espone
  `StartBrowserAppAsync("out")` su `AppBuilder` e usa `ISingleViewApplicationLifetime`.
- **File picker nel browser**: `GestioneContenutiView` usa già
  `TopLevel.StorageProvider` (`OpenFilePickerAsync` / `SaveFilePickerAsync`),
  che `Avalonia.Browser` implementa con i selettori/download nativi del browser.

## Decisioni

- **Persistenza sincrona senza toccare le viste**:
  - `StorageService.CartellaUtenteDefault()` usa `/` come base se
    `Environment.GetFolderPath(SpecialFolder.ApplicationData)` restituisce
    stringa vuota (comportamento di `browser-wasm`), ottenendo `/WsaQuiz`.
  - `StorageService` espone un callback statico opzionale
    `Action<string, string?>? OnFilePersisted`, invocato da `ScriviAtomico`
    (con il testo scritto) e da un nuovo helper `EliminaFile` (con `null`),
    usato anche da `ContenutiUtenteService.RimuoviMateria`. Su desktop, CLI,
    Android e iOS il callback resta `null`.
  - In `wwwroot/main.js`, prima di `runMain`, `setModuleImports("quaestio-host", ...)`
    registra 4 funzioni JS (`isTouchDevice`, `loadStorageJson`,
    `saveStorageFile`, `removeStorageFile`) chiamate via `[JSImport]` da
    `wsa.quiz.browser/Program.cs`.
- **UI Touch vs Desktop in `ISingleViewApplicationLifetime`**:
  - `AppEnv.UsaTouchInSingleView` (default `true`, così Android e iOS non
    cambiano) viene impostato da `wsa.quiz.browser` in base a `isTouchDevice()`.
  - In `App.OnFrameworkInitializationCompleted`, nel ramo
    `ISingleViewApplicationLifetime`, `AppEnv.TouchMode` prende il valore di
    `AppEnv.UsaTouchInSingleView`. Se `true`, carica `Sizes.Touch.axaml` e
    avvolge `MainView` nel `LayoutTransformControl`; se `false` (browser su PC),
    avvolge `MainView` in un `Border` (che è un `Decorator`, così
    `App.SostituisciVistaPrincipale` al cambio lingua funziona senza casi
    speciali).
- **Safe area nel browser mobile**: in `MainView.OnAttachedToVisualTree`, la
  lettura di `TopLevel.InsetsManager` viene abilitata anche quando
  `OperatingSystem.IsBrowser()` (oltre a `OperatingSystem.IsIOS()`).
