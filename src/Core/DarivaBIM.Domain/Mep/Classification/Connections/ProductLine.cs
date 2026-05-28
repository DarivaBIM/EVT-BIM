namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Linha de produto Tigre/generica. Detectada via lexicalLines auto-
    /// derivado do catalogo (vide secao 10.2 do rulebook). Continua util
    /// fora do contexto Tigre como classificador comercial generico.
    /// </summary>
    public enum ProductLine
    {
        Unknown = 0,
        Soldavel = 1,
        Roscavel = 2,
        Redux = 3,
        SerieNormal = 4,
        SerieReforcada = 5,
        Aquatherm = 6,
        ClicPex = 7,
        Ppr = 8,
        TigreFire = 9,
    }
}
