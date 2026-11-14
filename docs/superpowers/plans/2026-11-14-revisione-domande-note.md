# Revisione domande (roadmap punto 4) — note da Antigravity

Primo passaggio fatto il 2026-11-14 (commit edaa0a7…bb21a86): universalità, accenti/refusi, campo `tipo`.
## Esito

**Secondo passaggio fatto il 2026-11-14**: le 98 segnalazioni non duplicate sono state riprese da
Antigravity (Argon low: Flash era senza quota) con l'indice della risposta corretta in sola lettura,
poi rilette a mano. 81 domande corrette (errori tecnici, domande rese autosufficienti con contesto o
codice nel testo), le altre lasciate perché già risolte o segnalazioni infondate. Errori notevoli
trovati: RAID 6 (infra n=15) aveva come corretta la risposta sbagliata; C# n=28 (`"5" + 3`).
**Restano aperti** solo i quasi-duplicati (righe con "duplicat", "sovrappost", "ridondant"):
lasciati, eliminarli richiede di estendere `coppie_it_en.py`.

## Segnalazioni originali

Sotto le segnalazioni che Flash NON ha corretto nel primo passaggio (regola: non toccare i contenuti tecnici).
`n` = posizione della domanda nella materia (ordine di `coppie_it_en.py esporta`).

## sql

