namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Kind canonico do fitting, ortogonal a discipline/category/line. Substitui
    /// a string "subtype" do matcher legado por enum forte. Vide secao 5 do
    /// rulebook canonico para a tabela completa de mapeamento.
    /// </summary>
    public enum BaseKind
    {
        Unknown = 0,
        Elbow = 1,
        Tee = 2,
        Wye = 3,
        Cross = 4,
        Union = 5,
        Reducer = 6,
        Cap = 7,
        Valve = 8,
        MultiPort = 9,
        Fixture = 10,
    }
}
