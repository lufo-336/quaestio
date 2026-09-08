# M3 — Icona app + Splash screen Android — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dare all'app Android un'icona (spunta bianca su verde brand) e uno splash screen minimale, sostituendo l'icona di default e il tema di lancio attuali.

**Architecture:** Arte sorgente in SVG (`branding/`) come fonte di verità. Icona Android come *adaptive icon vettoriale* (API 26+) con fallback PNG legacy (API 24–25) rasterizzati da un tool dotnet usa-e-getta. Splash minimale via temi a tema *version-split* (no NuGet, no codice runtime). Tutte le modifiche vivono in `wsa.quiz.android/`, `branding/` e `tools/`; desktop/CLI/core intoccati.

**Tech Stack:** .NET 10 Android (Microsoft.Android SDK), Avalonia 12.0.2, Android resource XML (adaptive-icon / vector drawable / layer-list / styles), SkiaSharp + Svg.Skia (solo per il tool di rasterizzazione).

## Global Constraints

- Spec di riferimento: `docs/superpowers/specs/2026-06-23-m3-icona-splash-design.md` (verbatim).
- Branch di lavoro: `feature/android`.
- Colore brand sfondo icona: **`#1F7A4D`** (verde). Spunta: **`#FFFFFF`** (bianco).
- `minSdk = 24`, `targetSdk = 36`, `ApplicationId = it.eduits.wsaquiz`, label `"WSA Quiz"` — **invariati**.
- Viewport unificato dell'arte: **108** unità (coerente fra SVG e vector drawable Android).
- Adaptive foreground: contenuto entro il **safe zone centrale** (~36–72 su 108) — Android ritaglia i bordi.
- `docs/` è in `.gitignore`: spec e questo piano restano **locali, non committati**. Lo stato committato si annota nel `CLAUDE.md` (tracciato).
- Niente `roundIcon`. Niente libreria AndroidX `core-splashscreen`. Niente override di `OnCreate`.
- Niente modifiche a `Wsa.Quiz.Desktop`, `Wsa.Quiz.Cli`, `Wsa.Quiz.App`, `Wsa.Quiz.Core`.
- Dominio "asset/risorse": niente unit test classici. Il ciclo di verifica di ogni task è **build verde / ispezione visiva / controllo strutturale APK**, non `xunit`.

## File Structure

**Creati:**
- `branding/icon.svg` — icona piena (sfondo verde arrotondato + spunta), sorgente dei PNG legacy.
- `branding/icon-foreground.svg` — solo spunta su trasparente, viewport 108, safe-zone — sorgente concettuale del vector drawable.
- `tools/IconGen/IconGen.csproj` — console dotnet usa-e-getta (fuori da `Wsa.Quiz.sln`).
- `tools/IconGen/Program.cs` — rasterizza `branding/icon.svg` nei 5 PNG mipmap + un preview.
- `wsa.quiz.android/Resources/values/colors.xml` — `@color/icon_background`.
- `wsa.quiz.android/Resources/values/styles.xml` — `Theme.WsaQuiz.Splash` (base, API 24–30).
- `wsa.quiz.android/Resources/values-v31/styles.xml` — override Splash API (API 31+).
- `wsa.quiz.android/Resources/drawable/ic_launcher_foreground.xml` — vector drawable spunta.
- `wsa.quiz.android/Resources/drawable/splash_classic.xml` — layer-list (verde + spunta centrata).
- `wsa.quiz.android/Resources/mipmap-anydpi-v26/appicon.xml` — adaptive icon.
- `wsa.quiz.android/Resources/mipmap-{mdpi,hdpi,xhdpi,xxhdpi,xxxhdpi}/appicon.png` — PNG legacy (generati).

**Modificati:**
- `wsa.quiz.android/Properties/AndroidManifest.xml` — aggiunge `android:icon="@mipmap/appicon"`.
- `wsa.quiz.android/MainActivity.cs` — `Theme = "@style/Theme.WsaQuiz.Splash"`.
- `wsa.quiz/CLAUDE.md` — documenta comando IconGen + segna M3 icona/splash completo.

---

### Task 1: Arte sorgente SVG

