# Design — Estensione banca quiz Cloud Services e Container

> Approvato dall'utente il 18/07/2026 (approccio A). Serve come spec per la generazione.

## Obiettivo

Estendere `cloud_domande.json` da 100 a ~190 domande, con taglio orientato alle
**scelte architetturali** (il docente ha annunciato un quiz "sulla pratica").

## Requisiti chiave

1. **~70% delle nuove domande in stile scenario/scelta architetturale**: decidere tra
   IaaS/PaaS/serverless, VM vs container, ACI vs AKS vs Functions, Blob vs Table vs
   Cosmos vs Queue, event-driven sì/no, VNet/VPN, IaC, equivalenze AWS↔Azure,
   scaling (HPA/KEDA). ~30% teoria delle 13 dispense meno coperta (incluse Lez 12–13).
2. **Autosufficienza**: scenari generici ("un'azienda deve…"), nessun riferimento a
   esercizi specifici del corso. Le esercitazioni sono fonte dei *concetti*
   (binding, CORS, ACR, code, Cosmos serverless), non ambientazione.
3. **Approccio A — ricostruzione unica**: le 100 esistenti vengono convertite in
   `cloudservices.data.js` (sorgente oggi mancante), le nuove aggiunte lì, e il motore
   `MODELLO - quiz-engine.js` rigenera un unico JSON validato e mescolato.
   Gli `id` vengono rinumerati (sono solo progressivi).
4. **Categorie normalizzate** (10): Cloud Fundamentals · Azure · AWS · AWS vs Azure ·
   Container & Docker · Kubernetes · Serverless & Event-driven · Networking ·
   Terraform & IaC · Scenari architetturali. Mapping esistenti:
   "Microsoft Azure"→Azure, "Amazon Web Services"→AWS, "Azure vs AWS"→AWS vs Azure,
   "Cloud"→Cloud Fundamentals, "Docker"/"Container"→Container & Docker,
   "Terraform"→Terraform & IaC, "Serverless"→Serverless & Event-driven,
   "Scenario"/"Scenario Finale"/"Cloud Architecture"/"Architetture Cloud"→Scenari
   architetturali (con verifica caso per caso delle ultime due). Testo delle
   domande esistenti invariato.
5. **Bucket lunghezze L/S/M/Q ~25% ciascuno** sul totale (regola del MODELLO),
   verificati col DRY RUN del motore prima del `--write`.

## Fonti di verità

- `Manuale/Dispensa_Unica_Cloud_Services_e_Container.md` (13 lezioni)
- `Esercitazioni/Esercitazione 13072026/Manuale_Didattico_Architettura_Event-Driven.md`
- `Esercitazioni/Esercitazione Azure/` (AKS+ACR con Terraform)

## Distribuzione indicativa delle ~90 nuove

Scenari architetturali +25 · Serverless & Event-driven +15 · Azure +10 ·
AWS vs Azure +8 · Terraform & IaC +8 · Kubernetes +6 · Networking +6 · AWS +5 ·
Container & Docker +4 · Cloud Fundamentals +3 (aggiustabile in corso d'opera per
bilanciare i totali).

## Verifica finale

- DRY RUN: distribuzione L/S/M/Q ~25/25/25/25, posizioni A-D distribuite, nessun
  duplicato segnalato dal motore (che incrocia anche vecchie e nuove).
- JSON finale parsabile, ~190 domande, tutti i campi presenti, conteggi per
  categoria sensati.
- Round-trip sulle 100 vecchie: testo domanda/risposte/corretta/spiegazione
  identici all'originale (cambia solo `categoria` e l'ordine/id).
