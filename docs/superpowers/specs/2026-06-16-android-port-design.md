# Spec: Port Android dell'app Wsa.Quiz (Milestone 1)

- **Data:** 2026-06-16
- **Branch:** `feature/android`
- **Autore:** Luca Foglia (con Claude)
- **Stato:** approvato (design)

## Contesto

`Wsa.Quiz` è un'app quiz scritta in **Avalonia 12 / .NET 10**. Avalonia
supporta Android nativamente, quindi è possibile produrre un APK riutilizzando
il codice esistente — niente riscrittura.

Struttura attuale:

```
Wsa.Quiz.Core   libreria di logica (Models, Services: QuizService,
                StatisticheService, StorageService)
Wsa.Quiz.Cli    head a riga di comando
Wsa.Quiz.App    head desktop Avalonia (WinExe) + TUTTE le viste UI
                (App.axaml, MainWindow, Views/, State/, QuizColors)
```

Tre ostacoli al porting, già identificati:

1. **Lifecycle desktop-only.** `App` istanzia `MainWindow : Window` solo per
   `IClassicDesktopStyleApplicationLifetime`. Android usa
   `ISingleViewApplicationLifetime` e non ha il concetto di `Window`.
2. **Dialoghi basati su `Window`.** `ConfermaDialog` e `PausaDialog` derivano da
   `Window` e usano `ShowDialog(owner)`, API inesistenti su mobile.
3. **Caricamento dati da file system.** `StorageService` legge `materie.json` e
   `domande/**` da `AppContext.BaseDirectory`. Su Android i dati read-only vanno
   impacchettati come `AndroidAsset` e letti via stream, non da path su disco.

Stato toolchain sulla macchina (rilevato 2026-06-16):

- .NET SDK **10.0.300** presente.
- Workload **android NON installato**.
- **Java 25** presente; il build Android .NET richiede tipicamente **JDK 17**.
- **Android SDK non configurato** (nessun `ANDROID_HOME`/`ANDROID_SDK_ROOT`).

## Obiettivo della Milestone 1

> L'app si **compila per Android** e si **avvia su un emulatore**, mostrando la
> shell con i tab. L'app desktop continua a funzionare **identica** a prima.

Esplicitamente **fuori scope** in questa milestone (milestone successive):

- Conversione dialoghi `Window` → overlay in-page (M3).
- Caricamento dati da `AndroidAsset` (M2): su Android la lista materie può
  risultare vuota; l'obiettivo è solo l'avvio.
- Layout/ergonomia touch e pubblicazione su Play Store.

## Approccio scelto

Lavoro su branch `feature/android`. Niente copia duplicata del progetto:
isolamento via git.

### Restructure del codice

Estrarre le viste in una **libreria UI condivisa** così desktop e Android usano
lo stesso identico codice:

```
Wsa.Quiz.Core      (invariato)
Wsa.Quiz.UI    NUOVO: libreria. Contiene App.axaml(.cs), MainView (ex
                      MainWindow), Views/, State/, QuizColors.cs. Riferisce Core.
Wsa.Quiz.Desktop  ex Wsa.Quiz.App: ridotto a Program.cs + app.manifest +
                      riferimento a Wsa.Quiz.UI. Resta WinExe, net10.0.
Wsa.Quiz.Android  NUOVO: net10.0-android. MainActivity, AndroidManifest.xml,
                      icona, SplashActivity opzionale. Riferisce Wsa.Quiz.UI.
```

Razionale: una sola fonte di verità per la UI; i due head sono sottili
adattatori di piattaforma. È lo schema multipiattaforma standard di Avalonia.

> Nota: `Wsa.Quiz.App` viene **rinominato/spostato** in `Wsa.Quiz.Desktop` per
> chiarezza. In alternativa si può mantenere il nome `Wsa.Quiz.App` come head
> desktop; la decisione di rinominare è cosmetica e non blocca il porting.

### Refactor del lifecycle (in `Wsa.Quiz.UI`)

- `MainWindow : Window` → **`MainView : UserControl`**. Tutta la logica di
  orchestrazione (tab, boot, transizioni Home/Quiz/Riepilogo) resta invariata;
  cambia solo la classe base e la rimozione delle API specifiche di `Window`.
