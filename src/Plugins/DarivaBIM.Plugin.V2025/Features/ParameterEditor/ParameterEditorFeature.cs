using System;
using DarivaBIM.Revit.Abstractions.Ribbon;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.V2025.Features.ParameterEditor
{
    /// <summary>
    /// Manifest of the Parameter Editor feature. The modeless window owns its
    /// own <c>ExternalEvent</c> pair (selection + apply), so the DI surface
    /// stays empty for now.
    /// </summary>
    public static class ParameterEditorFeature
    {
        public static RibbonCommandId CommandId => RibbonCommandId.OpenParameterEditor;

        public static RibbonButtonDefinition Button => ParameterEditorButton.Definition;

        public static Type CommandType => typeof(ShowParameterEditorCommand);

        public static IServiceCollection AddServices(IServiceCollection services)
        {
            return services;
        }
    }
}
