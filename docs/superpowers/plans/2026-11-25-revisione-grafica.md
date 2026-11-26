# Revisione grafica "lucafoglia" — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Portare Quaestio nello stile del brand "lucafoglia" (B sobria, barra C2, 16 inchiostri, logo a6-2) e correggere le sovrapposizioni su telefono, senza cambiare funzioni né dati.

**Architecture:** Strato di token e stili sopra il tema Fluent (spec §4, approccio 1). I dati della tavolozza vivono in `Wsa.Quiz.Core` (testabili con xUnit); `wsa.quiz.app` li traduce in brush e risorse Avalonia. Le viste cambiano per stili globali; solo Cronologia, Sessioni in pausa e Statistiche passano da card a righe.

**Tech Stack:** .NET 10, Avalonia 12.0.2 + FluentTheme, xUnit, Archivo (OFL), SkiaSharp o Chrome headless per le icone.

**Spec:** `docs/superpowers/specs/2026-11-25-revisione-grafica-design.md` (riferimenti in `docs/superpowers/specs/assets/2026-11-25-revisione-grafica/`)

## Global Constraints

- Code-behind con `INotifyPropertyChanged`, niente MVVM; namespace `Wsa.Quiz.*`, `ApplicationId it.eduits.wsaquiz`, cartella dati `WsaQuiz` invariati.
- Trappole: 5 (`x:DataType`), 6/12/20 (bottoni accent: brush applicativi, mai `SystemAccentColorBrush`), 7 (`DynamicResource` di tipo Brush nei Setter), 10, 11, 26, 27, 29 (APK Release da build pulita). Leggere `docs/claude/trappole.md` prima di toccare quelle zone.
- Neutri chiaro: fondo `#e8e7e3`, rialzato `#f1f0ec`, testo `#141413`, attenuato `#57564f`, riga forte `#141413`, riga debole `#d6d5d0`. Scuro: `#0c0c0c`, `#161616`, `#f4f4f2`, `#a6a6a6`, `#f4f4f2`, `#212120`.
- Angoli vivi (raggio 0), niente `BoxShadow`, righe da 1 px, spazi multipli di 7.
- Carattere UI: Archivo (titoli peso ~800 larghezza ~75–80%); codice: JetBrains Mono NL invariato.
- Solo strumenti e font gratuiti.
- Ogni task chiude con: `dotnet build wsa.quiz.desktop` e `dotnet build wsa.quiz.browser` a 0 errori, `dotnet test wsa.quiz.core.tests` verde, commit e push (memoria "commit e push a ogni step").

## Review Focus

1. **Preferenze vecchie:** `preferenze.json` con `ColoreAccento` = `blunotte`/`magenta`/`verdeacqua`/`grafite`/id sconosciuto → l'app si apre col colore mappato (spec §5.2), senza eccezioni. Test in Task 1 e verifica in Task 2.
2. **Cambio tema chiaro↔scuro a runtime:** intestazione, marchio, testi degli esiti e barra devono aggiornarsi senza riavvio. Verifica a mano in Task 3, 4, 6.
3. **Pulsante accent su Android:** con ogni colore sceglibile il pulsante abilitato resta visibile e il testo si legge (testo nero o bianco dalla tavolozza). Verifica a mano in Task 9.
4. **Larghezza 360 dp:** nessun testo tagliato o sovrapposto (Statistiche, Sessioni in pausa con titolo lungo "Quizzone — Completo randomizzato", Home). Task 5.
5. **Contrasto del testo colorato:** testo sopra ogni inchiostro ≥ 4,5:1; testo esito su fondo chiaro ≥ 4,5:1. Test in Task 1.

---

> **Ripresa dal PC (nota di Claude, 2026-11-26).** Dal Mac è fatto tutto quello che si poteva: Task 1–8 su `main`
> (ultimo `ff5d303`), più la variante A del colore utente (riga sotto l'intestazione + trattino sui titoli dei
> riquadri, `e9887c0`, spec aggiornata). Restano, in quest'ordine, dal PC Windows (adb + workload Android, chiave
> di firma della v2.1.0, trappola 24):
> 1. **Task 0** — misure sul S21 con la 2.2.0 ancora installata, PRIMA del nuovo APK.
> 2. **Prove a vista sul telefono** (mai fatte): barra C2, Statistiche/Cronologia/Sospesi a righe, colori degli
>    esiti, colore utente su intestazione e titoli; Task 8 Step 4 (icona nel launcher).
> 3. **Task 9** — APK Release da build pulita (trappola 29), misure dopo, giro dei colori sul pulsante Avvia,
>    `CLAUDE.md` (Archivo al posto di Inter), `architettura-gui.md`, `trappole.md`.
> 4. Poi la GitHub Release (versione da decidere, la 2.2.0 non è mai stata pubblicata).

