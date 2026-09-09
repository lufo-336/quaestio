using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
        => Inizializza(storage, preferenze, new List<Materia>(), new List<Domanda>());

    /// <summary>
    /// Variante con i contenuti caricati, per il riquadro About (step 14).
    /// Le domande sono quelle ATTIVE (al netto delle sezioni nascoste), le stesse
    /// che la MainView conta nell'header.
    /// </summary>
    public void Inizializza(StorageService storage, PreferenzeUtente preferenze,
                            IReadOnlyCollection<Materia> materie,
                            IReadOnlyCollection<Domanda> domandeAttive)
    {
        _storage = storage;
        _temaScuro = preferenze.TemaScuro;

        _percorsoDati = storage.CartellaUtente;
        _contenutiEtichetta = $"{Plurale(materie.Count, "materia", "materie")} · " +
                              $"{Plurale(domandeAttive.Count, "domanda attiva", "domande attive")}";

        Raise(nameof(TemaScuro));
        Raise(nameof(PercorsoDati));
        Raise(nameof(ContenutiEtichetta));
    }

    private static string Plurale(int n, string singolare, string plurale)
        => $"{n} {(n == 1 ? singolare : plurale)}";

    // ------------------------------------------------------------------ ABOUT (step 14)

    /// <summary>Indirizzo del repository, mostrato e apribile dai crediti.</summary>
    public const string LinkRepo = "https://github.com/lufo-336/quaestio";

    /// <summary>Versione dell'app, da Directory.Build.props via attributi dell'assembly.</summary>
    public string Versione { get; } = LeggiVersione();

    private string _percorsoDati = "";
    /// <summary>Cartella con cronologia, sospesi, preferenze e contenuti importati.</summary>
    public string PercorsoDati => _percorsoDati;

    private string _contenutiEtichetta = "";
    /// <summary>Riassunto dei contenuti caricati, es. "10 materie · 2163 domande attive".</summary>
    public string ContenutiEtichetta => _contenutiEtichetta;

    /// <summary>
    /// Su Android non c'e' un file manager a cui passare la cartella dati, quindi
    /// il bottone "Apri cartella" resta nascosto.
    /// </summary>
    public bool MostraApriCartella => !AppEnv.TouchMode;

    private static string LeggiVersione()
    {
        var asm = typeof(ImpostazioniView).Assembly;

        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            // SourceLink accoda "+<hash del commit>": all'utente non serve.
            int piu = info.IndexOf('+');
            return piu >= 0 ? info[..piu] : info;
        }

        return asm.GetName().Version?.ToString(3) ?? "?";
    }

    private async void OnApriCartellaClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top?.Launcher is { } launcher && Directory.Exists(_percorsoDati))
                await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(_percorsoDati));
        }
        catch (Exception) { /* best effort: se il sistema non sa aprirla, pazienza */ }
    }

    private async void OnApriRepoClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top?.Launcher is { } launcher)
                await launcher.LaunchUriAsync(new Uri(LinkRepo));
        }
        catch (Exception) { /* idem */ }
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
