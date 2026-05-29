namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Os tres textos de um elemento que o Classify (fase 2.B) usa no score
    /// lexical: nome da familia, nome do tipo e descricao. POCO Domain puro
    /// (Revit-agnostic); o Adapter (ElementTextsReader, 1.B-3) preenche a partir
    /// do Element. Todos default "" (nunca null) para simplificar a tokenizacao
    /// downstream. Os pesos 3/2/1 (FamilyName/TypeName/Description) sao aplicados
    /// no Classify, NAO aqui.
    /// </summary>
    public sealed record ElementTexts
    {
        public string FamilyName { get; init; } = "";

        public string TypeName { get; init; } = "";

        public string Description { get; init; } = "";
    }
}
