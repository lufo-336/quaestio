using Avalonia.Media;
using Wsa.Quiz.Core.Models;

using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Wrapper di una singola riga di dettaglio nella vista di dettaglio cronologia.
/// A differenza di <see cref="DettaglioRisposta"/> (che e' nel Core e quindi
/// non puo' avere riferimenti UI), espone gia' i colori e i flag che servono
/// al template XAML per disegnare correttamente corrette e sbagliate.
/// </summary>
public class DettaglioRispostaItem : ObservableObject
{
    public DettaglioRisposta Dettaglio { get; }

    // Nomi nella lingua corrente: in cronologia restano le chiavi canoniche.
    public string MateriaNome => EtichetteContenuti.Correnti.Materia(Dettaglio.MateriaNome);
    public string Categoria => EtichetteContenuti.Correnti.Categoria(Dettaglio.MateriaNome, Dettaglio.Categoria);
    public string TestoDomanda => Dettaglio.TestoDomanda;
    public string RispostaData => Dettaglio.RispostaData;
    public string RispostaCorretta => Dettaglio.RispostaCorretta;
    public string Spiegazione => Dettaglio.Spiegazione;
    public bool Corretta => Dettaglio.Corretta;
    public int Tentativi => Dettaglio.Tentativi;

    /// <summary>Mostra la riga "Risposta corretta" sotto, solo se la risposta data era sbagliata.</summary>
    public bool MostraRispostaCorretta => !Dettaglio.Corretta;

    /// <summary>Mostra la riga del numero di tentativi, solo se >1 (rotazione).</summary>
    public bool MostraTentativi => Dettaglio.Tentativi > 1;

    public string TentativiEtichetta => string.Format(Strings.Dettaglio_CorrettaDopoTentativi, Dettaglio.Tentativi);

    /// <summary>Mostra l'etichetta del secondo colpo se indovinata al secondo tentativo.</summary>
    public bool MostraSecondoTentativo => Dettaglio.SecondoTentativo && Dettaglio.Corretta;

    public string SecondoTentativoEtichetta => Strings.Dettaglio_SecondoTentativo;

    public IBrush ColoreBordo => QuizColors.Esito(Dettaglio.Corretta);
    public IBrush ColoreRisposta => QuizColors.EsitoTesto(Dettaglio.Corretta);
    public string EsitoEtichetta { get; }
    public IBrush ColoreEsito => QuizColors.EsitoTesto(Dettaglio.Corretta);

    public DettaglioRispostaItem(DettaglioRisposta d)
    {
        Dettaglio = d;
        EsitoEtichetta = d.Corretta ? Strings.Dettaglio_Corretto : Strings.Quiz_Sbagliato;
    }

    public void NotificaCambioTema()
    {
        RaisePropertyChanged(nameof(ColoreRisposta));
        RaisePropertyChanged(nameof(ColoreEsito));
    }
}
