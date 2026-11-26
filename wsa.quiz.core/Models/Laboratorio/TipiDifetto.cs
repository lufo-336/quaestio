using System.Globalization;
using Wsa.Quiz.Core.Risorse;

namespace Wsa.Quiz.Core.Models.Laboratorio;

/// <summary>
/// Vocabolario fisso dei tipi di difetto ammessi nelle schede del Laboratorio.
/// Le etichette localizzate stanno in <see cref="Strings"/> con prefisso <c>Lab_Difetto_</c>.
/// </summary>
public static class TipiDifetto
{
    public const string Confine = "Confine";
    public const string DivisioneIntera = "DivisioneIntera";
    public const string NullVuoto = "NullVuoto";
    public const string CondizioneInvertita = "CondizioneInvertita";
    public const string Overflow = "Overflow";
    public const string OperandoSbagliato = "OperandoSbagliato";
    public const string ConfrontoStringhe = "ConfrontoStringhe";
    public const string MutazioneIterazione = "MutazioneIterazione";
    public const string EccezioneInghiottita = "EccezioneInghiottita";
    public const string RisorsaNonChiusa = "RisorsaNonChiusa";

    private static readonly CultureInfo CulturaCanonica = new("it");

    /// <summary>Elenco fisso degli id nell'ordine in cui vengono mostrati nella UI.</summary>
    public static readonly string[] Tutti =
    {
        Confine,
        DivisioneIntera,
        NullVuoto,
        CondizioneInvertita,
        Overflow,
        OperandoSbagliato,
        ConfrontoStringhe,
        MutazioneIterazione,
        EccezioneInghiottita,
        RisorsaNonChiusa,
    };

    /// <summary>True se <paramref name="id"/> appartiene al vocabolario.</summary>
    public static bool Valido(string id) => Tutti.Contains(id, StringComparer.Ordinal);

    /// <summary>Restituisce l'etichetta localizzata nella lingua corrente dell'app.</summary>
    public static string Etichetta(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;
        return Strings.ResourceManager.GetString($"Lab_Difetto_{id}", Strings.Culture) ?? id;
    }

    /// <summary>
    /// Restituisce l'etichetta canonica in italiano, usata come <c>Categoria</c>
    /// nei <see cref="DettaglioRisposta"/> così le statistiche restano coerenti al cambio lingua.
    /// </summary>
    public static string EtichettaCanonica(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;
        return Strings.ResourceManager.GetString($"Lab_Difetto_{id}", CulturaCanonica) ?? id;
    }
}
