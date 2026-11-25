# Spec — Revisione grafica di Quaestio nello stile "lucafoglia"

- **Data:** 2026-11-25
- **Stato:** design da approvare (brainstorming del 25/11 con mockup nel browser; domande del revisore risolte).
- **Riferimenti visivi:** `assets/2026-11-25-revisione-grafica/` (mockup, tavolozza, logo, screenshot dei difetti).
  Archivio completo dei loghi: `docs/brand/loghi/`. Sistema di partenza: lucablog
  (`~/dev/lucablog/src/styles/tokens.css`, copiato in `~/dev/lf_brand/token/`).

## 1. Perché

Luca sta costruendo un brand personale, **"lucafoglia"**, nato dal sito lucablog (stile svizzero a griglia
esposta). Quaestio deve essere **riconoscibile come parte della stessa famiglia**: chi conosce il sito deve
ritrovarne carattere, colori e regole. Il brand evolverà, ma da qualche parte deve cominciare: questa revisione è
il primo prodotto oltre al sito.

Insieme si correggono i difetti di impaginazione visti sul telefono (testi e pulsanti che si sovrappongono).

## 2. Obiettivi, non-obiettivi, anti-obiettivi

**Obiettivi**
- Aspetto "B sobria": Archivo, neutri Grafite (chiaro) / Arena (scuro), righe da 1 px al posto di ombre e card,
  angoli vivi, colore solo dove ha una funzione.
- Barra di navigazione "C2": campi di colore pieni, uno per sezione.
- Tavolozza di 16 inchiostri; il colore scelto dall'utente pescato da lì.
- Logo provvisorio **a6-2 · Corsie invertite** nell'intestazione e come icona dell'app.
- Nessuna sovrapposizione di testi/controlli su telefono (360 dp di larghezza).

**Non-obiettivi**
- Nessun cambio di funzioni, flussi o dati. La CLI non si tocca.
- Nessun nuovo carattere completo: il lettering del logo resta un disegno (il font "lucafoglia" è un progetto futuro).
- Nessun cambio della cartella dati `WsaQuiz`, dei namespace `Wsa.Quiz.*`, dell'`ApplicationId`.

**Anti-obiettivi (fallimento anche se "funziona")**
- L'app diventa più lenta all'avvio o meno fluida nello scorrimento sul S21.
- Un utente perde il colore scelto o il tema chiaro/scuro salvato nelle preferenze.
- Un pulsante abilitato diventa invisibile su Android (trappole 6, 20) o il testo sopra un colore non si legge.
- Rosso, blu, ocra e verde usati insieme come quaterna "decorativa" (ricorda Google: deciso dall'utente).

## 3. Vincoli

- Avalonia 12, code-behind con `INotifyPropertyChanged`, niente MVVM (CLAUDE.md).
- Trappole da rispettare in particolare: 5 (`x:DataType`), 6/12/20 (bottoni accent e colori su Android:
  usare brush applicativi, mai `SystemAccentColorBrush`), 7 (`DynamicResource` di tipo Brush nei Setter),
  10 (`BoxShadow` vicino a `ScrollViewer`), 11 e 26 (scroll), 27 (WebAssembly).
- Budget: solo strumenti gratuiti (memoria "Budget: solo abbonamento Pro"). Font con licenza libera.
- Desktop, Android, iOS e WebAssembly condividono `wsa.quiz.app`: ogni cambio vale per tutti.

## 4. Approcci considerati

1. **Strato di token e stili sopra il tema Fluent** — *scelto da Luca*. Fluent resta (tendine, contatori,
   interruttori continuano a funzionare); sopra: un dizionario di valori del brand, stili globali che tolgono
   arrotondamenti e ombre, interventi mirati su intestazione, barra, tavolozza, logo, schermate.
   Peso a runtime trascurabile (stili applicati al caricamento; togliere le ombre alleggerisce il disegno).
2. **Tema scritto da zero senza Fluent**: controllo totale, ma riscrivere i template di ogni controllo; settimane
   e molte trappole note. Scartato.
3. **Solo correzioni, colori e logo**: veloce, ma l'app resta "Fluent" (angoli tondi, ombre, pillola): non
   sembra lucablog. Scartato.

## 5. Decisioni prese (con Luca)

### 5.1 Neutri, carattere, forme
- **Chiaro (Grafite):** fondo `#e8e7e3`, superficie rialzata `#f1f0ec`, testo `#141413`, testo attenuato
  `#57564f`, righe `#141413` (forti) e `#d6d5d0` (deboli).
- **Scuro (Arena):** fondo `#0c0c0c`, superficie `#161616`, testo `#f4f4f2`, attenuato `#a6a6a6`, righe
  `#f4f4f2` (forti) e `#212120` (deboli).
- **Carattere:** Archivo (OFL) per tutta l'interfaccia, al posto di Inter. Titoli pesanti e stretti (peso ~800,
  larghezza ~75–80% come nei mockup), testo normale a larghezza piena. Il codice nelle domande resta JetBrains
  Mono NL.