- Le scorciatoie da tastiera (`Ctrl+Tab` ecc.) restano: innocue, semplicemente
  inutilizzate su touch.
- `App.OnFrameworkInitializationCompleted` gestisce **entrambi** i lifetime:
  - `IClassicDesktopStyleApplicationLifetime` → crea una `Window` e ci mette
    dentro `MainView` (comportamento desktop identico a oggi).
  - `ISingleViewApplicationLifetime` → assegna `MainView` a `singleView.MainView`.

### Head Android

- `Wsa.Quiz.Android.csproj` con `TargetFramework=net10.0-android`, package
  `Avalonia.Android`.
- `MainActivity : AvaloniaMainActivity<App>` che chiama
  `CustomizeAppBuilder` con `.UseAndroid()` (font Inter già usato).
- `AndroidManifest.xml` minimale (nome app, orientamento, min SDK ragionevole es.
  API 24+).
- Icona placeholder (si raffina dopo).

### Gestione dialoghi nella Milestone 1

I dialoghi `Window`/`ShowDialog` **non vengono ancora portati**. Per non rompere
la compilazione della libreria condivisa su Android, i tipi `ConfermaDialog` e
`PausaDialog` (e le loro chiamate `ShowDialog`) restano nel codice; usano API
Avalonia presenti anche nel target Android a livello di compilazione. Se in fase
di build emergono simboli non disponibili su `net10.0-android`, si isolano
dietro `#if !ANDROID` con un fallback no-op, rimandando il porting reale alla M3.
Obiettivo: la libreria **compila** per entrambi i target; il comportamento dei
dialoghi su Android sarà corretto in M3.

### Toolchain (prerequisito operativo)

1. `dotnet workload install android`.
2. Procurare/configurare **JDK 17** (Microsoft OpenJDK) e puntarlo al build
   Android (`JavaSdkDirectory` o `JAVA_HOME` dedicato), lasciando Java 25 per il
   resto del sistema.
3. Installare **Android SDK** (cmdline-tools, platform-tools, una platform es.
   API 34, build-tools) e impostare `ANDROID_HOME`.
4. Creare/avviare un **AVD** (emulatore) per il test.

Questi passi richiedono download cospicui e possibilmente privilegi elevati; se
qualche passo non è eseguibile in autonomia, viene segnalato all'utente con il
comando esatto da lanciare.

## Componenti e responsabilità

| Componente | Cosa fa | Dipende da |
|------------|---------|------------|
| `Wsa.Quiz.Core` | Logica quiz, statistiche, storage | — |
| `Wsa.Quiz.UI` | App Avalonia + viste + stato + `MainView` | Core, Avalonia |
| `Wsa.Quiz.Desktop` | Bootstrap desktop (`Program.Main`) | UI |
| `Wsa.Quiz.Android` | `MainActivity` + manifest Android | UI |

Flusso d'avvio Android: `MainActivity` → `App` → lifetime single-view →
`MainView` → boot dati (`StorageService`, possibilmente vuoto in M1) → shell tab.

## Rischi e mitigazioni

- **Toolchain non installabile in autonomia** → fornire comandi all'utente.
- **Java 25 incompatibile col build Android** → affiancare JDK 17 dedicato.
- **API Avalonia divergenti tra desktop e Android** (es. clipboard, dialog) →
  isolare con `#if` e rimandare a M2/M3.
- **Dati assenti su Android in M1** → atteso e documentato; risolto in M2.

## Criteri di successo (Milestone 1)

1. `Wsa.Quiz.Desktop` (ex App) compila ed esegue su Windows **identico** a oggi.
2. La solution compila per `net10.0-android` senza errori.
3. L'APK debug si installa su un emulatore e mostra la shell con i tab.

## Milestone successive (non in questo spec)

- **M2 — Dati:** impacchettare `materie.json` + `domande/**` come `AndroidAsset`,
  astrarre `StorageService` su stream/loader, popolare il quiz su Android.
- **M3 — UX mobile:** dialoghi `Window` → overlay in-page; layout responsive e
  touch-friendly; icona/splash definitivi.
- **M4 — Distribuzione:** keystore, AAB firmato, eventuale Play Store.
