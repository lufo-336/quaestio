# Quaestio — memoria di Claude Code condivisa fra macchine

> Creato l'**8 settembre 2026**. Stesso meccanismo già in uso per *ITS Corso* dal
> 7 agosto 2026 (`G:\Il mio Drive\ITS Corso\_claude\LEGGIMI.md`), ma applicato a un
> progetto che **non** vive sul Drive — e questo cambia due cose. Leggi il §2.

---

## 1. Il problema

Claude Code tiene tre cose separate:

| Cosa | Dove vive | Si sincronizza da sé? |
|---|---|---|
| `CLAUDE.md`, `docs/`, codice | nel repo | ✅ **sì, via git** |
| **memoria** (`memory/*.md`) | `%USERPROFILE%\.claude\projects\<slug>\memory` | ❌ **no** — una macchina sola |
| trascritti (`*.jsonl`) | `%USERPROFILE%\.claude\projects\<slug>\` | ❌ no, e **non serve** |

I trascritti sono un registratore: servono solo a `claude --resume` / `--continue`, non
vengono **mai** caricati all'avvio di una sessione nuova. Non sincronizzarli.

Per Quaestio il grosso è già risolto: dall'8/09/2026 anche `docs/` è tracciato nel repo
(prima era in `.gitignore` ed esisteva solo sulla VM), quindi `git clone` porta con sé
`CLAUDE.md`, le 18 fra spec e piani, e il workflow adb. **Resta scoperta solo la memoria.**

---

## 2. Due differenze rispetto a ITS Corso — leggile prima di eseguire

**a) Il progetto NON va spostato sul Drive.** Per ITS Corso il Drive è il veicolo di
sincronizzazione; qui il veicolo è **git**, ed è meglio (versioning, niente file di
conflitto). Mettere Quaestio su `G:` sarebbe attivamente dannoso:

- `dotnet build` di un progetto `OutputType=Exe` su Drive fallisce con
  `MSB4018 ... CreateAppHost ... sezione mappata dall'utente è aperta` — il client di
  Drive tiene l'`.exe` mappato in memoria mentre .NET prova a riscriverlo. Il workaround
  noto è `<UseAppHost>false</UseAppHost>`, che però qui toglierebbe proprio l'eseguibile
  che lo Step 14 deve produrre.
- `bin/`, `obj/` e `.git` sincronizzati sono zavorra pura: è esattamente il tipo di
  materiale ripulito il 6/08/2026 (1.757 MB in 73 cartelle).

Sul Drive va **solo** la cartella `memory`, che è di decine di KB.

**b) Lo slug è diverso su ogni macchina, ed è normale.** Lo slug deriva dal percorso della
cartella di lavoro, che qui contiene il nome utente Windows:

```
VM         C:\Users\<utente>\Documents\LUCUS\quaestio
           →  C--Users-luca-foglia-Documents-LUCUS-quaestio

PC locale  C:\Users\<utente>\...\quaestio
           →  C--Users-<utente>-...-quaestio          ← diverso, e va benissimo
```

Per ITS Corso lo slug combaciava perché il percorso era `G:\Il mio Drive\ITS Corso` su
entrambe. Qui no — **ma non serve che combaci**: la junction si crea su ogni macchina con
il *suo* slug locale, e punta alla stessa cartella sul Drive. I comandi qui sotto
calcolano lo slug da soli, non incollarlo a mano.

---

## 3. Aggancio — da eseguire su OGNI macchina

### Prerequisito

Google Drive montato su `G:`. Verifica: `Test-Path 'G:\Il mio Drive'` → `True`.

> ⚠️ Non fidarti di Git Bash per questo controllo: Drive è un filesystem virtuale, non
> compare in `/proc/partitions` né in `ls /`, e sembra assente anche quando c'è.
> Usa PowerShell.

### Passo 1 — Crea la cartella sul Drive (una volta sola, dalla prima macchina)

```powershell
New-Item -ItemType Directory -Force -Path 'G:\Il mio Drive\_claude\quaestio\memory' | Out-Null
```

