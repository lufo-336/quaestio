using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.Views;

/// <summary>
/// Base per le view tabellari (Cronologia, Sospesi) che condividono:
/// - una ObservableCollection di righe
/// - stato "nessuna riga / sottotitolo"
/// - pattern conferma inline eliminazione (max una alla volta)
/// - bottone Aggiorna e metodo Inizializza(StorageService)
///
/// Le classi derivate devono implementare <see cref="RicaricaCore"/> e possono
/// usare i metodi protetti per la conferma inline.
/// </summary>
public abstract class TabellaViewBase<TItem> : UserControl, INotifyPropertyChanged
    where TItem : class, IConfermaEliminazione
{
    public new event PropertyChangedEventHandler? PropertyChanged;

    protected StorageService? _storage;

    public ObservableCollection<TItem> Sessioni { get; } = new();

    private bool _nessunaSessione = true;
    public bool NessunaSessione
    {
        get => _nessunaSessione;
        protected set
        {
            if (_nessunaSessione != value)
            {
                _nessunaSessione = value;
                Raise();
            }
        }
    }

    private string _sottotitolo = "Caricamento...";
    public string Sottotitolo
    {
        get => _sottotitolo;
        protected set
        {
            if (_sottotitolo != value)
            {
                _sottotitolo = value;
                Raise();
            }
        }
    }

    protected TabellaViewBase()
    {
        DataContext = this;
    }

    public void Inizializza(StorageService storage)
    {
        _storage = storage;
        Ricarica();
    }

    /// <summary>Logica di caricamento specifica della view derivata.</summary>
    protected abstract void RicaricaCore();

    /// <summary>
    /// Wrapper che azzera la lista, chiama <see cref="RicaricaCore"/> e gestisce
    /// eccezioni settando il sottotitolo. Non chiude il dettaglio se aperto.
    /// </summary>
    public void Ricarica()
    {
        if (_storage == null) return;
        Sessioni.Clear();
        try
        {
            RicaricaCore();
        }
        catch (Exception ex)
        {
            Sottotitolo = $"Errore nel caricamento: {ex.Message}";
            NessunaSessione = true;
            return;
        }

        NessunaSessione = Sessioni.Count == 0;
        Sottotitolo = FormatSottotitolo(Sessioni.Count);
    }

    /// <summary>Testo del sottotitolo in base al numero di elementi caricati.</summary>
    protected abstract string FormatSottotitolo(int count);

    // ------------------------------------------------------------------ FOCUS DA TASTIERA

    /// <summary>
    /// Porta il focus sulla RIGA selezionata della lista (selezionando la prima se non
    /// c'e' selezione). Da usare al posto di <c>lista.Focus()</c>.
    ///
    /// Perche' non <c>lista.Focus()</c>: in Avalonia la <see cref="ListBox"/> ha
    /// <c>Focusable = false</c> (il focus va ai suoi <see cref="ListBoxItem"/>, esattamente
    /// come quando si clicca una riga col mouse). Chiamare <c>Focus()</c> sulla ListBox
    /// restituisce <c>false</c> e non fa nulla: il focus resta sullo UserControl, che e'
    /// un ANTENATO della lista — quindi il routing di KeyDown (radice -> elemento con
    /// focus) non attraversa mai la ListBox e le frecce non possono muovere la selezione.
    /// Vedi Trappola 21 in CLAUDE.md.
    /// </summary>
    protected static void FocusRigaSelezionata(ListBox lista)
    {
        if (lista.ItemCount == 0) return;
        if (lista.SelectedIndex < 0) lista.SelectedIndex = 0;

        if (lista.ContainerFromIndex(lista.SelectedIndex) is Control riga && riga.Focus()) return;

        // Container non ancora realizzato (lista appena popolata o appena tornata
        // visibile): riprovo dopo il prossimo giro di layout.
        Dispatcher.UIThread.Post(() =>
        {
            if (lista.ContainerFromIndex(lista.SelectedIndex) is Control riga2)
                riga2.Focus();
        }, DispatcherPriority.Loaded);
    }

    // ------------------------------------------------------------------ HANDLER COMUNI

    protected void OnAggiornaClick(object? sender, RoutedEventArgs e)
    {
        Ricarica();
    }

    // ----- Conferma inline eliminazione -----

    protected void OnEliminaClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b) return;
        if (b.DataContext is not TItem item) return;
        AzzeraConferme();
        item.InAttesaConfermaEliminazione = true;
    }

    protected void OnConfermaEliminaClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b) return;
        if (b.DataContext is not TItem item) return;
        EseguiEliminazione(item);
    }

    protected void OnAnnullaEliminaClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b) return;
        if (b.DataContext is not TItem item) return;
        item.InAttesaConfermaEliminazione = false;
    }

    /// <summary>
    /// Azzera lo stato di conferma su tutte le righe. Da chiamare prima di
    /// attivarne una nuova, per garantire che al massimo una sia in conferma.
    /// </summary>
    protected void AzzeraConferme()
    {
        foreach (var s in Sessioni)
            s.InAttesaConfermaEliminazione = false;
    }

    /// <summary>
    /// Elimina dal disco e rimuove dalla lista. Le derivate possono sovrascrivere
    /// se la logica di storage differisce (default: nessuna azione).
    /// </summary>
    protected virtual void EliminaDaStorage(TItem item) { }

    protected void EseguiEliminazione(TItem item)
    {
        try
        {
            EliminaDaStorage(item);
        }
        catch (Exception ex)
        {
            Sottotitolo = $"Errore nell'eliminazione: {ex.Message}";
            return;
        }

        Sessioni.Remove(item);
        NessunaSessione = Sessioni.Count == 0;
        Sottotitolo = FormatSottotitolo(Sessioni.Count);
    }

    // ------------------------------------------------------------------ INPC

    protected void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Contratto per gli item che supportano la conferma inline dell'eliminazione.
/// </summary>
public interface IConfermaEliminazione
{
    bool InAttesaConfermaEliminazione { get; set; }
}
