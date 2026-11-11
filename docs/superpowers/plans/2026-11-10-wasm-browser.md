# WebAssembly — Quaestio nel browser — Piano descrittivo

Spec: `docs/superpowers/specs/2026-11-10-wasm-browser-design.md`.
Tutti i percorsi sono relativi alla radice del worktree.

**Obiettivo:** nuovo head `wsa.quiz.browser` (`net10.0-browser`) sopra la
libreria `Wsa.Quiz.App`, con persistenza dei file utente in `localStorage` del
browser e scelta automatica tra UI Touch (telefono/tablet) e UI Desktop (PC).

**Architettura:** stesso schema di `wsa.quiz.ios` e `wsa.quiz.android` (head
sottile fuori da `Wsa.Quiz.sln` + libreria condivisa). L'head contiene il
csproj WebAssembly, i file statici `wwwroot/index.html`, `wwwroot/app.css`,
`wwwroot/main.js` e `Program.cs` (con il ponte `[JSImport]` verso
`localStorage` e il rilevamento touch/desktop). In `Wsa.Quiz.Core` e
`Wsa.Quiz.App` si aggiungono solo i punti d'aggancio minimi.

## Vincoli globali

- `net10.0` / `net10.0-browser`, Avalonia **12.0.2** (stessa versione degli
  altri csproj: non aggiornare).
- L'head browser **non** va aggiunto a `Wsa.Quiz.sln`.
- Leggere ogni file esistente prima di modificarlo e seguirne lo stile:
  commenti in italiano, apostrofi al posto delle lettere accentate nei commenti
  del codice C# (`e'`, `gia'`, `cosi'`).
- **Build di verifica** dopo ogni task:
  - `dotnet build Wsa.Quiz.sln` (0 errori, 0 warning);
  - dal Task 3 in poi anche `dotnet build wsa.quiz.browser` (0 errori C#; è
    normale l'unico warning di MSBuild su `@(NativeFileReference)` quando il
    workload `wasm-tools` non è installato sulla macchina).
- **Non fare commit**: il commit finale lo gestisce il chiamante dopo la
  revisione.

---

## Task 1: aggancio di persistenza in `Wsa.Quiz.Core`

**File:** modifica `wsa.quiz.core/Services/StorageService.cs` e
`wsa.quiz.core/Services/ContenutiUtenteService.cs`.

Intenzione: nel browser `Environment.GetFolderPath(SpecialFolder.ApplicationData)`
restituisce stringa vuota, e il file system POSIX vive solo in memoria
(`MEMFS`). Serve che `CartellaUtenteDefault()` restituisca un percorso assoluto
valido (`/WsaQuiz`) anche quando `ApplicationData` è vuoto, e che ogni scrittura
o cancellazione di file utente notifichi un callback statico opzionale così
l'head browser possa rispecchiarla in `window.localStorage`.

In `StorageService.cs`:
- Aggiungere una proprietà statica pubblica
  `public static Action<string, string?>? OnFilePersisted { get; set; }` con
  commento XML: se valorizzata (head WebAssembly), viene chiamata dopo ogni
  scrittura (`contenuto` non nullo) o cancellazione (`contenuto` nullo) di un
  file utente con il percorso assoluto completo. Su desktop, CLI, Android e iOS
  resta `null`.
- In `CartellaUtenteDefault()`: se `baseFolder` è `null` o vuoto
  (`string.IsNullOrEmpty(baseFolder)`), usare `"/"` come valore di `baseFolder`
  prima di `Path.Combine(baseFolder, "WsaQuiz")`. Aggiornare il commento XML
  aggiungendo `- Browser (WASM): /WsaQuiz (in MEMFS, sincronizzato con localStorage)`.
- In `ScriviAtomico(string percorso, string contenuto)`: subito dopo
  `File.Move(temp, percorso, overwrite: true)`, invocare
  `OnFilePersisted?.Invoke(Path.GetFullPath(percorso), contenuto)`.
- Aggiungere un metodo statico interno `internal static void EliminaFile(string percorso)`
  che: se `File.Exists(percorso)` chiama `File.Delete(percorso)`, e poi invoca
  `OnFilePersisted?.Invoke(Path.GetFullPath(percorso), null)`.
- In `EliminaPausa(string sessioneId)`: sostituire `File.Delete(_filePausa)`
  con `EliminaFile(_filePausa)`.

In `ContenutiUtenteService.cs`:
- In `RimuoviMateria(string materiaId)`: prima di
  `Directory.Delete(cartellaMateria, recursive: true)`, enumerare tutti i file
  presenti in `cartellaMateria` (`Directory.EnumerateFiles(cartellaMateria, "*", SearchOption.AllDirectories)`)
  e per ciascuno chiamare `StorageService.EliminaFile(file)`, così anche la
  rimozione di una materia importata pulisce le chiavi corrispondenti nel
  browser.

