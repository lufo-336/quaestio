# Localizzazione (interfaccia + formato domande multilingua) — design

> Data: 2026-10-06. Stato: approvato a voce, in attesa di revisione scritta.

## Scopo

Rendere Quaestio un progetto internazionale: l'interfaccia disponibile in
**inglese** oltre che in italiano, con una struttura che permetta di
**aggiungere altre lingue** in futuro senza toccare il codice delle view.

In questo giro: localizzazione completa dell'interfaccia + formato dei file di
traduzione delle domande, collaudato traducendo **una** materia (Cybersecurity,
60 domande). La traduzione delle altre materie è un progetto separato.

### Criteri di successo

- Dal selettore lingua in Impostazioni si passa a English e **tutta**
  l'interfaccia cambia lingua, senza riavviare l'app.
- La materia Cybersecurity si gioca in inglese (domande, risposte,
  spiegazioni, nome materia, categorie).
- Cronologia, sospesi, statistiche e sezioni nascoste continuano a funzionare
  attraverso un cambio di lingua.

### Assunzioni (confermate)

- La CLI resta in italiano.
- Alla prima apertura: lingua di sistema se supportata, altrimenti inglese.
  La scelta è persistita in `preferenze.json`.
- Una domanda senza traduzione si mostra in italiano.
- La cronologia conserva il testo nella lingua in cui si è giocato (una partita
  fatta in inglese resta in inglese anche riaperta in italiano).

## 1. Stringhe dell'interfaccia e cambio lingua

### Risorse

- `Strings.resx` (italiano, base/neutral) e `Strings.en.resx` in
  **`Wsa.Quiz.Core`** (es. `wsa.quiz.core/Risorse/`), con classe generata
  `public` (`PublicResXFileCodeGenerator`). Sta in Core perché anche Core ha
  stringhe user-facing: messaggi di `ValidatoreImportDomande`, intestazioni di
  `EsportazioneService`, etichette di `StatisticheService`/`FiltroCronologia`,
  testi di `ContenutiUtenteService`.
- Uso: `Strings.AvviaQuiz` nel code-behind, `{x:Static r:Strings.AvviaQuiz}`
  negli `.axaml`. Stringhe con parametri → `string.Format(Strings.X, ...)` con
  segnaposto `{0}` nella risorsa, mai concatenazioni di frammenti tradotti.
- Restano hard-coded solo i testi non linguistici (simboli, lettere `A/B/C/D`,
  icone, hex).

### CLI

`Wsa.Quiz.Cli` imposta `CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture
= new CultureInfo("it")` all'avvio: le stringhe di Core che usa restano
italiane anche su un sistema inglese.

### Preferenza e avvio

- Nuovo campo `PreferenzeUtente.Lingua` (`string`, default `""` = segui il
  sistema). Valori ammessi: i codici dell'elenco lingue (vedi sotto).
- In `App.OnFrameworkInitializationCompleted`, **prima** di creare la
  `MainView`: legge la preferenza, risolve la lingua (preferenza → lingua di
  sistema se supportata → `en`) e imposta `CultureInfo.CurrentUICulture` e
  `CurrentCulture` (per date/numeri).
- Elenco lingue supportate: una mappa statica unica, es.
  `Lingue.Supportate = { "it" → "Italiano", "en" → "English" }`. Aggiungere una
  lingua = un `Strings.<codice>.resx` + una riga qui + (opzionale) i file in
  `traduzioni/<codice>/`.

### Selettore in Impostazioni

- `ImpostazioniView`: il ComboBox oggi disabilitato (`LinguaModificabile`
  costante `false`, `LinguaAttuale` fisso `"Italiano"`) diventa vero, popolato
  dall'elenco lingue, ogni voce mostrata nel proprio nome nativo.
- **Disabilitato durante un quiz in corso** (Home mostra un `QuizView`), con
  nota "Termina o metti in pausa il quiz per cambiare lingua". Nessuna partita
  si perde in silenzio.

### Cambio lingua a runtime

1. Salva `Lingua` in `preferenze.json`.
2. Imposta le due `CultureInfo`.
3. `App` sostituisce la `MainView` con una nuova istanza: su desktop
   `window.Content = new MainView()`, su Android il `Child` del
   `LayoutTransformControl` che la avvolge. La nuova istanza ricarica dati e
   traduzioni e si riapre su Impostazioni grazie a `TabIniziale` (Step 13).

Va esposto un punto unico in `App` (es. `App.RicreaVistaPrincipale()`) chiamato
da `ImpostazioniView`; nessuna view tocca direttamente la finestra.

### Formati

Date, numeri e percentuali formattati con la cultura corrente. Il CSV di
`EsportazioneService` usa separatore e decimale della cultura: `it` → `;` e `,`
(come oggi), `en` → `,` e `.`.

