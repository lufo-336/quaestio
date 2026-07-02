// ============================================================================
//  QUIZ ENGINE — motore generico riutilizzabile per generare banchi di domande
//  Vale per QUALSIASI materia. Tu (o un'IA) scrivi solo il file dati con le
//  domande; questo motore: valida, calcola le quote di lunghezza, mescola
//  posizione delle risposte e ordine delle domande, e scrive il JSON finale.
//
//  USO:
//    node "MODELLO - quiz-engine.js" <file-dati.js> <output.json> [--write] [--seed=N] [--simile=6]
//
//    - senza --write  -> DRY RUN: stampa solo l'audit (non scrive nulla)
//    - con  --write   -> scrive il JSON nello schema standard
//    - --seed=N       -> seme per il mescolamento riproducibile (default 12345)
//    - --simile=N     -> soglia (in caratteri) entro cui le 4 risposte sono
//                        considerate "di lunghezza simile" (default 6)
//
//  FORMATO DEL FILE DATI (es. miamateria.data.js):
//    module.exports = [
//      { c:"Categoria", d:"Testo domanda?", r:["opz0","opz1","opz2","opz3"], k:1,
//        s:"Spiegazione: perche' la corretta e' giusta e (se utile) perche' le altre no." },
//      ...
//    ];
//    c=categoria  d=domanda  r=risposte (array)  k=indice 0-based della corretta  s=spiegazione
// ============================================================================
const fs = require('fs');
const path = require('path');

const args = process.argv.slice(2);
const dataPath = args.find(a => !a.startsWith('--'));
const outPath  = args.filter(a => !a.startsWith('--'))[1];
const doWrite  = args.includes('--write');
const seed     = parseInt((args.find(a=>a.startsWith('--seed=')) || '--seed=12345').split('=')[1], 10);
const SIMILE   = parseInt((args.find(a=>a.startsWith('--simile=')) || '--simile=6').split('=')[1], 10);

if (!dataPath) {
  console.error('ERRORE: indicare il file dati. Es: node "MODELLO - quiz-engine.js" miamateria.data.js out.json --write');
  process.exit(1);
}

let DATA;
try { DATA = require(path.resolve(process.cwd(), dataPath)); }
catch (e) { console.error('ERRORE nel caricare il file dati:', e.message); process.exit(1); }

// ---------- validazione ----------
const errs = [];
DATA.forEach((q, i) => {
  const n = i + 1;
  if (!q.d || typeof q.d !== 'string') errs.push(`#${n}: domanda mancante`);
  if (!Array.isArray(q.r) || q.r.length < 2) errs.push(`#${n}: servono almeno 2 risposte`);
  if (typeof q.k !== 'number' || q.k < 0 || q.k >= (q.r ? q.r.length : 0)) errs.push(`#${n}: indice corretto (k) fuori range`);
  if (q.r && new Set(q.r).size !== q.r.length) errs.push(`#${n}: risposte duplicate`);
  if (!q.s) errs.push(`#${n}: spiegazione mancante`);
});
const testi = DATA.map(q => q.d);
testi.forEach((t, i) => { if (testi.indexOf(t) !== i) errs.push(`#${i+1}: domanda duplicata ("${t.slice(0,40)}...")`); });
if (errs.length) { console.error('VALIDAZIONE FALLITA:\n - ' + errs.join('\n - ')); process.exit(1); }

// ---------- categoria di lunghezza della risposta corretta ----------
// L = la corretta e' la piu' LUNGA | S = la piu' CORTA | M = intermedia |
// Q = "simile": le 4 risposte stanno tutte entro SIMILE caratteri (nessun indizio di lunghezza)
function bucket(r, k) {
  const len = r.map(x => x.length);
  const max = Math.max(...len), min = Math.min(...len), lc = len[k];
  if (max - min <= SIMILE) return 'Q';
  if (lc === max) return 'L';
  if (lc === min) return 'S';
  return 'M';
}

// ---------- PRNG deterministico (mulberry32) ----------
function mulberry32(a){ return function(){ a|=0; a=a+0x6D2B79F5|0; let t=Math.imul(a^a>>>15,1|a); t=t+Math.imul(t^t>>>7,61|t)^t; return ((t^t>>>14)>>>0)/4294967296; }; }
const rnd = mulberry32(seed >>> 0);
function shuffle(arr){ for(let i=arr.length-1;i>0;i--){ const j=Math.floor(rnd()*(i+1)); [arr[i],arr[j]]=[arr[j],arr[i]]; } return arr; }

// ---------- costruzione + audit ----------
const counts = {L:0,S:0,M:0,Q:0};
let out = DATA.map(q => { const b = bucket(q.r, q.k); counts[b]++; return {categoria:q.c, domanda:q.d, risposte:q.r.slice(), rispostaCorretta:q.k, spiegazione:q.s, _b:b}; });

// mescola posizione delle risposte (la lunghezza/bucket NON cambiano)
for (const o of out){ const t = o.risposte[o.rispostaCorretta]; shuffle(o.risposte); o.rispostaCorretta = o.risposte.indexOf(t); }
// mescola ordine delle domande e rinumera
shuffle(out);
out = out.map((o,i)=>({ id:i+1, ...o }));

const N = out.length;
const pct = x => Math.round(x/N*100);
console.log(`\n=== AUDIT (${N} domande, soglia "simile" = ${SIMILE} caratteri, seed = ${seed}) ===`);
console.log('Distribuzione lunghezza risposta corretta:');
console.log(`  L piu' lunga : ${counts.L}  (${pct(counts.L)}%)`);
console.log(`  S piu' corta : ${counts.S}  (${pct(counts.S)}%)`);
console.log(`  M intermedia : ${counts.M}  (${pct(counts.M)}%)`);
console.log(`  Q simile     : ${counts.Q}  (${pct(counts.Q)}%)`);
const pos = {}; out.forEach(o => pos[o.rispostaCorretta] = (pos[o.rispostaCorretta]||0)+1);
const maxLetters = Math.max(...out.map(o => o.risposte.length));
const letters = Array.from({length:maxLetters}, (_,i)=> String.fromCharCode(65+i));
console.log('Posizione risposta corretta ' + letters.join('/') + ': ' + letters.map((_,i)=> pos[i]||0).join(' / '));
const cat = {}; out.forEach(o => cat[o.categoria]=(cat[o.categoria]||0)+1);
console.log('Domande per categoria: ' + Object.entries(cat).map(([k,v])=>`${k}=${v}`).join(', '));

if (doWrite){
  if (!outPath){ console.error('\nERRORE: con --write serve anche il percorso di output.'); process.exit(1); }
  const clean = out.map(o => ({ id:o.id, categoria:o.categoria, domanda:o.domanda, risposte:o.risposte, rispostaCorretta:o.rispostaCorretta, spiegazione:o.spiegazione }));
  fs.writeFileSync(path.resolve(process.cwd(), outPath), JSON.stringify(clean, null, 4), 'utf8');
  console.log(`\nOK -> scritto ${outPath} (${clean.length} domande).`);
} else {
  console.log('\n(DRY RUN: nessun file scritto. Aggiungi --write per generare il JSON.)');
}