- **Riquadri e card (deciso da Luca, 25/11, opzione A):** passata globale di stili — ogni card diventa un riquadro
  ad angoli vivi con bordo da 1 px, senza ombra. Solo le schermate a elenco (**Cronologia, Sessioni in pausa,
  Statistiche**) perdono le card: righe separate da linee da 1 px, come nel mockup B. Le altre (Home, Quiz,
  Riepilogo, Impostazioni, Gestione contenuti, dialoghi) cambiano solo per stili, colori e carattere, **senza mockup
  dedicati**: Luca le guarda nell'app e si correggono lì; una schermata che non convince si riprogetta poi con i
  mockup (feedback "mockup prima della UI").
- **Forme:** angoli vivi (raggio 0) su pulsanti, campi, riquadri; niente `BoxShadow`; separazioni con righe da
  1 px; spazi in multipli di 7 (7, 14, 28, 42…), come lucablog.

### 5.2 Tavolozza: 16 inchiostri
"Toni medi, pieni ma smorzati" (luminosità ~40–58%, saturazione ~40–65%). Riferimento:
`assets/.../tavolozza-16-inchiostri.html`.

| Inchiostro | Hex | Testo sopra | Ruolo |
|---|---|---|---|
| Rosso | `#d6414b` | nero | **riservato**: sbagliata, Elimina |
| Terracotta | `#e0703a` | nero | sezione Statistiche; sceglibile |
| Cacao | `#8f5f3d` | bianco | sceglibile |
| Ocra | `#ebaf00` | nero | **riservato**: a metà / seconda possibilità; sezione Pausa |
| Oliva | `#95a63f` | nero | sceglibile |
| Prato | `#3fa66a` | nero | sceglibile |
| Verde acqua | `#40a79b` | nero | **riservato**: giusta; sezione Contenuti |
| Petrolio | `#308ea6` | nero | sceglibile |
| Azzurro | `#539fd4` | nero | sceglibile |
| Blu | `#3a6cb8` | bianco | sezione Home; **colore utente predefinito** |
| Indaco | `#4d4dbc` | bianco | sceglibile |
| Viola | `#8559b0` | bianco | sezione Cronologia; sceglibile |
| Prugna | `#a63f8c` | bianco | sceglibile |
| Rosa antico | `#d06a95` | nero | sceglibile |
| Ardesia | `#5c6370` | bianco | sceglibile (il "senza colore") |
| Grigio | `#d7d7d7` (chiaro: `#c4c3bd`) | nero | sezione Impostazioni |

- **Esiti** (`QuizColors`): verde → verde acqua `#40a79b`, ambra → ocra `#ebaf00`, rosso → `#d6414b`, con testo
  nero sopra. Gli sfondi tenui della heatmap/barre si ricavano dagli stessi tre.
- **Colore utente** (`Tavolozza`): le 12 voci "sceglibili" + blu. I vecchi id salvati in `preferenze.json`
  si mappano così, senza perdere la scelta: `blu`→blu, `blunotte`→indaco, `indaco`→indaco, `viola`→viola,
  `magenta`→prugna, `verdeacqua`→petrolio, `cacao`→cacao, `grafite`→ardesia; id sconosciuto → blu.
- Ogni inchiostro porta il proprio colore di testo (colonna "Testo sopra"): il bianco fisso di oggi sui bottoni
  accent non basta più.
- **Dove va il colore utente** (deciso con la pagina della tavolozza, 25/11): pulsanti d'azione primari
  (`Button.accent`: Avvia, Ripassa, Riprendi, conferme dei dialoghi), link d'azione testuali (es. "Aggiorna"), e i
  controlli Fluent tramite `SystemAccentColor*` (caselle, interruttori, radio, selezioni). **Non** va più su
  intestazione e barra. Il **colore di sezione** vive solo nella barra di navigazione: dentro una sezione l'accento è
  sempre quello dell'utente (nel mockup C2 "Ripassa" e "Aggiorna" restano blu anche in Statistiche).
  `Tavolozza` resta quindi un accento globale; non serve un "colore di sezione" ereditato dalle viste.
- **Esiti usati come colore del testo** su fondo neutro (percentuali, esito, cronometro in Riepilogo, Cronologia,
  CronologiaDettaglio, Quiz): come in lucablog (`--c-*-text` in `tokens.css`) si usa una **variante per il testo**
  nel tema chiaro — ocra `#9b7300`, verde acqua `#33867c`, rosso `#d6414b` — e i colori pieni nel tema scuro.
  Le varianti di lucablog reggono 3:1 (testo grande/grassetto); dove il testo è piccolo e normale, chi costruisce
  scurisce la variante fino a 4,5:1 mantenendo la tinta (scelta di Claude, coerente col metodo del sito).

