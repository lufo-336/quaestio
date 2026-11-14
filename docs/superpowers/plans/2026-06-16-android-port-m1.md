# Port Android — Milestone 1 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Trasformare l'app Avalonia desktop in una soluzione multipiattaforma in cui lo stesso codice UI gira sia su desktop (invariato) sia su Android, e produrre un APK debug che si avvia su emulatore.

**Architecture:** `Wsa.Quiz.App` viene declassato da eseguibile desktop a **libreria UI condivisa**. Due head sottili la consumano: `Wsa.Quiz.Desktop` (eseguibile Windows) e `Wsa.Quiz.Android` (APK). La shell `MainWindow` diventa `MainView : UserControl` e `App` sceglie il lifetime in base alla piattaforma.

**Tech Stack:** .NET 10, Avalonia 12.0.2, Avalonia.Desktop, Avalonia.Android, workload `android`, Android SDK + emulatore.

**Spec di riferimento:** `docs/superpowers/specs/2026-06-16-android-port-design.md`

**Branch:** `feature/android` (già creato).

---

## Decisione di naming (deviazione consapevole dallo spec)

Lo spec proponeva di rinominare `Wsa.Quiz.App` → `Wsa.Quiz.UI`. In fase di
pianificazione si è scelto di **mantenere il nome `Wsa.Quiz.App`** per la
libreria condivisa: nessun `.axaml` usa `assembly=`, ma rinominare RootNamespace
imporrebbe di toccare ~25 dichiarazioni `namespace` e tutti gli `x:Class`, churn
inutile e rischioso. La libreria resta `Wsa.Quiz.App` (namespace invariato),
i due head sono `Wsa.Quiz.Desktop` e `Wsa.Quiz.Android`.

L'head Android usa il namespace **`Wsa.Quiz.Droid`** (NON `Wsa.Quiz.Android`):
un segmento `Android` nel namespace rifarebbe la Trappola 1 (shadowing del
namespace radice `Android.*` dell'SDK, come `Wsa.Quiz.Console` vs
`System.Console`).

## File Structure

