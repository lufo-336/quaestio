# Laboratorio di codice — design

Data: 2026-11-26 · Stato: bozza da approvare (rivista con Luca il 26/11)
Mockup: `assets/2026-11-26-laboratorio/` (`prevedi-output.html`, `trova-bug.html`, `ingresso.html`; frammenti per il visual companion, si aprono anche da soli nel browser).

## 1. Perché

Il codice ormai lo scrive l'IA; il lavoro si sposta su **leggere, giudicare e mettere alla prova** codice altrui: review, ricerca di difetti, test. I quiz a risposta multipla coprono già l'esame del corso; il Laboratorio allena queste abilità **per il lavoro**. È una nuova modalità di studio accanto ai quiz, non al loro posto.

Base didattica (ricerca fatta il 26/11): il tracciamento del codice ("prevedi cosa stampa") è il prerequisito della comprensione; i Parsons problems (riordina le righe) rendono quanto scrivere codice; la ricerca di bug funziona meglio se guidata (es. un vocabolario di tipi di difetto) che "alla cieca".

## 2. Obiettivi, non-obiettivi, anti-obiettivi

**Obiettivi**
- Una **scheda di codice** attraversata in **sequenza**: più passaggi di fila sullo stesso spezzone.
- Tre livelli di difficoltà (base, media, alta) e contenuto sufficiente a rendere l'esercizio **ripetibile**.
- Si parte da **C#**; poi Python, JavaScript, C/C++ con lo stesso formato.
- I risultati finiscono in **Cronologia e Statistiche** esistenti.

**Non-obiettivi (v1)**
- **Nessuna risposta aperta** (né output scritto a tastiera né spiegazioni a parole).
- **Scelta architetturale** (scenario → componenti): modalità a parte, fase successiva.
- **Riordina le righe** (Parsons): rimandato (confermato da Luca). Sulla stessa scheda, dopo tre passaggi sullo stesso codice, sarebbe banale; ha senso su schede proprie, da progettare dopo.
- SQL: "prevedi l'output" diventa una tabella, interfaccia diversa; dopo.
- Cronometro nelle schede. *(Mia scelta.)*
- **Motore e prompt di generazione** delle schede: si progettano dopo aver visto la v1 funzionare (decisione di Luca). La v1 parte con poche schede scritte a mano (§5.5).
- Laboratorio nella console (`wsa.quiz.cli`): no. La CLI legge però la stessa cronologia, quindi deve solo **tollerare** i record di Laboratorio (comparire in statistiche va bene, il "ripassa" CLI non deve provare ad aprirli come quiz). *(Mia scelta.)*
- Traduzione inglese dei contenuti delle schede (l'interfaccia sì, it/en come il resto). *(Mia scelta.)*

**Anti-obiettivi** (fallimento anche se funziona)
- Diventa "un quiz con un altro nome": scelta tra 4 alternative per ogni passo, si va per esclusione.
- **Chiavi di risposta sbagliate**: una scheda che dice "stampa 7" quando stampa 7,25 insegna il falso. Con contenuti generati dall'IA è il rischio principale (vedi §6).
- Rompere cronologia/statistiche esistenti o i dati utente già salvati.
- Serve l'IA o internet a runtime (vincolo: solo abbonamento Pro, niente API; l'app gira anche offline su Android/iOS/browser).

## 3. Vincoli

- Nessuna esecuzione di codice sul dispositivo (Android/iOS/WebAssembly non hanno un compilatore): **ogni risposta è precalcolata** nel JSON della scheda e la correzione è un confronto esatto.
- Stesso stack e stesse regole del progetto: Avalonia 12, code-behind con `INotifyPropertyChanged` (niente MVVM), compiled bindings con `x:DataType`, angoli vivi, token di `Tokens.axaml`, trappole in `docs/claude/trappole.md`.
- Dati read-only letti via `IFonteDati` (embedded su Android/iOS/browser, file nel bin su desktop/CLI), come `domande/`.

## 4. Approcci considerati

- **A. Travestirla da quiz** (ogni passo = `Domanda` a 4 risposte, riuso totale di `SessioneQuiz`). Scartato: niente tocco sulla riga, niente composizione dell'output; è l'anti-obiettivo n.1.
- **B. Modalità del tutto separata** (formato, sessione, salvataggi e statistiche propri). Scartato: rifà cronologia/statistiche che funzionano; due mondi che non si parlano.
- **C. Interazione nuova, archivio comune — scelto.** Formato scheda, sessione e vista nuovi; ogni sessione produce un normale `RisultatoQuiz` con un `DettaglioRisposta` per passaggio, quindi Cronologia, Statistiche e Riepilogo funzionano senza essere riscritti. Rischio: `DettaglioRisposta` nasce per "domanda → 4 risposte"; va usato senza rompere i record esistenti (solo campi additivi, se servono).

