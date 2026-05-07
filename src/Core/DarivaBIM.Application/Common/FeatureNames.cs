namespace DarivaBIM.Application.Common
{
    /// <summary>
    /// IDs estáveis das features. Usados como User-Agent HTTP, nome da pasta
    /// de cache no <c>%ProgramData%</c> e <c>internalName</c> dos botões da
    /// ribbon — convenção: o mesmo identificador legível em todas as três
    /// camadas, para que o operador rastreie a feature inteira por
    /// <c>grep</c>.
    /// </summary>
    public static class FeatureNames
    {
        public const string FamiliesImporter = "FamiliesImporterHub";
    }
}
