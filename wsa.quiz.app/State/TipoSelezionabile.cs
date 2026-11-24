using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Risorse;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Un pulsante del riquadro "Tipo di domande" in Home (codice / teoria /
/// architettura). Il conteggio cambia con la selezione di materie e categorie.
/// </summary>
public class TipoSelezionabile : ObservableObject
{
    /// <summary>Chiave canonica (<see cref="TipiDomanda"/>), salvata nelle preferenze.</summary>
    public string Id { get; }

    /// <summary>Nome nella lingua dell'interfaccia.</summary>
    public string Nome => Id switch
    {
        TipiDomanda.Codice => Strings.Home_TipoCodice,
        TipiDomanda.Teoria => Strings.Home_TipoTeoria,
        _                  => Strings.Home_TipoArchitettura,
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

    public TipoSelezionabile(string id) { Id = id; }
}