`_claude\` alla radice del Drive è pensata per i progetti che **non** vivono sul Drive:
oggi `quaestio`, domani `recordum` o `Quiz` con lo stesso schema. Non metterla dentro
`ITS Corso`, che è materiale del corso.

### Passo 2 — Aggancia la memoria (su ogni macchina)

Esegui in **PowerShell**, con la working directory dentro il repo Quaestio — lo slug
viene calcolato dal percorso corrente, quindi il comando è identico su tutte le macchine:

```powershell
$slug   = (Get-Location).Path -replace '[^A-Za-z0-9]','-'
$mem    = "$env:USERPROFILE\.claude\projects\$slug\memory"
$target = 'G:\Il mio Drive\_claude\quaestio\memory'

New-Item -ItemType Directory -Force -Path (Split-Path $mem) | Out-Null

# se esiste già una memory locale non vuota, mettila da parte invece di perderla
if (Test-Path $mem) {
    if ((Get-ChildItem $mem -File -ErrorAction SilentlyContinue).Count -gt 0) {
        Rename-Item $mem "memory.bak-locale"
    } else {
        Remove-Item $mem -Recurse -Force
    }
}

cmd /c mklink /J "$mem" "$target"
```

> `mklink /J` richiede che la destinazione **non esista**: per questo il ramo `else`
> cancella la cartella vuota. Se avevi memorie locali finiscono in `memory.bak-locale`,
> da fondere a mano nel Drive e poi eliminare.

### Passo 3 — Verifica

```powershell
(Get-Item $mem -Force).LinkType      # deve stampare: Junction
(Get-Item $mem -Force).Target        # deve stampare il percorso sul Drive
(Get-ChildItem $mem -File).Count     # numero di memorie condivise
```

Se `LinkType` è vuoto la junction non ha attaccato: `$mem` esiste ancora come cartella
vera. Cancellala e ripeti il passo 2.

### Passo 4 — Prova end-to-end

Sulla prima macchina chiedi a Claude di salvare una memoria qualunque, aspetta che Drive
sincronizzi (icona del client), e sull'altra macchina controlla che il file compaia in
`$mem`. Finché non l'hai visto, non dare per riuscito il setup.

---

## 4. Regole d'uso

1. **Mai lavorare dalle due macchine in contemporanea.** Drive risolve le collisioni
   creando file di conflitto, e l'indice `MEMORY.md` diverge.
2. **Se `G:` non è montato, la junction punta nel vuoto**: Claude parte senza memoria e
   può fallire in scrittura. Se sembra che "non si ricordi niente", **la prima cosa da
   controllare è che Drive sia montato** — non riscrivere le memorie.
3. **Mai copiare `%USERPROFILE%\.claude.json`** fra macchine: è stato-macchina
   (`machineID`, `userID`, onboarding, cache). Sovrascriverlo azzera la configurazione
   del PC.
4. **Mai copiare le credenziali** (`.credentials.json`,
   `Claude_Code-credentials.secure.dpapi`): sono cifrate con DPAPI, legate a utente +
   macchina, sull'altra non si decifrano. Fai `/login`.
5. **Non sincronizzare i `.jsonl`**: contengono il testo integrale dei file letti e
   scritti in sessione, quindi anche eventuali segreti, e non servono a niente all'avvio.

Per disfare su una macchina:

```powershell
cmd /c rmdir "$mem"     # rimuove SOLO la junction, il contenuto sul Drive resta
```

Nota: usa `rmdir` di `cmd`, non `Remove-Item -Recurse`, che seguirebbe la junction e
cancellerebbe i file veri sul Drive.

---

## 5. Cosa NON serve trasferire

Codice, `materie.json`, `domande/`, `dist/`, `CLAUDE.md` e `docs/` arrivano da
**`git clone https://github.com/lufo-336/quaestio.git`** e basta.

Sul PC serve poi: **.NET 10 SDK**, il workload `android` (`dotnet workload install android`)
e, per il test su device, `adb` da
`%LOCALAPPDATA%\Android\Sdk\platform-tools\` — il ciclo completo è nella sezione
«Test su device fisico (adb)» di `CLAUDE.md`.

È il `CLAUDE.md` a fare il lavoro pesante: la sezione «Stato attuale» e la roadmap sono la
memoria portabile del progetto. Se serve che Claude "sappia" qualcosa, scrivilo lì o in una
memoria — **non** contare sui trascritti delle conversazioni, che non vengono mai riletti.