**Files:**
- Create: `branding/icon.svg`
- Create: `branding/icon-foreground.svg`

**Interfaces:**
- Produces: due SVG con viewport `0 0 108 108`. La spunta è la polilinea `M34,56 L48,70 L76,38` (foreground/safe-zone); l'icona piena usa una spunta leggermente più grande `M30,56 L48,74 L80,36`. Stroke bianco, `round` cap/join. Queste coordinate sono riusate nel vector drawable (Task 3) e dal tool (Task 2).

- [ ] **Step 1: Creare `branding/icon.svg`** (icona piena: sfondo verde arrotondato + spunta)

```xml
<svg xmlns="http://www.w3.org/2000/svg" width="108" height="108" viewBox="0 0 108 108">
  <rect x="0" y="0" width="108" height="108" rx="20" ry="20" fill="#1F7A4D"/>
  <path d="M30 56 L48 74 L80 36" fill="none" stroke="#FFFFFF" stroke-width="12"
        stroke-linecap="round" stroke-linejoin="round"/>
</svg>
```

- [ ] **Step 2: Creare `branding/icon-foreground.svg`** (solo spunta, trasparente, safe-zone)

```xml
<svg xmlns="http://www.w3.org/2000/svg" width="108" height="108" viewBox="0 0 108 108">
  <path d="M34 56 L48 70 L76 38" fill="none" stroke="#FFFFFF" stroke-width="9"
        stroke-linecap="round" stroke-linejoin="round"/>
</svg>
```

- [ ] **Step 3: Verificare che siano XML ben formati**

Run (PowerShell):
```powershell
[xml](Get-Content branding/icon.svg) | Out-Null; [xml](Get-Content branding/icon-foreground.svg) | Out-Null; "OK"
```
Expected: stampa `OK` senza eccezioni.

- [ ] **Step 4: Commit**

```bash
git add branding/icon.svg branding/icon-foreground.svg
git commit -m "feat(android): arte sorgente SVG icona (spunta su verde)"
```

---

### Task 2: Tool di rasterizzazione + PNG legacy + ispezione visiva

**Files:**
- Create: `tools/IconGen/IconGen.csproj`
- Create: `tools/IconGen/Program.cs`
- Create (generati): `wsa.quiz.android/Resources/mipmap-{mdpi,hdpi,xhdpi,xxhdpi,xxxhdpi}/appicon.png`

**Interfaces:**
- Consumes: `branding/icon.svg` (Task 1).
- Produces: 5 PNG `appicon.png` (48/72/96/144/192 px) nelle cartelle mipmap + `tools/IconGen/preview-512.png` per ispezione.

- [ ] **Step 1: Creare `tools/IconGen/IconGen.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Aggiungere i pacchetti SkiaSharp + Svg.Skia**

Run:
```bash
cd tools/IconGen && dotnet add package SkiaSharp && dotnet add package Svg.Skia && cd ../..
```
Expected: due `PackageReference` aggiunti, restore OK.
> Contingenza offline: se il restore di `Svg.Skia` fallisce, vedi Step 3-bis (disegno diretto con la sola `SkiaSharp`, stesse coordinate dell'SVG).

- [ ] **Step 3: Creare `tools/IconGen/Program.cs`** (rasterizza l'SVG)

```csharp
using SkiaSharp;
using Svg.Skia;

// Esegui dalla cartella tools/IconGen: percorsi relativi alla radice repo.
string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string svgPath = Path.Combine(repoRoot, "branding", "icon.svg");
string resRoot = Path.Combine(repoRoot, "wsa.quiz.android", "Resources");

var targets = new (string dir, int size)[]
{
    ("mipmap-mdpi", 48), ("mipmap-hdpi", 72), ("mipmap-xhdpi", 96),
    ("mipmap-xxhdpi", 144), ("mipmap-xxxhdpi", 192),
};

using var svg = new SKSvg();
if (svg.Load(svgPath) is null)
    throw new InvalidOperationException($"SVG non caricato: {svgPath}");

