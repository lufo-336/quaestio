# M3 — Icona app + Splash screen Android (design)

> Spec del passo finale di codice di M3 (Port Android). Chiude M3 lato risorse
> grafiche; resta fuori scope solo la verifica visiva su device fisico (qui non
> c'è emulatore — vedi M1/M2).

## Obiettivo

Dare all'app Android un'**icona** e uno **splash screen** propri, al posto
dell'icona di default e del tema chiaro di lancio attuali. Coerenti col brand
WsA, leggibili a piccole dimensioni, fatti "nel modo corretto" (adaptive icon
vettoriale + Splash API nativa) perché il progetto ha anche valore didattico.

Stato di partenza:
- `wsa.quiz.android` non ha cartella `Resources/`: usa l'icona di default.
- `MainActivity` ha `Theme = "@android:style/Theme.Material.Light.NoActionBar"`
  → nessuno splash custom.
- Nessun logo/asset grafico esiste nel repo: l'arte va creata da zero.
- `ApplicationId = it.eduits.wsaquiz`, label `"WSA Quiz"`, `minSdk = 24`,
  `targetSdk = 36`.
- Colori brand in `Wsa.Quiz.Core`/`QuizColors.cs`: verde `#1F7A4D` (primario),
  ambra `#B8860B`, rosso `#B85450`.

## Decisioni prese (brainstorming)

| Tema | Decisione |
|---|---|
| Sorgente arte | La genero in **SVG** (nessun logo preesistente). |
| Concept icona | **Spunta ✓ bianca** su sfondo **verde brand `#1F7A4D`**, angoli arrotondati. |
| Splash | **Minimale**: sfondo verde + spunta centrata. Niente wordmark. |
| Approccio delivery | **Adaptive icon vettoriale + Splash API nativa**, con fallback PNG legacy per API 24–25. |
| Tool raster | Console usa-e-getta **SkiaSharp + Svg.Skia**, **fuori** da `Wsa.Quiz.sln`. |
| `roundIcon` | **Escluso** (l'adaptive copre i launcher rotondi; si aggiunge solo se serve). |

La spunta è un glifo volutamente semplice: reso distintivo da tratto deciso,
line-cap arrotondati e centratura ottica. Se a icona vista non convince, si itera
sul solo `icon-foreground.svg` senza toccare il resto della pipeline.

## Componenti

### 1. Artefatti grafici sorgente (fonte di verità, reviewable)

Nuova cartella `wsa.quiz/branding/`:

- **`icon.svg`** — icona completa: quadrato angoli arrotondati verde `#1F7A4D` +
  spunta ✓ bianca centrata. Canvas 108×108 (unità coerenti con il vector
  drawable). È il file che l'utente rivede e che alimenta i PNG legacy.
- **`icon-foreground.svg`** — solo la spunta su trasparente, canvas 108×108 con
  la spunta confinata nel **safe zone centrale ~66%** (≈ 36–72 sui 108), perché
  Android maschera i ~18dp esterni per lato degli adaptive icon.

La geometria della spunta (due segmenti, `M x1,y1 L x2,y2 L x3,y3`, stroke
bianco, `stroke-linecap=round`, `stroke-linejoin=round`) è definita una volta in
`icon-foreground.svg` e **trascritta identica** nel vector drawable Android
(§2). Essendo un'icona statica, il rischio di disallineamento è nullo.

### 2. Icona Android — adaptive vettoriale + fallback raster

In `wsa.quiz.android/Resources/` (il .NET Android SDK assegna automaticamente
build action `AndroidResource` a tutto ciò che sta sotto `Resources/`):

```
Resources/
├── values/
│   └── colors.xml                       <color name="icon_background">#1F7A4D</color>
├── drawable/
│   └── ic_launcher_foreground.xml       vector drawable: spunta bianca, viewport 108
├── mipmap-anydpi-v26/
│   └── appicon.xml                       <adaptive-icon>: background=@color, foreground=@drawable
├── mipmap-mdpi/appicon.png              48×48   (legacy API 24–25)
├── mipmap-hdpi/appicon.png              72×72
├── mipmap-xhdpi/appicon.png             96×96
├── mipmap-xxhdpi/appicon.png           144×144
└── mipmap-xxxhdpi/appicon.png          192×192
```

`appicon.xml` (adaptive):
```xml
<?xml version="1.0" encoding="utf-8"?>
<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
    <background android:drawable="@color/icon_background" />
    <foreground android:drawable="@drawable/ic_launcher_foreground" />
</adaptive-icon>
```

`ic_launcher_foreground.xml`: `<vector>` 108×108dp, viewport 108, con la spunta
disegnata come `<path>` a stroke bianco (`android:strokeColor="#FFFFFF"`,
`android:strokeWidth`, `android:strokeLineCap="round"`,
`android:strokeLineJoin="round"`, nessun `fillColor`).

### 3. Splash minimale — temi version-split (no NuGet, no codice runtime)

Scelta deliberata: **niente** libreria AndroidX `core-splashscreen` e **niente**
override di `OnCreate`. Si usano solo risorse a tema, divise per versione. Meno
rischio runtime — importante visto che qui non si può provare su device.

- `drawable/splash_classic.xml` → `<layer-list>`: primo item fondo verde
  (`@color/icon_background`), secondo item la spunta centrata
  (`@drawable/ic_launcher_foreground`, `android:gravity="center"`, dimensione
  esplicita). Usato come `windowBackground` su API 24–30.
- `values/styles.xml`:
  ```xml
  <style name="Theme.WsaQuiz.Splash" parent="@android:style/Theme.Material.Light.NoActionBar">
      <item name="android:windowBackground">@drawable/splash_classic</item>
  </style>
  ```
- `values-v31/styles.xml` (stesso nome di stile, override su Android 12+):
  ```xml
  <style name="Theme.WsaQuiz.Splash" parent="@android:style/Theme.Material.Light.NoActionBar">
      <item name="android:windowSplashScreenBackground">@color/icon_background</item>
      <item name="android:windowSplashScreenAnimatedIcon">@drawable/ic_launcher_foreground</item>
  </style>
  ```
- `MainActivity.cs`: `[Activity(Theme = "@style/Theme.WsaQuiz.Splash", ...)]`
  al posto dell'attuale `@android:style/Theme.Material.Light.NoActionBar`. Gli
  altri parametri (`Label`, `MainLauncher`, `ConfigurationChanges`) invariati.

Comportamento: su 24–30 il `windowBackground` verde con spunta è visibile prima
del primo frame Avalonia (che poi disegna sopra, opaco). Su 31+ la Splash Screen
API di sistema mostra fondo verde + spunta e si chiude da sola al primo frame.

### 4. Tool di rasterizzazione (usa-e-getta)

`tools/IconGen/` — console **dotnet + SkiaSharp + Svg.Skia** che legge
`branding/icon.svg` e scrive i 5 `appicon.png` nelle rispettive cartelle
`Resources/mipmap-*` con le dimensioni di §2.

- **Fuori da `Wsa.Quiz.sln`**: si lancia a mano, non entra nella build dell'app
  né nel CI implicito. SkiaSharp porta i propri binari nativi su Windows
  (nessun problema di dipendenze native).
- Comando documentato nel `CLAUDE.md` (sezione asset/branding), così rigenerare
  i PNG dopo un'eventuale modifica all'SVG è un comando solo.

### 5. Manifest / label

`Properties/AndroidManifest.xml`:
```xml
<application android:label="WSA Quiz"
             android:icon="@mipmap/appicon"
             android:allowBackup="true" />
```
(Aggiunto solo `android:icon`; label e `allowBackup` invariati. Nessun
`roundIcon`.)

## Verifica (senza device fisico)

Quello che si può fare davvero in questo ambiente:

1. **Visiva sull'arte** — rendere `icon.svg` in PNG e ispezionarlo
   direttamente (lettura immagine) prima di consegnare: l'estetica dell'icona è
   verificata realmente.
2. **Build** — `dotnet build wsa.quiz.android -c Release` verde; APK firmato
   prodotto (`bin/Release/net10.0-android/it.eduits.wsaquiz-Signed.apk`).
3. **Strutturale sull'APK** — `aapt dump badging <apk>` mostra
   `application-icon-*`; unzip dell'APK conferma la presenza di
   `res/mipmap-anydpi-v26/appicon.xml`, dei PNG mipmap e delle risorse splash
   (`res/values*/styles*`, `res/drawable/splash_classic*`).

**Non verificabile qui** (serve device reale, come M1/M2): la resa effettiva a
schermo dello splash all'avvio e dell'icona sul launcher (forma adaptive,
ritaglio, animazione splash su Android 12+).

## Fuori scope

- Wordmark/testo nello splash.
- `roundIcon` dedicato.
- Icona/finestra desktop (questo step è solo Android; il desktop resta com'è).
- Verifica visiva su device fisico (rimandata, ambiente senza emulatore).

## Impatto su desktop / CLI

Nessuno: tutte le modifiche vivono in `wsa.quiz.android/Resources/`, nel suo
manifest e `MainActivity`, più i sorgenti arte in `branding/` e il tool in
`tools/`. `Wsa.Quiz.Desktop`, `Wsa.Quiz.Cli`, `Wsa.Quiz.App` e `Wsa.Quiz.Core`
non vengono toccati.