| File | Responsabilità | Azione |
|------|----------------|--------|
| `wsa.quiz.app/Wsa.Quiz.App.csproj` | Libreria UI condivisa (Avalonia core, no Desktop) | Modify |
| `wsa.quiz.app/MainWindow.axaml(.cs)` → `MainView.axaml(.cs)` | Shell come `UserControl` | Rename+Modify |
| `wsa.quiz.app/App.axaml.cs` | Selezione lifetime desktop/single-view | Modify |
| `wsa.quiz.app/Program.cs` | (rimosso, spostato nell'head desktop) | Delete |
| `wsa.quiz.app/app.manifest` | (spostato nell'head desktop) | Move |
| `wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj` | Head eseguibile Windows | Create |
| `wsa.quiz.desktop/Program.cs` | Bootstrap desktop | Create |
| `wsa.quiz.desktop/app.manifest` | Manifest Windows | Create (da App) |
| `wsa.quiz.android/Wsa.Quiz.Android.csproj` | Head APK | Create |
| `wsa.quiz.android/MainActivity.cs` | Activity Avalonia | Create |
| `wsa.quiz.android/Properties/AndroidManifest.xml` | Manifest Android | Create |
| `Wsa.Quiz.sln` | Aggiunta dei due head | Modify (via `dotnet sln`) |

**Convenzione di verifica:** questo lavoro è scaffolding di progetti, non logica
con unit test. Il "test" di ogni task è un **comando di build/run** con output
atteso. La regola d'oro, ripetuta in ogni task: **il desktop deve continuare a
funzionare identico**.

---

### Task 1: Spostare l'head desktop e declassare `Wsa.Quiz.App` a libreria

**Files:**
- Create: `wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj`
- Create: `wsa.quiz.desktop/Program.cs`
- Create: `wsa.quiz.desktop/app.manifest`
- Modify: `wsa.quiz.app/Wsa.Quiz.App.csproj`
- Delete: `wsa.quiz.app/Program.cs`, `wsa.quiz.app/app.manifest`
- Modify: `Wsa.Quiz.sln`

- [ ] **Step 1: Verifica baseline — il desktop attuale gira**

Run: `dotnet build wsa.quiz.app/Wsa.Quiz.App.csproj -c Debug`
Expected: `Build succeeded`. (Conferma il punto di partenza prima di toccare.)

- [ ] **Step 2: Creare il csproj dell'head desktop**

Create `wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Wsa.Quiz.Desktop</RootNamespace>
    <AssemblyName>Wsa.Quiz.Desktop</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia.Desktop" Version="12.0.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\wsa.quiz.app\Wsa.Quiz.App.csproj" />
  </ItemGroup>

  <!-- Dati read-only copiati nell'output del solo head desktop. -->
  <ItemGroup>
    <None Include="..\materie.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Include="..\domande\**\*.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <Link>domande\%(RecursiveDir)%(Filename)%(Extension)</Link>
    </None>
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Spostare `Program.cs` nell'head desktop**

Create `wsa.quiz.desktop/Program.cs` (namespace nuovo, usa `App` dalla libreria):

```csharp
using Avalonia;
using System;
using Wsa.Quiz.App;

namespace Wsa.Quiz.Desktop;

internal class Program
{
    // STAThread per compatibilita' clipboard/dialog su Windows.
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

- [ ] **Step 4: Spostare `app.manifest`**

Copiare il contenuto di `wsa.quiz.app/app.manifest` in
`wsa.quiz.desktop/app.manifest` (identico, nessuna modifica al contenuto).

- [ ] **Step 5: Declassare `Wsa.Quiz.App` a libreria**

Replace l'intero `wsa.quiz.app/Wsa.Quiz.App.csproj` con:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Wsa.Quiz.App</RootNamespace>
    <AssemblyName>Wsa.Quiz.App</AssemblyName>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.0.2" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.2" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="12.0.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\wsa.quiz.core\Wsa.Quiz.Core.csproj" />
  </ItemGroup>

</Project>
```

Cosa è cambiato e perché: rimossi `OutputType=WinExe`, `ApplicationManifest`,
`BuiltInComInteropSupport` (sono cose da eseguibile, ora vivono nell'head
desktop); rimosso `Avalonia.Desktop` (backend desktop, va nell'head desktop);
rimossa la copia dei dati (va nell'head desktop). Restano i package Avalonia
**core** (cross-platform) e il riferimento a Core.

- [ ] **Step 6: Eliminare i file spostati dalla libreria**

```bash
rm wsa.quiz.app/Program.cs wsa.quiz.app/app.manifest
```

- [ ] **Step 7: Aggiornare la solution**

```bash
dotnet sln Wsa.Quiz.sln add wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj
```

Expected: `Project ... added to the solution.`

- [ ] **Step 8: Build della libreria + head desktop**

Run: `dotnet build wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj -c Debug`
Expected: `Build succeeded`. La libreria `Wsa.Quiz.App` viene compilata come
dipendenza senza errori XAML.

- [ ] **Step 9: VERIFICA CRITICA — il desktop gira identico**

Run: `dotnet run --project wsa.quiz.desktop`
Expected: la finestra "WSA Quiz" si apre con le 4 tab (Home/Cronologia/Sospesi/
Statistiche) e i dati caricati (N materie · N domande nell'header). Chiudere
la finestra. Se i dati non si caricano, controllare che `materie.json` e
`domande/` siano finiti in `wsa.quiz.desktop/bin/Debug/net10.0/`.

- [ ] **Step 10: Commit**

```bash
git add wsa.quiz.desktop/ wsa.quiz.app/Wsa.Quiz.App.csproj Wsa.Quiz.sln
git rm wsa.quiz.app/Program.cs wsa.quiz.app/app.manifest
git commit -m "refactor: split desktop head from shared UI library"
```

---

### Task 2: Lifecycle — `MainWindow` → `MainView : UserControl` + dual lifetime

**Files:**
- Rename: `wsa.quiz.app/MainWindow.axaml` → `wsa.quiz.app/MainView.axaml`
- Rename: `wsa.quiz.app/MainWindow.axaml.cs` → `wsa.quiz.app/MainView.axaml.cs`
- Modify: `wsa.quiz.app/App.axaml.cs`

- [ ] **Step 1: Rinominare i file della shell**

```bash
git mv wsa.quiz.app/MainWindow.axaml wsa.quiz.app/MainView.axaml
git mv wsa.quiz.app/MainWindow.axaml.cs wsa.quiz.app/MainView.axaml.cs
```

- [ ] **Step 2: Convertire la radice XAML da `Window` a `UserControl`**

In `wsa.quiz.app/MainView.axaml`, sostituire l'header (le prime 10 righe fino a
`WindowStartupLocation="CenterScreen">`) con:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             x:Class="Wsa.Quiz.App.MainView">
```

E il tag di chiusura `</Window>` in fondo al file → `</UserControl>`.
(Le proprietà `Title/Width/Height/MinWidth/MinHeight/WindowStartupLocation`
spariscono: le impostiamo sulla `Window` desktop nello Step 4.)

- [ ] **Step 3: Convertire la classe code-behind**

In `wsa.quiz.app/MainView.axaml.cs`:
- Cambiare `public partial class MainWindow : Window` →
  `public partial class MainView : UserControl`.
- Cambiare il costruttore `public MainWindow()` → `public MainView()`.
- Aggiungere/garantire gli using: `using Avalonia.Controls;` (per `UserControl`).
- Lasciare invariato tutto il resto (override `OnKeyDown`, `Caricamento()`, i
  riferimenti a `Tabs`, `HomeArea`, ecc.): `UserControl` espone `OnKeyDown`,
  `Focus()` e i named controls esattamente come prima.

- [ ] **Step 4: `App.axaml.cs` sceglie il lifetime**

Replace il corpo di `wsa.quiz.app/App.axaml.cs` con:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Wsa.Quiz.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var mainView = new MainView();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Desktop: la shell vive dentro una Window con le stesse dimensioni di prima.
            desktop.MainWindow = new Window
            {
                Title = "WSA Quiz",
                Width = 1080,
                Height = 760,
                MinWidth = 780,
                MinHeight = 540,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = mainView
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            // Android / single-view: la shell è la view radice, niente Window.
            singleView.MainView = mainView;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 5: Build**

Run: `dotnet build wsa.quiz.desktop/Wsa.Quiz.Desktop.csproj -c Debug`
Expected: `Build succeeded`. (Se compare `could not resolve property` su un
DataTemplate, è la Trappola 5: assicurarsi che `MainView.axaml` non abbia perso
attributi necessari nello Step 2 — ma qui non tocchiamo `x:DataType` interni.)

- [ ] **Step 6: VERIFICA CRITICA — desktop identico**

Run: `dotnet run --project wsa.quiz.desktop`
Expected: finestra "WSA Quiz" 1080×760 centrata, 4 tab funzionanti, dati
caricati, `Ctrl+Tab` cambia tab. Avviare un quiz breve e verificare che la
navigazione interna (A/B/C/D, Invio) funzioni come prima. Chiudere.

- [ ] **Step 7: Commit**

```bash
git add wsa.quiz.app/MainView.axaml wsa.quiz.app/MainView.axaml.cs wsa.quiz.app/App.axaml.cs
git commit -m "refactor: MainWindow -> MainView UserControl with dual lifetime"
```

---

### Task 3: Toolchain Android (prerequisito ambiente)

**Files:** nessuno (operazioni di ambiente). Alcuni passi possono richiedere
privilegi elevati o download cospicui: se un comando non è eseguibile in
autonomia, va passato all'utente con il prefisso `!` nella sessione.

- [ ] **Step 1: Installare il workload android**

Run: `dotnet workload install android`
Expected: termina con `Successfully installed workload(s) android.`
(Se chiede privilegi elevati, eseguirlo da terminale admin.)

- [ ] **Step 2: Verificare il workload**

Run: `dotnet workload list`
Expected: la tabella elenca `android` con una versione.

- [ ] **Step 3: Procurare un JDK 17**

Il build Android .NET vuole JDK 17; sulla macchina c'è Java 25 (troppo recente).
Installare **Microsoft OpenJDK 17** (es. da `winget install Microsoft.OpenJDK.17`)
e NON toccare il `JAVA_HOME` globale. Annotare il path, es.
`C:\Program Files\Microsoft\jdk-17.x.x`.

- [ ] **Step 4: Installare l'Android SDK + emulatore**

Opzione consigliata: installare **Android Studio** (porta SDK, platform-tools,
emulatore e SDK Manager con GUI) oppure i `commandline-tools` da soli. Servono:
platform-tools, una platform (es. `android-34`), build-tools, `emulator`, e una
system image (es. `system-images;android-34;google_apis;x86_64`).
Impostare `ANDROID_HOME` al path dell'SDK (es. `%LOCALAPPDATA%\Android\Sdk`).

Run (verifica): `& "$env:ANDROID_HOME\platform-tools\adb.exe" version`
Expected: stampa la versione di adb.

- [ ] **Step 5: Creare e avviare un emulatore (AVD)**

Creare un AVD (da Android Studio Device Manager, o `avdmanager create avd`).
Avviarlo, poi:

Run: `& "$env:ANDROID_HOME\platform-tools\adb.exe" devices`
Expected: l'emulatore compare come `emulator-5554   device`.

- [ ] **Step 6: Commit**

Nessun file di codice cambia in questo task. Se si è dovuto aggiungere un file
di configurazione locale (es. `Directory.Build.props` con `JavaSdkDirectory`),
committarlo:

```bash
git add -A
git commit -m "chore: android toolchain config" --allow-empty
```

---

### Task 4: Head Android (`Wsa.Quiz.Android`)

**Files:**
- Create: `wsa.quiz.android/Wsa.Quiz.Android.csproj`
- Create: `wsa.quiz.android/MainActivity.cs`
- Create: `wsa.quiz.android/Properties/AndroidManifest.xml`
- Modify: `Wsa.Quiz.sln`

- [ ] **Step 1: Creare il csproj dell'head Android**

Create `wsa.quiz.android/Wsa.Quiz.Android.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0-android</TargetFramework>
    <SupportedOSPlatformVersion>24</SupportedOSPlatformVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- RootNamespace NON 'Wsa.Quiz.Android': eviterebbe lo shadowing del
         namespace radice Android.* dell'SDK (vedi Trappola 1). -->
    <RootNamespace>Wsa.Quiz.Droid</RootNamespace>
    <AssemblyName>Wsa.Quiz.Android</AssemblyName>
    <ApplicationId>it.eduits.wsaquiz</ApplicationId>
    <ApplicationVersion>1</ApplicationVersion>
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
    <AndroidPackageFormat>apk</AndroidPackageFormat>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia.Android" Version="12.0.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\wsa.quiz.app\Wsa.Quiz.App.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Creare la `MainActivity`**

Create `wsa.quiz.android/MainActivity.cs`:

```csharp
using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Wsa.Quiz.App;

namespace Wsa.Quiz.Droid;

[Activity(
    Label = "WSA Quiz",
    Theme = "@android:style/Theme.Material.Light.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder)
            .WithInterFont();
}
```

Nota: `AvaloniaMainActivity<App>` istanzia `App` e usa il lifetime single-view;
`App.OnFrameworkInitializationCompleted` (Task 2) assegnerà `MainView`.

- [ ] **Step 3: Creare l'AndroidManifest**

Create `wsa.quiz.android/Properties/AndroidManifest.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
          android:versionCode="1"
          android:versionName="1.0">
  <uses-sdk android:minSdkVersion="24" android:targetSdkVersion="34" />
  <application android:label="WSA Quiz" android:allowBackup="true" />
</manifest>
```

- [ ] **Step 4: Aggiungere l'head alla solution**

```bash
dotnet sln Wsa.Quiz.sln add wsa.quiz.android/Wsa.Quiz.Android.csproj
```

Expected: `Project ... added to the solution.`

- [ ] **Step 5: VERIFICA — l'head Android compila**

Run: `dotnet build wsa.quiz.android/Wsa.Quiz.Android.csproj -c Debug`
Expected: `Build succeeded`. La libreria `Wsa.Quiz.App` viene ricompilata per il
consumo da `net10.0-android` senza errori. Se compaiono simboli mancanti
(es. su `ConfermaDialog`/`PausaDialog`), isolarli dietro `#if` come previsto
dallo spec — ma il caso atteso è build pulita, perché `Window` è in Avalonia
core.

- [ ] **Step 6: VERIFICA — il desktop NON si è rotto**

Run: `dotnet build Wsa.Quiz.sln -c Debug`
Expected: `Build succeeded` per tutti i progetti (Core, App, Cli, Desktop,
Android).

- [ ] **Step 7: Commit**

```bash
git add wsa.quiz.android/ Wsa.Quiz.sln
git commit -m "feat: add Android head project (Wsa.Quiz.Android)"
```

---

### Task 5: Deploy APK debug su emulatore

**Files:** nessuno (build + deploy).

- [ ] **Step 1: Avviare l'emulatore** (se non già attivo, vedi Task 3 Step 5)

Run: `& "$env:ANDROID_HOME\platform-tools\adb.exe" devices`
Expected: un device `emulator-XXXX  device`.

- [ ] **Step 2: Build + run sull'emulatore**

Run: `dotnet build wsa.quiz.android/Wsa.Quiz.Android.csproj -t:Run -c Debug`
Expected: l'APK viene compilato, installato e l'app si avvia sull'emulatore.

- [ ] **Step 3: VERIFICA DELLA MILESTONE — la shell appare**

Sull'emulatore: l'app "WSA Quiz" si apre e mostra la shell con le 4 tab
(Home/Cronologia/Sospesi/Statistiche). L'header materie/domande può indicare 0
(i dati non sono ancora impacchettati come asset — è atteso, è la Milestone 2).
Criterio di successo M1 = la UI si avvia e si naviga fra le tab.

- [ ] **Step 4: (Opzionale) Produrre il file APK su disco**

Run: `dotnet publish wsa.quiz.android/Wsa.Quiz.Android.csproj -c Debug -f net10.0-android`
Expected: l'APK debug è in `wsa.quiz.android/bin/Debug/net10.0-android/` (file
`*-Signed.apk`). Installabile su un dispositivo reale con
`adb install <file>.apk`.

- [ ] **Step 5: Commit**

Nessun artefatto di build va committato (`bin/` è in `.gitignore`). Aggiornare
eventualmente lo stato in `CLAUDE.md` (sezione roadmap: "Port Android M1
completata").

```bash
git add CLAUDE.md
git commit -m "docs: mark Android M1 (shell on emulator) complete"
```

---

## Self-Review

**Spec coverage:**
- Restructure UI condivisa + 2 head → Task 1, 4. ✔
- Refactor lifecycle (MainWindow→MainView, dual lifetime) → Task 2. ✔
- Head Android (MainActivity, manifest, csproj) → Task 4. ✔
- Toolchain (workload, JDK 17, SDK, emulatore) → Task 3. ✔
- Gestione dialoghi in M1 (compila, niente porting reale) → Task 4 Step 5. ✔
- Criteri di successo M1 (desktop intatto; build android; APK su emulatore) →
  Task 2 Step 6, Task 4 Step 5–6, Task 5 Step 3. ✔
- Fuori scope (asset dati, overlay dialoghi, layout touch) → non pianificati,
  rinviati a M2/M3 come da spec. ✔

**Placeholder scan:** nessun TODO/TBD; ogni step ha comando o codice completo.

**Type consistency:** `MainView` usato coerentemente in App.axaml.cs (Task 2) e
implicitamente dall'head Android via `App` (Task 4). `App` namespace
`Wsa.Quiz.App` invariato; head android namespace `Wsa.Quiz.Droid`. `Wsa.Quiz.App`
resta sia folder/csproj sia assembly della libreria. Versioni Avalonia `12.0.2`
coerenti su tutti i csproj.
