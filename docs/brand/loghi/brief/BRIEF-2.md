# Brief, secondo giro: sei varianti di logo per Quaestio

Leggi prima `BRIEF.md` in questa cartella: contesto del brand, colori, fondi, carattere, formato di consegna e regole
valgono ancora tutti. Questo file aggiunge il giudizio dell'utente sul primo giro e chiede **sei** varianti nuove.

## Il primo giro

Lo screenshot `giro1-sei.png` mostra le sei proposte (nell'ordine: Opus 1 Innesto, 2 Campo, 3 Sovrastampa; poi le tue
AG1 Registro, AG2 Taglio, AG3 Strati). I sorgenti sono `opus.html` e `agy.html` in questa cartella.

Cosa ha detto l'utente, parola per parola nel senso:

- **Opus 1 · Innesto**: gli piacciono **le righe orizzontali che entrano da sinistra e toccano "quaestio"
  nell'intestazione**. Non gli piace la "q" singola dell'icona.
- **Opus 3 · Sovrastampa**: l'idea della sovrastampa (due inchiostri che si sovrappongono e creano un terzo tono) è
  **interessante**, ma con solo due colori e con quel disegno di lettera non lo convince.
- **AG2 · Taglio**: carino, ma **un solo colore non lo convince**.
- **AG3 · Strati** (Q fatta di fasce orizzontali colorate): **forse quella che gli piace di più**, però la "Q" singola
  **non si ritrova nel marchio "quaestio"**: icona e intestazione non sembrano lo stesso logo.

## Cosa ne ricavo (vincoli per questo giro)

1. **Icona e marchio devono essere lo stesso segno.** Chi vede l'intestazione "quaestio" deve riconoscere l'icona, e
   viceversa. Il gesto dell'icona va trasposto nella parola (o nella q iniziale della parola), non solo accostato.
2. **Più di un colore**, ma con misura: da 3 a 5 inchiostri del brand (rosso `#d6414b`, blu `#3a6cb8`, ocra `#ebaf00`,
   verde acqua `#40a79b`, grigio `#d7d7d7`).
3. Direzioni da esplorare (scegli tu come ripartire le sei, almeno una per ciascuna):
   - **fasce orizzontali** che costruiscono o attraversano la lettera (sviluppo di AG3 e delle righe di Opus 1), con la
     parola "quaestio" attraversata o costruita dalle stesse fasce;
   - **sovrastampa** con 3–4 inchiostri (effetto risograph: `mix-blend-mode: multiply` sul chiaro, o toni di
     sovrapposizione calcolati a mano), con una lettera più forte di quella di Opus 3;
   - un'idea tua, diversa da queste.
4. Evita ancora: arcobaleni, pixel-art, la Q con coda a 45° che sembra una lente di ricerca, elementi appiccicati.
5. Tutte le icone devono reggere a 28 px.

## Consegna

File frammento HTML `agy2.html` in questa cartella, stesso formato del primo giro, con **sei** varianti:
classi CSS prefissate `.a2-`, `data-choice` da "a2-1" a "a2-6". Per ognuna: icona scura e chiara a 112 px, icona a
48 e 28 px, intestazione scura e chiara, nome e due-tre righe in italiano (idea, quanti colori, come l'icona si ritrova
nel marchio).
