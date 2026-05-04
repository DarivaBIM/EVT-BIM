using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Common.SharedParameters
{
    /// <summary>
    /// Snapshot do binding atual de um parâmetro encontrado pelo nome no
    /// projeto. Permite distinguir parâmetros nativos vs shared, GUID que
    /// bate vs não bate e binding como instance vs type.
    /// </summary>
    internal sealed class ExistingSharedParameterBindingInfo
    {
        public Definition Definition { get; init; } = null!;
        public ElementBinding Binding { get; init; } = null!;
        public bool IsShared { get; init; }
        public bool GuidMatches { get; init; }
    }
}
