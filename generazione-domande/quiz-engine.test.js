// Test del motore: node --test generazione-domande/
const test = require('node:test');
const assert = require('node:assert');
const { lunghezzaVisibile, bucket, problemiCodice, riferimentiCorso, auditBanco } = require('./quiz-engine.js');

test('lunghezzaVisibile ignora i backtick', () => {
  assert.strictEqual(lunghezzaVisibile('`abc`'), 3);
});

test('lunghezzaVisibile ignora le righe del fence', () => {
  assert.strictEqual(lunghezzaVisibile('```python\nx\n```'), 1);
});

test('bucket usa la lunghezza visibile', () => {
  assert.strictEqual(bucket(['`a`', 'bbbbbbbbbbbb', 'cc', 'dddddddddddddddddddd'], 0), 'S');
});

test('problemiCodice: fence non chiuso', () => {
  assert.ok(problemiCodice('```python\nx').includes('fence non chiuso'));
});

test('problemiCodice: backtick dispari', () => {
  assert.ok(problemiCodice('a `b').includes('backtick dispari'));
});

test('problemiCodice: blocco senza linguaggio', () => {
  assert.ok(problemiCodice('```\nx\n```').includes('blocco senza linguaggio'));
});

test('problemiCodice: testo pulito', () => {
  assert.deepStrictEqual(problemiCodice('Cosa fa `x`?\n```python\nprint(1)\n```'), []);
});

test('riferimentiCorso: lezione', () => {
  assert.deepStrictEqual(riferimentiCorso('Come visto a lezione'), ['lezion']);
});

test('riferimentiCorso: esercizio nel codice', () => {
  assert.deepStrictEqual(riferimentiCorso('# esercizio 3'), ['eserciz']);
});

test('riferimentiCorso: prof solo come parola intera', () => {
  assert.deepStrictEqual(riferimentiCorso('profilo utente'), []);
  assert.deepStrictEqual(riferimentiCorso('chiedi al prof.'), ['prof']);
});

test('auditBanco conta i bucket nello schema JSON finale', () => {
  const q = (risposte, k) => ({ categoria: 'X', domanda: 'D' + risposte.join(), risposte, rispostaCorretta: k, spiegazione: 's' });
  const banco = [
    q(['corta', 'una risposta decisamente molto lunga', 'media lunghezza', 'xx'], 1),        // L
    q(['corta', 'una risposta decisamente molto lunga', 'media lunghezza', 'xx'], 3),        // S
    q(['corta', 'una risposta decisamente molto lunga', 'media lunghezza', 'xx'], 2),        // M
    q(['uno', 'due', 'tre', 'quattro'], 0),                                                  // Q
  ];
  assert.deepStrictEqual(auditBanco(banco).buckets, { L: 1, S: 1, M: 1, Q: 1 });
});
