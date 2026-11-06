# iOS — Quaestio nel simulatore del Mac — Piano descrittivo

Spec: `docs/superpowers/specs/2026-11-06-ios-simulatore-design.md`.
Tutti i percorsi sono relativi alla radice del worktree.

**Obiettivo:** nuovo head `wsa.quiz.ios` (`net10.0-ios`) sopra la libreria
`Wsa.Quiz.App`, avviabile nel simulatore iOS; safe area iOS passata alla UI
condivisa con lo stesso meccanismo di Android.

**Architettura:** stesso schema di `wsa.quiz.android` (head sottile + libreria
condivisa). L'head contiene solo csproj, `AppDelegate`, entry point e
`Info.plist`. Nella libreria l'unico cambio è in `MainView`: su iOS le insets
vengono da `TopLevel.InsetsManager` invece che dalla `MainActivity`.

## Vincoli globali

- `net10.0` / `net10.0-ios`, Avalonia **12.0.2** (stessa versione degli altri
  csproj: non aggiornare).
- L'head iOS **non** va aggiunto a `Wsa.Quiz.sln`.
- Leggere ogni file esistente prima di modificarlo e seguirne lo stile:
  commenti in italiano, apostrofi al posto delle lettere accentate nei
  commenti del codice C# (`e'`, `gia'`), come in `wsa.quiz.android/*.cs`.
- Su Windows l'head iOS può non compilare (serve un Mac): **non** lanciare
  `dotnet build wsa.quiz.ios`. I build di controllo sono quelli sotto.

## File

- `wsa.quiz.ios/Wsa.Quiz.iOS.csproj` — **nuovo**
- `wsa.quiz.ios/AppDelegate.cs` — **nuovo**
- `wsa.quiz.ios/Main.cs` — **nuovo**
- `wsa.quiz.ios/Info.plist` — **nuovo**
- `wsa.quiz.app/MainView.axaml.cs` — modifica (safe area iOS)
- `wsa.quiz.app/AppEnv.cs` — modifica (solo commenti)

Comandi di build (dalla radice del worktree):

- app: `dotnet build wsa.quiz.app/Wsa.Quiz.App.csproj -c Debug`
- desktop: `dotnet build wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj -c Debug`

---

## Task 1: progetto `wsa.quiz.ios`

**File:** crea `wsa.quiz.ios/Wsa.Quiz.iOS.csproj`.

Modello: `wsa.quiz.android/Wsa.Quiz.Android.csproj` (leggerlo, stessa
struttura e stesso stile di commenti). `Sdk="Microsoft.NET.Sdk"`.

PropertyGroup, in quest'ordine:

| Proprietà | Valore | Nota da mettere nel commento |
|---|---|---|
| `OutputType` | `Exe` | come Android: senza, e' una libreria e non produce l'app |
| `TargetFramework` | `net10.0-ios` | |
| `SupportedOSPlatformVersion` | `15.0` | 15 e non 13 (template): serve per la chiave `UILaunchScreen` di Info.plist |
| `Nullable` | `enable` | |
| `ImplicitUsings` | `enable` | |
| `RootNamespace` | `Wsa.Quiz.Ios` | stesso motivo di `Wsa.Quiz.Droid` su Android (trappola 1) |
| `AssemblyName` | `Wsa.Quiz.iOS` | |
| `ApplicationId` | `it.eduits.wsaquiz` | uguale ad Android |
| `ApplicationTitle` | `Quaestio` | |
| `ApplicationVersion` | `1` | |
| `ApplicationDisplayVersion` | `1.0` | |

ItemGroup: `PackageReference` `Avalonia.iOS` versione `12.0.2`.
ItemGroup: `ProjectReference` a `..\wsa.quiz.app\Wsa.Quiz.App.csproj`.

Un commento in testa al file: head iOS, si builda solo su macOS con Xcode,
fuori dalla solution apposta (su Windows `dotnet build Wsa.Quiz.sln`
fallirebbe); comandi in `docs/claude/piattaforme-e-distribuzione.md`.

Build app e desktop (devono restare verdi: il csproj nuovo non li tocca).

---

## Task 2: `AppDelegate` ed entry point

**File:** crea `wsa.quiz.ios/AppDelegate.cs` e `wsa.quiz.ios/Main.cs`.
Namespace file-scoped `Wsa.Quiz.Ios`. Modello di stile:
`wsa.quiz.android/MainApplication.cs`.

`AppDelegate.cs`:
- `using` di `Foundation`, `Avalonia`, `Avalonia.iOS`.
- Classe `public partial class AppDelegate : AvaloniaAppDelegate<Wsa.Quiz.App.App>`
  con l'attributo `[Register("AppDelegate")]`. Il nome finisce in «Delegate»:
  racchiudere la dichiarazione fra `#pragma warning disable CA1711` /
  `restore CA1711`, come il template ufficiale.
- Override `protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)`
  che restituisce `base.CustomizeAppBuilder(builder)` seguito da
  `.WithInterFont()`, identico ad Android (expression-bodied come lì).
