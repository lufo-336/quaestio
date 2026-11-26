using System.Text.Json;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Models.Laboratorio;

namespace Wsa.Quiz.Core.Services;

/// <summary>
/// Caricamento, identificazione e selezione delle schede del Laboratorio di codice.
/// </summary>
public static class LaboratorioService
{
    /// <summary>Valore di <see cref="RisultatoQuiz.Modalita"/> per le sessioni di Laboratorio (non localizzato).</summary>
    public const string Modalita = "Laboratorio";

    /// <summary>Suffisso aggiunto al nome del linguaggio in <see cref="RisultatoQuiz.MateriaNome"/>.</summary>
    public const string SuffissoMateria = " · Laboratorio";

    /// <summary>Linguaggi supportati dal Laboratorio (v1: solo C#).</summary>
    public static readonly IReadOnlyList<(string Id, string Nome)> Linguaggi =
    [
        ("cs", "C#"),
    ];

    /// <summary>
    /// Carica tutte le schede valide dalle cartelle <c>laboratorio/&lt;id&gt;/</c>,
    /// valorizzando <see cref="SchedaLab.Linguaggio"/>. Un file illeggibile viene saltato
    /// senza bloccare l'avvio dell'app.
    /// </summary>
    public static List<SchedaLab> CaricaSchede(IFonteDati fonte)
    {
        var risultato = new List<SchedaLab>();
        foreach (var (id, _) in Linguaggi)
        {
            foreach (var file in fonte.ElencaJson($"laboratorio/{id}").OrderBy(f => f, StringComparer.Ordinal))
            {
                try
                {
                    string json = fonte.LeggiTesto(file);
                    var scheda = JsonSerializer.Deserialize<SchedaLab>(json, StorageService.OpzioniLettura);
                    if (scheda == null || !SchedaValida(scheda))
                    {
                        Console.Error.WriteLine($"[ATTENZIONE] Scheda incompleta in '{file}', scartata.");
                        continue;
                    }
                    scheda.Linguaggio = id;
                    risultato.Add(scheda);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[ATTENZIONE] Impossibile leggere '{file}': {ex.Message}");
                }
            }
        }
        return risultato;
    }

