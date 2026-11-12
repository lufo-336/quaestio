#!/usr/bin/env python3
"""Modifica affiancata italiano/inglese di un banco di domande.

    python3 -I generazione-domande/strumenti/coppie_it_en.py esporta <materiaId> <coppie.json>
    python3 -I generazione-domande/strumenti/coppie_it_en.py importa <materiaId> <coppie.json>

`esporta` scrive una lista con, per ogni domanda (nell'ordine del banco):
    {"n": 0, "it": {"domanda", "risposte", "spiegazione"}, "en": {...} | null}
Si modifica SOLO il testo dentro "it"/"en" (mai "n", mai il numero di risposte).
`importa` riporta le modifiche nel banco italiano e in traduzioni/en/<materiaId>.json
e ri-chiava le traduzioni (l'ID dipende dal testo: vedi converti_codice.py).
L'indice della risposta corretta non viene esportato e non cambia.
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from converti_codice import RADICE, genera_id, leggi, richiava, scrivi  # noqa: E402

CAMPI = ("domanda", "risposte", "spiegazione")


def banco(materia):
    materie = json.loads((RADICE / "materie.json").read_text(encoding="utf-8"))
    cartella = next(m["cartella"] for m in materie if m["id"] == materia)
    file_banco = sorted((RADICE / cartella).glob("*.json"))
    if len(file_banco) != 1:
        raise SystemExit(f"{materia}: serve un solo file di banco, trovati {len(file_banco)}")
    return file_banco[0], RADICE / f"traduzioni/en/{materia}.json"


def esporta(materia, out):
    p_it, p_en = banco(materia)
    it, _ = leggi(p_it)
    en, _ = leggi(p_en)
    coppie = []
    for n, d in enumerate(it):
        t = en["domande"].get(genera_id(d))
        coppie.append({
            "n": n,
            "it": {k: d.get(k, "") for k in CAMPI},
            "en": {k: t[k] for k in CAMPI if k in t} if t else None,
        })
    Path(out).write_text(json.dumps(coppie, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{materia}: {len(coppie)} domande esportate in {out}")


def importa(materia, src):
    p_it, p_en = banco(materia)
    it, f_it = leggi(p_it)
    en, f_en = leggi(p_en)
    coppie = json.loads(Path(src).read_text(encoding="utf-8"))
    if [c["n"] for c in coppie] != list(range(len(it))):
        raise SystemExit(f"{materia}: le coppie non corrispondono al banco (numero o ordine diverso)")
    ids = []
    for d, c in zip(it, coppie):
        vecchio = genera_id(d)
        if len(c["it"]["risposte"]) != len(d["risposte"]):
            raise SystemExit(f"{materia} n={c['n']}: numero di risposte cambiato")
        for k in CAMPI:
            d[k] = c["it"][k]
        t = en["domande"].get(vecchio)
        if t is not None and c["en"] is not None:
            if "risposte" in c["en"] and len(c["en"]["risposte"]) != len(d["risposte"]):
                raise SystemExit(f"{materia} n={c['n']}: numero di risposte inglesi cambiato")
            t.update(c["en"])
        ids.append((vecchio, genera_id(d)))
    n = richiava(en, ids, materia)
    scrivi(p_it, it, f_it)
    scrivi(p_en, en, f_en)
    print(f"{materia}: importato, {n} chiavi di traduzione spostate")


if __name__ == "__main__":
    if len(sys.argv) == 4 and sys.argv[1] in ("esporta", "importa"):
        (esporta if sys.argv[1] == "esporta" else importa)(sys.argv[2], sys.argv[3])
    else:
        raise SystemExit(__doc__)
