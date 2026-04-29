using System;

namespace DarivaBIM.Revit.Abstractions.Ribbon
{
    /// <summary>
    /// Resolves a stable <see cref="RibbonCommandId"/> to the concrete
    /// <c>IExternalCommand</c> Type for the current Revit version. Each plugin
    /// version (V2023, V2024, V2025, V2026, V2027) supplies its own implementation.
    /// </summary>
    public interface ICommandRegistry
    {
        Type GetCommandType(RibbonCommandId commandId);

        bool TryGetCommandType(RibbonCommandId commandId, out Type? type);
    }
}
