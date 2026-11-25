using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Wsa.Quiz.Core.Models.Statistiche;
using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Riga categoria nel pannello dettaglio di una materia nelle Statistiche:
/// barra errore, contatori, pulsante Ripassa e lista errori espandibile inline.
/// </summary>
public class CategoriaStatItem : ObservableObject
{
    public CategoriaStat Stat { get; }

    public string MateriaCanon => Stat.Materia;
    public string CategoriaCanon => Stat.Categoria;
    public string NomeMostrato { get; }
    public string Livello { get; }
    public double PctErrore => Stat.PctErrore;
    public string PctFormattata => $"{Stat.PctErrore:P0}";
    public IBrush ColoreLivello => QuizColors.HeatmapBordo(Stat.PctErrore);
    public IBrush ColoreLivelloTesto => QuizColors.HeatmapTesto(Stat.PctErrore);

    public int Visti => Stat.Visti;
    public double Errori => Stat.Errori;
    public IReadOnlyList<DomandaSbagliata> Sbagliate => Stat.Sbagliate;
    public int NumSbagliate => Stat.Sbagliate.Count;
    public bool HaSbagliate => NumSbagliate > 0;

    public string RiepilogoNumeri { get; }
    public string EtichettaRipassa => string.Format(Strings.Statistiche_RipassaConConteggio, NumSbagliate);
    public string EtichettaSbagliate => NumSbagliate switch
    {
        0 => Strings.Statistiche_ZeroSbagliate,
        1 => Strings.Statistiche_UnaSbagliata,
        _ => string.Format(Strings.Statistiche_NSbagliate, NumSbagliate)
    };

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetField(ref _isExpanded, value))
            {
                RaisePropertyChanged(nameof(MostraSbagliateInline));
                RaisePropertyChanged(nameof(FrecciaEspansione));
            }
        }
    }

    public bool MostraSbagliateInline => _isExpanded && HaSbagliate;
    public string FrecciaEspansione => _isExpanded ? "▾" : "▸";

    public CategoriaStatItem(CategoriaStat stat)
    {
        Stat = stat;
        NomeMostrato = EtichetteContenuti.Correnti.Categoria(stat.Materia, stat.Categoria);
        Livello = QuizService.LivelloDi(stat.PctErrore);
        string errFmt = stat.Errori.ToString("0.##");
        RiepilogoNumeri = string.Format(Strings.Statistiche_CellaTooltip, errFmt, stat.Visti);
    }

    public RichiestaRipasso CreaRichiestaRipasso()
    {
        var ids = Stat.Sbagliate
            .Select(s => s.IdDomanda)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new RichiestaRipasso(Stat.Materia, Stat.Categoria, ids);
    }

    public void NotificaCambioTema() => RaisePropertyChanged(nameof(ColoreLivelloTesto));
}