Verifica: `dotnet build Wsa.Quiz.sln`.

---

## Task 2: scelta UI Touch / UI Desktop in `Wsa.Quiz.App` e safe area browser

**File:** modifica `wsa.quiz.app/AppEnv.cs`, `wsa.quiz.app/App.axaml.cs` e
`wsa.quiz.app/MainView.axaml.cs`.

Intenzione: `Avalonia.Browser` usa `ISingleViewApplicationLifetime` (come
Android e iOS), ma nel browser l'app può girare sia su telefono/tablet (dove
serve la UI Touch) sia su un PC desktop (dove serve la UI Desktop a due colonne
con le tab in alto).

In `AppEnv.cs`:
- Aggiungere una proprietà statica pubblica
  `public static bool UsaTouchInSingleView { get; set; } = true;` con commento
  XML: valore usato da `App.OnFrameworkInitializationCompleted` quando il
  lifetime e' `ISingleViewApplicationLifetime`. Default `true` (Android e iOS
  sono sempre touch); l'head browser lo imposta all'avvio in base al
  dispositivo (`true` su telefono/tablet, `false` su PC desktop).

In `App.axaml.cs`:
- Nel ramo `else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)`
  di `OnFrameworkInitializationCompleted()`:
  - impostare `AppEnv.TouchMode = AppEnv.UsaTouchInSingleView;` (invece di
    `true` fisso);
  - se `AppEnv.TouchMode` è `true`, mantenere il codice attuale (aggiunta di
    `Sizes.Touch.axaml` a `Resources.MergedDictionaries` e
    `singleView.MainView = new LayoutTransformControl { LayoutTransform = new ScaleTransform(AppEnv.ScalaTouch, AppEnv.ScalaTouch), Child = new MainView() }`);
  - se `AppEnv.TouchMode` è `false` (browser su PC desktop), non caricare
    `Sizes.Touch.axaml` e assegnare
    `singleView.MainView = new Border { Child = new MainView() };` (commento:
    si avvolge `MainView` in un `Border`, che eredita da `Decorator`, cosi'
    `SostituisciVistaPrincipale` al cambio lingua continua a funzionare tramite
    `case Decorator dec:`).

