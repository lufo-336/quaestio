namespace Wsa.Quiz.App.State;

/// <summary>
/// Voce del filtro materia in Cronologia: il ComboBox mostra l'etichetta nella
/// lingua corrente, il filtro usa la chiave canonica salvata in cronologia.
/// Chiave vuota = nessun filtro ("tutte le materie").
/// </summary>
public sealed record VoceMateria(string Chiave, string Etichetta)
{
    public override string ToString() => Etichetta;
}