### 5.3 Intestazione
- Fondo neutro (non più il colore d'accento), riga da 1 px sotto nel colore delle righe forti.
- A sinistra il **marchio a6-2** ("quaestio" a doppia corsia: corsia esterna colorata per lettera, anima neutra
  bianca o nera secondo il tema) come vettore: i tracciati sono in
  `assets/.../logo-a6-2-marchio-scuro.svg` / `-chiaro.svg` (viewBox 184×44, solo `path` e `rect` pieni, usabili
  come `Path.Data` in Avalonia).
- A destra il testo attenuato "12 materie · 2483 domande" come oggi.

### 5.4 Navigazione "C2"
- **Touch (telefono/tablet):** la pillola flottante diventa una barra a tutta larghezza, a filo del bordo
  inferiore (sopra l'inset dei tasti Android), fatta di sei campi di colore pieni, uno per sezione, nell'ordine:
  Home blu, Cronologia viola, Pausa ocra, Statistiche terracotta, Contenuti verde acqua, Impostazioni grigio.
  Gli inattivi mostrano solo l'icona; **l'attivo si allarga (~2,4×) e mostra icona + nome**. Testo/icona nel
  colore "Testo sopra" della tabella. Riferimento: `assets/.../mockup-barra-c2.html` (variante C2).
- **Desktop/web (proposta di Claude, da confermare a vista):** la stessa logica sulle tab in alto: ogni tab ha
  una fascia di 3–4 px del suo colore, la tab attiva è un campo pieno del suo colore. Il piano prevede un mockup
  (2–3 varianti affiancate) e l'ok di Luca **prima** di scriverla; se bocciata, le tab restano come oggi con i soli
  stili globali (angoli vivi, Archivo, colori neutri) finché non si sceglie un'alternativa.

### 5.5 Logo e icone dell'app
- Icona **a6-2**: Q a doppia corsia (indaco, azzurro, verde acqua, terracotta) su fondo Arena (scuro) o Grafite
  (chiaro). Tracciati in `assets/.../logo-a6-2-icona-scuro.svg` / `-chiaro.svg` (viewBox 112×112).
- L'icona di sistema usa la **versione scura** (come l'attuale "Q" bianca su nero).
- Va prodotta per: Android (mipmap legacy di ogni densità + icona adattiva), iOS, desktop
  (`Assets/Branding/quaestio-icon.png` e icona della finestra), web (favicon/manifest di `wsa.quiz.browser`).
  Rasterizzazione con strumenti gratuiti (SkiaSharp, già usato al rebrand del 18/07, o Chrome headless).
- È provvisorio: deve restare facile da sostituire (un solo sorgente SVG per forma, PNG rigenerabili).

### 5.6 Correzioni di impaginazione (dagli screenshot di Luca)
- **Statistiche** (`difetto-statistiche-*.jpg`): "Aggiorna" scavalca la riga del titolo; il campo numerico
  mostra "3(" invece di "30"; il testo di "Ripassa (20)" non è centrato verticalmente.
- **Sessioni in pausa** (`difetto-sessioni-pausa.jpg`): "Riprendi" ed "Elimina" coprono titolo, riga del progresso
  e tempo; su telefono i pulsanti vanno sotto il testo. Margini diversi da Statistiche (card rientrata di più).
  "1 sessioni in pausa": singolare/plurale.
- **Tutte le schermate**: passata a 360 dp di larghezza, tema chiaro e scuro, alla ricerca di sovrapposizioni e
  testi tagliati; ciò che si trova si elenca e si corregge nello stesso modo.

## 6. Come si verifica

- Build `wsa.quiz.desktop` e `wsa.quiz.browser` puliti, `dotnet test wsa.quiz.core.tests` verde, a ogni tappa.
- Prova a mano su desktop (chiaro/scuro) e sul Galaxy S21 (APK Release da build pulita, trappola 29).
- **Prestazioni:** tempo di avvio a freddo e fluidità di scorrimento (Cronologia, Statistiche) sul S21 **prima e
  dopo**; nessun peggioramento percepibile.
- Preferenze esistenti: un `preferenze.json` con ciascuno dei vecchi id colore si apre col colore mappato.
- Contrasto: testo sopra ogni inchiostro ≥ 4,5:1 (testo normale) secondo la colonna "Testo sopra".

## 7. Lasciato a chi costruisce

- Struttura dei file di risorse (un `Tokens.axaml` unico o diviso per colori/spazi/caratteri) e nomi delle chiavi.
- Come ottenere le istanze statiche di Archivo (pesi e larghezze) da file OFL gratuiti: se Avalonia non regge
  l'asse di larghezza del variabile, generare istanze fisse (es. fontTools instancer) e includerle come asset.
- Valori hover/pressed degli inchiostri (es. ±10% verso nero/bianco come oggi in `Tavolozza.Mescola`).
- Ordine e dimensione delle tappe del piano, purché ognuna lasci l'app compilabile e provabile.
- Icone della barra: si riusano i `Path` attuali (stroke 2), cambiando solo il colore.
