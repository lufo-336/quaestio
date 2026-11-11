using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Rappresenta un colore d'accento selezionabile nella schermata Impostazioni.
/// </summary>
public sealed class ColoreSelezionabile : ObservableObject
{
    public string Id { get; }
    public string Nome { get; }
    public IBrush Pennello { get; }

    private bool _selezionato;
    public bool Selezionato
    {
        get => _selezionato;
        set
        {
            if (SetField(ref _selezionato, value))
            {
                RaisePropertyChanged(nameof(BordoSelezione));
            }
        }
    }

    public IBrush BordoSelezione
    {
        get
        {
            if (!_selezionato) return Brushes.Transparent;
            // Dal tema ATTIVO, non da TryFindResource: sull'Application restituisce il
            // valore del tema chiaro anche in tema scuro (anello nero su nero).
            return Application.Current?.ActualThemeVariant == ThemeVariant.Dark
                ? Brushes.White
                : Brushes.Black;
        }
    }

    public ColoreSelezionabile(string id, string nome, IBrush pennello, bool selezionato = false)
    {
        Id = id;
        Nome = nome;
        Pennello = pennello;
        _selezionato = selezionato;
    }

    public void NotificaCambioTema()
    {
        if (_selezionato)
            RaisePropertyChanged(nameof(BordoSelezione));
    }
}
