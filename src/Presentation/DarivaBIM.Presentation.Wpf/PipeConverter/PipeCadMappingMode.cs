namespace DarivaBIM.Presentation.Wpf.PipeConverter
{
    /// <summary>
    /// Indica como o layer do CAD selecionado representa os tubos:
    /// <list type="bullet">
    /// <item><see cref="Unifilar"/>: uma única linha por tubo (diâmetro vem do
    /// input do usuário).</item>
    /// <item><see cref="Bifilar"/>: duas linhas paralelas formam o tubo e a
    /// distância entre elas determina o diâmetro (arredondado para um
    /// diâmetro nominal disponível no tipo selecionado).</item>
    /// </list>
    /// </summary>
    public enum PipeCadMappingMode
    {
        Unifilar = 0,
        Bifilar = 1,
    }
}
