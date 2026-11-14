# Repo pubblico ordinato — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax for tracking. Le fasi di scrittura (Task 2, 4) vanno ad Antigravity via `~/.claude/skills/antigravity/scripts/agy_run.py`, le altre le fa Claude.

**Goal:** preparare `lufo-336/quaestio` a diventare pubblico: README inglese con screenshot e diagramma, LICENSE, doc di lavoro ripuliti, metadati GitHub; poi (su via libera) visibilità pubblica.

**Architecture:** solo documentazione e asset; nessun codice dell'app cambia.

**Tech Stack:** Markdown, Mermaid, PNG; `gh` CLI; testa browser (WASM) e simulatore iOS per gli screenshot.

**Spec:** `docs/superpowers/specs/2026-11-14-repo-pubblico-design.md`

## Global Constraints

- README in **inglese**; doc di lavoro (`CLAUDE.md`, `docs/claude/`, `docs/superpowers/`) restano **in italiano** e **pubblici**.
- Niente riscrittura della storia git; l'email `luca.foglia@edu-its.it` nei commit resta.
- Nessuna rinomina di codice/namespace/cartelle (`Wsa.Quiz.*`, `wsa.quiz.*`, `WsaQuiz`, `it.eduits.wsaquiz`).
- Tono sobrio e onesto: l'app è **vibe-coded** con l'IA, lo storico è il percorso di un dilettante. Niente marketing.
- Nessun ringraziamento nominale: solo un ringraziamento generico ai compagni di corso che hanno provato l'app.
- Licenza MIT, `Copyright (c) 2026 Luca Foglia`.
- Link: web `https://lufo-336.github.io/quaestio/`, APK `https://github.com/lufo-336/quaestio/releases/latest`.
- `.gitignore` e cambio di visibilità solo nel Task 6, solo su ok esplicito di Luca.

## Esito revisione dati sensibili (fatta il 2026-11-14, prima del piano)

