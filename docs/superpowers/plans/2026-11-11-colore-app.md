# Colore dell'app scelto dall'utente — piano

> Data: 2026-11-11. Approvato da Luca in chat (variante "A — pallini", mockup
> `/tmp/qmock/colore.html`). Colori scelti da Claude su delega di Luca.

## Obiettivo

In Impostazioni, sotto "Tema", un riquadro **Colore** con 8 pallini. Il colore scelto
sostituisce il blu attuale ovunque l'app usa il colore d'accento. Cambio immediato
(niente riavvio), scelta salvata in `preferenze.json`, valida su desktop, Android,
iOS e web.

Regole generali:
- Leggere `CLAUDE.md` e `docs/claude/trappole.md` (in particolare **5, 6, 7, 12, 20**).
- Code-behind + `INotifyPropertyChanged`, niente MVVM. Commenti in italiano.
- Testi visibili in **entrambe** le risorse `wsa.quiz.core/Risorse/Strings.resx` (it) e
  `Strings.en.resx` (en). Non creare `Strings.Designer.cs` (è generato in `obj/`).
- Toccare SOLO i file elencati. Non modificare `.sln`, `.csproj`, `.props`. Non fare commit.

## La tavolozza

| Id | it | en | Base | Hover | Pressed |
|---|---|---|---|---|---|
| `blu` (default) | Blu | Blue | `#0078D4` | `#2B90DE` | `#005FB0` |
| `blunotte` | Blu notte | Navy | `#23408E` | `#3555A8` | `#1A3170` |
| `indaco` | Indaco | Indigo | `#4F46E5` | `#6A62EE` | `#3F37C4` |
| `viola` | Viola | Purple | `#7E3FB8` | `#9459CC` | `#663096` |
| `magenta` | Magenta | Magenta | `#B4236E` | `#C73E86` | `#931B59` |
| `verdeacqua` | Verde acqua | Teal | `#00838F` | `#1A9AA6` | `#006A74` |
| `cacao` | Cacao | Cocoa | `#7A4E2D` | `#8F6342` | `#613D22` |
| `grafite` | Grafite | Graphite | `#3D4B5C` | `#526174` | `#2E3946` |

Il blu default deve restare **identico** a oggi (`#0078D4` / `#2B90DE` / `#005FB0`).

## Task 1 — Modello e preferenza

- `wsa.quiz.core/Models/PreferenzeUtente.cs`: nuova proprietà
  `public string ColoreAccento { get; set; } = "blu";` con commento `///` come le altre.
  I `preferenze.json` esistenti senza il campo caricano il default.

## Task 2 — Tavolozza e applicazione dei colori

Nuovo file `wsa.quiz.app/Tavolozza.cs` (namespace `Wsa.Quiz.App`), accanto a `QuizColors.cs`:

- `public sealed record ColoreAccento(string Id, string NomeRisorsa, Color Base, Color Hover, Color Pressed)`
  (`NomeRisorsa` = chiave resx del nome, es. `"Colore_Blu"`; aggiungere una proprietà
  `Nome` che legge `Strings.ResourceManager.GetString(NomeRisorsa, Lingue.Cultura)`, o
  equivalente già usato nel progetto per le stringhe localizzate).
- `public static class Tavolozza` con:
  - `public static IReadOnlyList<ColoreAccento> Colori` — gli 8 della tabella, in quell'ordine.
  - `public static ColoreAccento Trova(string? id)` — il colore con quell'id, altrimenti `blu`.
  - `public static void Applica(string? id)`: imposta in `Application.Current!.Resources`:
    - **Brush** (nuovi oggetti `SolidColorBrush` a ogni chiamata):
      `"AccentoBrush"` = Base, `"AccentoHoverBrush"` = Hover, `"AccentoPressedBrush"` = Pressed.
    - **Color** per il tema Fluent (caselle, interruttori, tab, barre di avanzamento):
      `"SystemAccentColor"` = Base;
      `"SystemAccentColorDark1/2/3"` = Base scurito del 10% / 20% / 30%;
      `"SystemAccentColorLight1/2/3"` = Base schiarito del 15% / 30% / 45%
      (mescolando con nero / bianco; helper privato `Mescola(Color a, Color b, double t)`).
      I Light servono al tema scuro, dove Fluent li usa: così anche Grafite resta visibile.
- Commento in testa al file: perché brush propri in hex e non `SystemAccentColorBrush`
  (trappola 20) e perché `DynamicResource` di tipo **Brush** e non Color nei Setter (trappola 7).

## Task 3 — Usare i brush al posto del blu scritto a mano

Sostituire i valori esadecimali del blu con `{DynamicResource ...}` di tipo Brush:

- `wsa.quiz.app/App.axaml`:
  - In `Application.Resources` → `ResourceDictionary`, aggiungere i default (blu) così il
    primo frame è già corretto:
    `<SolidColorBrush x:Key="AccentoBrush" Color="#0078D4"/>`,
    `<SolidColorBrush x:Key="AccentoHoverBrush" Color="#2B90DE"/>`,
    `<SolidColorBrush x:Key="AccentoPressedBrush" Color="#005FB0"/>`.
  - Stili `Button.accent` (normale / `:pointerover` / `:pressed`): `Background` e
    `BorderBrush` → `{DynamicResource AccentoBrush}` / `AccentoHoverBrush` /
    `AccentoPressedBrush`. **Non** toccare `:disabled` né i `danger`. Aggiornare il
    commento esistente (resta valido il motivo: niente `SystemAccentColorBrush`).
- `wsa.quiz.app/MainView.axaml`: `HeaderBorder` `Background="#0078D4"` e lo stile
  `Button.nav.attiva Border.indicatore` → `{DynamicResource AccentoBrush}`.
