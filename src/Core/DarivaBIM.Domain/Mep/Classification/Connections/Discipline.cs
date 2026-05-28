namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Disciplina MEP da peca classificada. No MVP 1 so Plumbing e suportada;
    /// outras disciplinas terao rulebooks proprios (vide secao 13 do doc).
    /// </summary>
    public enum Discipline
    {
        Unknown = 0,
        Plumbing = 1,
        Hvac = 2,
        Electrical = 3,
        Gas = 4,
    }
}
