using System.Text;

namespace Wsa.Quiz.Core.Services;

/// <summary>Tipo di un pezzo di testo di domanda/risposta/spiegazione.</summary>
public enum TipoSegmento { Testo, Inline, Blocco }

/// <summary>
/// Un pezzo di testo. Per <see cref="TipoSegmento.Blocco"/>: <c>Testo</c> = righe di
/// codice unite da \n (senza le righe ```), <c>Linguaggio</c> = parola dopo ``` o null.
/// </summary>
public sealed record SegmentoTesto(TipoSegmento Tipo, string Testo, string? Linguaggio = null);

/// <summary>
/// Riconosce il codice nei testi delle domande: blocchi ```lang ... ``` e
/// codice inline tra backtick singoli. Usato da console e GUI, cosi' il
/// formato e' interpretato allo stesso modo ovunque. Mai eccezioni: un
/// backtick non chiuso resta testo, un ``` non chiuso arriva a fine testo.
/// </summary>
public static class TestoConCodice
{
    public static IReadOnlyList<SegmentoTesto> Analizza(string? testo)
    {
        var risultato = new List<SegmentoTesto>();
        if (string.IsNullOrEmpty(testo)) return risultato;

        string s = testo.Replace("\r\n", "\n");
        var buf = new StringBuilder();
        bool dopoBlocco = false; // i \n subito dopo un blocco non vanno nel testo

        void Flush()
        {
            string t = buf.ToString();
            buf.Clear();
            if (dopoBlocco) t = t.TrimStart('\n');
            if (t.Length > 0) risultato.Add(new SegmentoTesto(TipoSegmento.Testo, t));
            dopoBlocco = false;
        }

        int i = 0;
        while (i < s.Length)
        {
            if (string.CompareOrdinal(s, i, "```", 0, 3) == 0)
            {
                // il blocco va a capo da solo: via i \n prima del fence
                while (buf.Length > 0 && buf[^1] == '\n') buf.Length--;
                Flush();

                int fineRiga = s.IndexOf('\n', i + 3);
                if (fineRiga < 0) fineRiga = s.Length;
                string lang = s.Substring(i + 3, fineRiga - i - 3).Trim();

                int inizio = Math.Min(fineRiga + 1, s.Length);
                int chiusura = s.IndexOf("```", inizio, StringComparison.Ordinal);
                int fine = chiusura < 0 ? s.Length : chiusura;

                string codice = s.Substring(inizio, fine - inizio).Trim('\n');
                risultato.Add(new SegmentoTesto(TipoSegmento.Blocco, codice, lang.Length > 0 ? lang : null));

                i = chiusura < 0 ? s.Length : chiusura + 3;
                dopoBlocco = true;
                continue;
            }

            if (s[i] == '`')
            {
                int chiusura = s.IndexOf('`', i + 1);
                if (chiusura > i + 1)
                {
                    Flush();
                    risultato.Add(new SegmentoTesto(TipoSegmento.Inline, s.Substring(i + 1, chiusura - i - 1)));
                    i = chiusura + 1;
                    continue;
                }
            }

            buf.Append(s[i]);
            i++;
        }

        Flush();
        return risultato;
    }

    /// <summary>Il testo come lo legge l'utente: senza backtick e senza le righe ```.</summary>
    public static string SenzaMarcatori(string? testo)
    {
        var sb = new StringBuilder();
        var segmenti = Analizza(testo);
        for (int k = 0; k < segmenti.Count; k++)
        {
            var seg = segmenti[k];
            if (seg.Tipo == TipoSegmento.Blocco)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(seg.Testo);
                if (k < segmenti.Count - 1) sb.Append('\n');
            }
            else sb.Append(seg.Testo);
        }
        return sb.ToString();
    }
}
