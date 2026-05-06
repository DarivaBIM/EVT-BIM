namespace DarivaBIM.Presentation.Wpf.BatchParameterEditor
{
    /// <summary>
    /// Revit-agnostic mirror of <c>Autodesk.Revit.DB.StorageType</c>. Lives
    /// in Presentation.Wpf so the ViewModel can describe parameter typing
    /// without leaking RevitAPI into a layer that is supposed to be neutral
    /// (see ADR-0010).
    /// </summary>
    public enum ParameterValueKind
    {
        Text,
        Integer,
        Decimal,
        ElementReference,
        Unknown
    }
}
