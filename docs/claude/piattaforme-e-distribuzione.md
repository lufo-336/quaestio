# Piattaforme, distribuzione e test su device

<!-- Spostato da CLAUDE.md il 2026-11-05 per alleggerire il contesto: testo invariato. -->

### Sul PC Linux (Fedora)

Dal 2026-09-09 il progetto si compila **interamente** anche su Fedora, head
Android compreso. Niente è nel PATH di sistema: le variabili stanno in
`~/.bashrc`, ma **le shell non interattive non lo leggono**, quindi negli script
vanno esportate a mano.

```bash
export PATH="$HOME/.dotnet:$PATH"            # SDK 10.0.401, installazione utente
export DOTNET_ROOT="$HOME/.dotnet"
export JAVA_HOME="$HOME/.local/jdk/jdk-17.0.20.1+1"   # serve solo all'head Android
export ANDROID_HOME="$HOME/Android/Sdk"
```

Come è stato messo in piedi (da rifare solo su una macchina nuova):
`dotnet workload install android` — se fallisce con "Downloading
microsoft.android.sdk.linux ... failed" è **transitorio**, basta riprovare; poi
il JDK 17 (di sistema c'è solo la JRE, senza `javac`) e infine l'SDK Android con
`dotnet build wsa.quiz.android -t:InstallAndroidDependencies
-p:AndroidSdkDirectory=$HOME/Android/Sdk -p:AcceptAndroidSDKLicenses=True`.
Quest'ultimo porta con sé **`adb`** (`~/Android/Sdk/platform-tools/adb`), quindi
il workflow su device della sezione apposita funziona anche da qui, senza `sudo`.

> **Insidia da orologio**: se l'ora di sistema salta all'indietro (NTP, RTC in
> local time), i binari in `bin/` risultano *più recenti* dei sorgenti appena
> scritti e **MSBuild salta la ricompilazione** — il codice nuovo sembra non
> esistere e si perde tempo a cercare l'errore nel posto sbagliato. Sintomo:
> `CS0246` su un tipo che il file contiene di sicuro. Rimedio: `--no-incremental`
> su `dotnet build` (`dotnet publish` lo rifiuta con `MSB1001: Unknown switch`),
> oppure `touch` sui sorgenti.

## Distribuzione (publish desktop)

L'head desktop è **`wsa.quiz.desktop`** (`net10.0`, `OutputType=WinExe` — vedi
Trappola 4). Per consegnare un eseguibile che parta con doppio click si usa
`dotnet publish`, **non** `dotnet run`.

Tre opzioni, tre concetti distinti:

| Flag | Effetto |
|---|---|
| `-r <RID>` | sceglie piattaforma + CPU di destinazione (Runtime Identifier) |
| `--self-contained` | impacchetta **anche il runtime .NET** → gira su PC senza .NET installato (output più grande, ~70–80 MB) |
| `-p:PublishSingleFile=true` | un solo file invece di una cartella di DLL |

**Importante**: un binario è **specifico per OS+CPU**. Non esiste un unico file
che giri sia su Windows sia su Mac: si pubblica una volta per ogni target. Stesso
sorgente, pacchetti finali diversi. RID utili: `win-x64`, `win-arm64`,
`osx-arm64` (Mac Apple Silicon), `osx-x64` (Mac Intel), `linux-x64`.

> **`PublishSingleFile=true` da solo NON basta** (scoperto 2026-09-09, Step 14).
> Lascia accanto all'eseguibile le librerie native (`libSkiaSharp`,
> `libHarfBuzzSharp`, `av_libglesv2`) e i `.pdb` — su win-x64 sono 101 MB di soli
> simboli. Copiando il solo `.exe` l'app **non parte**. Servono altre due
> proprietà, usate in tutti i comandi qui sotto:
> `-p:IncludeNativeLibrariesForSelfExtract=true` (impacchetta le native) e
> `-p:DebugType=none` (niente `.pdb`).

### Windows (`.exe` singolo autonomo)

```powershell
dotnet publish wsa.quiz.desktop -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none
```

Output: `wsa.quiz.desktop\bin\Release\net10.0\win-x64\publish\Wsa.Quiz.Desktop.exe`
— copiabile e avviabile su qualsiasi Windows x64, senza installare .NET.
(Per i PC ARM, es. Surface recenti, ripetere con `-r win-arm64`.)

**Uso portabile su questo PC** (2026-11-06): il solo exe copiato come
`%LOCALAPPDATA%\Programs\Quaestio\Quaestio.exe` + collegamento
`Quaestio.lnk` sul desktop. Niente installazione; i dati restano in
`%APPDATA%\WsaQuiz` come con `dotnet run`. Per aggiornarlo: rifare il publish e
ricopiare l'exe (con l'app chiusa).

Accanto all'eseguibile il publish copia anche `materie.json` e `domande/`, ma
**non servono**: se non li trova nel bin, `MainView.Caricamento` ripiega sulle
risorse incorporate (la stessa strada di Android, vedi M2). Verificato il
2026-09-09 su linux-x64 copiando il **solo** eseguibile in una cartella vuota:
l'app parte, e un harness headless conferma che dalle risorse incorporate
escono 10 materie / 2163 domande.