### Task 0: Misura di partenza sul S21

- [x] Fatto 2026-11-26 (Claude, da Fedora con adb): numeri in fondo, sezione "Misure".

Serve il "prima" per l'anti-obiettivo sulle prestazioni (spec §2, §6). Richiede il telefono collegato via adb e l'APK 2.2.0 installato.

- [ ] **Step 1:** trovare l'activity: `adb shell cmd package resolve-activity --brief it.eduits.wsaquiz` (ultima riga).
- [ ] **Step 2:** avvio a freddo, 5 volte: `adb shell am force-stop it.eduits.wsaquiz && adb shell am start -W -n <activity>`; annotare `TotalTime` e la mediana.
- [ ] **Step 3:** scorrimento: `adb shell dumpsys gfxinfo it.eduits.wsaquiz reset`, scorrere a mano Cronologia e Statistiche per ~10 s, poi `adb shell dumpsys gfxinfo it.eduits.wsaquiz | grep -E "Total frames|Janky"`.
- [ ] **Step 4:** scrivere i numeri in fondo a questo piano, sezione "Misure", e fare commit.

### Task 1: Inchiostri nel core (dati, migrazione id, contrasto)

- [x] Fatto 2026-11-25 (agy): file `wsa.quiz.core/Services/Inchiostri.cs`, `wsa.quiz.core.tests/InchiostriTests.cs`, `wsa.quiz.core/Risorse/Strings.resx`, `wsa.quiz.core/Risorse/Strings.en.resx` — verifica: non eseguita (sessione headless senza shell; compilazione/test demandati al processo esterno)

**Files:**
- Create: `wsa.quiz.core/Services/Inchiostri.cs`
- Modify: `wsa.quiz.core/Risorse/Strings.resx`, `wsa.quiz.core/Risorse/Strings.en.resx` (nomi colore)
- Test: `wsa.quiz.core.tests/InchiostriTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record Inchiostro(string Id, string NomeRisorsa, string Hex, string? HexChiaro, bool TestoNero, bool Sceglibile, string? HexTestoSuChiaro)` — `HexChiaro` solo per grigio (`#c4c3bd`); `HexTestoSuChiaro` solo per i tre riservati (variante per il testo su fondo chiaro).
  - `public static class Inchiostri { IReadOnlyList<Inchiostro> Tutti; Inchiostro Trova(string id); string MigraIdColore(string? id); double Contrasto(string hexA, string hexB); const string Predefinito = "blu"; }`
  - Id (minuscoli, senza spazi): `rosso, terracotta, cacao, ocra, oliva, prato, verdeacqua, petrolio, azzurro, blu, indaco, viola, prugna, rosaantico, ardesia, grigio`, in quest'ordine, con hex e testo dalla tabella spec §5.2.
  - Chiavi risorsa `Colore_<Nome>` (es. `Colore_RosaAntico`); en: Red, Terracotta, Cocoa, Ochre, Olive, Meadow, Teal, Petrol, Sky blue, Blue, Indigo, Violet, Plum, Old rose, Slate, Grey.

- [x] **Step 1: Write the failing tests** in `InchiostriTests.cs`:
  - `Tutti_Sedici_InOrdine`: `Inchiostri.Tutti.Select(i => i.Id)` uguale alla lista sopra.
  - `Sceglibili_SonoDodici`: gli id con `Sceglibile` sono esattamente `terracotta, cacao, oliva, prato, petrolio, azzurro, blu, indaco, viola, prugna, rosaantico, ardesia` (12); `rosso`, `ocra`, `verdeacqua`, `grigio` non sono sceglibili.
  - `Migra_VecchiId` (Theory): `blu→blu`, `blunotte→indaco`, `indaco→indaco`, `viola→viola`, `magenta→prugna`, `verdeacqua→petrolio`, `cacao→cacao`, `grafite→ardesia`, `null→blu`, `""→blu`, `"xyz"→blu`, `"BLUNOTTE"→indaco`; ogni id già nuovo e sceglibile resta sé stesso.
  - `TestoSopra_Contrasto`: per ogni inchiostro, `Contrasto(Hex, TestoNero ? "#000000" : "#ffffff") >= 4.5`.
  - `TestoEsito_SuChiaro_Contrasto`: per `rosso`, `ocra`, `verdeacqua`, `Contrasto(HexTestoSuChiaro, "#e8e7e3") >= 4.5`.
  - `Contrasto_Riferimento`: `Contrasto("#000000", "#ffffff")` ≈ 21 (±0.01).
