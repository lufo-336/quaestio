# Preferenze utente persistite (Step 13 + Step 10 dark mode) — Design

Data: 2026-07-14
Branch: `feature/android`
Contesto: chiude **Step 13** della roadmap (`settings.json`: ultime opzioni
quiz, ultima tab aperta, scelta dark mode) e in corso d'opera anche **Step 10**
(dark mode), che Step 13 presupponeva già completato ("Persistenza nelle
preferenze utente (step 13)" nella nota di Step 10) ma non lo era: oggi l'app
non ha alcun controllo di tema, solo `RequestedThemeVariant="Default"`
(segue il sistema) in `App.axaml`.

## Obiettivo

Al riavvio, l'app ricorda:
1. l'ultima **sezione** aperta (Home/Cronologia/Sospesi/Statistiche/Gestione
   contenuti, ora + Impostazioni);
2. le ultime **opzioni quiz** (randomizza, rotazione, cronometro, limite
   domande, modalità a tempo) — **non** materie/categorie selezionate, quelle
   restano una scelta esplicita ad ogni sessione come oggi;
3. la scelta esplicita di **tema Chiaro/Scuro** (non "segui sistema": un
   toggle binario in una nuova sezione "Impostazioni").

Poiché non esisteva alcun controllo tema, la scelta Scuro dev'essere
**effettivamente leggibile** appena introdotta: questo design include anche
la messa in sicurezza mirata dei colori hardcoded noti (Step 10), non solo la
persistenza.

## Decisioni (da brainstorming)

- Tema: **Chiaro/Scuro con toggle**, niente opzione "Sistema" — scelta
  esplicita e persistita.
- Il toggle vive in una **nuova sezione "Impostazioni"** (6ª voce di
  navigazione), non in un bottone nell'header/footer — più scalabile per
  preferenze future.
- Le opzioni quiz salvate sono solo i **toggle/numeri** di `HomeView`
  (randomizza, rotazione, cronometro, limite, modalità a tempo); materie e
  categorie selezionate **non** vengono ricordate.
- Dark mode: **fix mirato**, non redesign completo. Si corregge il problema
  certo già documentato (`QuizColors.TempoNeutro`) e si fa un audit puntuale
  degli altri file con colori hardcoded, correggendo solo le combinazioni
  realmente rotte trovate. I due dialoghi (`PausaDialog`, `ConfermaDialog`)
  restano forzati `Light` **di proposito** (scelta di design preesistente, non
  un bug) e non vengono toccati.

## Architettura

### 1. Modello dati e persistenza

Nuova classe `PreferenzeUtente` in `Wsa.Quiz.Core.Models`:

```csharp
public class PreferenzeUtente
{
    public int TabIniziale { get; set; } = 0;
    public bool TemaScuro { get; set; } = false;

    public bool RandomizzaOrdine { get; set; } = true;
    public bool Rotazione { get; set; } = false;
    public bool Cronometro { get; set; } = false;
    public bool LimitaDomande { get; set; } = false;
    public int LimiteN { get; set; } = 10;
    public bool ModalitaTempo { get; set; } = false;
    public int MinutiTempo { get; set; } = 10;
}
```

