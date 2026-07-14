using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.Views;

/// <summary>
/// Schermata "Impostazioni": toggle tema chiaro/scuro (Step 10 + Step 13).
/// E' la sua stessa DataContext, stesso pattern non-MVVM delle altre view.
/// </summary>
public partial class ImpostazioniView : UserControl, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;

    private StorageService? _storage;

    private bool _temaScuro;
    public bool TemaScuro
    {
        get => _temaScuro;
        set
        {
            if (_temaScuro == value) return;
            _temaScuro = value;
            Raise();
            ApplicaTema();
            Salva();
        }
    }

    public ImpostazioniView()
    {
        InitializeComponent();
        DataContext = this;
    }

    /// <summary>Chiamato dalla MainView al boot con le preferenze gia' caricate.</summary>
    public void Inizializza(StorageService storage, PreferenzeUtente preferenze)
    {
        _storage = storage;
        _temaScuro = preferenze.TemaScuro;
        Raise(nameof(TemaScuro));
    }

    private void ApplicaTema()
    {
        Application.Current!.RequestedThemeVariant = _temaScuro ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private void Salva()
    {
        if (_storage == null) return;
        var prefs = _storage.CaricaPreferenze();
        prefs.TemaScuro = _temaScuro;
        _storage.SalvaPreferenze(prefs);
    }

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
