using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Models.Statistiche;
using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Voce materia nella lista laterale (desktop) o nella schermata iniziale (mobile)
/// delle Statistiche: riepilogo errori, conteggi per livello e lista categorie.
/// </summary>
public class MateriaStatItem : ObservableObject
{
    public StatistichePerMateria Stat { get; }
    public string MateriaCanon => Stat.Materia;
    public string MateriaMostrata { get; }
    public IReadOnlyList<CategoriaStatItem> Categorie { get; }

    public int TotaleViste { get; }
    public double TotaleErrori { get; }
    public double PctErrore => TotaleViste == 0 ? 0.0 : TotaleErrori / TotaleViste;
    public string PctFormattata => $"{PctErrore:P0}";
    public IBrush ColoreLivello => QuizColors.HeatmapBordo(PctErrore);

    public int CountRosse { get; }
    public int CountAmbra { get; }
    public int CountVerdi { get; }
    public int TotaleCategorie => Categorie.Count;

    public bool HaRosse => CountRosse > 0;
    public bool HaAmbra => CountAmbra > 0;
    public bool HaVerdi => CountVerdi > 0;

    public IReadOnlyList<string> IdSbagliateUniche { get; }
    public int NumSbagliate => IdSbagliateUniche.Count;
    public bool HaSbagliate => NumSbagliate > 0;

    /// <summary>Pulsante rapido sulla card materia: visibile solo su mobile (su desktop e' nel pannello destro).</summary>
    public bool MostraRipassaSuCard => AppEnv.TouchMode && HaSbagliate;

    public string EtichettaRipassaBreve => string.Format(Strings.Statistiche_RipassaConConteggio, NumSbagliate);
    public string EtichettaRipassaMateria => string.Format(Strings.Statistiche_RipassaMateria, NumSbagliate);
    public string RiepilogoDettaglio => string.Format(Strings.Statistiche_RiepilogoMateria, TotaleViste, NumSbagliate);

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public MateriaStatItem(StatistichePerMateria stat)
    {
        Stat = stat;
        MateriaMostrata = EtichetteContenuti.Correnti.Materia(stat.Materia);
        Categorie = stat.Categorie.Select(c => new CategoriaStatItem(c)).ToList();

        TotaleViste = stat.Categorie.Sum(c => c.Visti);
        TotaleErrori = stat.Categorie.Sum(c => c.Errori);

        CountRosse = Categorie.Count(c => c.Livello == LivelliDebolezza.Rosse);
        CountAmbra = Categorie.Count(c => c.Livello == LivelliDebolezza.Ambra);
        CountVerdi = Categorie.Count(c => c.Livello == LivelliDebolezza.Verdi);

        IdSbagliateUniche = stat.Categorie
            .SelectMany(c => c.Sbagliate)
            .Select(s => s.IdDomanda)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public RichiestaRipasso CreaRichiestaRipasso()
        => new(Stat.Materia, null, IdSbagliateUniche);
}
