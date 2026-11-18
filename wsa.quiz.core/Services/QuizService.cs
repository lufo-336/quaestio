using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Models.Statistiche;

namespace Wsa.Quiz.Core.Services;

/// <summary>
/// Logica di business: filtri, shuffle, preparazione domande, rotazione delle sbagliate.
/// </summary>
public static class QuizService
{
    // Random.Shared e' thread-safe (.NET 6+) — sostituisce l'istanza statica non thread-safe.

    // --------------------------------------------------------- SHUFFLE / FILTRI

    public static List<Domanda> DomandeRandomizzate(IEnumerable<Domanda> sorgente)
    {
        var lista = sorgente.ToList();
        for (int i = lista.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (lista[i], lista[j]) = (lista[j], lista[i]);
        }
        return lista;
    }

    public static List<Domanda> FiltraPerCategorie(IEnumerable<Domanda> sorgente, List<string> categorie)
    {
        if (categorie == null || categorie.Count == 0) return sorgente.ToList();
        var set = new HashSet<string>(categorie, StringComparer.OrdinalIgnoreCase);
        return sorgente.Where(d => set.Contains(d.Categoria)).ToList();
    }

    public static List<Domanda> FiltraPerMaterie(IEnumerable<Domanda> sorgente, List<string> materieId)
    {
        if (materieId == null || materieId.Count == 0) return sorgente.ToList();
        var set = new HashSet<string>(materieId, StringComparer.OrdinalIgnoreCase);
        return sorgente.Where(d => set.Contains(d.MateriaId)).ToList();
    }

    /// <summary>
    /// Toglie le domande dei tipi esclusi. Senza esclusioni non filtra nulla;
    /// con almeno un'esclusione passano solo le domande con un tipo ammesso
    /// (quelle senza tipo restano fuori: non si sa se sono "solo codice").
    /// </summary>
    public static List<Domanda> FiltraPerTipo(IEnumerable<Domanda> sorgente, ICollection<string> tipiEsclusi)
    {
        if (tipiEsclusi == null || tipiEsclusi.Count == 0) return sorgente.ToList();
        var esclusi = new HashSet<string>(tipiEsclusi, StringComparer.OrdinalIgnoreCase);
        return sorgente
            .Where(d => TipiDomanda.Tutti.Contains(d.Tipo, StringComparer.OrdinalIgnoreCase) && !esclusi.Contains(d.Tipo))
            .ToList();
    }

    public static List<Domanda> PrendiN(IEnumerable<Domanda> sorgente, int n)
    {
        if (n <= 0) return sorgente.ToList();
        var rand = DomandeRandomizzate(sorgente);
        return rand.Take(n).ToList();
    }

    // ---------------------------------------------------------- CATEGORIE

    /// <summary>
    /// Restituisce un dizionario ordinato: categoria → numero di domande disponibili.
    /// </summary>
    public static List<(string Categoria, int Conteggio)> RiepilogoCategorie(IEnumerable<Domanda> domande)
    {
        return domande
            .GroupBy(d => d.Categoria)
            .Select(g => (g.Key, g.Count()))
            .OrderBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ---------------------------------------------------------- PREPARA DOMANDA

    /// <summary>
    /// Restituisce una DomandaPreparata con le risposte mescolate e l'indice della
    /// corretta ricalcolato rispetto al nuovo ordine.
    /// </summary>
    public static DomandaPreparata PreparaDomanda(Domanda d)
    {
        int idxCorretta = d.RispostaCorretta;
        string testoCorretto = (idxCorretta >= 0 && idxCorretta < d.Risposte.Count)
            ? d.Risposte[idxCorretta]
            : d.Risposte.FirstOrDefault() ?? "";

        var shuffled = d.Risposte.ToList();
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        int nuovoIdx = shuffled.IndexOf(testoCorretto);
        if (nuovoIdx < 0) nuovoIdx = 0; // fallback difensivo

        return new DomandaPreparata
        {
            Originale = d,
            RisposteShufflate = shuffled,
            IndiceCorrettoShufflato = nuovoIdx
        };
    }

    // ---------------------------------------------------------- FORMATTAZIONE

    /// <summary>
    /// Formatta una durata in modo leggibile: "hh:mm:ss" se >= 1 ora, altrimenti "mm:ss".
    /// </summary>
    public static string FormattaDurata(TimeSpan t)
    {
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes:00}:{t.Seconds:00}";
    }

    // ---------------------------------------------------------- PUNTEGGIO

    /// <summary>
    /// Punteggio semplice: 10 punti per ogni risposta corretta al primo tentativo,
    /// 7 per quelle corrette al secondo tentativo (seconda possibilità),
    /// 4 per quelle corrette dopo qualche errore (modalità rotazione),
    /// 0 per quelle errate o non risposte.
    /// </summary>
    public static int CalcolaPunteggio(RisultatoQuiz r)
    {
        int punti = 0;
        foreach (var d in r.Dettagli)
        {
            if (!d.Corretta) continue;
            if (d.Tentativi <= 1)
                punti += d.SecondoTentativo ? 7 : 10;
            else
                punti += 4;
        }
        return punti;
    }

    // ---------------------------------------------------------- PUNTI DEBOLI / LIVELLI