- [ ] **Step 2:** `dotnet test wsa.quiz.core.tests --filter InchiostriTests` → FAIL (tipo inesistente).
- [x] **Step 3:** implementare `Inchiostri.cs` (contrasto WCAG: luminanza relativa sRGB, `(L1+0.05)/(L2+0.05)`). Per `HexTestoSuChiaro` partire dalle varianti di lucablog (ocra `#9b7300`, verde acqua `#33867c`, rosso `#d6414b`) e scurirle mantenendo la tinta finché il test passa (fondo `#e8e7e3` → luminanza ≤ ~0,138). Aggiungere le stringhe `Colore_*` mancanti, togliere quelle non più usate (`Colore_BluNotte`, `Colore_Magenta`, `Colore_Grafite`).
- [ ] **Step 4:** `dotnet test wsa.quiz.core.tests` → tutti PASS.
- [ ] **Step 5:** commit `feat(core): tavolozza dei 16 inchiostri, migrazione colori e contrasto` + push.

### Task 2: Colore utente sugli inchiostri

- [x] Fatto 2026-11-25 (agy): file `wsa.quiz.app/Tavolozza.cs`, `wsa.quiz.app/App.axaml`, `wsa.quiz.app/Views/ConfermaDialog.axaml`, `wsa.quiz.app/Views/PausaDialog.axaml`, `wsa.quiz.app/Views/StatisticheView.axaml`, `wsa.quiz.app/Views/ImpostazioniView.axaml.cs`, `wsa.quiz.core/Services/StorageService.cs`, `wsa.quiz.core/Risorse/Strings.resx`, `wsa.quiz.core/Risorse/Strings.en.resx`, `wsa.quiz.core.tests/PreferenzeMigrazioneTests.cs` — verifica: non eseguita (sessione headless senza shell; compilazione, test e prova a mano demandati a Claude)

**Files:**
- Modify: `wsa.quiz.app/Tavolozza.cs`, `wsa.quiz.app/App.axaml` (stili `Button.accent`), `wsa.quiz.app/Views/ConfermaDialog.axaml`, `wsa.quiz.app/Views/PausaDialog.axaml`, `wsa.quiz.app/Views/StatisticheView.axaml` (Setter con `AccentoBrush`), punto di caricamento delle preferenze (dove si legge `PreferenzeUtente.ColoreAccento`)

**Interfaces:**
- Consumes: `Inchiostri.Tutti`, `Inchiostri.MigraIdColore`, `Inchiostro.TestoNero`.
- Produces:
  - `Tavolozza.Colori` = solo inchiostri sceglibili, nell'ordine di `Inchiostri.Tutti`; `ColoreAccento(string Id, string NomeRisorsa, Color Base, Color Hover, Color Pressed, Color Testo)` (cade `BasePerScuro`).
  - Risorsa nuova `AccentoTestoBrush` (Brush) impostata da `Tavolozza.Applica`, accanto a `AccentoBrush`/`AccentoHoverBrush`/`AccentoPressedBrush`.

- [x] **Step 1:** `Tavolozza` costruisce `Colori` da `Inchiostri` (hover/pressed con `Mescola` ±10%/20% come oggi); `Trova`/`Applica` passano l'id da `Inchiostri.MigraIdColore`. `Applica` imposta anche `AccentoTestoBrush`.
- [x] **Step 2:** tutti i `Foreground="White"`/`Value="White"` degli stili accent (App.axaml, ConfermaDialog, PausaDialog, StatisticheView) diventano `{DynamicResource AccentoTestoBrush}`; valore iniziale in `App.axaml`: blu `#3a6cb8` con testo `#FFFFFF`.
- [x] **Step 3:** al caricamento delle preferenze, se `MigraIdColore(id) != id`, salvare l'id migrato (una volta).
- [ ] **Step 4:** prova a mano desktop: con `preferenze.json` a `"ColoreAccento": "magenta"` l'app si apre in prugna e il file viene riscritto con `prugna`; in Impostazioni ci sono 12 colori con nomi it/en; scegliendo un inchiostro chiaro (azzurro, oliva) il testo dei bottoni è nero, con uno scuro (indaco, cacao) è bianco.
- [ ] **Step 5:** build desktop+browser, test, commit `feat(app): colore utente scelto fra gli inchiostri` + push.