I default coincidono con quelli oggi hardcoded in `HomeView`: senza
`preferenze.json` (utenti esistenti, prima esecuzione dopo l'update) tab e
opzioni quiz restano **identici a oggi** (parte da Home). Il tema è l'unica
eccezione: il default `TemaScuro = false` forza **Chiaro** al primo avvio
anche se il sistema è impostato scuro — conseguenza accettata della scelta
"toggle binario esplicito, niente opzione Sistema" (vedi Non-obiettivi).

`StorageService` (già gestisce `cronologia.json` e `quiz_in_pausa.json` con
scrittura atomica) si estende con:

```csharp
public PreferenzeUtente CaricaPreferenze();   // file assente/corrotto -> default
public void SalvaPreferenze(PreferenzeUtente prefs);
```

Stesso pattern di `CaricaCronologia`/`SalvaRisultato`: nuovo campo privato
`_filePreferenze` (validato anti-directory-traversal come gli altri due),
lettura con `try/catch` che ricade sui default se il file è assente o
corrotto, scrittura via `ScriviAtomico` (temp + rename). Nessuna classe di
servizio nuova.

### 2. Caricamento al boot e applicazione

In `MainView.Caricamento()`, subito dopo la costruzione di `_storage`:

1. `var prefs = _storage.CaricaPreferenze();`
2. Applico subito il tema: `Application.Current!.RequestedThemeVariant =
   prefs.TemaScuro ? ThemeVariant.Dark : ThemeVariant.Light;` — Avalonia
   ricalcola live le `DynamicResource`, non serve riavviare l'app né
   ristrutturare `App.axaml.cs`.
3. Le opzioni quiz vengono passate a `HomeView.Inizializza(...)` (nuovo
   overload/parametro `PreferenzeUtente`) che inizializza
   `RandomizzaOrdine`/`Rotazione`/`Cronometro`/`LimitaDomande`/`LimiteN`/
   `ModalitaTempo`/`MinutiTempo` da lì invece dai default hardcoded attuali
   nei campi privati.
4. Dopo aver costruito tutte le sezioni, `IndiceSezione = prefs.TabIniziale`
   (clampato 0-5) invece di partire implicitamente da 0.

### 3. Sezione "Impostazioni" (6ª voce di navigazione)

Nuova view `ImpostazioniView.axaml`/`.axaml.cs`, stesso pattern minimale
delle altre view di sola-lettura/configurazione (costruttore + `Inizializza
(_storage, prefs)`): un `ToggleSwitch` Avalonia (`OnContent="Scuro"`,
`OffContent="Chiaro"`) legato a `TemaScuro`. Al cambio: salva subito
(`_storage.SalvaPreferenze(...)`) e applica dal vivo
`Application.Current.RequestedThemeVariant` (stesso meccanismo del boot).

Integrazione nella navigazione a 6 sezioni — tocca `MainView.axaml`/`.axaml.cs`
in questi punti meccanici (oggi cablati per 5 sezioni):

- **Desktop**: nuovo `TabItem Header="Impostazioni"` nel `TabControl`, con
  `ContentControl x:Name="ImpostazioniArea"`.
- **Touch**: nuovo `ContentControl x:Name="ImpostazioniAreaTouch"` in
  `SwipePages`; nuova icona (ingranaggio, line-art coerente con le altre:
  `Path` stroke, 24×24) nella bottom-nav; `Grid.ColumnDefinitions` da
  `"*,*,*,*,*"` a `"*,*,*,*,*,*"`.
- **Code-behind**: nuovi campi `_impostazioniView`/`_impostazioniArea`;
  wiring in entrambi i rami di `ConfiguraNavigazione`; `IndiceSezione` clamp
  da 4 a 5; `OnKeyDown` (Ctrl+Tab) `const int n = 5` → `6` + nuovo `case 5`
  nello switch del focus; `DimensionaPagine` e l'array icone di `AggiornaNav`
  estesi a 6 elementi; wiring in `Caricamento()` (istanzia, `Inizializza`,
  assegna `.Content`).

### 4. Messa in sicurezza dark mode (scope mirato)

- **Fix certo**: `QuizColors.TempoNeutro` (default del `TempoBrush` sul
  cronometro, oggi `#1F1F1F` hardcoded, illeggibile in tema scuro) diventa
  theme-aware. Stessa famiglia di soluzione della Trappola 9 già documentata:
  o un colore neutro che si legge su entrambi gli sfondi, o lettura dinamica
  da `SystemBaseHighColor` se il contenitore del cronometro non ha già un
  background hardcoded proprio.
- **Audit mirato** (non redesign) sui restanti file con colori hex
  (`StatisticheView`, `GestioneContenutiView`, `CronologiaView`,
  `SospesiView`, `QuizView`, `RiepilogoView`, `HomeView`,
  `CategoriaDettaglioView`, `CronologiaDettaglioView`, `App.axaml`): per
  ciascuno, verificare — non assumere — che un `Background` hardcoded abbia
  un `Foreground` hardcoded abbinato (pattern Trappola 9, già immune al
  tema) e che nessun `Foreground` `DynamicResource`/system color sieda su un
  `Background` hardcoded chiaro (o viceversa). Si corregge solo ciò che
  risulta realmente rotto.
- `PausaDialog`/`ConfermaDialog`: **non toccati**, restano
  `ThemeVariantScope RequestedThemeVariant="Light"` di proposito.

### 5. Trigger di salvataggio

`preferenze.json` viene riscritto (atomico) quando:

- l'**indice di sezione cambia davvero** (non al ripristino iniziale da
  `prefs.TabIniziale`, per evitare una scrittura ridondante ad ogni boot);
- una qualunque **opzione quiz** in `HomeView` cambia (i setter hanno già gli
  hook `Raise()`, ci si aggancia il salvataggio lì);
- il **toggle tema** in `ImpostazioniView` cambia.

Nessun debounce: il file è piccolo, le interazioni sono a bassa frequenza
(non si scrive ad ogni frame/keystroke se non quando l'utente tocca
esplicitamente un controllo), coerente con la semplicità del resto del
codice.

## Non-obiettivi

- Nessuna opzione "segui il sistema operativo" per il tema (deciso: solo
  Chiaro/Scuro espliciti).
- Nessuna persistenza di materie/categorie selezionate.
- Nessun redesign sistematico di tutti i colori dell'app: solo fix dei
  problemi verificati.
- Nessuna verifica su device Android fisico in questo lavoro (gap già
  tracciato, rimandato come da decisione utente) — si verifica solo che
  l'APK Android continui a **compilare** in Release senza regressioni, non
  il comportamento touch reale a schermo.

## Piano di verifica

- `dotnet build Wsa.Quiz.sln -c Debug` verde su tutti i 5 progetti.
- Test manuale desktop: cambio opzioni quiz + tab attiva + tema → chiudi app
  → riapri → verifico che tab/opzioni/tema ripristinati corrispondano a
  quanto salvato.
- Test manuale desktop: toggle Scuro → verifico leggibilità su tutte le 6
  sezioni, incluso il cronometro in modalità a tempo (il caso noto rotto).
- `dotnet build wsa.quiz.android -c Release` verde (nessuna regressione di
  build Android); nessuna verifica runtime su device in questa iterazione.
