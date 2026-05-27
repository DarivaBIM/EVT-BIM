namespace DarivaBIM.Revit.Abstractions.Ribbon
{
    /// <summary>
    /// Stable, version-agnostic identifier for every command the plugin can
    /// surface on the ribbon. The actual <c>IExternalCommand</c> type each id
    /// resolves to is supplied by the per-version plugin via <see cref="ICommandRegistry"/>.
    /// </summary>
    public enum RibbonCommandId
    {
        ImportFamilies,
        CalculatePressure,
        SizePump,
        OpenTigreQuantifica,
        WritePipeCodes,
        OpenSupportMap,
        OpenSettings,
        ShowFamiliesPane,
        OpenPipeConverter,
        OpenFloorDrainExtension,
        OpenBatchParameterEditor,
        OpenUtilizationPointInsertion,
    }
}
