// ============================================================================
//  QUIZ ENGINE — motore generico riutilizzabile per generare banchi di domande
//  Vale per QUALSIASI materia. Tu (o un'IA) scrivi solo il file dati con le
//  domande; questo motore: valida, calcola le quote di lunghezza, mescola
//  posizione delle risposte e ordine delle domande, e scrive il JSON finale.
//
//  USO:
//    node quiz-engine.js <file-dati.js> <output.json> [--write] [--seed=N] [--simile=6]
//    node quiz-engine.js --audit <banco.json> [--simile=6]
//
//    - senza --write  -> DRY RUN: stampa solo l'audit (non scrive nulla)
//    - con  --write   -> scrive il JSON nello schema standard
//    - --audit        -> AUDIT di un banco JSON già generato (non scrive nulla)
//    - --seed=N       -> seme per il mescolamento riproducibile (default 12345)
//    - --simile=N     -> soglia (in caratteri) entro cui le 4 risposte sono
//                        considerate "di lunghezza simile" (default 6)
//
//  FORMATO DEL FILE DATI (es. miamateria.data.js):
//    module.exports = [
//      { c:"Categoria", d:"Testo domanda?", r:["opz0","opz1","opz2","opz3"], k:1,
//        t:"teoria"|"codice"|"architettura",
//        s:"Spiegazione: perche' la corretta e' giusta e (se utile) perche' le altre no." },
//      ...
//    ];
//    c=categoria  d=domanda  r=risposte (array)  k=indice 0-based della corretta  s=spiegazione
// ============================================================================
const fs = require('fs');
const path = require('path');

// ---------- calcolo lunghezza visibile ----------
// Restituisce la lunghezza senza backtick e senza le righe di apertura/chiusura fence (```lang o ```).
function lunghezzaVisibile(s) {
  if (typeof s !== 'string') return 0;
  const lines = s.split(/\r?\n/);
  const filtered = lines.filter(line => !line.trim().startsWith('```'));
  return filtered.join('\n').replace(/`/g, '').length;
}

// ---------- categoria di lunghezza della risposta corretta ----------
// L = la corretta e' la piu' LUNGA | S = la piu' CORTA | M = intermedia |
// Q = "simile": le 4 risposte stanno tutte entro SIMILE caratteri (nessun indizio di lunghezza)
function bucket(risposte, k, simile = 6) {
  if (!Array.isArray(risposte) || risposte.length === 0) return 'M';
  const len = risposte.map(x => lunghezzaVisibile(x));
  const max = Math.max(...len);
  const min = Math.min(...len);
  const lc = len[k];
  if (max - min <= simile) return 'Q';
  if (lc === max) return 'L';
  if (lc === min) return 'S';
  return 'M';
}

// ---------- validazione blocchi di codice e backtick ----------
// Restituisce una lista di problemi rilevati:
// "fence non chiuso", "backtick dispari", "blocco senza linguaggio"
function problemiCodice(testo) {
  if (typeof testo !== 'string') return [];
  const problemi = [];

  // 1. Verifica parità totale dei backtick
  const totalBackticks = (testo.match(/`/g) || []).length;
  if (totalBackticks % 2 !== 0) {
    problemi.push('backtick dispari');
  }

  // 2. Verifica fence dei blocchi di codice
  const lines = testo.split(/\r?\n/);
  let inFence = false;
  let bloccoSenzaLinguaggio = false;

  for (const line of lines) {
    const trimmed = line.trim();
    if (trimmed.startsWith('```')) {
      if (!inFence) {
        inFence = true;
        const lang = trimmed.slice(3).trim();
        if (!lang) {
          bloccoSenzaLinguaggio = true;
        }
      } else {
        inFence = false;
      }
    }
  }

  if (inFence) {
    problemi.push('fence non chiuso');
  }
  if (bloccoSenzaLinguaggio) {
    problemi.push('blocco senza linguaggio');
  }

  return problemi;
}

