using System;
using System.Collections.Generic;
using DarivaBIM.Plugin.V2026.Commands;
using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.V2026.Ribbon
{
    /// <summary>
    /// Maps every <see cref="RibbonCommandId"/> to the concrete
    /// <c>IExternalCommand</c> Type for the Revit 2026 plugin.
    /// </summary>
    public sealed class CommandRegistry : ICommandRegistry
    {
        private readonly Dictionary<RibbonCommandId, Type> _commands = new Dictionary<RibbonCommandId, Type>
        {
            { RibbonCommandId.WriteTigreCodes,    typeof(ApplyTigreCodesCommand) },
            { RibbonCommandId.ShowFamiliesPane,   typeof(ShowFamiliesPaneCommand) },
            { RibbonCommandId.OpenPipeConverter,  typeof(ShowPipeConverterCommand) },
            { RibbonCommandId.OpenProlongador,    typeof(ShowProlongadorCommand) },
            { RibbonCommandId.OpenParameterEditor, typeof(ShowParameterEditorCommand) },
        };

        public Type GetCommandType(RibbonCommandId commandId)
        {
            if (!_commands.TryGetValue(commandId, out Type? type))
                throw new KeyNotFoundException($"No command registered for {commandId}.");
            return type;
        }

        public bool TryGetCommandType(RibbonCommandId commandId, out Type? type)
        {
            return _commands.TryGetValue(commandId, out type);
        }
    }
}
