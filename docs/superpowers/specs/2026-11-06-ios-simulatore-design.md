# iOS — primo passo: Quaestio nel simulatore del Mac (design)

## Intento

Avere Quaestio funzionante su iOS **nel simulatore di Xcode sul MacBook Pro
M1 di Luca**, con la stessa UI touch di Android. Luca non ha un iPhone e non
vuole chiederlo in prestito; ha un iPad (non M) per un passo successivo.
Budget: nessuna spesa (niente Apple Developer Program).

## Obiettivi

- Nuovo head `wsa.quiz.ios` (`net10.0-ios`) sopra la libreria `Wsa.Quiz.App`,
  come `wsa.quiz.android`.
- L'app parte nel simulatore iPhone e iPad, carica le 11 materie dalle risorse
  incorporate, si gioca una partita, cronologia e preferenze restano dopo il
  riavvio dell'app.
- Header e bottom-nav non finiscono sotto notch / status bar / home indicator.

## Non obiettivi (passi successivi)

- iPad vero (Apple ID gratuito, profilo di 7 giorni, max 3 app per device).
- Icona dell'app e launch screen personalizzati (si usa il default).
- App Store / TestFlight (richiederebbero i 99 $/anno).
- Mac Catalyst.

## Vincoli verificati (2026-11-06)

- Il build iOS richiede **macOS + Xcode**. Ogni versione del workload `ios` di
  .NET 10 vuole una versione precisa di Xcode (errore esplicito se non
  coincide). Le ultime (Xcode 26.5/26.6, workload 10.0.300+/10.0.302) chiedono
  **macOS 26.2+ (Tahoe)**; Xcode 26.0-26.1 chiedono macOS 15.6+. L'M1 supporta
  Tahoe. ([dotnet/macios releases](https://github.com/dotnet/macios/releases))
- Template ufficiale Avalonia 12 (`Avalonia.Templates`, `xplat/AvaloniaTest.iOS`):
  `AppDelegate : AvaloniaAppDelegate<App>` con `[Register("AppDelegate")]` e
  `CustomizeAppBuilder(...).WithInterFont()`, entry point
  `UIApplication.Main(args, null, typeof(AppDelegate))`, nessun SceneDelegate,
  `SupportedOSPlatformVersion` 13.0. Diversamente da Android (trappola 14),
  l'AppDelegate iOS **è** generico.
- La libreria passa da sola in modalità touch (`ISingleViewApplicationLifetime`
  → `AppEnv.TouchMode`) e ripiega sulle risorse incorporate se non trova
  `materie.json` accanto all'eseguibile: su iOS non serve altro.
- Dati utente: `CartellaUtenteDefault()` (`SpecialFolder.ApplicationData`)
  punta dentro la sandbox dell'app, scrivibile.

## Decisioni

- **Versione Avalonia 12.0.2** come il resto del repo (il template è già a
  12.1.x: l'aggiornamento è un lavoro a parte).
- **Non nella solution**: con l'head iOS dentro, `dotnet build Wsa.Quiz.sln` su
  Windows fallirebbe. Si builda con `dotnet build wsa.quiz.ios`. *(Scelta di
  Opus.)*
- iOS minimo **15.0** (non 13.0): permette la chiave `UILaunchScreen` di
  Info.plist al posto di uno storyboard. Senza launch screen iOS fa girare
  l'app in modalità compatibilità, rimpicciolita. *(Scelta di Opus.)*
- `ApplicationId` `it.eduits.wsaquiz`, nome mostrato «Quaestio», come Android.
- **Safe area**: su Android le insets arrivano dalla `MainActivity` in
  `AppEnv.SetSystemBarInsetsPx`. Su iOS la fonte è l'`InsetsManager` del
  `TopLevel` di Avalonia (`SafeAreaPadding`, in unità logiche), convertito in
  pixel e passato allo stesso `AppEnv.SetSystemBarInsetsPx`. Così
  `MainView.ApplicaSafeArea` resta l'unico punto che applica i padding. È
  un'ipotesi: se nel simulatore la UI è già al posto giusto, o sbagliata, la
  prova manuale decide. *(Scelta di Opus.)*
- `AppEnv.ScalaTouch` (0,85) resta quello di Android: si tara dopo averla vista.
- Rotazioni: **iPhone solo verticale** (la UI touch è pensata in colonna),
  **iPad tutte e quattro**. *(Scelta di Opus.)*

## Chi fa cosa

- Codice: piano descrittivo → Haiku su Windows (build di app e desktop) →
  Sonnet in revisione. Verificato poi: l'head iOS **compila anche su Windows**
  (solo C#, `iossimulator-x64`); l'app vera si produce solo sul Mac.
- Build e prova nel simulatore: **Luca sul Mac**, con i comandi esatti forniti
  da Opus.