- n=180: La spiegazione indica che REPEATABLE READ non previene le phantom read (corretto secondo lo standard ANSI SQL; nell'implementazione di PostgreSQL REPEATABLE READ previene anche le phantom read tramite Snapshot Isolation).
- n=182: Nella spiegazione italiana dice 'alta selettivita' associata a molte righe selezionate, mentre in inglese dice 'low selectivity' (selezionare una percentuale elevata di righe corrisponde a bassa selettività).
- n=208: Quasi duplicato della domanda n=193 (entrambe chiedono cosa distingue la BCNF dalla 3NF, con risposta corretta equivalente basata sul determinante superchiave).
- n=209: Quasi duplicato della domanda n=196 (entrambe chiedono quando la denormalizzazione è una scelta progettuale legittima/giustificata, con risposta corretta basata su ridondanza controllata per ridurre i JOIN nelle letture).

## cpp

- n=30: in en risposte e spiegazione usano 'SI/NO' invece di 'YES/NO'
- n=33: la spiegazione afferma che Java usa la keyword 'generic', ma in Java i generics non usano una keyword (si usa direttamente la sintassi dei tipi generici con parentesi angolari)
- n=72: in it.spiegazione si afferma che &s crea "un puntatore al puntatore (`char(*)[50]`)", ma char(*)[50] è un puntatore ad array e non un puntatore a puntatore (char**).
- n=81: la domanda chiede "Perché l'accesso all'heap è più lento dello stack?", ma risposta e spiegazione confrontano i costi di allocazione e deallocazione (ricerca blocchi liberi vs incremento del pointer dello stack), non la velocità di accesso/dereferenziazione in memoria.
- n=110: su macOS le funzioni matematiche sono incluse in libSystem e non richiedono il flag `-lm` per il linking a differenza di Linux
- n=117: in C l'operatore di uguaglianza restituisce un int (0), non FALSE; il tipo booleano con 'false' è standardizzato solo da C99 (<stdbool.h>) o C23, mentre 'FALSE' non è una keyword/costante dello standard C
- n=163: la spiegazione afferma che void* può contenere l'indirizzo di qualsiasi tipo inclusa una funzione, ma nello standard C void* è puntatore a oggetto e la conversione con puntatori a funzione non è garantita (è supportata come estensione POSIX)

## cs

- n=2: Console.ReadLine() ha tipo di ritorno string? e può restituire null se lo stream di input termina (EOF).
- n=21: Ambiguità tra le opzioni "L" e "l oppure L": sintatticamente C# accetta sia 'l' che 'L' come suffisso long (anche se 'l' genera il warning CS0078 per leggibilità).
- n=23: Console.ReadLine() restituisce formalmente string? (nullable string), potendo restituire null a fine stream.
- n=25: Nella spiegazione si cita 'if (x = 5)': in C# questa espressione genera errore di compilazione CS0029 poiché l'assegnazione produce un intero e l'istruzione if richiede un booleano (a differenza del C/C++).
- n=28: Errore tecnico nel quiz: in C# l'espressione '"5" + 3' è valida e restituisce la stringa "53" (l'operatore + con un operando string accetta object e converte il secondo operando). La domanda e la spiegazione affermano erroneamente che genera errore di compilazione.
- n=32: in it le risposte riportano "true"/"false" minuscoli, mentre Console.WriteLine su bool stampa "True"/"False" con iniziale maiuscola (come correttamente riportato in en e nella spiegazione)
- n=144: nella spiegazione si afferma che "int[5] numeri" è C++, ma in C++ la sintassi dell'array è "int numeri[5];" (oppure std::array<int, 5>), mentre "int[5] numeri" non è sintassi valida in C++.
- n=164: domanda quasi duplicata di n=155 (entrambe chiedono quale metodo svuota o rimuove tutti gli elementi da una List<T>, con risposta Clear)
- n=167: la spiegazione afferma che modificare la variabile di foreach non cambia l'array originale, ma in C# la riassegnazione della variabile di iterazione genera un errore di compilazione (CS1656); inoltre per array di tipi riferimento le proprietà degli elementi referenziati sono modificabili anche dentro un foreach
- n=201: la domanda chiede "Cosa significa che due metodi sono 'overloaded' se hanno la stessa firma?", ma due metodi con la stessa firma non costituiscono overloading valido (è una dichiarazione duplicata non permessa dal compilatore)
- n=206: quasi duplicato di n=197 (entrambe chiedono il significato di static nei metodi e hanno come risposta corretta che appartengono alla classe e non a istanze specifiche)
- n=208: la risposta afferma che "gli array sono passati per riferimento"; in C# il riferimento all'array viene passato per valore (a meno dell'uso esplicito della parola chiave ref)
- n=250: la domanda fa riferimento a un esercizio d'aula specifico (metodo Info() e campo abilita della classe Studente), rendendo le opzioni B e C prive di contesto generale senza il codice visto a lezione
- n=283: in C# i reference type sono passati per valore (viene copiato il riferimento) a meno di usare ref; la domanda parla impropriamente di "passate per riferimento", anche se la risposta precisa "si passa il valore del riferimento"
- n=285: la risposta corretta afferma che class ha "passaggio per riferimento", mentre in C# anche i reference type sono passati per valore (copia del riferimento) a meno di usare ref
- n=299: quasi duplicato della domanda n=271 (comportamento del compilatore in assenza di costruttore esplicito)
- n=311: Nella domanda in inglese "What is the name of the base class constructor called from a derived class?" era una traduzione letterale errata di "Come si chiama" (non chiede il nome ma la sintassi di invocazione); allineata a "How is the base class constructor called from a derived class?".
- n=325: La risposta corretta afferma che "Il compilatore decide a runtime quale versione del metodo chiamare in base al tipo reale dell'oggetto", ma la risoluzione a runtime del binding dinamico è gestita dal runtime (CLR/vtable) e non dal compilatore. Contenuto mantenuto invariato come da regole.
- n=359: Fa riferimento alla struttura specifica della tabella Materiali (colonne Id, Codice, Tipo, Disponibile) e al metodo CaricaTutti() di un esercizio d'aula.
- n=360: Fa riferimento allo schema specifico di una tabella Materiali di un progetto d'aula senza fornire lo schema.
- n=361: Fa riferimento all'implementazione del metodo CaricaTutti di un progetto d'aula senza fornire il codice.
- n=362: Fa riferimento allo schema della tabella Materiali di un progetto d'aula senza fornire lo schema.
- n=363: Fa riferimento al mapping del metodo Aggiungi di un progetto d'aula senza fornire il codice.
- n=382: Errore tecnico: in C# 8+ i metodi di default nelle interfacce non usano la parola chiave 'default' (usata invece in Java); in C# si scrive direttamente il corpo del metodo nell'interfaccia.
- n=402: Quasi duplicato della domanda n=391 (entrambe chiedono quale keyword C# lancia un'eccezione).
- n=403: Fa riferimento al progetto d'aula (gestore BtnAggiungiLibro_Click, metodo _db.Aggiungi() e vincolo UNIQUE sul campo Codice); ha poco senso fuori dal contesto del corso.
- n=404: Presuppone l'architettura specifica dell'applicazione del corso (gestione dell'eccezione con try-catch e visualizzazione all'utente in MainForm per inserimento di record duplicati); ha poco senso fuori dal contesto del corso.
- n=406: Fa riferimento all'implementazione specifica del progetto del corso (cosa viene mostrato nel catch di BtnAggiungiLibro_Click: titolo 'Errore DB'); senza il codice dell'esercitazione d'aula è impossibile rispondere.
- n=419: Duplicato della domanda n=413 (entrambe chiedono la differenza tra First e FirstOrDefault in LINQ).
- n=430: con s1 null, s1.Equals(s2) lancia NullReferenceException mentre s1 == s2 gestisce i null in modo sicuro; sono del tutto equivalenti solo con riferimenti non null (o usando object.Equals).
- n=447: parzialmente ridondante rispetto a n=442 (entrambe vertono sul fatto che le proprietà generate con init siano di sola lettura / non riassegnabili dopo la creazione).
- n=448: parzialmente sovrapposta a n=444 (entrambe vertono sull'uso dell'espressione with per produrre una copia modificata di un record immutabile).
- n=469: la domanda presupponeva l'implementazione della classe Studente vista a lezione (getter con ToUpper()); esplicitato nel testo che Studente restituisce il nome in maiuscolo.
- n=492: La domanda fa riferimento a una classe specifica dell'esercitazione (StaticRepository) e non ha senso generale al di fuori del corso.
- n=504: La domanda fa riferimento al refactoring di un campo specifico (Abilita da string a bool) di una classe dell'esercitazione (Studente).
- n=530: in en risposte[1] corretto refuso da "mandatory `NULL`" a "mandatory non-`NULL`" (allineato all'italiano "non `NULL`")

## frontend

- n=12: en.spiegazione contiene un backtick non chiuso ('<article> elements.').
- n=69: la domanda fa riferimento a "classe ES6 moderna", ma i private class fields con sintassi '#' sono stati introdotti nello standard ES2022 (ES13) e non in ES6 (ES2015).
- n=96: la risposta corretta afferma che seleziona il primo li che è 'figlio diretto' di un ul, ma il selettore 'ul li:first-child' usa il combinatore discendente (spazio) e non il selettore di figlio diretto ('>').
- n=113: tecnicamente un componente React può restituire anche array di elementi, stringhe, numeri, booleani o null, non solo un singolo elemento o un frammento.
- n=174: nella risposta corretta ("`undefined` è l'assenza di valore non assegnata dal sistema") la formulazione è ambigua o invertita rispetto alla spiegazione: è il programmatore a non aver assegnato il valore, mentre undefined viene assegnato dal motore/sistema JavaScript.

## infra

- n=15: Nella spiegazione la frase finale ("La risposta 1 confonde 'tre o più' con il limite reale di 'tre'" / "Answer A confuses...") risulta incongruente: l'opzione corretta è proprio "Almeno 3 dischi" (cioè tre o più guasti provocano la perdita di dati), per cui non si tratta di una risposta errata che fa confusione ma dell'opzione corretta.
- n=29: La classificazione di KVM come Type 1 o Type 2 è controversa nella letteratura tecnica (architettura ibrida che poggia su un kernel Linux, ma opera bare-metal tramite estensioni hardware); la domanda e la spiegazione adottano la classificazione Type 1.
- n=41: Nella spiegazione sostituito il riferimento "Trappola d'esame" con "Trappola comune" / "Common trap".
- n=45: Nella spiegazione rimosso l'incipit "Trappola d'esame citata nella guida" per neutralizzare il riferimento al contesto didattico.
- n=47: Nella risposta A in inglese corretto l'articolo ("by an hyperscale" -> "by a hyperscale").
- n=49: Nella spiegazione rimosso il riferimento al laboratorio del corso ("Nel lab del corso si è usato per aggiungere/togliere il volume Docker").
- n=50: Il riferimento alle "5 varianti" dell'architettura Client/Server fa riferimento a una tassonomia specifica della letteratura didattica (Standalone, File Server, Database Server, Application Server, Web-based).
- n=70: I namespace di Linux nel kernel attuale sono 8 (si sono aggiunti cgroup nel 2016 e time nel 2020), mentre la risposta corretta ne indica 6.

## cybersecurity

- n=14: Nel protocollo TLS il messaggio ServerHello negozia versione e cipher suite; il certificato X.509 e la chiave pubblica vengono inviati in un messaggio separato (Certificate), sebbene nello stesso flight del server.
- n=20: Il quarto distractor ("Lancia attacchi automatici di massa contro chiunque ne abbia") presenta una formulazione tronca ("ne abbia").
- n=22: Nella spiegazione si afferma che la lunghezza ha effetto 'esponenziale' mentre il charset ha effetto 'lineare nei bit': formalmente l'entropia H in bit è lineare nella lunghezza (H = l * log2(N)) e logaritmica nella dimensione del charset N; è lo spazio delle combinazioni possibili (N^l) a essere esponenziale rispetto alla lunghezza.
- n=24: Sotto VPN il traffico di rete (inclusa la negoziazione TLS e l'SNI) è incapsulato e cifrato nel tunnel, quindi non è visibile all'ISP o sulla rete locale; è visibile solo al provider VPN o a valle del tunnel (in assenza di ECH). L'inciso 'anche se sotto VPN' nella domanda risulta tecnicamente fuorviante.
- n=54: La risposta corretta ("L'uso di chiavi effimere che proteggono sessioni passate compromesse") presenta una formulazione ambigua: la forward secrecy protegge la riservatezza delle sessioni passate nel caso in cui la chiave a lungo termine del server venga compromessa in futuro, non protegge sessioni già compromesse.

## ux

- n=3: Fa riferimento per nome alla persona 'Emily' derivata dagli scenari del corso, sebbene la sua disabilità sia esplicitata nel testo della domanda.
- n=12: La domanda presuppone la conoscenza delle personas del corso (l'opzione corretta 'Francesco' non include alcuna descrizione, a differenza delle altre).
- n=31: la risposta corretta ("Sono ciò che Luca sente e Francesco legge") e la spiegazione fanno riferimento a personas specifiche del corso (Luca e Francesco) non note a un utente esterno.
- n=32: la domanda ("Cosa rischia Francesco (autismo)...") e la spiegazione fanno riferimento alla persona del corso "Francesco".
- n=37: in en la seconda risposta era identica alla terza ("Discover, Define, Develop, Deliver"); corretta la seconda con la traduzione di "Scopri, Disegna, Sviluppa, Consegna" ("Discover, Design, Develop, Deliver").
- n=52: in en rimosso il prefisso errato "A " nelle quattro opzioni di risposta ("A to choose..." -> "To choose...").
- n=59: la domanda e la spiegazione fanno riferimento alla persona del corso "Valerio (bassa visione da glaucoma)".
- n=65: fa riferimento alle personas specifiche introdotte nel corso (Francesco, Emily, Valerio, Steven), senza le quali la domanda non ha senso
- n=73: fa riferimento alla persona specifica Luca introdotta nel corso, senza la quale la domanda non ha senso
- n=85: fa riferimento alle personas specifiche del corso (Luca, Valerio, Francesco, Emily, Steven), senza le quali la domanda non ha senso
- n=86: fa riferimento alle personas specifiche introdotte nel corso (Steven, Luca, Emily, Valerio), senza le quali la domanda non ha senso

## restfulapi

- n=6: AppError non è una classe standard di Express o JavaScript ma un'implementazione personalizzata del corso; il valore di default dello status (500) è una scelta arbitraria di quel codice e la domanda non ha senso per un pubblico generico.
- n=11: L'opzione distattrice afferma che Array.prototype.sort non è stabile sugli array di oggetti; da ECMAScript 2019 (Node.js 12+) l'algoritmo di ordinamento è garantito stabile dalle specifiche.
- n=22: La domanda originale faceva riferimento a 'TaskFlow', applicazione del corso (riscritta in modo generico per rimuovere il riferimento d'aula); inoltre il claim 'ruolo' nel payload del token è in italiano anziché nel convenzionale 'role'.
- n=38: la domanda si riferisce all'implementazione specifica del progetto didattico (TaskFlow) e non ha senso senza quel contesto
- n=60: refuso in en.spiegazione ("It is opinionated" -> "It is unopinionated"), corretto.
- n=62: "il middleware autentica" faceva riferimento al middleware del corso; generalizzato in "un middleware di autenticazione" (con backtick su Authorization).
- n=63: "struttura a cartelle raccomandata" (riferimento alle linee guida del corso) reso neutro in "tipica struttura a cartelle".
- n=89: rimosso riferimento al contesto d'aula ("del corso" / "of the course") nel distruttore risposte[1].
- n=101: Fa riferimento a un file locale di progetto (utils/errors.js) e a factory custom (conflict, badReq, notFound, forbidden) per la classe AppError, non standard e privi di senso al di fuori del corso.
- n=139: fa riferimento alla classe convenzionale AppError (non nativa di Express/JavaScript); testo reso universale rimuovendo "del corso".
- n=140: assume una convenzione specifica (campo error sempre di tipo stringa), non universale (es. RFC 7807 usa title/detail, altre API usano oggetti in error).

## versioncontrol

- n=9: la domanda originale ("nell'appendice sui commit atomici") faceva riferimento a un'analogia specifica della dispensa (anche "fotografia" è un'analogia comune per i commit in Git).
- n=59: la domanda chiede quale valore impostare per il default branch durante l'installazione di Git for Windows riferendosi originariamente alla "guida di installazione" del corso (l'installer ufficiale di Git for Windows mantiene ancora `master` come opzione predefinita a meno di scegliere il sovrascrivi con `main`).
- n=62: la domanda fa riferimento alle voci di menu 'Ripristina questa versione' e 'Crea una copia' (tipiche della cronologia versioni di Google Docs) senza specificare lo strumento o il contesto.
- n=76: la domanda dipendeva dalle preferenze della guida del corso sulla scelta dell'editor (nano o VS Code) nell'installer di Git for Windows.
- n=101: Un file `.gitignore` viene riconosciuto da Git anche nelle sottocartelle del progetto (dove si applica a quella sottocartella e alle sue discendenti), non necessariamente solo nella directory radice.
- n=112: A partire da Git 2.0, gli spazi bianchi finali (trailing spaces) non preceduti da backslash `\` vengono ignorati automaticamente nelle regole di `.gitignore`, quindi uno spazio finale non rompe più silenziosamente la regola.
- n=123: domanda sull'estrazione del pacchetto zip di Cmder specifica del setup d'aula e fuori tema rispetto a version control
- n=130: la domanda sulla sintassi `---` non specifica che si riferisce a Markdown
- n=176: Nella versione originale la domanda ometteva il comando dell'alias (`git config --global alias.lg ...` suggerito dalla guida), rendendo impossibile capire cosa facesse l'alias senza la dispensa; esplicitato il valore dell'alias nella domanda rimuovendo il riferimento alla guida.
- n=227: fa riferimento a un'analogia specifica della dispensa ("il branch main 'pubblicato' = libro pubblicato").
- n=232: fa riferimento a un'analogia specifica della dispensa ("le quattro zone / oggetti sul letto / valigia").

## cloudservices

- n=125: Spiegazione e opzioni incoerenti: la spiegazione afferma che 'l'ultima ha addirittura sette campi' e che 'la seconda espressione scatta ogni 5 ore', ma la prima opzione ha 7 campi ('0 5 * * * * *') e la terza opzione scatta ogni 5 ore ('0 0 */5 * * *'); inoltre in inglese la terza opzione ha 7 campi ('0 0 */5 * * * *').
- n=153: La motivazione fa riferimento a limiti specifici dell'interfaccia web del portale Azure con account Azure for Students (dimensioni VM filtrate nella UI).
- n=156: Lo status HTTP standard per una richiesta asincrona accettata ma non ancora elaborata è 202 Accepted, anche se nella pratica alcune API restituiscono 200 OK.
- n=165: Il traffico di 'kubectl port-forward' attraversa sia l'API server sia il kubelet del nodo di destinazione prima di raggiungere il container: la presenza di entrambe le opzioni nelle risposte rende la domanda ambigua.

## python

- n=22: La domanda fa riferimento a un esempio d'aula specifico (composizione con classi Gasoline ed Electricity e metodo refuel) di cui non viene fornito il codice sorgente nel quiz.
- n=71: L'opzione "Con la parola chiave protocol" cita protocol come parola chiave (in Python Protocol è una classe del modulo typing, non una keyword); inoltre da Python 3.8 i Protocol (PEP 544) sono un modo standard per definire interfacce (tipizzazione strutturale / static duck typing).
- n=91: la spiegazione inglese include "A describes the correct output.", riferimento rigido alla lettera dell'opzione non presente nel testo italiano.
- n=104: la spiegazione inglese include "A describes the correct behavior:", ma l'output corretto è 'None 1' (indice 2) e l'app mescola le opzioni.
- n=123: Nelle risposte in inglese rimossi i prefissi spuri "A ", "B ", "C ", "D ", introdotti per errore di traduzione a partire dalla preposizione italiana "A".
- n=141: Nella domanda in inglese corretto il refuso di traduzione da "what is the name of the somma function in the module?" a "After importing math_utils, how is the somma function of the module called?".
- n=142: Sostituito il riferimento d'aula al corso "WSA" con "Python" nel codice e nelle risposte (it ed en).