### Task 3: Fondamenta — Archivo, neutri, forme, intestazione col marchio

- [x] Fatto 2026-11-25 (agy, Step 2–5; Step 1 già pronto da Claude): file `wsa.quiz.app/Themes/Tokens.axaml`, `wsa.quiz.app/Controls/MarchioQuaestio.axaml`, `wsa.quiz.app/Controls/MarchioQuaestio.axaml.cs`, `wsa.quiz.app/App.axaml`, `wsa.quiz.app/MainView.axaml`, `wsa.quiz.app/Wsa.Quiz.App.csproj`, `wsa.quiz.desktop/Program.cs`, `wsa.quiz.android/MainApplication.cs`, `wsa.quiz.ios/AppDelegate.cs`, `wsa.quiz.browser/Program.cs` — verifica: non eseguita (sessione headless senza shell; Step 6–7 prova a mano, build, test e commit demandati a Claude)

**Files:**
- Create: `wsa.quiz.app/Themes/Tokens.axaml`, `wsa.quiz.app/Controls/MarchioQuaestio.axaml` (+ `.axaml.cs`), file Archivo in `wsa.quiz.app/Assets/Fonts/` (+ `OFL-Archivo.txt`)
- Modify: `wsa.quiz.app/App.axaml` (merge Tokens, stili globali), `wsa.quiz.app/MainView.axaml` (intestazione, fondo), `wsa.quiz.desktop/Program.cs`, `wsa.quiz.android/MainApplication.cs`, `wsa.quiz.ios/AppDelegate.cs`, `wsa.quiz.browser/Program.cs` (font di default)