void Render(int size, string outPath)
{
    using var bmp = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(SKColors.Transparent);
    float scale = size / 108f;
    canvas.Scale(scale);
    canvas.DrawPicture(svg.Picture);
    canvas.Flush();
    using var img = SKImage.FromBitmap(bmp);
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    File.WriteAllBytes(outPath, data.ToArray());
    Console.WriteLine($"  {Path.GetFileName(Path.GetDirectoryName(outPath))}/appicon.png ({size}px)");
}

Console.WriteLine($"SVG: {svgPath}");
foreach (var (dir, size) in targets)
    Render(size, Path.Combine(resRoot, dir, "appicon.png"));

Render(512, Path.Combine(repoRoot, "tools", "IconGen", "preview-512.png"));
Console.WriteLine("Fatto.");
```

- [ ] **Step 3-bis (solo se Svg.Skia non si restora): variante senza SVG**

Rimuovi `Svg.Skia` e usa la sola `SkiaSharp`, disegnando le stesse coordinate dell'SVG:
```csharp
void Render(int size, string outPath)
{
    using var bmp = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(SKColors.Transparent);
    float s = size / 108f;
    canvas.Scale(s);
    using (var bg = new SKPaint { Color = new SKColor(0x1F, 0x7A, 0x4D), IsAntialias = true })
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(0, 0, 108, 108), 20, 20), bg);
    using var check = new SKPaint
    {
        Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke,
        StrokeWidth = 12, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round
    };
    using var path = new SKPath();
    path.MoveTo(30, 56); path.LineTo(48, 74); path.LineTo(80, 36);
    canvas.DrawPath(path, check);
    canvas.Flush();
    using var img = SKImage.FromBitmap(bmp);
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    File.WriteAllBytes(outPath, data.ToArray());
}
```

- [ ] **Step 4: Generare i PNG**

Run:
```bash
dotnet run --project tools/IconGen
```
Expected: stampa le 5 righe mipmap + `Fatto.`; i file `wsa.quiz.android/Resources/mipmap-*/appicon.png` esistono.

- [ ] **Step 5: Ispezione visiva dell'arte** (verifica estetica reale)

Aprire e guardare `tools/IconGen/preview-512.png`: deve mostrare una spunta bianca, tratto pieno e centrato, su quadrato verde con angoli arrotondati. Se non convince, iterare su `branding/icon.svg` (Task 1) e rifare Step 4.

- [ ] **Step 6: Commit**

```bash
git add tools/IconGen/IconGen.csproj tools/IconGen/Program.cs wsa.quiz.android/Resources/mipmap-mdpi/appicon.png wsa.quiz.android/Resources/mipmap-hdpi/appicon.png wsa.quiz.android/Resources/mipmap-xhdpi/appicon.png wsa.quiz.android/Resources/mipmap-xxhdpi/appicon.png wsa.quiz.android/Resources/mipmap-xxxhdpi/appicon.png
git commit -m "feat(android): tool IconGen + PNG legacy mipmap dell'icona"
```
> Nota: `preview-512.png` NON va committato (artefatto di ispezione); lascialo fuori dal `git add`.

---

### Task 3: Icona Android — adaptive vettoriale + manifest

**Files:**
- Create: `wsa.quiz.android/Resources/values/colors.xml`
- Create: `wsa.quiz.android/Resources/drawable/ic_launcher_foreground.xml`
- Create: `wsa.quiz.android/Resources/mipmap-anydpi-v26/appicon.xml`
- Modify: `wsa.quiz.android/Properties/AndroidManifest.xml`

**Interfaces:**
- Consumes: i PNG `@mipmap/appicon` (Task 2), il colore brand.
- Produces: risorse `@color/icon_background`, `@drawable/ic_launcher_foreground`, `@mipmap/appicon` (adaptive su v26+, raster sotto). Il drawable foreground è riusato dallo splash (Task 4).

- [ ] **Step 1: Creare `wsa.quiz.android/Resources/values/colors.xml`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <color name="icon_background">#1F7A4D</color>
</resources>
```

- [ ] **Step 2: Creare `wsa.quiz.android/Resources/drawable/ic_launcher_foreground.xml`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<vector xmlns:android="http://schemas.android.com/apk/res/android"
    android:width="108dp" android:height="108dp"
    android:viewportWidth="108" android:viewportHeight="108">
    <path
        android:strokeColor="#FFFFFF"
        android:strokeWidth="9"
        android:strokeLineCap="round"
        android:strokeLineJoin="round"
        android:pathData="M34,56 L48,70 L76,38" />
