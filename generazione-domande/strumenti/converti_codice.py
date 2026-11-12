#!/usr/bin/env python3
"""Conversione one-shot del codice nelle domande al formato a blocchi.

Uso (dalla radice del repo):
    python3 -I generazione-domande/strumenti/converti_codice.py
        Python: codice dopo la prima riga vuota -> blocco ```python;
        risposte di output tra backtick; C#: ``` -> ```csharp.
    python3 -I generazione-domande/strumenti/converti_codice.py --richiava <materiaId> <banco_vecchio.json>
        Dopo una modifica a mano di un banco: confronta vecchio e attuale per
        posizione e sposta le chiavi di traduzioni/en/<materiaId>.json sui nuovi ID.

Perche' serve ri-chiavare: l'ID di una domanda (StorageService.GeneraId) e'
SHA256(testo|risposte unite da \\u0001|indice corretto), primi 6 byte in hex.
Cambiare il testo cambia l'ID e la traduzione inglese non si aggancia piu'.
Idempotente: rilanciato su dati gia' convertiti non cambia nulla.
"""
import hashlib
import json
import re
import sys
from pathlib import Path

RADICE = Path(__file__).resolve().parents[2]
PROSA = re.compile(r"^[A-ZÀ-Ý][a-zà-ÿ]+ [a-zà-ÿ]")


def genera_id(d):
    raw = f"{d['domanda']}|{chr(1).join(d['risposte'])}|{d['rispostaCorretta']}"
    return hashlib.sha256(raw.encode("utf-8")).digest()[:6].hex()


def leggi(percorso):
    testo = percorso.read_text(encoding="utf-8")
    dati = json.loads(testo)
    for indent in (2, 4):
        if json.dumps(dati, ensure_ascii=False, indent=indent) == testo.rstrip("\n"):
            return dati, (indent, testo.endswith("\n"))
    raise SystemExit(f"Formato JSON di {percorso} non riconosciuto (indent 2/4)")


def scrivi(percorso, dati, formato):
    indent, nl_finale = formato
    s = json.dumps(dati, ensure_ascii=False, indent=indent)
    percorso.write_text(s + ("\n" if nl_finale else ""), encoding="utf-8")


def in_blocco_python(testo):
    if "```" in testo or "\n\n" not in testo:
        return testo
    testa, codice = testo.split("\n\n", 1)
    return f"{testa}\n\n```python\n{codice}\n```"


# Risposte che sono parole italiane, non output.
PAROLE = {"Due", "Una", "Nessuna", "Niente", "(stringa vuota)"}
# "Solleva X: motivo" -> solo il nome dell'eccezione e' codice.
VERBO_ECCEZIONE = re.compile(r"^(Solleva|Raises) ([A-Za-z_]\w*)(.*)$", re.S)
# "Stampa X" / "Restituisce X" -> X e' codice, se non e' una frase.
VERBO_VALORE = re.compile(r"^(Stampa|Prints|Restituisce|Returns) (.+)$", re.S)
FRASE = re.compile(r"^[a-zà-ÿ]+ [a-zà-ÿ]")

# Risposte miste testo+codice decise a mano: italiano originale -> (it, en).
MANUALI = {
    "x e poi x": ("`x` e poi `x`", "`x` and then `x`"),
    "x e poi una riga vuota": ("`x` e poi una riga vuota", "`x` and then an empty line"),
    "None e poi x": ("`None` e poi `x`", "`None` and then `x`"),
    "x e poi None": ("`x` e poi `None`", "`x` and then `None`"),
    "pari e poi nessun pari": ("`pari` e poi `nessun pari`", "`pari` and then `nessun pari`"),
    "Nessuna: stampa 0": ("Nessuna: stampa `0`", "None: prints `0`"),
    "Nessuna: stampa None": ("Nessuna: stampa `None`", "None: prints `None`"),
    "Funziona: a vale 1 e b vale 2": ("Funziona: `a` vale 1 e `b` vale 2", "Works: `a` is 1 and `b` is 2"),
    "Funziona: a vale 2 e b vale 1": ("Funziona: `a` vale 2 e `b` vale 1", "Works: `a` is 2 and `b` is 1"),
    "Funziona, ma b.start() solleverà un errore": ("Funziona, ma `b.start()` solleverà un errore", "Works, but `b.start()` will raise an error"),
    "Funziona, start() restituisce None": ("Funziona, `start()` restituisce `None`", "Works, `start()` returns `None`"),
    "ProgrammingError: connessione chiusa": ("`ProgrammingError`: connessione chiusa", "`ProgrammingError`: connection closed"),
    "Una tupla vuota ()": ("Una tupla vuota `()`", "An empty tuple `()`"),
    "Una tupla: (1, 2.5)": ("Una tupla: `(1, 2.5)`", "A tuple: `(1, 2.5)`"),
    "Crea un dizionario con chiave [1, 2]": ("Crea un dizionario con chiave `[1, 2]`", "Creates a dictionary with key `[1, 2]`"),
    "Ciao ospite Ciao Anna": ("`Ciao ospite Ciao Anna`", "`Ciao ospite Ciao Anna`"),
    "Viene creata una nuova stringa Ciao assegnata a s": ("Viene creata una nuova stringa `Ciao` assegnata a `s`", "A new string `Ciao` is created and assigned to `s`"),
}


def con_verbo(r):
    """Applica le regole 'Solleva X' / 'Stampa X'; None se non si applicano."""
    m = VERBO_ECCEZIONE.match(r)
    if m:
        return f"{m.group(1)} `{m.group(2)}`{m.group(3)}"
    m = VERBO_VALORE.match(r)
    if m and not FRASE.match(m.group(2)):
        return f"{m.group(1)} `{m.group(2)}`"
    return None