- `wsa.quiz.app/Views/ConfermaDialog.axaml` e `wsa.quiz.app/Views/PausaDialog.axaml`:
  i loro stili `Button.accent` locali usano `#2D6FB8` / `#3A7FCC` → `AccentoBrush` /
  `AccentoHoverBrush`. Il `ThemeVariantScope RequestedThemeVariant="Light"` dei
  dialoghi resta com'è (è voluto).

## Task 4 — Applicare il colore all'avvio

- `wsa.quiz.app/MainView.axaml.cs`, subito dopo
  `Application.Current!.RequestedThemeVariant = _preferenze.TemaScuro ? ...` (caricamento
  preferenze al boot): `Tavolozza.Applica(_preferenze.ColoreAccento);`

## Task 5 — Riquadro "Colore" in Impostazioni

- `wsa.quiz.app/Views/ImpostazioniView.axaml`: nuovo `Border` identico per stile a quello
  del Tema, inserito **subito dopo** il riquadro Tema. Contenuto: `Grid ColumnDefinitions="*,Auto"`
  con a sinistra titolo `Impostazioni_Colore` + descrizione `Impostazioni_ColoreDescrizione`
  (stesso stile del Tema, descrizione con `TextWrapping="Wrap"`), a destra un
  `ItemsControl` con `ItemsSource="{Binding ColoriDisponibili}"`, `ItemsPanel` =
  `WrapPanel` orizzontale (`ItemSpacing`/`LineSpacing` 8), `Margin="12,0,0,0"`.
  Su touch lo spazio a destra può essere stretto: va bene che i pallini vadano a capo.
- Ogni elemento (`DataTemplate` con `x:DataType` del tipo elemento, trappola 5) è un
  `Button` rotondo senza contenuto testuale:
  - `Width`/`Height` 28 (desktop); `CornerRadius` 14; `Background` = brush del colore;
    `Padding="0"`; `BorderThickness="3"`; `BorderBrush` = `{DynamicResource SystemBaseHighColor}`
    se selezionato, altrimenti `Transparent` (proprietà `BordoSelezione` nell'elemento);
  - `ToolTip.Tip` e `AutomationProperties.Name` = nome localizzato;
  - `Click` → handler che imposta `ColoreSelezionato` sull'Id dell'elemento.
  - Stile locale per `:pointerover`/`:pressed` che **non** cambi lo sfondo (il pallino
    deve restare del suo colore): selettore
    `Button.pallino /template/ ContentPresenter#PART_ContentPresenter` con Background
    legato al Background del bottone (`{TemplateBinding Background}`) negli stati
    pointerover e pressed, `Classes="pallino"` sul bottone.
- Nuovo tipo `wsa.quiz.app/State/ColoreSelezionabile.cs` (`ObservableObject` come gli
  altri in `State/`): `Id`, `Nome`, `IBrush Pennello`, `bool Selezionato` (con `Raise`
  di `BordoSelezione`), `IBrush BordoSelezione`.
- `wsa.quiz.app/Views/ImpostazioniView.axaml.cs`:
  - `public IReadOnlyList<ColoreSelezionabile> ColoriDisponibili` costruita da `Tavolozza.Colori`.
  - In `Inizializza(...)`: segna selezionato quello di `preferenze.ColoreAccento`.
  - Click su un pallino: aggiorna `Selezionato` di tutti, `Tavolozza.Applica(id)`,
    `Salva(p => p.ColoreAccento = id)` (stesso helper `Salva` usato dal Tema).
- Nuove risorse (it / en):
  - `Impostazioni_Colore` — `Colore` / `Colour`
  - `Impostazioni_ColoreDescrizione` — `Barra in alto, bottoni e selezioni.` / `Top bar, buttons and selections.`
  - `Colore_Blu`, `Colore_BluNotte`, `Colore_Indaco`, `Colore_Viola`, `Colore_Magenta`,
    `Colore_VerdeAcqua`, `Colore_Cacao`, `Colore_Grafite` — nomi della tabella.

## Verifica (la fa chi orchestra)

- `dotnet build wsa.quiz.desktop` e `dotnet build wsa.quiz.browser` senza errori.
- Publish Release del browser servito in locale su porta nuova: con default l'app è
  identica a prima; scegliendo ogni colore cambiano subito barra in alto, "Avvia quiz",
  caselle spuntate, interruttore Tema, linea sotto la tab, barra di avanzamento del
  quiz, bottoni dei dialoghi; dopo il reload il colore resta; in tema scuro Grafite
  resta visibile.
- Android (trappola 20): da verificare su device quando possibile.

## Esito (2026-11-11)

- Codice scritto dalla pipeline Antigravity (write → build → review: 0 problemi), build
  verde al primo colpo, nessun file fuori piano.
- Verifica nel browser (Chrome headless su porta nuova, preferenze impostate da una
  pagina di supporto): barra, linea della tab, anello del pallino e controlli Fluent
  cambiano colore; il default resta `#0078D4`.
- Corretti da Claude dopo la verifica:
  - in tema scuro Fluent non usa le varianti Light: Grafite (caselle, interruttore,
    linea della tab) quasi invisibile → `BasePerScuro` (`#8A9BB0`) solo per i controlli
    Fluent in tema scuro, riapplicato a ogni cambio di tema (`Tavolozza.Riapplica`);
  - anello del pallino selezionato nero anche in tema scuro: `TryFindResource` sull'Application
    restituisce il valore del tema chiaro → colore deciso da `ActualThemeVariant`.
- Da verificare a mano: desktop, Android (trappola 20), iOS.