</vector>
```

- [ ] **Step 3: Creare `wsa.quiz.android/Resources/mipmap-anydpi-v26/appicon.xml`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
    <background android:drawable="@color/icon_background" />
    <foreground android:drawable="@drawable/ic_launcher_foreground" />
</adaptive-icon>
```

- [ ] **Step 4: Aggiungere `android:icon` al manifest**

In `wsa.quiz.android/Properties/AndroidManifest.xml`, sostituire la riga `<application ... />` con:
```xml
  <application android:label="WSA Quiz"
               android:icon="@mipmap/appicon"
               android:allowBackup="true" />
```

- [ ] **Step 5: Build Debug per validare le risorse**

Run:
```bash
dotnet build wsa.quiz.android -c Debug
```
Expected: `Build succeeded`. Nessun errore `aapt`/`AAPT`/risorsa mancante (in particolare niente "resource mipmap/appicon not found").

- [ ] **Step 6: Commit**

```bash
git add wsa.quiz.android/Resources/values/colors.xml wsa.quiz.android/Resources/drawable/ic_launcher_foreground.xml wsa.quiz.android/Resources/mipmap-anydpi-v26/appicon.xml wsa.quiz.android/Properties/AndroidManifest.xml
git commit -m "feat(android): adaptive icon vettoriale + android:icon nel manifest"
```

---

### Task 4: Splash screen — temi version-split

**Files:**
- Create: `wsa.quiz.android/Resources/drawable/splash_classic.xml`
- Create: `wsa.quiz.android/Resources/values/styles.xml`
- Create: `wsa.quiz.android/Resources/values-v31/styles.xml`
- Modify: `wsa.quiz.android/MainActivity.cs`

**Interfaces:**
- Consumes: `@color/icon_background` e `@drawable/ic_launcher_foreground` (Task 3).
- Produces: stile `Theme.WsaQuiz.Splash` (definito due volte, base + v31), usato come `Theme` della `MainActivity`.

- [ ] **Step 1: Creare `wsa.quiz.android/Resources/drawable/splash_classic.xml`** (API 24–30)

```xml
<?xml version="1.0" encoding="utf-8"?>
<layer-list xmlns:android="http://schemas.android.com/apk/res/android">
    <item android:drawable="@color/icon_background" />
    <item android:gravity="center"
          android:width="120dp" android:height="120dp"
          android:drawable="@drawable/ic_launcher_foreground" />
</layer-list>
```

- [ ] **Step 2: Creare `wsa.quiz.android/Resources/values/styles.xml`** (tema base)

```xml
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <style name="Theme.WsaQuiz.Splash" parent="@android:style/Theme.Material.Light.NoActionBar">
        <item name="android:windowBackground">@drawable/splash_classic</item>
    </style>
</resources>
```

- [ ] **Step 3: Creare `wsa.quiz.android/Resources/values-v31/styles.xml`** (Splash API, API 31+)

```xml
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <style name="Theme.WsaQuiz.Splash" parent="@android:style/Theme.Material.Light.NoActionBar">
        <item name="android:windowSplashScreenBackground">@color/icon_background</item>
        <item name="android:windowSplashScreenAnimatedIcon">@drawable/ic_launcher_foreground</item>
    </style>
</resources>
```

- [ ] **Step 4: Puntare la `MainActivity` al tema splash**

In `wsa.quiz.android/MainActivity.cs`, cambiare l'attributo `Theme` da
`Theme = "@android:style/Theme.Material.Light.NoActionBar"` a:
```csharp
    Theme = "@style/Theme.WsaQuiz.Splash",
```
(Lasciare invariati `Label`, `MainLauncher`, `ConfigurationChanges`.)

- [ ] **Step 5: Build Debug per validare temi e risorse**

Run:
```bash
dotnet build wsa.quiz.android -c Debug
```
Expected: `Build succeeded`. Nessun errore di stile/tema mancante (`Theme.WsaQuiz.Splash` risolto).

- [ ] **Step 6: Commit**