In `MainView.axaml.cs`:
- In `OnAttachedToVisualTree`: estendere la condizione `if (OperatingSystem.IsIOS())`
  a `if (OperatingSystem.IsIOS() || OperatingSystem.IsBrowser())`, aggiornando
  il commento sopra: anche nel browser mobile (quando `TouchMode` e' attivo) si
  legge `InsetsManager` del `TopLevel` se presente.

Verifica: `dotnet build Wsa.Quiz.sln`.

---

## Task 3: progetto `wsa.quiz.browser` (csproj + file statici + `Program.cs`)

**File:** crea `wsa.quiz.browser/Wsa.Quiz.Browser.csproj`,
`wsa.quiz.browser/wwwroot/index.html`, `wsa.quiz.browser/wwwroot/app.css`,
`wsa.quiz.browser/wwwroot/main.js` e `wsa.quiz.browser/Program.cs`.

### 3.1 `wsa.quiz.browser/Wsa.Quiz.Browser.csproj`
- `<Project Sdk="Microsoft.NET.Sdk.WebAssembly">`.
- Commento in testa (stile `Wsa.Quiz.iOS.csproj`): head WebAssembly per il
  browser; sta fuori da `Wsa.Quiz.sln` cosi' il build della solution non
  richiede il workload `wasm-tools`.
- `PropertyGroup`:
  - `TargetFramework`: `net10.0-browser`
  - `OutputType`: `Exe`
  - `AllowUnsafeBlocks`: `true`
  - `Nullable`: `enable`
  - `ImplicitUsings`: `enable`
  - `RootNamespace`: `Wsa.Quiz.Browser`
  - `AssemblyName`: `Wsa.Quiz.Browser`
  - `WasmMainJSPath`: `wwwroot\main.js`
- `ItemGroup` con `PackageReference` a `Avalonia.Browser` versione `12.0.2`.
- `ItemGroup` con `ProjectReference` a `..\wsa.quiz.app\Wsa.Quiz.App.csproj`.

### 3.2 `wsa.quiz.browser/wwwroot/app.css`
Foglio di stile minimale per l'host Avalonia a tutto schermo:
- `html, body, #out` con `margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background-color: #1e1e1e; font-family: sans-serif;`
- schermata di caricamento centrata (`.quaestio-splash`) con titolo «Quaestio»
  bianco e sottotitolo «Caricamento in corso…», che viene rimossa da Avalonia
  appena il canvas viene montato dentro `#out`.

### 3.3 `wsa.quiz.browser/wwwroot/index.html`
HTML5 minimale (`lang="it"`):
- `<meta charset="utf-8" />`
- `<meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover" />`
- `<title>Quaestio</title>`
- `<base href="./" />`
- `<link rel="stylesheet" href="./app.css" />`
- Nel `<body>`: `<div id="out"><div class="quaestio-splash"><h1>Quaestio</h1><p>Caricamento in corso…</p></div></div>` e `<script type="module" src="./main.js"></script>`.

### 3.4 `wsa.quiz.browser/wwwroot/main.js`
Modulo ES che avvia il runtime .NET WebAssembly ed espone a C# il modulo
`"quaestio-host"`:
- `import { dotnet } from './_framework/dotnet.js';`
- Crea il runtime con `await dotnet.withDiagnosticTracing(false).withApplicationArgumentsFromQuery().create();`
- Chiama `dotnetRuntime.setModuleImports("quaestio-host", { ... })` definendo 4
  funzioni:
  1. `isTouchDevice: () => window.matchMedia("(pointer: coarse)").matches || window.innerWidth < 768`
  2. `loadStorageJson: () => { ... }`: scorre `window.localStorage`, raccoglie
     tutte le chiavi che iniziano con `"wsaquiz:"` in un oggetto
     `{ [chiaveSenzaPrefisso]: valore }` e restituisce `JSON.stringify(out)`
     (in un blocco `try/catch` che in caso di errore restituisce `"{}"`).
  3. `saveStorageFile: (path, content) => { try { window.localStorage.setItem("wsaquiz:" + path, content); } catch { } }`
  4. `removeStorageFile: (path) => { try { window.localStorage.removeItem("wsaquiz:" + path); } catch { } }`
- Infine ottiene `const config = dotnetRuntime.getConfig();` ed esegue
  `await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);`.

### 3.5 `wsa.quiz.browser/Program.cs`
Namespace file-scoped `Wsa.Quiz.Browser`:
- Classe `internal sealed partial class Program` (deve essere `partial` per il
  source generator di `[JSImport]`).
- Dichiarazione dei 4 metodi `[JSImport("...", "quaestio-host")]`:
  `IsTouchDevice()`, `LoadStorageJson()`, `SaveStorageFile(string path, string content)`
  e `RemoveStorageFile(string path)`.
- Metodo privato `IdrataFileSystemDaLocalStorage()` chiamato all'inizio di
  `Main`:
  - legge il JSON da `LoadStorageJson()`, lo deserializza in
    `Dictionary<string, string>` (in un `try/catch` per non bloccare mai
    l'avvio);
  - per ogni coppia `(percorso, contenuto)`: se il percorso inizia con `"/"`,
    crea la directory padre (`Path.GetDirectoryName(percorso)`) con
    `Directory.CreateDirectory` e scrive il file con `File.WriteAllText(percorso, contenuto)`.
- In `private static Task Main(string[] args)`:
  1. chiama `IdrataFileSystemDaLocalStorage()`;
  2. registra `StorageService.OnFilePersisted = (percorso, contenuto) => { if (contenuto != null) SaveStorageFile(percorso, contenuto); else RemoveStorageFile(percorso); };`;
  3. imposta `AppEnv.UsaTouchInSingleView = IsTouchDevice();`;
  4. restituisce `BuildAvaloniaApp().WithInterFont().StartBrowserAppAsync("out");`.
- Metodo pubblico `public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Wsa.Quiz.App.App>();`.

Verifica: `dotnet build Wsa.Quiz.sln` e `dotnet build wsa.quiz.browser`.

---

## Task 4: documentazione in `docs/claude/piattaforme-e-distribuzione.md`

**File:** modifica `docs/claude/piattaforme-e-distribuzione.md`.

Aggiungere in fondo una sezione concisa `## WebAssembly (browser)` che spiega:
- che il progetto `wsa.quiz.browser` (`net10.0-browser`) sta fuori da
  `Wsa.Quiz.sln` come `wsa.quiz.ios` e `wsa.quiz.android`;
- comando di controllo veloce della compilazione C# (funziona su ogni macchina
  anche senza workload): `dotnet build wsa.quiz.browser`;
- prerequisito una tantum per eseguire o pubblicare il bundle WebAssembly
  completo (link di SkiaSharp/HarfBuzzSharp in `dotnet.native.wasm`):
  `dotnet workload install wasm-tools` (su macOS/Linux con `sudo`);
- comando per avviare il server locale di prova:
  `dotnet run --project wsa.quiz.browser` e aprire l'URL `http://localhost:...`
  stampato a terminale;
- comando per generare la cartella statica distribuibile gratuitamente (GitHub
  Pages / Cloudflare Pages): `dotnet publish wsa.quiz.browser -c Release`
  (output in `wsa.quiz.browser/bin/Release/net10.0-browser/browser-wasm/AppBundle/`).
