using System.Globalization;

namespace Wsa.Quiz.Core.Risorse;

/// <summary>Una lingua selezionabile. ToString() = nome nativo, per i ComboBox.</summary>
public sealed record VoceLingua(string Codice, string Nome)
{
    public override string ToString() => Nome;
}

/// <summary>
/// Lingue dell'app. La lingua e' globale al processo: e' la CultureInfo corrente,
/// letta dalla classe Strings generata e dalla formattazione di date e numeri.
/// Aggiungere una lingua = un Strings.&lt;codice&gt;.resx + una riga in Supportate.
/// </summary>
public static class Lingue
{
    public const string Base = "it";

    public static readonly IReadOnlyList<VoceLingua> Supportate =
    [
        new("it", "Italiano"),
        new("en", "English"),
    ];

    /// <summary>Lingua da usare: preferenza se supportata, poi quella di sistema, poi inglese.</summary>
    public static string Risolvi(string? preferenza, CultureInfo sistema)
    {
        if (!string.IsNullOrEmpty(preferenza) && EsisteLingua(preferenza)) return preferenza;
        string sys = sistema.TwoLetterISOLanguageName;
        return EsisteLingua(sys) ? sys : "en";
    }

    private static CultureInfo? _cultura;

    /// <summary>
    /// Cultura dell'app: da usare per formattare date e numeri. E' un campo
    /// statico e non CultureInfo.CurrentCulture perche' quella e' AsyncLocal: i
    /// callback accodati prima di un cambio lingua (timer, job del dispatcher)
    /// la vedrebbero ancora vecchia.
    /// </summary>
    public static CultureInfo Cultura => _cultura ?? CultureInfo.CurrentUICulture;

    /// <summary>Imposta la lingua per tutto il processo, anche per i callback gia' accodati.</summary>
    public static void Applica(string codice)
    {
        var c = new CultureInfo(codice);
        _cultura = c;
        Strings.Culture = c;
        CultureInfo.CurrentUICulture = c;
        CultureInfo.CurrentCulture = c;
        CultureInfo.DefaultThreadCurrentUICulture = c;
        CultureInfo.DefaultThreadCurrentCulture = c;
    }

    /// <summary>Codice della lingua attiva, ricondotto a una supportata (fallback: base).</summary>
    public static string Corrente
    {
        get
        {
            string c = Cultura.TwoLetterISOLanguageName;
            return EsisteLingua(c) ? c : Base;
        }
    }

    private static bool EsisteLingua(string codice) => Supportate.Any(l => l.Codice == codice);
}
