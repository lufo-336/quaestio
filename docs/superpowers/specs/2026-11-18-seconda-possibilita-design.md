# Seconda possibilità sulla risposta sbagliata — design

Data: 2026-11-18. Richiesta da Luca; decisioni prese in conversazione.
Piccolo cambio, design breve. Segue (progetto separato) la partita sui punti deboli.

## Perché
Chi sbaglia oggi vede subito la risposta giusta e non può riprovare. Luca vuole una
modalità in cui, dopo uno sbaglio, si riprova subito la stessa domanda, e il risultato
conta come "mezzo errore" invece che come errore pieno.

## Comportamento (deciso da Luca)
Nuova opzione **Seconda possibilità** in Home, accanto a Rotazione (indipendente da essa).
1. Prima risposta sbagliata: nessuna soluzione né spiegazione. La risposta data va in rosso
   e si disattiva, le altre restano cliccabili ("ultima possibilità").
2. Seconda risposta giusta: **mezzo errore**. Seconda sbagliata: errore pieno, poi
   soluzione e spiegazione come oggi.
3. Con Rotazione attiva la domanda rientra in coda sia dopo errore pieno sia dopo mezzo
   errore. Quando riappare ha **un solo tentativo**: la seconda possibilità vale solo alla
   prima comparsa della domanda nella partita.

## Obiettivi / non obiettivi
- Sì: opzione persistita nelle preferenze come `Rotazione`; testi it/en; sessioni sospese e
  riprese funzionano.
- No: console (`wsa.quiz.cli`) non toccata; nessun terzo tentativo; la partita sui punti
  deboli è un altro progetto.
- Anti-obiettivo: i salvataggi esistenti (cronologia, sospesi, preferenze) devono caricarsi
  senza migrazioni.

## Approccio (una sola strada sensata)
Si estende la state machine esistente `SessioneQuiz`, come fa la Rotazione. Alternative
scartate: un secondo oggetto domanda per il retry (duplica lo stato), o salvare due
`DettaglioRisposta` per comparsa (falserebbe "domande viste" nelle statistiche).

Scelte mie, da controllare:
- Una comparsa = un solo `DettaglioRisposta`. `Corretta` = esito finale. Nuovo campo
  `SecondoTentativo` (bool, default false): vero se la comparsa ha richiesto il secondo
  colpo, sia riuscito sia no.
- `OpzioniQuiz.SecondaPossibilita` e `PreferenzeUtente.SecondaPossibilita`, default false.
- Peso errore (in `StatisticheService`): `Corretta && Tentativi==1` → 0,5 se
  `SecondoTentativo` altrimenti 0; tutto il resto 1 (regola attuale invariata). `PctErrore`
  = somma pesi / visti. `CategoriaStat.Errori` diventa decimale; "sbagliate" include i mezzi
  errori.
- Punteggio (`QuizService.CalcolaPunteggio`): corretta con `SecondoTentativo` = 7 punti
  (10 al primo colpo, 4 dopo rotazione).
- Rotazione: dopo un mezzo errore la domanda rientra in coda ma **non incrementa**
  `_tentativiPerId`, così alla ripresentazione `Tentativi==1` e, se azzeccata, non conta come
  un secondo errore (altrimenti peserebbe 1,5 in totale).
- Un solo tentativo alla ripresentazione: serve ricordare quali domande sono già apparse
  nella partita (un insieme di id, salvato anche nella pausa), perché dopo un mezzo errore
  `_tentativiPerId` non le registra. La seconda possibilità si offre solo se l'id non c'è.
- Pausa a metà secondo tentativo: alla ripresa la domanda riparte da zero (nessuno stato
  nuovo da salvare).
- Dettaglio cronologia: etichetta "corretta al secondo tentativo"; i quattro bottoni
  ricostruiti mostrano la risposta finale.

## Lascio al costruttore
Nome esatto delle stringhe, aspetto del bottone disattivato (stile già esistente per
`Sbagliata`), controllo che heatmap e Statistiche mostrino i decimali senza rumore,
test su `StatisticheService`/`CalcolaPunteggio` e una prova a mano su desktop e Android.