## 5. Come funziona (deciso con Luca)

### 5.1 Ingresso (mockup `ingresso.html`, variante A)
In Home, in alto, un interruttore **Quiz | Laboratorio**. Su Laboratorio i riquadri sotto diventano:
- **Linguaggio** (v1: solo C#; gli altri compaiono quando hanno schede).
- **Livello**: Base / Media / Alta.
- **Schede**: "Solo non ancora viste" oppure **"Ripassa quelle sbagliate"**.
- Barra in basso: conteggio schede disponibili + **Inizia**.

La barra di navigazione resta a 6 sezioni.

**Lo stesso interruttore Quiz | Laboratorio** compare in cima a **Cronologia**, **Statistiche** e **Sospesi** (decisione di Luca). È **un'unica scelta per tutta l'app** *(mia scelta)*: cambiandola in un punto cambia ovunque e viene ricordata in `PreferenzeUtente` (campo additivo). In Cronologia diventa un criterio in più di `FiltroCronologia`; in Statistiche filtra le materie "… · Laboratorio"; in Sospesi filtra le sessioni sospese per tipo.

### 5.2 La sequenza di una scheda
Tutti i passaggi lavorano sulla **stessa variante** (il codice con il difetto) e sulla stessa chiamata di esempio. *(Mia scelta: prevedere l'output del codice difettoso e poi confrontarlo con il requisito è esattamente il ragionamento di chi fa review.)*

1. **Prevedi l'output** (mockup `prevedi-output.html`)
   - *Base* — variante C: un menu per ogni riga stampata, valori proposti più esche.
   - *Media e alta* — variante B: componi l'output toccando tessere in ordine (una tessera = una riga), con tessere esca. Correzione esatta.
   - Nessun secondo tentativo. Se sbagli: output giusto mostrato, differenza evidenziata, si prosegue.
2. **Trova il bug** (mockup `trova-bug.html`): si tocca la riga.
   - *Base* — A: riga + **tipo di errore** (vocabolario fisso, §5.4).
   - *Media* — B: riga + **correzione** scelta tra 3 righe (una giusta, due trappole plausibili).
   - *Alta* — C: **review completa**: 2–3 difetti, si segnano più righe dando a ognuna il tipo; il contatore dice quanti difetti ci sono; i falsi allarmi tolgono punti.
   - Seconda possibilità (base e media): riga sbagliata → un indizio ("il problema è dentro il ciclo") e un secondo tentativo; se fallisce anche quello la riga giusta si illumina e si prosegue. Così il passo 3 ha sempre senso.
3. **Rompi la funzione**: 3–4 input candidati, scegli quello che fa fallire il codice (almeno uno dei difetti, al livello alto). Dopo la risposta, per ogni input: atteso ed effettivo (precalcolati nella scheda).

### 5.3 Punteggio e archivio
- Ogni passo: 1 se giusto al primo colpo, ½ al secondo, 0 se sbagliato (come la seconda possibilità dei quiz: `Tentativi`/`SecondoTentativo`/`PesoErrore()` già esistenti).
- Review completa (alta): difetti trovati col tipo giusto meno falsi allarmi, normalizzato a 0..1. *Formula esatta al builder*, purché un "segna tutte le righe" non convenga.
- Una sessione di Laboratorio (N schede di fila) = **un `RisultatoQuiz`** con `Modalita` "Laboratorio" e un `DettaglioRisposta` per passaggio:
  - `MateriaNome` = "C# · Laboratorio" (così in Statistiche è una voce a sé, separata dai quiz C#);
  - `Categoria` = **tipo di difetto** della variante (es. "Divisione intera"), così Statistiche dice "sbagli sempre gli off-by-one";
  - `IdDomanda` = id stabile `lab:<linguaggio>:<scheda>:<variante>:<passo>`;
  - `TestoDomanda`/`RispostaData`/`RispostaCorretta`/`Spiegazione` testuali leggibili in Cronologia.
- Fine sessione: `RiepilogoView` esistente (prende un `RisultatoQuiz`).
- "Ripassa" di Statistiche su una voce "… · Laboratorio" deve aprire il Laboratorio in modalità "ripassa quelle sbagliate", **non** un quiz su una materia inesistente.

### 5.3-bis Pausa e ripresa (decisione di Luca: nella v1)
- Si può sospendere una sessione di Laboratorio; compare in **Sospesi** (sotto Laboratorio).
- Granularità **per scheda** *(mia scelta)*: si salvano l'elenco di schede/varianti della sessione e i `DettaglioRisposta` delle schede **già concluse**; alla ripresa si riparte **dall'inizio della scheda in corso**. Niente stato a metà passo.
- Formato su disco: additivo rispetto a `SessionePausa` (o un tipo affine nello stesso file), senza rompere le pause dei quiz già salvate. *Dettaglio al builder.*

### 5.4 Vocabolario dei tipi di difetto (C#, v1)
Lista fissa nel codice, etichette in `Strings.resx`/`Strings.en.resx`. Proposta iniziale: off-by-one / confine, divisione intera, null / vuoto non gestito, condizione invertita, overflow, variabile o operando sbagliato, confronto di stringhe (case/culture), mutazione di una collezione durante l'iterazione, eccezione inghiottita, risorsa non chiusa. *Il builder può affinarla*; ogni difetto di ogni scheda deve usarne una.

### 5.5 Ripetibilità e volume
- Ogni scheda ha **2–4 varianti** (stesso codice "sano", difetti diversi). All'apertura si pesca una variante non ancora vista; esaurite, la meno recente.
- "Ripassa quelle sbagliate": varianti il cui ultimo tentativo ha almeno un passo con `PesoErrore() > 0`, ricavate dalla cronologia.
- **v1**: un primo lotto di schede C# scritte a mano per provare il tutto, almeno 3 per livello con 2 varianti ciascuna. *(Mia scelta sul numero.)*
- Obiettivo di volume, dopo il motore di generazione: **30 schede** per linguaggio (12 base, 12 media, 6 alta) ≈ 90 varianti.
- Dimensioni indicative: base 8–12 righe e 1 difetto evidente; media 15–25 righe e 1 difetto sottile; alta 30+ righe e 2–3 difetti di gravità diversa.

## 6. Contenuti: formato, generazione, verifica

- Cartella `laboratorio/<linguaggio>/` (accanto a `domande/`), un JSON per scheda, incorporata/copiata come `domande/` in tutti i csproj che la usano.
- Ogni scheda contiene almeno: id, titolo, livello, requisito (cosa deve fare il codice), chiamata di esempio, e per ogni variante: righe di codice, difetti (riga, tipo, indizio, spiegazione; correzioni per il livello media), output effettivo riga per riga + esche, input candidati con atteso/effettivo/`espone`. *Schema esatto al builder.*
- **Guida/prompt di generazione**: fuori dalla v1, da progettare dopo (vedi non-obiettivi). Le schede le genererà Claude dentro l'abbonamento; il formato v1 deve restare abbastanza semplice da poterle scrivere a mano.
- **Verifica a due livelli** (contro l'anti-obiettivo "chiavi sbagliate"):
  1. *Coerenza* (unit test in `wsa.quiz.core.tests`, come l'integrità delle domande): righe dei difetti esistenti, esattamente una correzione giusta, almeno un input che espone e uno che non espone, esche diverse dall'output vero, tipi di difetto nel vocabolario, id unici.
  2. *Esecuzione reale* (approvato da Luca): un test solo-sviluppo che **compila ed esegue** ogni variante C# (Roslyn, `Microsoft.CodeAnalysis.CSharp`, dipendenza del solo progetto di test) e controlla che output ed "effettivo" degli input coincidano con la scheda. Python e C/C++ avranno un controllo analogo con gli interpreti/compilatori della macchina di sviluppo, quando arriveranno.

## 7. Lasciato al builder

- Nomi di classi/file, struttura della sessione dedicata (piccola macchina a stati, non un'estensione di `SessioneQuiz`).
- Schema JSON esatto e formula della review completa (entro i vincoli sopra).
- Numero di schede per sessione (proposta: 5, modificabile in Home) e persistenza delle scelte Laboratorio in `PreferenzeUtente` (campi additivi).
- Come Cronologia mostra il dettaglio di un passo di Laboratorio (verificare che `CronologiaDettaglioView` regga `RisposteShufflate` vuote).
- Layout desktop/tablet (i mockup sono telefono), restando nello stile della revisione grafica.

## 8. Prerequisito

Su `main` c'è un commit **wip** dello swipe tra sezioni (`cc168f9`, non verificato). Prima di iniziare: `dotnet build Wsa.Quiz.sln` verde e swipe chiuso o messo da parte.