    private static bool SchedaValida(SchedaLab scheda)
    {
        if (string.IsNullOrWhiteSpace(scheda.Id) || scheda.Varianti == null || scheda.Varianti.Count == 0)
            return false;

        foreach (var v in scheda.Varianti)
        {
            if (v == null ||
                v.Righe == null || v.Righe.Count == 0 ||
                v.Output == null || v.Output.Count == 0 ||
                v.Difetti == null || v.Difetti.Count == 0 ||
                v.Input == null || v.Input.Count == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Costruisce l'id stabile di un passo per la cronologia:
    /// <c>lab:&lt;linguaggio&gt;:&lt;scheda&gt;:&lt;variante&gt;:&lt;passo&gt;</c>
    /// con <paramref name="passo"/> uguale a <c>output</c>, <c>bug</c> o <c>input</c>.
    /// </summary>
    public static string IdPasso(SchedaLab s, VarianteLab v, string passo) =>
        $"lab:{s.Linguaggio}:{s.Id}:{v.Id}:{passo}";

    /// <summary>True se il dettaglio appartiene a una sessione di Laboratorio.</summary>
    public static bool EDettaglioLab(DettaglioRisposta? d) =>
        d != null && !string.IsNullOrEmpty(d.IdDomanda) && d.IdDomanda.StartsWith("lab:", StringComparison.Ordinal);

    /// <summary>True se il risultato appartiene a una sessione di Laboratorio.</summary>
    public static bool ERisultatoLab(RisultatoQuiz? r) =>
        r != null && string.Equals(r.Modalita, Modalita, StringComparison.Ordinal);

    /// <summary>
    /// Seleziona al massimo <paramref name="quante"/> coppie (scheda, variante) del livello richiesto,
    /// garantendo al più un elemento per scheda:
    /// - se <paramref name="ripassa"/> è false: preferisce varianti mai viste (una a caso per scheda,
    ///   schede con novità mescolate), poi schede con tutte le varianti viste dalla meno recente;
    /// - se <paramref name="ripassa"/> è true: solo coppie il cui ultimo tentativo ha almeno un passo
    ///   con <see cref="DettaglioRisposta.PesoErrore"/> &gt; 0, ordinate dalla meno recente.
    /// </summary>
    public static List<(SchedaLab Scheda, VarianteLab Variante)> Prepara(
        IEnumerable<SchedaLab> schede,
        string livello,
        bool ripassa,
        int quante,
        IEnumerable<RisultatoQuiz> cronologia,
        Random rnd)
    {
        if (quante <= 0) return new();

        var filtrate = schede
            .Where(s => s.Varianti.Count > 0 && string.Equals(s.Livello, livello, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (filtrate.Count == 0) return new();

        var storico = EstraiStoricoVarianti(cronologia);

        if (ripassa)
        {
            var daRipassare = new List<(SchedaLab Scheda, VarianteLab Variante, DateTime DataOra)>();
            foreach (var scheda in filtrate)
            {
                (VarianteLab Variante, DateTime DataOra)? scelta = null;
                foreach (var v in scheda.Varianti)
                {
                    if (!storico.TryGetValue((scheda.Linguaggio, scheda.Id, v.Id), out var stato) || !stato.Sbagliata)
                        continue;
                    if (scelta == null || stato.DataOra < scelta.Value.DataOra)
                        scelta = (v, stato.DataOra);
                }
                if (scelta != null)
                    daRipassare.Add((scheda, scelta.Value.Variante, scelta.Value.DataOra));
            }

            return daRipassare
                .OrderBy(x => x.DataOra)
                .Take(quante)
                .Select(x => (x.Scheda, x.Variante))
                .ToList();
        }

        var conNuove = new List<(SchedaLab Scheda, VarianteLab Variante)>();
        var tutteViste = new List<(SchedaLab Scheda, VarianteLab Variante, DateTime DataOra)>();

        foreach (var scheda in filtrate)
        {
            var nuove = new List<VarianteLab>();
            VarianteLab? menoRecente = null;
            DateTime dataMinima = DateTime.MaxValue;

            foreach (var v in scheda.Varianti)
            {
                if (!storico.TryGetValue((scheda.Linguaggio, scheda.Id, v.Id), out var stato))
                {
                    nuove.Add(v);
                }
                else if (menoRecente == null || stato.DataOra < dataMinima)
                {
                    menoRecente = v;
                    dataMinima = stato.DataOra;
                }
            }

            if (nuove.Count > 0)
                conNuove.Add((scheda, nuove[rnd.Next(nuove.Count)]));
            else if (menoRecente != null)
                tutteViste.Add((scheda, menoRecente, dataMinima));
        }

        // Fisher-Yates sulle schede con almeno una variante mai vista
        for (int i = conNuove.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            (conNuove[i], conNuove[j]) = (conNuove[j], conNuove[i]);
        }

        return conNuove
            .Concat(tutteViste.OrderBy(x => x.DataOra).Select(x => (x.Scheda, x.Variante)))
            .Take(quante)
            .ToList();
    }

    /// <summary>
    /// Restituisce per il livello indicato: numero di schede con almeno una variante mai vista,
    /// totale schede disponibili e numero di schede con almeno una variante da ripassare.
    /// </summary>
    public static (int Nuove, int Totale, int DaRipassare) Conteggi(
        IEnumerable<SchedaLab> schede,
        string livello,
        IEnumerable<RisultatoQuiz> cronologia)
    {
        var filtrate = schede
            .Where(s => s.Varianti.Count > 0 && string.Equals(s.Livello, livello, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (filtrate.Count == 0) return (0, 0, 0);

        var storico = EstraiStoricoVarianti(cronologia);
        int nuove = 0;
        int daRipassare = 0;

        foreach (var s in filtrate)
        {
            if (s.Varianti.Any(v => !storico.ContainsKey((s.Linguaggio, s.Id, v.Id))))
                nuove++;
            if (s.Varianti.Any(v => storico.TryGetValue((s.Linguaggio, s.Id, v.Id), out var st) && st.Sbagliata))
                daRipassare++;
        }

        return (nuove, filtrate.Count, daRipassare);
    }

    private static Dictionary<(string Ling, string SchedaId, string VarianteId), (DateTime DataOra, bool Sbagliata)>
        EstraiStoricoVarianti(IEnumerable<RisultatoQuiz>? cronologia)
    {
        var storico = new Dictionary<(string Ling, string SchedaId, string VarianteId), (DateTime DataOra, bool Sbagliata)>();
        if (cronologia == null) return storico;

        // Ordine cronologico stabile: a parità di DataOra prevale il record successivo in lista.
        foreach (var risultato in cronologia.OrderBy(r => r.DataOra))
        {
            if (risultato.Dettagli == null || risultato.Dettagli.Count == 0) continue;

            var gruppi = new Dictionary<(string Ling, string SchedaId, string VarianteId), bool>();
            foreach (var d in risultato.Dettagli)
            {
                if (!TryEstraiChiave(d.IdDomanda, out var chiave)) continue;
                bool errato = d.PesoErrore() > 0;
                if (gruppi.TryGetValue(chiave, out bool giaErrato))
                    gruppi[chiave] = giaErrato || errato;
                else
                    gruppi[chiave] = errato;
            }

            foreach (var (chiave, sbagliata) in gruppi)
                storico[chiave] = (risultato.DataOra, sbagliata);
        }

        return storico;
    }

    private static bool TryEstraiChiave(
        string? idDomanda,
        out (string Ling, string SchedaId, string VarianteId) chiave)
    {
        chiave = default;
        if (string.IsNullOrEmpty(idDomanda) || !idDomanda.StartsWith("lab:", StringComparison.Ordinal))
            return false;

        var parti = idDomanda.Split(':');
        if (parti.Length != 5) return false;

        chiave = (parti[1], parti[2], parti[3]);
        return true;
    }
}
