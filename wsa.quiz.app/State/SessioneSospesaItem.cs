using System;
using System.Linq;
using Wsa.Quiz.App.Views;
using Wsa.Quiz.Core.Models;
using Wsa.Quiz.Core.Models.Laboratorio;
using Wsa.Quiz.Core.Risorse;
using Wsa.Quiz.Core.Services;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Wrapper di una riga della tabella sospesi (Quiz o Laboratorio). Espone proprieta'
/// formattate per il binding XAML (data pausa, modalita' label, avanzamento parziale).
/// E' observable solo per supportare la conferma inline dell'azione Elimina:
/// <see cref="InAttesaConfermaEliminazione"/> commuta i bottoni della riga
/// fra "Riprendi/Elimina" e "Si', elimina/Annulla".
/// </summary>
public class SessioneSospesaItem : ObservableObject, IConfermaEliminazione
{
    /// <summary>Riferimento alla pausa quiz originale (null se la riga appartiene al Laboratorio).</summary>
    public SessionePausa? Pausa { get; }

    /// <summary>Riferimento alla pausa del Laboratorio originale (null se la riga appartiene ai quiz).</summary>
    public PausaLaboratorio? PausaLab { get; }

    /// <summary>True se la pausa appartiene al Laboratorio.</summary>
    public bool ELaboratorio => PausaLab != null;

    public string SessioneId { get; }
    public string DataFormattata { get; }
    public string Modalita { get; }
    public string MateriaEtichetta { get; }
    public string Avanzamento { get; }
    public string TempoTrascorsoFormattato { get; }
    public string TipoEtichetta { get; }

    // ------------------------------------------------------------------ STATO CONFERMA ELIMINA

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

    /// <summary>Inverso di <see cref="InAttesaConfermaEliminazione"/>, comodo per l'XAML.</summary>
    public bool NonInAttesaConferma => !_inAttesaConferma;

    // ------------------------------------------------------------------ COSTRUZIONE

    public SessioneSospesaItem(SessionePausa p)
    {
        Pausa = p;
        SessioneId = p.SessioneId;

        DataFormattata = p.DataOraPausa.ToString("dd MMM yyyy · HH:mm", Lingue.Cultura);

        Modalita = string.IsNullOrWhiteSpace(p.Opzioni.NomeModalita)
            ? (p.ModalitaRotazione ? Strings.Sospesi_Rotazione : Strings.Sospesi_Classica)
            : p.Opzioni.NomeModalita;

        MateriaEtichetta = string.IsNullOrWhiteSpace(p.Opzioni.MateriaNomeLabel)
            ? "—"
            : p.Opzioni.MateriaNomeLabel;

        if (p.ModalitaRotazione)
        {
            int corrette = p.CorretteIds?.Count ?? 0;
            int totale = p.TotaleUnicheRotazione;
            Avanzamento = totale > 0
                ? string.Format(Strings.Sospesi_PadroneggiateSbagliate, corrette, totale, p.SbagliateContatore)
                : string.Format(Strings.Sospesi_Padroneggiate, corrette);
            TipoEtichetta = Strings.Sospesi_Rotazione;
        }
        else
        {
            int fatte = p.EffettuateClassica;
            int rimaste = p.CodaDomandeIds?.Count ?? 0;
            int totale = fatte + rimaste;
            Avanzamento = totale > 0
                ? string.Format(Strings.Sospesi_RisposteNDiM, fatte, totale)
                : string.Format(Strings.Sospesi_RisposteN, fatte);
            TipoEtichetta = Strings.Sospesi_Classica;
        }

        TempoTrascorsoFormattato = QuizService.FormattaDurata(p.TempoTrascorso);
    }

    public SessioneSospesaItem(PausaLaboratorio p)
    {
        PausaLab = p;
        SessioneId = p.SessioneId;

        DataFormattata = p.DataOraPausa.ToString("dd MMM yyyy · HH:mm", Lingue.Cultura);

        string nomeLing = LaboratorioService.Linguaggi
            .FirstOrDefault(l => string.Equals(l.Id, p.Linguaggio, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(l.Nome, p.Linguaggio, StringComparison.OrdinalIgnoreCase))
            .Nome ?? (string.IsNullOrWhiteSpace(p.Linguaggio) ? "—" : p.Linguaggio);

        string livelloEtichetta = p.Livello.ToLowerInvariant() switch
        {
            LivelliLab.Base => Strings.Lab_Livello_Base,
            LivelliLab.Media => Strings.Lab_Livello_Media,
            LivelliLab.Alta => Strings.Lab_Livello_Alta,
            _ => p.Livello
        };

        Modalita = $"{nomeLing}{LaboratorioService.SuffissoMateria}";
        MateriaEtichetta = string.IsNullOrWhiteSpace(livelloEtichetta) ? "—" : livelloEtichetta;

        int totale = p.Coppie?.Count ?? 0;
        int corrente = totale > 0 ? Math.Clamp(p.IndiceScheda + 1, 1, totale) : 0;
        Avanzamento = string.Format(Strings.Lab_SchedaNDiM, corrente, totale);
        TipoEtichetta = Strings.Lab_ModoLaboratorio;

        TempoTrascorsoFormattato = QuizService.FormattaDurata(p.TempoTrascorso);
    }
}