Dimensioni reali misurate il 2026-09-09 (Release, self-contained, single file
con native incluse): **~101 MB** l'eseguibile linux-x64.

### Mac (eseguibile autonomo)

```powershell
dotnet publish wsa.quiz.desktop -c Release -r osx-arm64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none
```

Output: `...\bin\Release\net10.0\osx-arm64\publish\Wsa.Quiz.Desktop` (Mach-O,
niente `.exe`). Per Mac Intel usare `-r osx-x64`.

**Caveat Mac (importante, dato che si compila da una VM Windows):**
- Il `publish` da Windows **produce** il binario Mac (cross-compile ok), ma quel
  binario **non si può eseguire né firmare da Windows**.
- Su Mac va reso eseguibile (`chmod +x Wsa.Quiz.Desktop`) e di norma **firmato**
  (`codesign`), altrimenti Gatekeeper lo blocca al primo avvio.
- Quello prodotto è un **eseguibile nudo**, non un bundle `.app` cliccabile dal
  Finder: il `.app` (con `Info.plist`, icona) e la notarizzazione vanno fatti
  **su un Mac**. Per il progetto didattico l'eseguibile da terminale è
  sufficiente; il packaging `.app` completo è materiale di Step 14.

> Riduzione dimensioni: `-p:PublishTrimmed=true` rimuove il codice non usato, ma
> il trimming può rompere il caricamento via reflection (Avalonia/`AssetLoader`).
> Da provare e **verificare a runtime** prima di distribuire, non darlo per
> scontato. Vedi anche Step 14 (icona, About, installer).

### Android (APK su GitHub Releases)

L'APK consegnabile **non sta nel repo**: dall'8/09/2026 vive come allegato di una
release su GitHub. Prima era committato in `dist/`, e sei versioni da ~30 MB nella
storia pesavano 75 dei 76 MB che ogni clone si portava dietro; la storia è stata
riscritta per toglierle e `dist/` è ora nel `.gitignore`.

Per pubblicare una versione nuova: `dotnet build wsa.quiz.android -c Release`, poi

```powershell
gh release create apk-AAAA-MM-GG `
  wsa.quiz.android/bin/Release/net10.0-android/it.eduits.wsaquiz-Signed.apk `
  --title "APK Android AAAA-MM-GG" --notes "cosa cambia"
```

Nessun commit: l'APK è un artefatto, non sorgente. Le release stanno su
<https://github.com/lufo-336/quaestio/releases>.

Release pubblicate finora: `apk-2026-07-21`, `apk-2026-09-09`, `apk-2026-10-23` (firmata su Windows: chiave diversa dalla 09-09) e **`apk-2026-11-06`** (corrente, domande in inglese; stessa chiave della 10-23).
Nelle note di release conviene sempre segnalare **se la firma è cambiata**: chi
aggiorna deve sapere che dovrà disinstallare e perdere i dati locali.

### iOS (simulatore sul Mac) — dal 2026-11-06

Head `wsa.quiz.ios` (`net10.0-ios`), **fuori dalla solution**. Su Windows
`dotnet build wsa.quiz.ios` compila il C# (utile come controllo), ma l'app si
produce e si lancia solo su **macOS + Xcode**. Ogni versione del workload `ios`
vuole una versione precisa di Xcode: se non coincide il build lo dice, e si
installa quella (Xcode 26.5/26.6 chiedono macOS 26.2+).

Una volta sul Mac:

```sh
sw_vers -productVersion; xcodebuild -version; dotnet --version
sudo dotnet workload install ios
sudo xcode-select -s /Applications/Xcode.app && sudo xcodebuild -license accept
xcodebuild -runFirstLaunch && xcodebuild -downloadPlatform iOS   # runtime del simulatore
```

Ogni volta:

```sh
git fetch && git checkout feature/ios && git pull        # (poi: main)
xcrun simctl list devices available | grep -E "iPhone|iPad"
dotnet build wsa.quiz.ios -t:Run -r iossimulator-arm64 -p:_DeviceName=:v2:udid=<UDID>
```

Senza `-p:_DeviceName` usa il simulatore predefinito.

**Con Xcode 27 (verificato il 2026-11-09, Mac M1 8 GB, Xcode 27.2, SDK
10.0.403 / workload iOS 27.1)** il comando sopra non basta:

- **Simulator.app non esiste più**: il simulatore si vede da **DeviceHub**
  (`/Applications/Xcode.app/Contents/Applications/DeviceHub.app`).
- **`-t:Run` fallisce** (MT1008: mlaunch cerca Simulator.app). Si fa build
  normale e poi si installa e lancia con `simctl`.
- **Versione Xcode**: il workload 27.1 vuole Xcode 27.1; con 27.2 serve
  `-p:ValidateXcodeVersion=false`.
- **Dopo un cambio di SDK/workload, pulire `bin/` e `obj/` del head iOS.** Il
  build incrementale si tiene la `libxamarin-dotnet-debug.dylib` del workload
  vecchio (in `obj/.../nativelibraries/`) mentre rigenera `main.arm64.mm` col
  nuovo, quindi il link fallisce con
  `Undefined symbols: _xamarin_initialize_dynamic_registrar`.

