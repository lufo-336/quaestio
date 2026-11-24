using System.Collections.Generic;

namespace Wsa.Quiz.App.State;

/// <summary>
/// Richiesta di avvio di una partita di ripasso dalle Statistiche,
/// per un'intera materia (<see cref="CategoriaCanon"/> null) oppure
/// per una singola categoria.
/// </summary>
public sealed record RichiestaRipasso(
    string MateriaCanon,
    string? CategoriaCanon,
    IReadOnlyList<string> IdDomandeSbagliate);