// ---------- rilevazione riferimenti al corso ----------
// Cerca parole sospette (case-insensitive, parola intera o prefisso):
// corso, lezion, slide, docente, prof, dispens, esercitazion, eserciz, es1..es99, laboratorio, compito
const PATTERNS_CORSO = [
  { id: 'corso', regex: /\bcors[oi]\b/i },
  { id: 'lezion', regex: /\blezion/i },
  { id: 'slide', regex: /\bslide/i },
  { id: 'docente', regex: /\bdocent/i },
  { id: 'prof', regex: /\bprof(?:\b|\.)/i },
  { id: 'dispens', regex: /\bdispens/i },
  { id: 'esercitazion', regex: /\besercitazion/i },
  { id: 'eserciz', regex: /\beserciz/i },
  { id: 'es1..es99', regex: /\bes[1-9]\d?\b/ },  // minuscolo: ES6 (ECMAScript) non e' un esercizio
  { id: 'laboratorio', regex: /\blaboratori/i },
  { id: 'compito', regex: /\bcompit[oi]\b/i },
];

function riferimentiCorso(testo) {
  if (typeof testo !== 'string') return [];
  const trovati = [];
  for (const p of PATTERNS_CORSO) {
    if (p.regex.test(testo)) {
      trovati.push(p.id);
    }
  }
  return trovati;
}

// ---------- audit completo di un banco domande ----------
// Accetta domande nello schema finale (o intermedio) e restituisce { buckets, posizioni, categorie, avvisi }
const TIPI = ['codice', 'teoria', 'architettura']; // come TipiDomanda.cs

function auditBanco(domande, simile = 6) {
  const buckets = { L: 0, S: 0, M: 0, Q: 0 };
  const posizioni = {};
  const categorie = {};
  const avvisi = [];

  if (!Array.isArray(domande)) {
    return { buckets, posizioni, categorie, avvisi };
  }

  domande.forEach((q, i) => {
    const id = q.id !== undefined ? q.id : (i + 1);
    const risposte = Array.isArray(q.risposte) ? q.risposte : (Array.isArray(q.r) ? q.r : []);
    const corretta = q.rispostaCorretta !== undefined ? q.rispostaCorretta : q.k;
    const cat = q.categoria || q.c || 'Senza categoria';
    const domandaTesto = typeof q.domanda === 'string' ? q.domanda : (typeof q.d === 'string' ? q.d : '');
    const spiegazioneTesto = typeof q.spiegazione === 'string' ? q.spiegazione : (typeof q.s === 'string' ? q.s : '');

    // Calcolo bucket lunghezza
    const b = bucket(risposte, corretta, simile);
    buckets[b] = (buckets[b] || 0) + 1;

    // Tipo trasversale (campo "tipo" nel JSON, "t" nel file dati)
    const tipo = q.tipo !== undefined ? q.tipo : q.t;
    if (!TIPI.includes(tipo)) avvisi.push(`#${id}: tipo mancante o non valido (${JSON.stringify(tipo)}; ammessi: ${TIPI.join(', ')})`);

    // Posizione risposta corretta
    if (typeof corretta === 'number') {
      posizioni[corretta] = (posizioni[corretta] || 0) + 1;
    }

    // Conteggio per categoria
    categorie[cat] = (categorie[cat] || 0) + 1;

    // Analisi codice e riferimenti al corso
    const campi = [
      { nome: 'domanda', testo: domandaTesto },
      ...risposte.map((r, rIdx) => ({ nome: `risposta ${rIdx + 1}`, testo: r })),
      { nome: 'spiegazione', testo: spiegazioneTesto }
    ];

    for (const campo of campi) {
      if (typeof campo.testo === 'string' && campo.testo) {
        const prob = problemiCodice(campo.testo);
        for (const p of prob) {
          avvisi.push(`#${id} (${campo.nome}): problema codice: ${p}`);
        }
        const rif = riferimentiCorso(campo.testo);
        for (const r of rif) {
          avvisi.push(`#${id} (${campo.nome}): riferimento al corso: ${r}`);
        }
      }
    }
  });

  return { buckets, posizioni, categorie, avvisi };
}

