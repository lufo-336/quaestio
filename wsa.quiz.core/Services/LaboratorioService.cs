using System.Text.Json;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Models.Laboratorio;
using Wsa.Quiz.Core.Risorse;

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
    /// valorizzando <see cref="SchedaLab.Linguaggio"/> e sovrapponendo l'eventuale
    /// traduzione per <paramref name="lingua"/>. Un file illeggibile viene saltato
    /// senza bloccare l'avvio dell'app.
    /// </summary>
    public static List<SchedaLab> CaricaSchede(IFonteDati fonte, string lingua = Lingue.Base)
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
                    if (!string.IsNullOrWhiteSpace(lingua) && lingua != Lingue.Base)
                        ApplicaTraduzione(fonte, scheda, lingua);
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

    /// <summary>
    /// Sovrappone titolo, requisito, indizi e spiegazioni tradotti da
    /// <c>traduzioni/&lt;lingua&gt;/laboratorio/&lt;linguaggio&gt;/&lt;schedaId&gt;.json</c>.
    /// Mai bloccante: file assente o rotto lascia la scheda in italiano;
    /// campo nullo o vuoto resta in italiano; variante con numero di difetti
    /// diverso resta in italiano.
    /// </summary>
    private static void ApplicaTraduzione(IFonteDati fonte, SchedaLab scheda, string lingua)
    {
        string percorso = $"traduzioni/{lingua}/laboratorio/{scheda.Linguaggio}/{scheda.Id}.json";
        if (!fonte.Esiste(percorso)) return;

        TraduzioneScheda? t;
        try
        {
            t = JsonSerializer.Deserialize<TraduzioneScheda>(fonte.LeggiTesto(percorso), StorageService.OpzioniLettura);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ATTENZIONE] Traduzione '{percorso}' illeggibile: {ex.Message}");
            return;
        }
        if (t == null) return;

        if (!string.IsNullOrWhiteSpace(t.Titolo)) scheda.Titolo = t.Titolo;
        if (!string.IsNullOrWhiteSpace(t.Requisito)) scheda.Requisito = t.Requisito;
        if (t.Varianti == null) return;

        var perId = new Dictionary<string, VarianteLab>(StringComparer.Ordinal);
        foreach (var v in scheda.Varianti)
            perId.TryAdd(v.Id, v);

        foreach (var (varId, tv) in t.Varianti)
        {
            if (!perId.TryGetValue(varId, out var variante))
            {
                Console.Error.WriteLine($"[ATTENZIONE] {percorso}: variante '{varId}' senza corrispondenza.");
                continue;
            }
            if (tv?.Difetti == null) continue;
            if (tv.Difetti.Count != variante.Difetti.Count)
            {
                Console.Error.WriteLine($"[ATTENZIONE] {percorso}: variante '{varId}' ha {tv.Difetti.Count} difetti invece di {variante.Difetti.Count}.");
                continue;
            }
            for (int i = 0; i < variante.Difetti.Count; i++)
            {
                var td = tv.Difetti[i];
                if (td == null) continue;
                if (!string.IsNullOrWhiteSpace(td.Indizio)) variante.Difetti[i].Indizio = td.Indizio;
                if (!string.IsNullOrWhiteSpace(td.Spiegazione)) variante.Difetti[i].Spiegazione = td.Spiegazione;
            }
        }
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

    // ------------------------------------------------------------------ REGOLE DI CORREZIONE

    /// <summary>True se le righe previste coincidono esattamente, nell'ordine, con <see cref="VarianteLab.Output"/>.</summary>
    public static bool OutputCorretto(VarianteLab? variante, IReadOnlyList<string>? righe)
    {
        if (variante == null || righe == null) return false;
        return righe.Count == variante.Output.Count
            && righe.SequenceEqual(variante.Output, StringComparer.Ordinal);
    }

    /// <summary>True se <paramref name="riga"/> (1-based) coincide con la riga del difetto principale.</summary>
    public static bool BugRigaCorretta(VarianteLab? variante, int riga)
    {
        if (variante == null || variante.Difetti.Count == 0) return false;
        return riga == variante.Difetti[0].Riga;
    }

    /// <summary>True se riga (1-based) e tipo coincidono con il difetto della variante (livello base).</summary>
    public static bool BugTipoCorretto(VarianteLab? variante, int riga, string? tipo)
    {
        if (variante == null || variante.Difetti.Count == 0) return false;
        var difetto = variante.Difetti[0];
        return riga == difetto.Riga && string.Equals(tipo, difetto.Tipo, StringComparison.Ordinal);
    }

    /// <summary>True se riga (1-based) e correzione coincidono con il difetto della variante (livello media).</summary>
    public static bool BugCorrezioneCorretta(VarianteLab? variante, int riga, string? correzione)
    {
        if (variante == null || variante.Difetti.Count == 0) return false;
        var difetto = variante.Difetti[0];
        return riga == difetto.Riga && string.Equals(correzione, difetto.Correzione, StringComparison.Ordinal);
    }

    /// <summary>True se l'input candidato all'indice indicato espone un difetto.</summary>
    public static bool InputCorretto(VarianteLab? variante, int indice)
    {
        if (variante == null || indice < 0 || indice >= variante.Input.Count) return false;
        return variante.Input[indice].Espone;
    }

    /// <summary>
    /// Calcola il punteggio normalizzato (0..1) della review completa (livello alta):
    /// +1 per ogni difetto segnato sulla riga giusta con il tipo giusto,
    /// +0,5 se sulla riga giusta con tipo sbagliato,
    /// -1 per ogni segno su una riga senza difetti;
    /// il totale viene diviso per il numero di difetti e limitato a [0, 1].
    /// </summary>
    public static double PunteggioReview(VarianteLab? variante, IReadOnlyDictionary<int, string>? segni)
    {
        if (variante == null || variante.Difetti.Count == 0 || segni == null || segni.Count == 0)
            return 0.0;

        var righeDifetti = new Dictionary<int, string>();
        foreach (var d in variante.Difetti)
            righeDifetti.TryAdd(d.Riga, d.Tipo);

        double punti = 0.0;
        foreach (var d in variante.Difetti)
        {
            if (segni.TryGetValue(d.Riga, out var tipoSegnato))
                punti += string.Equals(tipoSegnato, d.Tipo, StringComparison.Ordinal) ? 1.0 : 0.5;
        }

        foreach (var (riga, _) in segni)
        {
            if (!righeDifetti.ContainsKey(riga))
                punti -= 1.0;
        }

        return Math.Clamp(punti / variante.Difetti.Count, 0.0, 1.0);
    }

    /// <summary>
    /// Converte il punteggio della review (0..1) nei flag per l'archivio:
    /// 1 → giusto (<c>Corretta = true, SecondoTentativo = false</c>);
    /// 0,5 ≤ p &lt; 1 → peso ½ (<c>Corretta = true, SecondoTentativo = true</c>);
    /// &lt; 0,5 → sbagliato (<c>Corretta = false, SecondoTentativo = false</c>).
    /// </summary>
    public static (bool Corretta, bool SecondoTentativo) EsitoReview(double punteggio)
    {
        if (punteggio >= 1.0) return (true, false);
        if (punteggio >= 0.5) return (true, true);
        return (false, false);
    }

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

    /// <summary>
    /// Dagli id sbagliati (<c>lab:&lt;linguaggio&gt;:&lt;scheda&gt;:&lt;variante&gt;:&lt;passo&gt;</c>)
    /// ricava le coppie (scheda, variante) ancora esistenti in <paramref name="schede"/>,
    /// mantenendo al più una coppia per scheda (la prima incontrata).
    /// </summary>
    public static List<(SchedaLab Scheda, VarianteLab Variante)> CoppieDaId(
        IEnumerable<SchedaLab>? schede,
        IEnumerable<string>? idSbagliati)
    {
        var risultato = new List<(SchedaLab Scheda, VarianteLab Variante)>();
        if (schede == null || idSbagliati == null) return risultato;

        var perChiave = new Dictionary<(string Ling, string SchedaId, string VarianteId), (SchedaLab Scheda, VarianteLab Variante)>();
        foreach (var s in schede)
        {
            foreach (var v in s.Varianti)
                perChiave.TryAdd((s.Linguaggio, s.Id, v.Id), (s, v));
        }

        if (perChiave.Count == 0) return risultato;

        var schedeViste = new HashSet<(string Ling, string SchedaId)>();
        foreach (var id in idSbagliati)
        {
            if (!TryEstraiChiave(id, out var chiave)) continue;
            if (!perChiave.TryGetValue(chiave, out var coppia)) continue;
            if (!schedeViste.Add((chiave.Ling, chiave.SchedaId))) continue;
            risultato.Add(coppia);
        }

        return risultato;
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