```bash
git add wsa.quiz.android/Resources/drawable/splash_classic.xml wsa.quiz.android/Resources/values/styles.xml wsa.quiz.android/Resources/values-v31/styles.xml wsa.quiz.android/MainActivity.cs
git commit -m "feat(android): splash minimale (windowBackground + Splash API v31)"
```

---

### Task 5: Verifica APK Release + documentazione

**Files:**
- Modify: `wsa.quiz/CLAUDE.md`

**Interfaces:**
- Consumes: tutte le risorse dei Task 1–4.
- Produces: APK Release firmato con icona e splash impacchettati; `CLAUDE.md` aggiornato.

- [ ] **Step 1: Build Release (APK firmato)**

Run:
```bash
dotnet build wsa.quiz.android -c Release
```
Expected: `Build succeeded`; esiste `wsa.quiz.android/bin/Release/net10.0-android/it.eduits.wsaquiz-Signed.apk`.

- [ ] **Step 2: Verifica strutturale — icona dichiarata**

Run (PowerShell; aapt è nelle build-tools dell'Android SDK):
```powershell
$aapt = Get-ChildItem "$env:LOCALAPPDATA\Android\Sdk\build-tools\*\aapt.exe","$env:ANDROID_HOME\build-tools\*\aapt.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
& $aapt.FullName dump badging wsa.quiz.android/bin/Release/net10.0-android/it.eduits.wsaquiz-Signed.apk | Select-String "application-icon|application:"
```
Expected: righe `application-icon-*: 'res/mipmap-*/appicon.png'` (e/o l'adaptive) e `application: label='WSA Quiz' icon='res/mipmap-anydpi-v26/appicon.xml'` (o un mipmap raster).
> Se `aapt.exe` non si trova, usare lo Step 3 (unzip) come verifica equivalente.

- [ ] **Step 3: Verifica strutturale — risorse impacchettate**

Run (PowerShell):
```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
$apk = "wsa.quiz.android/bin/Release/net10.0-android/it.eduits.wsaquiz-Signed.apk"
$z = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $apk))
$z.Entries | Where-Object { $_.FullName -match 'mipmap|splash|appicon|^res/values' } | Select-Object -ExpandProperty FullName
$z.Dispose()
```
Expected: compaiono voci per `res/mipmap-anydpi-v26/appicon.xml` (compilata in binario, nome può variare), i `res/mipmap-*/appicon.png`, e le risorse di stile/splash. La presenza dei mipmap PNG + adaptive conferma l'impacchettamento.

- [ ] **Step 4: Aggiornare `CLAUDE.md`**

In `wsa.quiz/CLAUDE.md`:
1. Nella sezione **Port Android — M3**, segnare *icona + splash completi (2026-06-23)* con: adaptive icon vettoriale (spunta su verde) + PNG legacy 24–25, splash version-split, `MainActivity` su `Theme.WsaQuiz.Splash`. Annotare che la **resa su device fisico resta non verificata** (no emulatore su questa VM); verificate solo build Release + struttura APK.
2. Aggiungere nella sezione asset/branding il comando di rigenerazione PNG:
   `dotnet run --project tools/IconGen` (legge `branding/icon.svg`).
3. Nello "Stato attuale", aggiornare M3 da "in corso (restano icona/splash)" a "icona/splash completi; resta solo verifica visiva su device".

- [ ] **Step 5: Commit**

```bash
git add wsa.quiz/CLAUDE.md
git commit -m "docs: M3 icona + splash Android completi (stato + comando IconGen)"
```

---

## Note finali

- **Non verificabile in questo ambiente** (serve device reale, come M1/M2): resa a schermo dello splash all'avvio, forma/ritaglio dell'icona adaptive sul launcher, animazione splash su Android 12+. Da provare su un telefono fisico.
- L'unica verifica visiva reale fatta qui è l'ispezione di `preview-512.png` (Task 2, Step 5).
- Se l'icona a `preview-512.png` non convince esteticamente, iterare su `branding/icon.svg` + `branding/icon-foreground.svg` e rieseguire Task 2 Step 4 (e, se cambia la geometria della spunta, riallineare `pathData` nel vector drawable del Task 3).
