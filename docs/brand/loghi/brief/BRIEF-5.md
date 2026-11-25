# Brief, quinto giro: sei varianti con regole nuove

Leggi prima `BRIEF.md` (contesto, colori, fondi, formato di consegna). I giri 2, 3 e 4 (`agy2.html`, `agy3.html`,
`agy4a.html`, `agy4b.html`, screenshot `giro4-dodici.png`) **non hanno soddisfatto** l'utente. I riferimenti visivi
restano i poster svizzeri `ispira-1-isometrico.png`, `ispira-2-beethoven.jpg`, `ispira-3-cerchi.png`.

## Regole nuove dell'utente (vincolanti)

1. **Almeno 4 colori del brand devono comparire** nel logo: rosso `#d6414b`, blu `#3a6cb8`, ocra `#ebaf00`,
   verde acqua `#40a79b`. Bianco `#f4f4f2`, grigio `#d7d7d7`/`#c4c3bd` e nero `#0c0c0c`/`#141413` servono solo a
   completare: lo spazio negativo, i fondi, i riempimenti. (Nelle proposte con 1–3 colori questa regola mancava.)
2. **La Q dell'icona e la parola "quaestio" devono essere nello stesso carattere.** La Q (o q) dell'icona è la stessa
   lettera che compare nella parola, dello stesso disegno: niente Q costruita a parte e parola in un altro font.
   Il colore e la costruzione geometrica possono vestire la lettera, ma la lettera è quella del font.
3. Leggibile a 28 px; niente lente di ricerca, niente mirino/bersaglio, niente arcobaleno sfumato.

## Caratteri da provare

- **Archivo** (quello di lucablog): `archivo-var.woff2`, pesi 400–900, larghezza 62–100%.
- **Un monospazio geometrico.** L'utente ha chiesto Euclid Mono (Swiss Typefaces: geometrico, la Q maiuscola è
  "cerchio + angolo retto"), che è a pagamento e non è disponibile qui. Usa come sostituto **Space Mono**
  (`SpaceMono-Regular.ttf`, `SpaceMono-Bold.ttf` in questa cartella, servito da `/files/SpaceMono-Bold.ttf`), e
  scrivi nel testo che è un sostituto di Euclid Mono.
- **Uno stencil geometrico.** L'utente ha chiesto Euclid Stencil (deriva da un geometrico bold; i tagli non seguono
  le regole dello stencil fisico ma creano "mosaici di frammenti", spesso tagli diagonali in direzioni opposte).
  Non è disponibile: **disegnalo tu** come tracciati SVG per le otto lettere che servono (q, u, a, e, s, t, i, o, più
  la Q maiuscola), geometriche e bold, con tagli che dividono le lettere in frammenti. I frammenti si prestano a
  ricevere i 4 colori.
- **Un carattere nuovo**: disegna tu un lettering geometrico originale per "quaestio" (tracciati SVG, costruito con
  cerchi, quarti di corona e barre come nei poster), da cui nasce la Q dell'icona.

## Sei varianti

- a5-1 e a5-2: **Archivo** (due idee diverse);
- a5-3: **monospazio** (Space Mono come sostituto di Euclid Mono);
- a5-4: **stencil** disegnato da te;
- a5-5 e a5-6: **carattere nuovo** disegnato da te (due idee diverse).

Per i caratteri caricati da file usa `@font-face` con `url('/files/<nome file>')`. Nei caratteri disegnati da te, la
parola dell'intestazione va composta con gli stessi tracciati della Q.

## Consegna

File `agy5.html` in questa cartella, stesso formato dei giri precedenti (icona scura e chiara a 112 px, 48 e 28 px,
intestazione scura e chiara, nome e due-tre righe in italiano: carattere usato, quali colori, come la Q e la parola
condividono il carattere). Classi CSS prefissate `.a5-`, `data-choice` da "a5-1" a "a5-6".
