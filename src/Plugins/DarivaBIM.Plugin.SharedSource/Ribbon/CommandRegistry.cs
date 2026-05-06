using System;
using System.Collections.Generic;
using DarivaBIM.Plugin.Features.BatchParameterEditor;
using DarivaBIM.Plugin.Features.FamiliesImporter;
using DarivaBIM.Plugin.Features.FloorDrainExtension;
using DarivaBIM.Plugin.Features.PipeCadMapper;
using DarivaBIM.Plugin.Features.PipeCodes;
using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Plugin.Ribbon
{
    /// <summary>
    /// Maps every <see cref="RibbonCommandId"/> to the concrete
    /// <c>IExternalCommand</c> Type for the Revit 2026 plugin. Each entry
    /// reads its values from the corresponding feature manifest in
    /// <c>Features/&lt;Tool&gt;/&lt;Tool&gt;Feature.cs</c>.
    /// </summary>
    public sealed class CommandRegistry : ICommandRegistry
    {
        private readonly Dictionary<RibbonCommandId, Type> _commands = new Dictionary<RibbonCommandId, Type>
        {
            { PipeCodesFeature.CommandId,         PipeCodesFeature.CommandType },
            { FamiliesImporterFeature.CommandId,  FamiliesImporterFeature.CommandType },
            { PipeCadMapperFeature.CommandId,     PipeCadMapperFeature.CommandType },
            { FloorDrainExtensionFeature.CommandId, FloorDrainExtensionFeature.CommandType },
            { BatchParameterEditorFeature.CommandId, BatchParameterEditorFeature.CommandType },
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