```sh
U=<UDID>                                   # es. iPhone 17
xcrun simctl boot $U                       # se non è già acceso
open /Applications/Xcode.app/Contents/Applications/DeviceHub.app
rm -rf wsa.quiz.ios/bin wsa.quiz.ios/obj   # solo dopo un cambio di SDK/workload
dotnet build wsa.quiz.ios -r iossimulator-arm64 -p:ValidateXcodeVersion=false
xcrun simctl install $U wsa.quiz.ios/bin/Debug/net10.0-ios/iossimulator-arm64/Wsa.Quiz.iOS.app
xcrun simctl launch --console-pty $U it.eduits.wsaquiz   # stdout dell'app nel terminale
xcrun simctl io $U screenshot /tmp/quaestio-ios.png      # screenshot senza toccare DeviceHub
xcrun simctl shutdown all                                # quando si ha finito
```

Con 8 GB di RAM conviene usare **un solo simulatore**. Un primo avvio con il Mac
sotto pressione di memoria (watchdog 0x8BADF00D nel log) ha dato schermata
bianca; rifatto con il Mac scarico e un build pulito, l'app è partita normale.
Il messaggio `Socket error while connecting to IDE on 127.0.0.1:10000` è
innocuo: è il debugger che non trova l'IDE.

Dati utente nella sandbox
dell'app (restano finché non la si disinstalla dal simulatore). Il device vero
(iPad, Apple ID gratuito, profilo di 7 giorni) è un passo successivo: lì il
Release usa AOT + trimming, da verificare.

### Test su device fisico (adb)

> Ricostruito dai comandi realmente usati nelle sessioni su device (Samsung
> Galaxy S21 5G, SM-G991B, density 480, nav a 3 tasti). Sostituisce la vecchia
> memory `android-device-testing`, che non esiste più: queste note vivono qui
> perché il repo le porta su ogni macchina, la memory no.

`adb` non è nel PATH: sta in
`C:\Users\<utente>\AppData\Local\Android\Sdk\platform-tools\adb.exe`. Negli
script PowerShell si mette in `$adb` e si invoca con l'operatore di chiamata
(`& $adb ...`), altrimenti il path con spazi non viene interpretato.

**Ciclo tipico** (build → installa → avvia → guarda):

```powershell
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
$apk = "wsa.quiz.android\bin\Release\net10.0-android\it.eduits.wsaquiz-Signed.apk"

& $adb devices -l                       # se vuoto: kill-server + start-server
& $adb shell am force-stop it.eduits.wsaquiz
& $adb install -r $apk                  # -r = reinstalla conservando i dati
& $adb logcat -c                        # pulisci PRIMA di avviare
& $adb shell monkey -p it.eduits.wsaquiz -c android.intent.category.LAUNCHER 1
```

`monkey` è il modo affidabile di lanciare l'app senza conoscere il nome esatto
dell'activity. Per un **cold start vero** (necessario per il bug della
bottom-nav) serve `force-stop` prima, altrimenti si riattacca al processo vivo.

**Screenshot** — due varianti, la prima evita di scrivere sul device:

```powershell
& $adb exec-out screencap -p > shot.png                      # preferita
& $adb shell screencap -p /sdcard/q.png; & $adb pull /sdcard/q.png shot.png
& $adb shell rm /sdcard/q.png                                # pulizia
```

**Input sintetico** (utile per riprodurre il bug bottom-nav senza toccare il
telefono): `& $adb shell input tap <x> <y>` e
`& $adb shell input swipe <x1> <y1> <x2> <y2> <ms>`. Le coordinate sono in **px
fisici**: leggerle con `& $adb shell wm size` e `& $adb shell wm density`.
Lasciare ~1s di `Start-Sleep` prima dello screenshot, o si cattura il frame
di transizione. Se lo schermo è spento: `& $adb shell input keyevent KEYCODE_WAKEUP`.

**Log**: `& $adb logcat -d | Select-String -Pattern "wsaquiz|AndroidRuntime|mono"`.
`-d` scarica e termina (senza, resta in streaming e blocca la sessione).

**Ispezione utile in debug**:

```powershell
& $adb shell dumpsys window                            # insets, RenderScaling
& $adb shell settings get secure navigation_mode       # 0=3 tasti, 2=gesture
& $adb shell run-as it.eduits.wsaquiz ls -R /data/data/it.eduits.wsaquiz/files
```

L'ultimo mostra la cartella utente dell'app (cronologia, sospesi,
`preferenze.json`, `ContenutiUtente/`) — funziona solo su build **Debug**.

**APK Debug installabile a mano** (per iterare senza Release, vedi Trappola 16):

```powershell
dotnet build wsa.quiz.android -c Debug -t:Build `
  -p:EmbedAssembliesIntoApk=true -p:AndroidUseFastDeployment=false
```

Senza queste due proprietà il Debug usa il fast-deployment e l'APK installato
via `adb install` crasha con `monodroid: No assemblies found ... Exiting`.