// ---------- PRNG deterministico (mulberry32) ----------
function mulberry32(a) {
  return function() {
    a |= 0; a = a + 0x6D2B79F5 | 0;
    let t = Math.imul(a ^ a >>> 15, 1 | a);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}

module.exports = {
  lunghezzaVisibile,
  bucket,
  problemiCodice,
  riferimentiCorso,
  auditBanco,
};

// ============================================================================
//  ESECUZIONE DA RIGA DI COMANDO (CLI)
// ============================================================================
if (require.main === module) {
  const args = process.argv.slice(2);
  const isAudit = args.includes('--audit') || args.some(a => a.startsWith('--audit='));
  const SIMILE = parseInt((args.find(a => a.startsWith('--simile=')) || '--simile=6').split('=')[1], 10);
  const seed = parseInt((args.find(a => a.startsWith('--seed=')) || '--seed=12345').split('=')[1], 10);
  const doWrite = args.includes('--write');

  if (isAudit) {
    // Nuova modalità CLI: node quiz-engine.js --audit <banco.json> [--simile=N]
    let auditFile = null;
    const auditArg = args.find(a => a.startsWith('--audit='));
    if (auditArg) {
      auditFile = auditArg.slice('--audit='.length);
    } else {
      const idx = args.indexOf('--audit');
      if (args[idx + 1] && !args[idx + 1].startsWith('--')) {
        auditFile = args[idx + 1];
      } else {
        auditFile = args.find(a => !a.startsWith('--'));
      }
    }

    if (!auditFile) {
      console.error('ERRORE: indicare il file banco JSON. Es: node quiz-engine.js --audit domande.json [--simile=6]');
      process.exit(1);
    }

    let banco;
    try {
      const raw = fs.readFileSync(path.resolve(process.cwd(), auditFile), 'utf8');
      banco = JSON.parse(raw.replace(/^\uFEFF/, ''));
    } catch (e) {
      console.error('ERRORE nel caricare il banco JSON:', e.message);
      process.exit(1);
    }

    const rep = auditBanco(banco, SIMILE);
    const N = banco.length;
    const pct = x => Math.round(x / (N || 1) * 100);

    console.log(`\n=== AUDIT (${N} domande, soglia "simile" = ${SIMILE} caratteri) ===`);
    console.log('Distribuzione lunghezza risposta corretta:');
    console.log(`  L piu' lunga : ${rep.buckets.L}  (${pct(rep.buckets.L)}%)`);
    console.log(`  S piu' corta : ${rep.buckets.S}  (${pct(rep.buckets.S)}%)`);
    console.log(`  M intermedia : ${rep.buckets.M}  (${pct(rep.buckets.M)}%)`);
    console.log(`  Q simile     : ${rep.buckets.Q}  (${pct(rep.buckets.Q)}%)`);

    const maxLetters = Math.max(...banco.map(o => (o.risposte || o.r || []).length), 4);
    const letters = Array.from({ length: maxLetters }, (_, i) => String.fromCharCode(65 + i));
    console.log('Posizione risposta corretta ' + letters.join('/') + ': ' + letters.map((_, i) => rep.posizioni[i] || 0).join(' / '));
    console.log('Domande per categoria: ' + Object.entries(rep.categorie).map(([k, v]) => `${k}=${v}`).join(', '));

    if (rep.avvisi.length) {
      console.log(`\nAvvisi (${rep.avvisi.length}):`);
      rep.avvisi.forEach(a => console.log('  - ' + a));
    } else {
      console.log('\nNessun avviso.');
    }
    return;
  }

  // Modalità standard: node quiz-engine.js <file-dati.js> <output.json> [--write] [--seed=N] [--simile=N]
  const nonFlagArgs = args.filter(a => !a.startsWith('--'));
  const dataPath = nonFlagArgs[0];
  const outPath = nonFlagArgs[1];

  if (!dataPath) {
    console.error('ERRORE: indicare il file dati. Es: node quiz-engine.js miamateria.data.js out.json [--write]');
    process.exit(1);
  }

  let DATA;
  try {
    DATA = require(path.resolve(process.cwd(), dataPath));
  } catch (e) {
    console.error('ERRORE nel caricare il file dati:', e.message);
    process.exit(1);
  }

  // ---------- validazione ----------
  const errs = [];
  DATA.forEach((q, i) => {
    const n = i + 1;
    if (!q.d || typeof q.d !== 'string') errs.push(`#${n}: domanda mancante`);
    if (!Array.isArray(q.r) || q.r.length < 2) errs.push(`#${n}: servono almeno 2 risposte`);
    if (typeof q.k !== 'number' || q.k < 0 || q.k >= (q.r ? q.r.length : 0)) errs.push(`#${n}: indice corretto (k) fuori range`);
    if (q.r && new Set(q.r).size !== q.r.length) errs.push(`#${n}: risposte duplicate`);
    if (!q.s) errs.push(`#${n}: spiegazione mancante`);
    if (!TIPI.includes(q.t)) errs.push(`#${n}: tipo (t) mancante o non valido (ammessi: ${TIPI.join(', ')})`);
  });
  const testi = DATA.map(q => q.d);
  testi.forEach((t, i) => { if (testi.indexOf(t) !== i) errs.push(`#${i + 1}: domanda duplicata ("${t.slice(0, 40)}...")`); });
  if (errs.length) { console.error('VALIDAZIONE FALLITA:\n - ' + errs.join('\n - ')); process.exit(1); }

  const rnd = mulberry32(seed >>> 0);
  function shuffle(arr) { for (let i = arr.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [arr[i], arr[j]] = [arr[j], arr[i]]; } return arr; }

  // ---------- costruzione + mescolamento ----------
  let out = DATA.map(q => {
    const b = bucket(q.r, q.k, SIMILE);
    return { categoria: q.c, tipo: q.t, domanda: q.d, risposte: q.r.slice(), rispostaCorretta: q.k, spiegazione: q.s, _b: b };
  });

  // mescola posizione delle risposte (la lunghezza/bucket NON cambiano)
  for (const o of out) {
    const t = o.risposte[o.rispostaCorretta];
    shuffle(o.risposte);
    o.rispostaCorretta = o.risposte.indexOf(t);
  }
  // mescola ordine delle domande e rinumera
  shuffle(out);
  out = out.map((o, i) => ({ id: i + 1, ...o }));

  // ---------- audit ----------
  const rep = auditBanco(out, SIMILE);
  const N = out.length;
  const pct = x => Math.round(x / (N || 1) * 100);

  console.log(`\n=== AUDIT (${N} domande, soglia "simile" = ${SIMILE} caratteri, seed = ${seed}) ===`);
  console.log('Distribuzione lunghezza risposta corretta:');
  console.log(`  L piu' lunga : ${rep.buckets.L}  (${pct(rep.buckets.L)}%)`);
  console.log(`  S piu' corta : ${rep.buckets.S}  (${pct(rep.buckets.S)}%)`);
  console.log(`  M intermedia : ${rep.buckets.M}  (${pct(rep.buckets.M)}%)`);
  console.log(`  Q simile     : ${rep.buckets.Q}  (${pct(rep.buckets.Q)}%)`);

  const maxLetters = Math.max(...out.map(o => o.risposte.length));
  const letters = Array.from({ length: maxLetters }, (_, i) => String.fromCharCode(65 + i));
  console.log('Posizione risposta corretta ' + letters.join('/') + ': ' + letters.map((_, i) => rep.posizioni[i] || 0).join(' / '));
  console.log('Domande per categoria: ' + Object.entries(rep.categorie).map(([k, v]) => `${k}=${v}`).join(', '));

  if (rep.avvisi.length) {
    console.log(`\nAvvisi (${rep.avvisi.length}):`);
    rep.avvisi.forEach(a => console.log('  - ' + a));
  }

  // ---------- scrittura o dry run ----------
  if (doWrite) {
    // Blocco della scrittura in presenza di fence non chiuso o backtick dispari
    const bloccanti = [];
    DATA.forEach((q, i) => {
      const n = i + 1;
      const campi = [
        { nome: 'domanda', testo: q.d },
        ...(q.r || []).map((r, rIdx) => ({ nome: `risposta ${rIdx + 1}`, testo: r })),
        { nome: 'spiegazione', testo: q.s }
      ];
      for (const c of campi) {
        if (typeof c.testo === 'string') {
          const prob = problemiCodice(c.testo).filter(p => p === 'fence non chiuso' || p === 'backtick dispari');
          if (prob.length) {
            bloccanti.push(`#${n} (${c.nome}): ${prob.join(', ')}`);
          }
        }
      }
    });

    if (bloccanti.length > 0) {
      console.error('\nSCRITTURA BLOCCATA: trovati problemi di codice non ammessi (fence non chiuso o backtick dispari):\n - ' + bloccanti.join('\n - '));
      process.exit(1);
    }

    if (!outPath) {
      console.error('\nERRORE: con --write serve anche il percorso di output.');
      process.exit(1);
    }
    const clean = out.map(o => ({ id: o.id, categoria: o.categoria, tipo: o.tipo, domanda: o.domanda, risposte: o.risposte, rispostaCorretta: o.rispostaCorretta, spiegazione: o.spiegazione }));
    fs.writeFileSync(path.resolve(process.cwd(), outPath), JSON.stringify(clean, null, 4), 'utf8');
    console.log(`\nOK -> scritto ${outPath} (${clean.length} domande).`);
  } else {
    console.log('\n(DRY RUN: nessun file scritto. Aggiungi --write per generare il JSON.)');
  }
}