## 2. Traduzioni delle domande

### Posizione

Nuova cartella `traduzioni/<lingua>/<materiaId>.json` alla radice del repo.
**Fuori** da `domande/`: `AssetLoader.GetAssets` su Android enumera
ricorsivamente, quindi una sottocartella di `domande/` verrebbe caricata come
file di domande (schema sbagliato). Inclusa come le domande: `AvaloniaResource`
in `Wsa.Quiz.App.csproj` e `None` con `Link` nel csproj desktop. La CLI non la
include.

### Formato

```json
{
  "nome": "Cybersecurity",
  "categorie": { "Fondamenti": "Fundamentals" },
  "domande": {
    "a1b2c3d4e5f6": {
      "domanda": "…",
      "risposte": ["…", "…", "…", "…"],
      "spiegazione": "…"
    }
  }
}
```

- Chiave delle domande = `Domanda.Id` **finale** (compreso l'eventuale suffisso
  `-2` di collisione), calcolato sempre sul testo italiano.
- `risposte` nello **stesso ordine** dell'originale: `rispostaCorretta` non si
  traduce e resta valida.
- Tutti i campi sono opzionali: ciò che manca resta in italiano.

### Caricamento

In `StorageService`, dopo il calcolo degli `Id` in `CaricaDomandeMateria`: se la
lingua corrente non è `it` ed esiste `traduzioni/<lingua>/<materia.Id>.json`
(letto tramite `IFonteDati`, quindi funziona su desktop e Android), applica
l'overlay:

- `DomandaTesto`, `Risposte`, `Spiegazione` sostituiti per le domande presenti;
- `MateriaNome` (sulle domande e sulla `Materia`) sostituito da `nome`;
- nuovo campo `Domanda.CategoriaVisualizzata` (`[JsonIgnore]`, default =
  `Categoria`) impostato da `categorie`.

**Invarianti**: `Id`, `RispostaCorretta`, `Categoria`, `MateriaId` non vengono
mai modificati dall'overlay.

### Categorie

`Domanda.Categoria` resta la **chiave canonica italiana**, usata da
`OpzioniQuiz.Categorie` (`"Materia/Categoria"`), `SezioneEsclusa`,
`StatisticheService`, `DettaglioRisposta.Categoria`. Le view che mostrano una
categoria passano a `CategoriaVisualizzata` (o a una mappa chiave → nome
visualizzato dove lavorano su stringhe, es. heatmap e Gestione contenuti).

### Errori (mai bloccanti)

| Caso | Comportamento |
|---|---|
| File traduzione assente | materia in italiano |
| File malformato | materia in italiano, avviso su `Console.Error` |
| `risposte` con numero diverso dall'originale | quella domanda in italiano, avviso |
| `Id` senza domanda corrispondente (orfano) | ignorato, avviso |

### Contenuti importati

Le materie/domande importate da "Gestione contenuti" non hanno traduzioni e si
mostrano come sono state importate.

### Materia pilota

**Cybersecurity** (60 domande). Uno script usa-e-getta nello scratchpad genera
lo scheletro (`Id` + testo italiano) leggendo le domande con lo stesso
`StorageService`, poi il file viene riempito con la traduzione e committato in
`traduzioni/en/cybersecurity.json`. Lo script non entra nel repo.

## 3. Fuori scope

- Traduzione delle altre 9 materie (progetto separato con propria spec, che
  deciderà anche un eventuale tool di traduzione in blocco).
- CLI in inglese.
- `generazione-domande/` (guida e motore IA).
- README e documentazione.
- Traduzione retroattiva dei testi già salvati in cronologia.

## 4. Verifica

1. **Harness headless Core**:
   - caricamento con `en` → testi sostituiti, `Id`/`RispostaCorretta`/
     `Categoria` identici al caricamento `it`;
   - numero di risposte diverso → domanda in italiano;
   - `Id` orfano → ignorato;
   - lingua senza file → italiano;
   - **ogni chiave di `Strings.resx` esiste in `Strings.en.resx`** con valore
     non vuoto.
2. **Harness headless sulla vera `MainView`**:
   - avvio con `Lingua="en"` → etichette chiave in inglese;
   - cambio lingua da Impostazioni → vista ricreata, riaperta su Impostazioni;
   - selettore disabilitato con un `QuizView` attivo.
3. **Grep di copertura**: nessun attributo `Text=`/`Content=`/`Header=`/
   `Watermark=`/`ToolTip.Tip=` con letterale linguistico negli `.axaml`, salvo
   eccezioni annotate.
4. **Build** dei 5 progetti; prova a vista dell'app desktop in inglese su Fedora.