def converti_risposta(r_it, r_en):
    """(nuova it, nuova en) per una risposta di una domanda con codice."""
    if "`" in r_it:
        return r_it, r_en
    if r_it in MANUALI:
        return MANUALI[r_it]
    if r_it in PAROLE:
        return r_it, r_en
    v = con_verbo(r_it)
    if v is not None:
        return v, (con_verbo(r_en) or r_en) if r_en else r_en
    if VERBO_VALORE.match(r_it) or PROSA.match(r_it):
        return r_it, r_en
    return f"`{r_it}`", (f"`{r_en}`" if r_en and "`" not in r_en else r_en)


def fence_csharp(testo):
    # Solo i fence di apertura (pari: 0, 2, ...) senza linguaggio diventano ```csharp.
    righe = testo.split("\n")
    aperto = False
    for i, riga in enumerate(righe):
        if riga.startswith("```"):
            if not aperto and riga.strip() == "```":
                righe[i] = "```csharp"
            aperto = not aperto
    return "\n".join(righe)


def richiava(trad, coppie, materia):
    """coppie: lista (id_vecchio, id_nuovo). Sposta le voci di trad['domande']."""
    nuove_ids = [n for _, n in coppie]
    if len(set(nuove_ids)) != len(nuove_ids):
        raise SystemExit(f"{materia}: due domande producono lo stesso nuovo ID")
    vecchie = trad["domande"]
    mappa = dict(coppie)
    orfane = [k for k in vecchie if k not in mappa]
    if orfane:
        raise SystemExit(f"{materia}: chiavi di traduzione senza domanda: {orfane[:5]}")
    trad["domande"] = {mappa[k]: v for k, v in vecchie.items()}
    return sum(1 for k in vecchie if mappa[k] != k)


def converti_python():
    p_it = RADICE / "domande/python/python_domande.json"
    p_en = RADICE / "traduzioni/en/python.json"
    it, f_it = leggi(p_it)
    en, f_en = leggi(p_en)
    coppie, avvolte, lasciate = [], [], []
    for d in it:
        vecchio = genera_id(d)
        t = en["domande"].get(vecchio)
        nuovo_testo = in_blocco_python(d["domanda"])
        if nuovo_testo != d["domanda"]:
            d["domanda"] = nuovo_testo
            if t and t.get("domanda"):
                t["domanda"] = in_blocco_python(t["domanda"])
            for i, r in enumerate(d["risposte"]):
                r_en = t["risposte"][i] if t and t.get("risposte") else None
                n_it, n_en = converti_risposta(r, r_en)
                d["risposte"][i] = n_it
                if r_en is not None:
                    t["risposte"][i] = n_en
                (avvolte if n_it != r else lasciate).append(f"{n_it}  ||  {n_en}")
        coppie.append((vecchio, genera_id(d)))
    n = richiava(en, coppie, "python")
    scrivi(p_it, it, f_it)
    scrivi(p_en, en, f_en)
    print(f"python: {n} chiavi di traduzione spostate")
    print("\n== Risposte CAMBIATE (it || en) ==")
    for r in avvolte:
        print("  " + r)
    print("\n== Risposte lasciate come PROSA ==")
    for r in lasciate:
        print("  " + r)


def converti_csharp():
    p_it = RADICE / "domande/cs/csharp_domande_unificate.json"
    p_en = RADICE / "traduzioni/en/cs.json"
    it, f_it = leggi(p_it)
    en, f_en = leggi(p_en)
    coppie = []
    for d in it:
        vecchio = genera_id(d)
        nuovo_testo = fence_csharp(d["domanda"])
        if nuovo_testo != d["domanda"]:
            d["domanda"] = nuovo_testo
            t = en["domande"].get(vecchio)
            if t and t.get("domanda"):
                t["domanda"] = fence_csharp(t["domanda"])
        coppie.append((vecchio, genera_id(d)))
    n = richiava(en, coppie, "cs")
    scrivi(p_it, it, f_it)
    scrivi(p_en, en, f_en)
    print(f"cs: {n} chiavi di traduzione spostate")


def richiava_da_vecchio(materia, vecchio_path):
    materie = json.loads((RADICE / "materie.json").read_text(encoding="utf-8"))
    cartella = next(m["cartella"] for m in materie if m["id"] == materia)
    file_banco = sorted((RADICE / cartella).glob("*.json"))
    if len(file_banco) != 1:
        raise SystemExit(f"{materia}: --richiava supporta un solo file di banco, trovati {len(file_banco)}")
    attuale, _ = leggi(file_banco[0])
    vecchio = json.loads(Path(vecchio_path).read_text(encoding="utf-8"))
    if len(vecchio) != len(attuale):
        raise SystemExit(f"{materia}: {len(vecchio)} domande nel vecchio, {len(attuale)} nell'attuale")
    p_en = RADICE / f"traduzioni/en/{materia}.json"
    en, f_en = leggi(p_en)
    n = richiava(en, [(genera_id(v), genera_id(a)) for v, a in zip(vecchio, attuale)], materia)
    scrivi(p_en, en, f_en)
    print(f"{materia}: {n} chiavi di traduzione spostate")


if __name__ == "__main__":
    if len(sys.argv) == 4 and sys.argv[1] == "--richiava":
        richiava_da_vecchio(sys.argv[2], sys.argv[3])
    elif len(sys.argv) == 1:
        converti_python()
        converti_csharp()
    else:
        raise SystemExit(__doc__)
