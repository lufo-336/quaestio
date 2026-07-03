using Wsa.Quiz.App.Views;
using Wsa.Quiz.Core.Models;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Wrapper di una riga della tabella materie in "Gestione contenuti".
/// Observable solo per la conferma inline dell'eliminazione (solo le
/// materie importate sono eliminabili: la UI nasconde il bottone se
/// !Importata, vedi GestioneContenutiView.axaml).
/// </summary>
public class MateriaListItem : ObservableObject, IConfermaEliminazione
{
    public string Id { get; }
    public string Nome { get; }
    public int ConteggioDomande { get; }
    public bool Importata { get; }
    public string Badge => Importata ? "Importata" : "Built-in";

    private bool _inAttesaConferma;
    public bool InAttesaConfermaEliminazione
    {
        get => _inAttesaConferma;
        set
        {
            if (SetField(ref _inAttesaConferma, value))
                RaisePropertyChanged(nameof(NonInAttesaConferma));
        }
    }

    public bool NonInAttesaConferma => !_inAttesaConferma;

    public MateriaListItem(Materia materia, int conteggioDomande)
    {
        Id = materia.Id;
        Nome = materia.Nome;
        ConteggioDomande = conteggioDomande;
        Importata = materia.Importata;
    }
}
