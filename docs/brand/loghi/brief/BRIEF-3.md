# Brief, terzo giro: sei varianti di logo per Quaestio, dai poster svizzeri

Leggi prima `BRIEF.md` (contesto, colori, fondi, carattere, formato di consegna, regole) e `BRIEF-2.md`
(giudizio sul primo giro). Il secondo giro (`agy2.html`, screenshot `giro2-sei.png`) è stato giudicato
**"non ci siamo ancora"**: troppo "lettera colorata", poco costruito.

## Le tre immagini di riferimento dell'utente (guardale tutte)

- `ispira-1-isometrico.png`: copertina "Swiss design". Figura **isometrica** fatta di **nastri piegati**: ogni faccia
  ha un colore piatto (blu, giallo, arancio, nero, grigio), angoli vivi, il volume nasce solo dai piani di colore.
  Un piccolo modulo cubico (arancio/grigio/nero) accanto.
- `ispira-2-beethoven.jpg`: poster "beethoven" di Josef Müller-Brockmann. **Moduli ripetuti**: quarti di
  corona circolare fatti di **più archi concentrici paralleli** (come corsie), ruotati e accostati in un ritmo.
  Solo nero su bianco: la forza è nella ripetizione del modulo.
- `ispira-3-cerchi.png`: poster su Müller-Brockmann. **Archi concentrici spezzati** di spessori diversi, aperti
  da un lato come una "C", ruotati l'uno rispetto all'altro, filetti sottili tra un anello e l'altro, e un
  **disco rosso pieno** al centro. Nero, bianco, un solo colore forte.

## Cosa ne ricavo

La Q non va più "disegnata come lettera e poi colorata": va **costruita con forme geometriche primarie**
(archi, corone, dischi, barre, nastri piegati) come nei poster, e la lettera deve emergere dalla costruzione.
Una Q è per natura un cerchio più una coda: i riferimenti 2 e 3 sono già quasi una Q.

## Sei varianti, due per direzione

1. **Archi concentrici** (rif. 3): Q fatta di anelli concentrici spezzati e ruotati, la coda come barra o come
   anello che esce; un disco o un anello in colore. Uno o due colori forti più bianco/nero, oppure i colori del
   brand assegnati agli anelli (massimo 4).
2. **Modulo a corsie** (rif. 2): Q composta da quarti di corona a più corsie parallele (3–5 corsie, una per
   colore del brand, oppure bianco/nero con una corsia colorata). La coda è lo stesso modulo o un suo pezzo.
   Le corsie orizzontali dell'intestazione (che all'utente piacevano: righe che entrano da sinistra e toccano
   "quaestio") possono essere la continuazione delle corsie dell'arco.
3. **Nastro isometrico** (rif. 1): Q come nastro piegato in assonometria, facce in colori piatti (ocra, rosso,
   blu più nero o grigio), angoli vivi. Deve restare leggibile a 28 px: semplifica al massimo.

## Vincoli che restano

- Icona e marchio "quaestio" devono essere **lo stesso segno**: nell'intestazione il gesto dell'icona deve
  vedersi (per esempio la q iniziale costruita con lo stesso modulo, o le corsie che arrivano alla parola).
  La parola "quaestio" resta leggibile e pulita in Archivo: non riempirla di strisce.
- Colori solo dal brand (rosso `#d6414b`, blu `#3a6cb8`, ocra `#ebaf00`, verde acqua `#40a79b`, grigio
  `#d7d7d7`), più nero/bianco dei fondi. Con misura.
- Evita: lente di ricerca (coda a 45° fuori da un anello semplice), arcobaleni, pixel-art.
- Leggibile a 28 px.

## Consegna

File frammento `agy3.html` in questa cartella, stesso formato dei giri precedenti, con **sei** varianti:
classi CSS prefissate `.a3-`, `data-choice` da "a3-1" a "a3-6" (a3-1 e a3-2 archi concentrici, a3-3 e a3-4
modulo a corsie, a3-5 e a3-6 nastro isometrico). Per ognuna: icona scura e chiara a 112 px, icona a 48 e 28
px, intestazione scura e chiara, nome e due-tre righe in italiano (quale riferimento, l'idea, quanti colori,
come l'icona si ritrova nel marchio).
