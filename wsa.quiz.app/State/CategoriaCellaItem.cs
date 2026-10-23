using Avalonia.Media;
using Wsa.Quiz.Core.Models.Statistiche;
using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Wrapper per una cella della heatmap statistiche: espone etichetta
/// formattata e i brush di bordo/sfondo dipendenti dalla PctErrore.
/// </summary>
public class CategoriaCellaItem
{
    public CategoriaStat Stat { get; }

    public CategoriaCellaItem(CategoriaStat stat)
    {
        Stat = stat;
    }

    public string Etichetta => $"{EtichetteContenuti.Correnti.Categoria(Stat.Materia, Stat.Categoria)} {Stat.PctErrore:P0}";
    public string Tooltip => string.Format(Strings.Statistiche_CellaTooltip, Stat.Errori, Stat.Visti);
    public IBrush ColoreBordo => QuizColors.HeatmapBordo(Stat.PctErrore);
    public IBrush ColoreSfondo => QuizColors.HeatmapSfondo(Stat.PctErrore);
}
