namespace DarivaBIM.Domain.Mep.Classification.Ports
{
    /// <summary>
    /// Papel canonico de cada porta de uma peca MEP. Substitui a heuristica
    /// posicional do matcher legado por enum forte, permitindo regras de
    /// conexao deterministicas (inline vs ramal, run-grande vs run-pequeno).
    /// Vide secao 6 do rulebook canonico.
    /// </summary>
    public enum PortRole
    {
        Unknown,
        Inlet,
        Outlet,
        RunA,
        RunB,
        RunLarge,
        RunSmall,
        Branch,
        BranchLeft,
        BranchRight,
        Inspection,
    }
}
