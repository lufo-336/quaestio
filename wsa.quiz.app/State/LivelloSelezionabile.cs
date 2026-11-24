using Avalonia.Media;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Risorse;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Un pulsante del riquadro "Punti deboli" in Home (rosse / ambra / verdi / nuove).
/// Il conteggio cambia con la selezione di materie e categorie.
/// </summary>
public class LivelloSelezionabile : ObservableObject
{
    /// <summary>Chiave canonica (<see cref="LivelliDebolezza"/>), salvata nelle preferenze.</summary>
    public string Id { get; }

    /// <summary>Nome nella lingua dell'interfaccia.</summary>
    public string Nome => Id switch
    {
        LivelliDebolezza.Rosse => Strings.Home_LivelloRosse,
        LivelliDebolezza.Ambra => Strings.Home_LivelloAmbra,
        LivelliDebolezza.Verdi => Strings.Home_LivelloVerdi,
        _                      => Strings.Home_LivelloNuove,
    };

    /// <summary>Colore del pallino visivo accanto al nome.</summary>
    public IBrush ColorePallino => Id switch
    {
        LivelliDebolezza.Rosse => QuizColors.RossoBrush,
        LivelliDebolezza.Ambra => QuizColors.AmbraBrush,
        LivelliDebolezza.Verdi => QuizColors.VerdeBrush,
        _                      => QuizColors.NeutroBrush,
    };

    private int _numDomande;
    public int NumDomande
    {
        get => _numDomande;
        set { if (SetField(ref _numDomande, value)) RaisePropertyChanged(nameof(Etichetta)); }
    }

    public string Etichetta => $"{Nome} · {NumDomande}";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public LivelloSelezionabile(string id) { Id = id; }
}