- Commento: a differenza di Android (trappola 14, dove l'Activity non e'
  generica e l'App si sceglie in `MainApplication`), su iOS in Avalonia 12
  l'App si sceglie qui, col parametro generico; schema preso dal template
  ufficiale `Avalonia.Templates` `xplat/AvaloniaTest.iOS`.

`Main.cs`:
- `using UIKit;`
- Classe `public class Application` con `static void Main(string[] args)` che
  chiama `UIApplication.Main(args, null, typeof(AppDelegate))`. Commento di una
  riga: entry point iOS, avvia l'`AppDelegate`.

Build app e desktop.

---

## Task 3: `Info.plist`

**File:** crea `wsa.quiz.ios/Info.plist` (XML plist 1.0 standard, con
l'intestazione `<?xml ...?>` e il `DOCTYPE` Apple
`-//Apple//DTD PLIST 1.0//EN`, `http://www.apple.com/DTDs/PropertyList-1.0.dtd`).

Chiavi del `dict`, in quest'ordine:

| Chiave | Valore |
|---|---|
| `CFBundleDisplayName` | stringa `Quaestio` |
| `LSRequiresIPhoneOS` | `true` |
| `MinimumOSVersion` | stringa `15.0` |
| `UIDeviceFamily` | array di interi `1` (iPhone) e `2` (iPad) |
| `UILaunchScreen` | dizionario vuoto (launch screen di sistema, niente storyboard) |
| `UIRequiredDeviceCapabilities` | array con la stringa `arm64` |
| `UISupportedInterfaceOrientations` | array: `UIInterfaceOrientationPortrait` |
| `UISupportedInterfaceOrientations~ipad` | array: `UIInterfaceOrientationPortrait`, `UIInterfaceOrientationPortraitUpsideDown`, `UIInterfaceOrientationLandscapeLeft`, `UIInterfaceOrientationLandscapeRight` |

Niente `CFBundleIdentifier` / `CFBundleVersion` / `CFBundleShortVersionString`:
li mette il build da `ApplicationId` / `ApplicationVersion` /
`ApplicationDisplayVersion` del csproj. Niente `Entitlements.plist` (il
simulatore non lo richiede).

Build app e desktop.

---

## Task 4: safe area iOS in `MainView`

**File:** modifica `wsa.quiz.app/MainView.axaml.cs` (sezione
`// --- SAFE AREA (edge-to-edge Android)`) e i commenti di
`wsa.quiz.app/AppEnv.cs`.

Intenzione: su iOS nessuno chiama `AppEnv.SetSystemBarInsetsPx` (lo fa solo la
`MainActivity` Android), quindi `ApplicaSafeArea` non applica nulla e header e
bottom-nav finiscono sotto notch e home indicator. Su iOS la fonte delle
insets è l'`InsetsManager` del `TopLevel` di Avalonia; le si passa ad `AppEnv`
così `ApplicaSafeArea` resta l'unico punto che applica i padding.

- In `OnAttachedToVisualTree`, dopo la sottoscrizione esistente a
  `AppEnv.SystemBarInsetsChanged` e prima di `ApplicaSafeArea()`: solo se
  `OperatingSystem.IsIOS()`, prendere `TopLevel.GetTopLevel(this)?.InsetsManager`
  e, se non è null, salvarlo in un campo privato, sottoscriversi al suo evento
  `SafeAreaChanged` e passare subito i valori attuali (vedi sotto).
- Un metodo privato `InoltraSafeAreaIos()` (o nome simile) legge
  `SafeAreaPadding` (una `Thickness` in unità logiche del TopLevel), la
  moltiplica per `RenderScaling` del TopLevel (fallback 1.0, come già fa
  `ApplicaSafeArea`) per ottenere pixel fisici, e chiama
  `AppEnv.SetSystemBarInsetsPx(left, top, right, bottom)`. Da lì l'evento
  esistente porta già ad `ApplicaSafeArea`, che riconverte i pixel in unità di
  layout. Il gestore di `SafeAreaChanged` chiama solo questo metodo.
- In `OnDetachedFromVisualTree`: desottoscrivere `SafeAreaChanged` se il campo
  non è null e azzerarlo.
- Desktop e Android: comportamento invariato (su Android si continua a usare
  la `MainActivity`, non l'InsetsManager, che lì riporta bottom=0).
- Aggiornare il commento `<summary>` sopra `OnAttachedToVisualTree`, il titolo
  della sezione (`edge-to-edge Android/iOS`) e, in `AppEnv.cs`, i commenti di
  `SystemBarInsetsPx` e `SetSystemBarInsetsPx`: su iOS il valore arriva da
  `MainView` (InsetsManager di Avalonia), su Android dalla `MainActivity`.

Se in Avalonia 12.0.2 `InsetsManager`, `SafeAreaPadding` o `SafeAreaChanged`
hanno nomi o firme diversi, non inventare: cercarli nella documentazione XML
del pacchetto (`%USERPROFILE%\.nuget\packages\avalonia\12.0.2\lib\net10.0\*.xml`,
grep di `InsetsManager` e `SafeArea`) e usare quelli veri.

Build app e desktop.