**Interfaces:**
- Produces (chiavi risorsa, Brush in `ThemeDictionaries` Light/Dark con i valori dei Global Constraints): `Sfondo`, `SfondoRialzato`, `TestoPrincipale`, `TestoAttenuato`, `RigaForte`, `RigaDebole`; `FontFamily` `FontTitoli` (Archivo stretto, pesante). Controllo `MarchioQuaestio` (nessuna proprietà; altezza 28 nell'intestazione).

- [x] **Step 1:** procurare Archivo (OFL) in istanze statiche: almeno Regular, SemiBold, Bold a larghezza piena e una ExtraBold stretta (~75–80%) per i titoli; se si parte dal variabile, generarle con `fontTools.varLib.instancer` (gratuito). Includerle come `AvaloniaResource`.
- [x] **Step 2:** nei quattro head sostituire `.WithInterFont()` con `.With(new FontManagerOptions { DefaultFamilyName = "avares://Wsa.Quiz.App/Assets/Fonts#Archivo" })` (nome famiglia esatto da verificare sul file).
- [x] **Step 3:** `Tokens.axaml` con le chiavi sopra; in `App.axaml` merge e override delle risorse Fluent `ControlCornerRadius` e `OverlayCornerRadius` a `0`; fondo della radice di `MainView` = `Sfondo`, testo = `TestoPrincipale`.
- [x] **Step 4:** `MarchioQuaestio`: `Viewbox` con un `Canvas` 184×44 e un `Path` per ogni `path`/`rect` di `assets/.../logo-a6-2-marchio-scuro.svg` (le corsie colorate tengono i loro hex; l'anima neutra usa `{DynamicResource TestoPrincipale}`, così cambia col tema).
- [x] **Step 5:** intestazione di `MainView`: fondo `Sfondo`, `BorderThickness="0,0,0,1"` `BorderBrush={DynamicResource RigaForte}`, a sinistra `MarchioQuaestio` al posto del `TextBlock "Quaestio"`, a destra `StatoHeaderText` in `TestoAttenuato` senza `Opacity`.
- [ ] **Step 6:** prova a mano: desktop chiaro e scuro (cambio tema da Impostazioni senza riavvio → marchio e fondi seguono); WebAssembly (`dotnet run --project wsa.quiz.browser`) mostra Archivo (trappola 27).
- [ ] **Step 7:** build, test, commit `feat(app): neutri, Archivo, angoli vivi e marchio a6-2 nell'intestazione` + push.

### Task 4: Colori degli esiti

- [x] Fatto 2026-11-25 (agy, Step 1–3): file `wsa.quiz.app/QuizColors.cs`, `wsa.quiz.app/Themes/Tokens.axaml`, `wsa.quiz.app/App.axaml`, `wsa.quiz.app/State/RisultatoCronologiaItem.cs`, `wsa.quiz.app/State/DettaglioRispostaItem.cs`, `wsa.quiz.app/State/SessioneQuiz.cs`, `wsa.quiz.app/State/MateriaStatItem.cs`, `wsa.quiz.app/State/CategoriaStatItem.cs`, `wsa.quiz.app/Views/RiepilogoView.axaml(.cs)`, `wsa.quiz.app/Views/CronologiaDettaglioView.axaml(.cs)`, `wsa.quiz.app/Views/CronologiaView.axaml(.cs)`, `wsa.quiz.app/Views/StatisticheView.axaml(.cs)`, `wsa.quiz.app/Views/QuizView.axaml(.cs)`, `wsa.quiz.app/Views/SospesiView.axaml`, `wsa.quiz.app/Views/HomeView.axaml`, `wsa.quiz.app/Views/GestioneContenutiView.axaml`, `wsa.quiz.app/Views/ImpostazioniView.axaml`, `wsa.quiz.app/MainView.axaml` — verifica: non eseguita (sessione headless senza shell; Step 4–5 prova a mano, build, test e commit demandati a Claude)

**Files:**
- Modify: `wsa.quiz.app/QuizColors.cs`, `wsa.quiz.app/Controls/BarraLivelli.cs` e i chiamanti di `QuizColors` che lo usano come `Foreground` (`State/RisultatoCronologiaItem.cs`, `State/DettaglioRispostaItem.cs`, `State/SessioneQuiz.cs`, `State/MateriaStatItem.cs`, `State/CategoriaStatItem.cs`, `State/LivelloSelezionabile.cs`, `Views/RiepilogoView.axaml.cs`, `Views/CronologiaDettaglioView.axaml.cs`, `Views/StatisticheView.axaml.cs`)

**Interfaces:**
- Consumes: `Inchiostri.Trova("verdeacqua"|"ocra"|"rosso")` (`Hex`, `HexTestoSuChiaro`).
- Produces: in `QuizColors` le costanti `Verde`/`Ambra`/`Rosso` diventano gli hex degli inchiostri; brush di riempimento invariati di nome (`VerdeBrush`, `AmbraBrush`, `RossoBrush`, `HeatmapBordo`, `HeatmapSfondo`); nuovi `VerdeTestoBrush`, `AmbraTestoBrush`, `RossoTestoBrush` che restituiscono `HexTestoSuChiaro` nel tema chiaro e `Hex` nel tema scuro.

- [x] **Step 1:** aggiornare costanti e sfondi tenui (heatmap/barre) derivandoli dai tre inchiostri; `Danger*` = rosso inchiostro e sue mescole.
- [x] **Step 2:** dove un brush esito è usato come **colore del testo**, passare alla variante `*TestoBrush`; dove è un **riempimento** con testo sopra, il testo è nero.
- [x] **Step 3:** assicurare che le viste che mostrano testi esito si aggiornino al cambio tema (es. ricalcolo sull'evento `ActualThemeVariantChanged` dove i valori sono calcolati in code-behind).
- [ ] **Step 4:** prova a mano: Riepilogo, Cronologia, CronologiaDettaglio, Statistiche, cronometro del Quiz in chiaro e scuro; cambio tema senza riavvio.
- [ ] **Step 5:** build, test, commit `feat(app): esiti con gli inchiostri e varianti per il testo` + push.

### Task 5: Passata sulle schermate e correzioni

**Files:**
- Modify: `wsa.quiz.app/Views/*.axaml` (tutte), in particolare `StatisticheView.axaml`, `SospesiView.axaml`, `CronologiaView.axaml`; `wsa.quiz.core/Risorse/Strings*.resx` (plurale sessioni)

**Interfaces:**
- Consumes: chiavi di Task 3 (`Sfondo`, `SfondoRialzato`, `RigaForte`, `RigaDebole`, `TestoAttenuato`).

- [x] **Step 1:** in tutte le viste: togliere `CornerRadius` e `BoxShadow` dalle card (raggio 0), bordo 1 px `RigaDebole`, fondo `SfondoRialzato` o trasparente; colori esadecimali sparsi sostituiti dalle chiavi dei token quando sono neutri.
- [x] **Step 2:** Cronologia, Sessioni in pausa, Statistiche: niente card; ogni voce è una riga con `BorderThickness="0,1,0,0"` `RigaDebole` (l'ultima chiude con `0,1,0,1`), stessi margini orizzontali delle altre schermate (spec §5.6).
- [x] **Step 3:** Statistiche: "Aggiorna" diventa un link testuale nell'accento sulla riga del titolo, senza scavalcarlo; filtri (Tutto/Ultime + campo numero + "partite") su riga propria; campo numerico largo abbastanza per tre cifre; contenuto di "Ripassa (n)" centrato verticalmente.
- [x] **Step 4:** Sessioni in pausa: in `TouchMode` "Riprendi" ed "Elimina" vanno **sotto** testo e progresso, mai sopra; titolo con `TextWrapping` o `TextTrimming` senza coprire nulla. Contatore: stringhe distinte per 1 e per n (es. `Sospesi_ContaUno` "1 sessione in pausa." / `Sospesi_ContaMolte` "{0} sessioni in pausa."; en "1 paused session." / "{0} paused sessions.").
- [ ] **Step 5:** controllo a 360 di larghezza: finestra desktop stretta a 360 e browser in modalità telefono (`UsaTouchInSingleView`), tema chiaro e scuro, tutte le schermate; elencare in fondo al piano ("Difetti trovati") ogni altra sovrapposizione e correggerla.
- [ ] **Step 6:** build, test, commit `fix(app): schermate in stile sobrio, elenchi a righe e sovrapposizioni su telefono` + push.

### Task 6: Barra di navigazione C2 (touch)

**Files:**
- Modify: `wsa.quiz.app/MainView.axaml` (stili `Button.nav`, `BottomNav`), `wsa.quiz.app/MainView.axaml.cs` (stato attivo, riserva spazio)

**Interfaces:**
- Consumes: `Inchiostri.Trova(id)` per `blu, viola, ocra, terracotta, verdeacqua, grigio` (ordine delle sei sezioni); `Inchiostro.TestoNero`.

- [x] **Step 1:** `BottomNav` a tutta larghezza, a filo del bordo inferiore sopra l'inset Android (padding da `ApplicaSafeArea` come oggi), altezza 56, nessun raggio, nessuna ombra; via `NavSfondo`/`NavBordo`/`NavOmbra`.
- [x] **Step 2:** ogni `Button.nav` ha fondo pieno del colore della sua sezione; inattivo = solo icona; attivo = colonna larga ~2,4× con icona + nome della sezione (stringhe `Shell_Tab*`); icona e testo nel colore "testo sopra" dell'inchiostro. Le icone `Path` restano quelle di oggi.
- [x] **Step 3:** `SzRiservaNav` aggiornato alla nuova altezza, così l'ultimo contenuto di ogni pagina scorre sopra la barra.
- [ ] **Step 4:** prova nel browser in modalità telefono e su emulatore/telefono: cambio sezione col tocco e con lo swipe aggiorna la colonna larga.
- [ ] **Step 5:** build, test, commit `feat(app): barra di navigazione a campi di colore (C2)` + push.

### Task 7: Tab desktop/web — mockup, ok, implementazione

- [x] Fatto 2026-11-25 (Claude, Step 3: build+test+prova nel browser chiaro/scuro, commit `d24d1df`). Step 1 fatto 2026-11-25 (Claude): `docs/superpowers/specs/assets/2026-11-25-revisione-grafica/mockup-tab-desktop.html` (+ `.png`), varianti A fascia sotto / B solo l'attiva / C fascia sopra — scelto A da Luca. Step 2 fatto 2026-11-25 (agy): file `wsa.quiz.app/MainView.axaml`, `wsa.quiz.app/MainView.axaml.cs` — verifica: non eseguita (sessione headless senza shell).

- [ ] **Step 1:** mockup HTML con 2–3 varianti affiancate delle tab in alto (base: spec §5.4, fascia 3–4 px del colore di sezione, attiva piena), tema chiaro e scuro; mostrarle a Luca. **Fermarsi fino al suo ok.**
- [x] **Step 2:** implementare la variante scelta sullo stile `TabItem` di `Tabs` in `MainView.axaml` (colori da `Inchiostri` come in Task 6). Se nessuna è scelta: solo stili globali, nessun colore di sezione.
- [x] **Step 3:** prova desktop chiaro/scuro e browser su PC; build, test, commit `feat(app): tab desktop coi colori di sezione` + push.

### Task 8: Icone dell'app a6-2

- [x] Fatto 2026-11-25 (Claude): file `branding/*.svg`, `wsa.quiz.android/Resources/` (vector drawable, fondo `#0c0c0c`, 10 PNG), AppIcon iOS, `quaestio.ico`, `quaestio-icon.png`, favicon web — PNG da Chrome headless + Pillow; tonde con la Q nella zona sicura — verifica: build ok; Step 4 (icona a vista su desktop, launcher, scheda) da fare

**Files:**
- Modify: `branding/icon.svg`, `branding/icon-foreground.svg`, `wsa.quiz.android/Resources/drawable/ic_launcher_foreground.xml`, `wsa.quiz.android/Resources/values/ic_launcher_background.xml`, `wsa.quiz.android/Resources/mipmap-*/ic_launcher*.png` (10 file), `wsa.quiz.ios/Assets.xcassets/AppIcon.appiconset/*`, `wsa.quiz.desktop/quaestio.ico`, `wsa.quiz.app/Assets/Branding/quaestio-icon.png`
- Create: favicon/icone per `wsa.quiz.browser/wwwroot` (+ `<link rel="icon">` in `index.html`)

- [ ] **Step 1:** `branding/icon.svg` = `assets/.../logo-a6-2-icona-scuro.svg`; `icon-foreground.svg` = solo la Q (senza fondo) ridimensionata nella zona sicura dell'icona adattiva (66/108 del lato); colore di fondo adattivo `#0c0c0c`.
- [ ] **Step 2:** `ic_launcher_foreground.xml` come vector drawable con gli stessi tracciati (gli archi `A` sono ammessi in `pathData`).
- [ ] **Step 3:** rigenerare i PNG (mipmap legacy e round di ogni densità, iOS AppIcon nelle misure di `Contents.json`, `.ico` desktop 16–256, `quaestio-icon.png`, favicon) con SkiaSharp o Chrome headless; nessuno strumento a pagamento.
- [ ] **Step 4:** verifica: icona visibile nella finestra desktop, nel launcher Android (emulatore o S21) e nella scheda del browser.
- [ ] **Step 5:** commit `feat: icona dell'app a6-2 su tutte le piattaforme` + push.

### Task 9: Verifica finale e documentazione

- [ ] **Step 1:** APK Release da build pulita (pulire `bin`/`obj`, trappola 29), installarlo sul S21; ripetere le misure di Task 0 e scriverle in "Misure". Nessun peggioramento percepibile (avvio mediano entro +10%, frame janky non in aumento).
- [ ] **Step 2:** con Luca, giro a mano sul telefono: ogni colore sceglibile sul pulsante "Avvia" (Review Focus 3), tema chiaro/scuro, tutte le sezioni.
- [ ] **Step 3:** aggiornare `CLAUDE.md` (Stack: Archivo al posto di Inter; rimando alla spec) e `docs/claude/architettura-gui.md` (token, inchiostri, barra C2, colori degli esiti); nuove trappole incontrate in `docs/claude/trappole.md`.
- [ ] **Step 4:** commit `docs: revisione grafica lucafoglia` + push.

---

## Misure

**Prima (Task 0, 2026-11-26, S21 `R3CRC0LH0RH`, 2.2.0, da Fedora):**
- Avvio a freddo (`am start -W`, 5 prove): 2665, 2272, 2298, 2302, 2289 ms. **Mediana 2298 ms**; soglia Task 9: ≤ 2528 ms (+10%).
- Scorrimento: `dumpsys gfxinfo` dà 0 frame (Avalonia disegna su una `SurfaceView`, non su HWUI). Misurato invece con `dumpsys SurfaceFlinger --latency '<layer SurfaceView BLAST>'` durante 16 swipe su Home: **126 frame, 0 intervalli > 25 ms**. Cronologia/Statistiche non scorrevoli (cronologia vuota sul telefono): misurata solo la Home.

**Dopo (Task 9):** (da compilare)

## Difetti trovati

(da compilare in Task 5)
