namespace DarivaBIM.Revit.Adapters.Common.SharedParameters
{
    /// <summary>
    /// Como o shared parameter deve ser vinculado ao projeto: como parâmetro
    /// de instância (vive em cada elemento) ou de tipo (vive no
    /// <c>ElementType</c> e é compartilhado por todas as instâncias).
    /// </summary>
    public enum SharedParameterBindingKind
    {
        Instance,
        Type,
    }
}
