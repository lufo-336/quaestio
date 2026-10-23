using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Una sezione (categoria) di una materia nella tab "Gestione contenuti".
/// Puo' essere nascosta dai quiz ("Nascondi", con conferma inline) o
/// ripristinata. <see cref="Esclusa"/> è fissa per istanza (il reload ricrea
/// gli item); solo <see cref="InAttesaConfermaNascondi"/> cambia a runtime.
/// </summary>
public class SezioneListItem : ObservableObject
{
    public string MateriaId { get; }
    /// <summary>Chiave canonica: e' quella che si passa a Nascondi/RipristinaSezione.</summary>
    public string Categoria { get; }
    public int ConteggioDomande { get; }
    public bool Esclusa { get; }
    public string Etichetta { get; }
    public string ConteggioEtichetta => string.Format(Strings.Gestione_NDomandeBreve, ConteggioDomande);

    public SezioneListItem(string materiaId, string materiaNome, string categoria, int conteggioDomande, bool esclusa)
    {
        MateriaId = materiaId;
        Categoria = categoria;
        Etichetta = EtichetteContenuti.Correnti.Categoria(materiaNome, categoria);
        ConteggioDomande = conteggioDomande;
        Esclusa = esclusa;
    }

    private bool _inAttesaConferma;
    public bool InAttesaConfermaNascondi
    {
        get => _inAttesaConferma;
        set
        {
            if (SetField(ref _inAttesaConferma, value))
            {
                RaisePropertyChanged(nameof(MostraBottoneNascondi));
                RaisePropertyChanged(nameof(MostraConfermaNascondi));
            }
        }
    }

    /// <summary>Bottone "Nascondi": solo su sezione attiva e non in conferma.</summary>
    public bool MostraBottoneNascondi => !Esclusa && !_inAttesaConferma;

    /// <summary>Conferma inline "Sicuro?": solo su sezione attiva e in attesa.</summary>
    public bool MostraConfermaNascondi => !Esclusa && _inAttesaConferma;

    /// <summary>Riga sbiadita quando la sezione è nascosta.</summary>
    public double OpacitaRiga => Esclusa ? 0.5 : 1.0;
}
