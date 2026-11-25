# Brief: tre varianti di logo per Quaestio

Sei un graphic designer di scuola svizzera (tipografia internazionale: Müller-Brockmann, Crouwel, Vignelli).
Devi proporre **tre varianti di logo** per **Quaestio**, un'app di quiz per studiare (desktop, Android, iOS, web).

## Il contesto del brand

- Quaestio fa parte del brand personale **"lucafoglia"**, che nasce dal sito lucablog: stile svizzero, griglia esposta,
  angoli vivi, righe da 1 px al posto di ombre, colore **funzionale** (mai decorativo a caso).
- **Carattere**: Archivo variabile (pesi 400–900, larghezza 62–100%). Titoli pesanti e stretti
  (es. peso 850, larghezza 68–80%). Il file è `archivo-var.woff2` nella stessa cartella di questo brief.
- **Fondi**: scuro "Arena" `#0c0c0c` (testo `#f4f4f2`), chiaro "Grafite" `#e8e7e3` (testo `#141413`).
- **I colori di base del brand** (bande del menu di lucablog, in quest'ordine): rosso `#d6414b`, blu `#3a6cb8`,
  ocra `#ebaf00`, verde acqua `#40a79b`, grigio `#d7d7d7` (nel chiaro `#c4c3bd`). Sono toni medi, pieni ma smorzati,
  come inchiostri da stampa: non pastelli, non fluorescenti.
- Nell'intestazione dell'app l'utente ha già scelto come direzione: la parola **"quaestio"** in minuscolo, Archivo
  peso ~800 largo ~80%, preceduta da **cinque linee verticali sottili** nei cinque colori (3 px di larghezza, 2 px di
  distanza, alte quanto la x-height circa). Puoi tenerla, raffinarla o proporre un'alternativa coerente con l'icona.
- Il vecchio logo è un monogramma "Q" bianco su nero.

## Cosa vuole l'utente (parole sue)

- Unire i colori del brand nel logo, **ma senza esagerare**: "troppi diventerebbe un casino". Valuta tu quanti:
  i 5 di base, oppure 4 togliendo il grigio/bianco, o meno se il disegno è più forte.
- Tre proposte precedenti sono state giudicate **"piuttosto orribili"** (vedi `bocciate.png` in questa cartella):
  - L1: anello ellittico con la coda fatta di cinque fasce diagonali (sembrava un adesivo appiccicato, coda goffa);
  - L2: Q pixelata su griglia di quadrati con coda di quadratini colorati (sembra una O quadrata, effetto videogioco);
  - L3: anello fatto di 16 spicchi colorati con coda nera (ruota dei colori, arcobaleno, troppi colori).
  Evita questi errori: niente arcobaleni, niente effetto pixel-art, niente elementi appiccicati.

## Cosa consegnare

Un **unico file HTML frammento** (niente `<html>`, `<head>`, `<body>`: solo `<style>` + contenuto) nel percorso
indicato nel tuo compito. Il font va dichiarato così (il server lo serve da quel percorso):

```css
@font-face { font-family:'Archivo'; src:url('/files/archivo-var.woff2') format('woff2-variations'); font-weight:400 900; font-stretch:62% 100%; }
```

Per **ciascuna delle tre varianti**, una riga con:
1. l'**icona dell'app** in SVG inline (viewBox `0 0 112 112`, sfondo pieno, angoli arrotondati come icona di sistema,
   `border-radius` ~21%), su fondo **scuro** e su fondo **chiaro**, a 112 px;
2. la stessa icona a **48 px** e a **28 px** (deve restare leggibile e riconoscibile);
3. l'**intestazione dell'app** com'è oggi: una barra larga ~600 px, alta ~44 px, fondo scuro e poi fondo chiaro, con a
   sinistra il marchio (segno + "quaestio") e a destra il testo grigio "12 materie · 2483";
4. un **nome** breve e due-tre righe di spiegazione in italiano: l'idea, quanti colori usa e perché.

Ogni contenitore di variante deve avere `data-choice="<id>"` e `onclick="toggleSelect(this)"` (servono al visualizzatore).
Usa CSS proprio, con classi prefissate (es. `.op-` o `.ag-`) per non collidere con altri frammenti.

**Preferisci tracciati SVG (`<path>`) per la Q dell'icona** invece di `<text>`, così la forma è sotto controllo e non
dipende dal font; se usi `<text>` con Archivo, verifica che la forma regga. Le tre varianti devono essere **davvero
diverse tra loro** come idea, non tre ritocchi della stessa.

## Regole

- Scrivi **solo** il file richiesto. Non toccare nient'altro nel repository. Niente commit.
- Se qualcosa non è chiaro, decidi tu con buon gusto e annotalo nel testo della variante.
