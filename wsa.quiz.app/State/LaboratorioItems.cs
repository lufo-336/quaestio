using System;
using System.Collections.Generic;
using Avalonia.Media;
using Wsa.Quiz.Core.Risorse;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Singola riga di codice nella <c>LaboratorioView</c>.
/// Espone il numero di riga 1-based e gli stati visivi (selezione / esito).
/// </summary>
public class RigaCodiceItem : ObservableObject
{
    public int Numero { get; }
    public string Testo { get; }
    public string NumeroFormattato => Numero.ToString();

    private bool _interattiva;
    public bool Interattiva
    {
        get => _interattiva;
        set => SetField(ref _interattiva, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    private bool _isCorretta;
    public bool IsCorretta
    {
        get => _isCorretta;
        set => SetField(ref _isCorretta, value);
    }

    private bool _isSbagliata;
    public bool IsSbagliata
    {
        get => _isSbagliata;
        set => SetField(ref _isSbagliata, value);
    }

    public RigaCodiceItem(int numero, string testo)
    {
        Numero = numero;
        Testo = testo;
    }
}

/// <summary>
/// Riga di menu a tendina per una singola riga stampata nel passo 1 base ("Prevedi l'output").
/// </summary>
public class MenuOutputItem : ObservableObject
{
    public int Indice { get; }
    public IReadOnlyList<string> Voci { get; }
    public string EtichettaRiga => string.Format(Strings.Lab_RigaOutputN, Indice + 1);

    private string? _voceSelezionata;
    public string? VoceSelezionata
    {
        get => _voceSelezionata;
        set => SetField(ref _voceSelezionata, value);
    }

    private bool _puoModificare = true;
    public bool PuoModificare
    {
        get => _puoModificare;
        set => SetField(ref _puoModificare, value);
    }

    private bool _mostraEsito;
    public bool MostraEsito
    {
        get => _mostraEsito;
        private set => SetField(ref _mostraEsito, value);
    }

    private bool _isCorretta;
    public bool IsCorretta
    {
        get => _isCorretta;
        private set
        {
            if (SetField(ref _isCorretta, value))
                AggiornaStatoVisivo();
        }
    }

    private bool _isSbagliata;
    public bool IsSbagliata
    {
        get => _isSbagliata;
        private set
        {
            if (SetField(ref _isSbagliata, value))
                AggiornaStatoVisivo();
        }
    }

    private string _valoreAtteso = string.Empty;
    public string ValoreAtteso
    {
        get => _valoreAtteso;
        private set
        {
            if (SetField(ref _valoreAtteso, value))
                RaisePropertyChanged(nameof(EtichettaEsitoRiga));
        }
    }

    public string EtichettaEsitoRiga => !MostraEsito
        ? string.Empty
        : IsCorretta
            ? "✓"
            : $"✗  {string.Format(Strings.Lab_OutputAttesoPrefisso, ValoreAtteso)}";

    public IBrush ColoreBordo => !MostraEsito
        ? Brushes.Transparent
        : QuizColors.Esito(IsCorretta);

    public IBrush ColoreEsitoTesto => !MostraEsito
        ? QuizColors.TempoNeutro
        : QuizColors.EsitoTesto(IsCorretta);

    public MenuOutputItem(int indice, IReadOnlyList<string> voci)
    {
        Indice = indice;
        Voci = voci;
    }

    /// <summary>Imposta l'esito della riga dopo la conferma.</summary>
    public void ImpostaEsito(string valoreAtteso)
    {
        bool ok = string.Equals(_voceSelezionata, valoreAtteso, StringComparison.Ordinal);
        ValoreAtteso = valoreAtteso;
        PuoModificare = false;
        MostraEsito = true;
        IsCorretta = ok;
        IsSbagliata = !ok;
        AggiornaStatoVisivo();
    }

    public void NotificaCambioTema() => RaisePropertyChanged(nameof(ColoreEsitoTesto));

    private void AggiornaStatoVisivo()
    {
        RaisePropertyChanged(nameof(EtichettaEsitoRiga));
        RaisePropertyChanged(nameof(ColoreBordo));
        RaisePropertyChanged(nameof(ColoreEsitoTesto));
    }
}

/// <summary>
/// Pulsante del vocabolario tipi di difetto nel passo 2 base ("Trova il bug").
/// </summary>
public class TipoDifettoItem : ObservableObject
{
    public string Id { get; }
    public string Etichetta { get; }

    private bool _interattivo = true;
    public bool Interattivo
    {
        get => _interattivo;
        set => SetField(ref _interattivo, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    private bool _isCorretta;
    public bool IsCorretta
    {
        get => _isCorretta;
        set => SetField(ref _isCorretta, value);
    }

    private bool _isSbagliata;
    public bool IsSbagliata
    {
        get => _isSbagliata;
        set => SetField(ref _isSbagliata, value);
    }

    public TipoDifettoItem(string id, string etichetta)
    {
        Id = id;
        Etichetta = etichetta;
    }
}

/// <summary>
/// Input candidato selezionabile nel passo 3 ("Rompi la funzione").
/// </summary>
public class InputCandidatoItem : ObservableObject
{
    public int Indice { get; }
    public string Chiamata { get; }
    public string Atteso { get; }
    public string Effettivo { get; }
    public bool Espone { get; }

    private bool _interattivo = true;
    public bool Interattivo
    {
        get => _interattivo;
        set => SetField(ref _interattivo, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    private bool _isCorretta;
    public bool IsCorretta
    {
        get => _isCorretta;
        set => SetField(ref _isCorretta, value);
    }

    private bool _isSbagliata;
    public bool IsSbagliata
    {
        get => _isSbagliata;
        set => SetField(ref _isSbagliata, value);
    }

    private bool _mostraDettaglioEsito;
    public bool MostraDettaglioEsito
    {
        get => _mostraDettaglioEsito;
        set
        {
            if (SetField(ref _mostraDettaglioEsito, value))
                RaisePropertyChanged(nameof(MostraBadgeEspone));
        }
    }

    public bool MostraBadgeEspone => _mostraDettaglioEsito && Espone;

    public InputCandidatoItem(int indice, string chiamata, string atteso, string effettivo, bool espone)
    {
        Indice = indice;
        Chiamata = chiamata;
        Atteso = atteso;
        Effettivo = effettivo;
        Espone = espone;
    }
}

/// <summary>
/// Linguaggio selezionabile nel riquadro Laboratorio della Home.
/// </summary>
public class LinguaggioLabItem : ObservableObject
{
    public string Id { get; }
    public string Nome { get; }
    public int NumSchede { get; }
    public string Etichetta => $"{Nome}  ({NumSchede})";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public LinguaggioLabItem(string id, string nome, int numSchede)
    {
        Id = id;
        Nome = nome;
        NumSchede = numSchede;
    }
}