    private sealed class ChiaveCategoriaComparer : IEqualityComparer<(string Materia, string Categoria)>
    {
        public static readonly ChiaveCategoriaComparer Instance = new();

        public bool Equals((string Materia, string Categoria) x, (string Materia, string Categoria) y)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x.Materia, y.Materia)
                && StringComparer.OrdinalIgnoreCase.Equals(x.Categoria, y.Categoria);
        }

        public int GetHashCode((string Materia, string Categoria) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Materia ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Categoria ?? string.Empty));
        }
    }

    /// <summary>
    /// Restituisce il livello di debolezza in base alla percentuale di errore (0..1).
    /// Soglie allineate a QuizColors.HeatmapBordo:
    /// &lt; 0.20 verdi; &lt; 0.60 ambra; altrimenti rosse.
    /// </summary>
    public static string LivelloDi(double pctErrore)
    {
        if (pctErrore < 0.20) return LivelliDebolezza.Verdi;
        if (pctErrore < 0.60) return LivelliDebolezza.Ambra;
        return LivelliDebolezza.Rosse;
    }

    /// <summary>
    /// Mappa ogni (Materia, Categoria) al rispettivo livello di debolezza calcolato dalle statistiche.
    /// Chiavi case-insensitive.
    /// </summary>
    public static Dictionary<(string Materia, string Categoria), string> LivelliPerCategoria(IEnumerable<StatistichePerMateria> stat)
    {
        var dict = new Dictionary<(string Materia, string Categoria), string>(ChiaveCategoriaComparer.Instance);
        if (stat == null) return dict;
        foreach (var mat in stat)
        {
            if (mat.Categorie == null) continue;
            foreach (var cat in mat.Categorie)
            {
                dict[(mat.Materia, cat.Categoria)] = LivelloDi(cat.PctErrore);
            }
        }
        return dict;
    }

    /// <summary>
    /// Filtra le domande tenendo solo quelle il cui livello di categoria non è escluso.
    /// Se livelliEsclusi è vuoto o nullo non filtra nulla; categorie assenti dalle statistiche
    /// sono considerate "nuove".
    /// </summary>
    public static List<Domanda> FiltraPerLivello(
        IEnumerable<Domanda> sorgente,
        ICollection<string>? livelliEsclusi,
        IReadOnlyDictionary<(string Materia, string Categoria), string>? livelli)
    {
        if (sorgente == null) return new List<Domanda>();
        if (livelliEsclusi == null || livelliEsclusi.Count == 0) return sorgente.ToList();

        var esclusi = new HashSet<string>(livelliEsclusi, StringComparer.OrdinalIgnoreCase);
        return sorgente.Where(d =>
        {
            string liv = LivelliDebolezza.Nuove;
            if (livelli != null)
            {
                if (livelli.TryGetValue((d.MateriaNome, d.Categoria), out var trovato))
                    liv = trovato;
                else
                {
                    // Fallback se il dizionario passato non usa un comparer case-insensitive
                    foreach (var kv in livelli)
                    {
                        if (string.Equals(kv.Key.Materia, d.MateriaNome, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(kv.Key.Categoria, d.Categoria, StringComparison.OrdinalIgnoreCase))
                        {
                            liv = kv.Value;
                            break;
                        }
                    }
                }
            }
            return !esclusi.Contains(liv);
        }).ToList();
    }

    /// <summary>
    /// Restituisce l'insieme di tutti gli IdDomanda delle domande sbagliate presenti nelle statistiche.
    /// </summary>
    public static HashSet<string> IdSbagliate(IEnumerable<StatistichePerMateria> stat)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (stat == null) return set;
        foreach (var mat in stat)
        {
            if (mat.Categorie == null) continue;
            foreach (var cat in mat.Categorie)
            {
                if (cat.Sbagliate == null) continue;
                foreach (var sb in cat.Sbagliate)
                {
                    if (!string.IsNullOrEmpty(sb.IdDomanda))
                        set.Add(sb.IdDomanda);
                }
            }
        }
        return set;
    }

    /// <summary>
    /// Se il pool supera il limite ed esistono id prioritari, prende prima
    /// le domande prioritarie (in ordine casuale) e poi completa con le altre
    /// (in ordine casuale) fino al limite. Se non ci sono priorità o il limite
    /// non è superato, si comporta come un normale limite/Take.
    /// </summary>
    public static List<Domanda> ApplicaLimiteConPriorita(
        IEnumerable<Domanda> sorgente,
        int limite,
        ICollection<string>? idPrioritari)
    {
        var lista = sorgente.ToList();
        if (limite <= 0 || lista.Count <= limite)
            return lista;

        if (idPrioritari == null || idPrioritari.Count == 0)
            return lista.Take(limite).ToList();

        var setPrioritari = new HashSet<string>(idPrioritari, StringComparer.OrdinalIgnoreCase);
        var prioritarie = lista.Where(d => setPrioritari.Contains(d.Id)).ToList();
        var altre = lista.Where(d => !setPrioritari.Contains(d.Id)).ToList();

        var risultato = DomandeRandomizzate(prioritarie).Take(limite).ToList();
        if (risultato.Count < limite)
        {
            risultato.AddRange(DomandeRandomizzate(altre).Take(limite - risultato.Count));
        }
        return risultato;
    }
}