- Nessun segreto vero (chiavi, token, password, keystore, file di firma) in tutta la storia; nessun blob > 500 KB; `.git` 7,9 MB. Le occorrenze di "password/token/secret" sono contenuto delle domande.
- Note delle Release: pulite.
- Nei file attuali, da togliere (Task 2): l'email iCloud e l'identità Apple Development (identità di firma Apple Development con Apple ID e team ID) in `docs/claude/piattaforme-e-distribuzione.md:237` → sostituire con un segnaposto (`"Apple Development: <tuo Apple ID> (<TEAM>)"`); percorsi con nomi utente di macchina (utenti Windows/Linux delle macchine personali) in `docs/claude/LEGGIMI-*.md` e in 5 piani di `docs/superpowers/plans/`.
- Nello storico restano (commit `5027f8f` per l'identità Apple): accettato, niente riscrittura. **Da riferire a Luca** nel rapporto finale.

## Review Focus

- Link relativi del README (immagini `docs/images/*.png`, `docs/claude/trappole.md`, `generazione-domande/…`) che puntano a file inesistenti → devono risolversi tutti.
- Il diagramma Mermaid con sintassi non valida → GitHub mostra un errore al posto del disegno.
- Fatti numerici superati nel README (materie, domande, versione, piattaforme) → devono coincidere con il repo (`materie.json`, `Directory.Build.props`).
- Dati personali reintrodotti dalla pulizia (percorsi, email) → `git grep` deve tornare vuoto.
- Comandi di build nel README non validi su una macchina pulita → devono coincidere con `docs/claude/piattaforme-e-distribuzione.md`.

---

### Task 1: Screenshot (Claude)

**Files:**
- Create: `docs/images/home-desktop.png`, `docs/images/home-mobile.png`, `docs/images/statistics.png`, `docs/images/history.png`, `docs/images/quiz.png`

**Interfaces:**
- Produces: i cinque file sopra, usati dal Task 3.

- [ ] **Step 1:** Avviare la testa browser in locale (comando in `docs/claude/piattaforme-e-distribuzione.md`), aprirla in Chrome a ~1280×800, interfaccia in inglese.
- [ ] **Step 2:** Giocare 3-4 partite brevi su materie diverse (almeno una con errori) per popolare Cronologia e Statistiche.
- [ ] **Step 3:** Catturare Home, Statistiche, Cronologia e una domanda con blocco di codice (es. Python o C#) → PNG larghezza ~1200 px.
- [ ] **Step 4:** Simulatore iOS (iPhone, via `simctl`, vedi memoria `quaestio-ios-mac-ambiente`): Home in inglese → `home-mobile.png`.
- [ ] **Step 5:** Verifica: `ls -la docs/images` mostra 5 PNG, ciascuno < 600 KB (ridurre con `sips` se serve).
- [ ] **Step 6:** Commit `docs(images): screenshot per il README` + push.

### Task 2: Pulizia dei doc di lavoro (agy, ruolo doc)

**Files:**
- Modify: `CLAUDE.md`, `docs/claude/*.md`, i 5 piani in `docs/superpowers/plans/` con percorsi personali
- Create: `docs/README.md` (inglese)
- Possibile delete: `docs/claude/LEGGIMI-memoria-junction.md`, `docs/claude/LEGGIMI-multimacchina.md`

- [ ] **Step 1:** Lanciare `agy_run.py --role doc --timeout 900s` con prompt: applicare la sezione "Pulizia dei documenti di lavoro" della spec e i punti "da togliere" dell'Esito revisione di questo piano. In `CLAUDE.md`: aggiornare layout repo (11 materie, teste `desktop/android/ios/browser`, `wsa.quiz.core.tests`, `generazione-domande/`, `traduzioni/`), togliere la frase su `dotnet run --project wsa.quiz.app`. I due `LEGGIMI-*`: eliminarli se descrivono solo la configurazione delle macchine di Luca (junction della memoria Claude, sincronizzazione), togliendo anche i rimandi a loro. Contenuto tecnico (trappole, architettura) intatto. Piani in `docs/superpowers/`: sostituire solo i percorsi personali con forme neutre (`<repo>`, `%LOCALAPPDATA%\Android\Sdk`, `~/…`). `docs/README.md`: 10-20 righe in inglese che spiegano `claude/` (contesto che Claude Code legge a ogni sessione), `superpowers/` (spec e piani scritti prima di ogni lavoro), `images/`; tutti in italiano. Niente commit.
- [ ] **Step 2:** Verifica (Claude): `git grep -n -I -E "<nomi utente e Apple ID personali>" -- docs CLAUDE.md` → solo la riga della spec sull'email dei commit. `git grep -n "LEGGIMI-"` → vuoto se eliminati. `git diff --stat` coerente; leggere il diff di `CLAUDE.md` e `docs/claude/trappole.md` (devono restare tecnicamente intatti).
- [ ] **Step 3:** Commit `docs: pulizia dei documenti di lavoro per il repo pubblico` + push.

### Task 3: README inglese + LICENSE (agy, ruolo doc)

**Files:**
- Modify: `README.md` (riscritto da zero, in inglese)
- Create: `LICENSE`

**Interfaces:**
- Consumes: `docs/images/*.png` (Task 1), `docs/README.md` (Task 2).

- [ ] **Step 1:** Lanciare `agy_run.py --role doc --timeout 900s` con prompt: scrivere `README.md` seguendo **in ordine** le 15 sezioni di "Contenuto del README" della spec, con le immagini di `docs/images/` (Home desktop e mobile affiancate in alto, le altre nella sezione Features), diagramma Mermaid come da sezione "Diagramma" della spec, fonti fattuali = README attuale, `docs/claude/*.md`, `materie.json`, `Directory.Build.props`, `generazione-domande/GUIDA_GENERAZIONE_DOMANDE.md`, `wsa.quiz.core/Models/Domanda.cs` (esempio JSON con `tipo`). Rispettare i Global Constraints di questo piano. Creare `LICENSE` MIT standard. Niente commit.
- [ ] **Step 2:** Verifica (Claude): ogni link relativo del README esiste (`grep -o '](\([^)h][^)]*\)' README.md` → `ls` di ciascuno); numeri materie/domande uguali a quelli calcolati da `materie.json` + JSON in `domande/`; Mermaid valido (`npx -y @mermaid-js/mermaid-cli` se disponibile, altrimenti lettura attenta).
- [ ] **Step 3:** Revisione indipendente: `agy_run.py --role review --plan-file docs/superpowers/plans/2026-11-14-repo-pubblico.md` (se la quota Argon è esaurita, revisione di Claude sul solo README). Correggere i punti ALTA/MEDIA.
- [ ] **Step 4:** Commit `docs: README in inglese e licenza MIT` + push.

### Task 4: Metadati GitHub (Claude)

- [ ] **Step 1:** `gh repo edit lufo-336/quaestio --description "Multi-subject quiz app for studying, built with C#/.NET 10 and Avalonia — desktop, Android, iOS and web. A student's vibe-coded learning project." --homepage https://lufo-336.github.io/quaestio/ --add-topic csharp,dotnet,avalonia,avaloniaui,quiz,education,cross-platform,webassembly`
- [ ] **Step 2:** Verifica: `gh repo view --json description,homepageUrl,repositoryTopics`.

### Task 5: Rapporto a Luca (Claude)

- [ ] **Step 1:** Riferire: esito revisione dati sensibili (cosa resta nello storico: identità Apple in `5027f8f`, percorsi nei piani, email edu); link al README su GitHub; cosa manca (Task 6).
- [ ] **Step 2:** Aggiornare memoria roadmap e `docs/claude/storia-e-roadmap.md` (punto 6: fatto fino al Task 5).

### Task 6: Ultimo passo — SOLO su via libera esplicito di Luca (Claude)

- [ ] **Step 1:** `.gitignore`: aggiungere `.remember/`, `.revisione/`, `.superpowers/`, `.claude/`. Verifica: `git status --short` non li mostra più.
- [ ] **Step 2:** Commit + push; `gh repo edit lufo-336/quaestio --visibility public --accept-visibility-change-consequences`.
- [ ] **Step 3:** Verifica: `gh repo view --json visibility` → `PUBLIC`; il sito Pages risponde.
