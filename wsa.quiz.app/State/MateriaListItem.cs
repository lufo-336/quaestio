using System;
using System.Collections.Generic;
using System.Linq;
using Wsa.Quiz.App.Views;
using Wsa.Quiz.Core.Models;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Wrapper di una riga della tabella materie in "Gestione contenuti".
/// Observable per la conferma inline dell'eliminazione dell'intera materia
/// (solo le importate sono eliminabili: la UI nasconde il bottone se
/// !Importata). Espone anche <see cref="Sezioni"/> (le categorie) per
/// nasconderle/ripristinarle singolarmente.
/// </summary>
public class MateriaListItem : ObservableObject, IConfermaEliminazione
{
    public string Id { get; }
    public string Nome { get; }
    public int ConteggioDomande { get; }
    public bool Importata { get; }
    public string Badge => Importata ? "Importata" : "Built-in";

    /// <summary>Le sezioni (categorie) della materia, ordinate per nome.</summary>
    public IReadOnlyList<SezioneListItem> Sezioni { get; }
    public bool HaSezioni => Sezioni.Count > 0;

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

    /// <param name="domandeMateria">TUTTE le domande della materia (incluse le sezioni nascoste).</param>
    /// <param name="sezioniEscluse">Set (materiaId, categoria) delle sezioni nascoste.</param>
    public MateriaListItem(Materia materia, IReadOnlyCollection<Domanda> domandeMateria,
        ISet<(string MateriaId, string Categoria)> sezioniEscluse)
    {
        Id = materia.Id;
        Nome = materia.Nome;
        Importata = materia.Importata;
        ConteggioDomande = domandeMateria.Count;

        Sezioni = domandeMateria
            .GroupBy(d => d.Categoria)
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new SezioneListItem(
                materia.Id, g.Key, g.Count(), sezioniEscluse.Contains((materia.Id, g.Key))))
            .ToList();
    }
}
