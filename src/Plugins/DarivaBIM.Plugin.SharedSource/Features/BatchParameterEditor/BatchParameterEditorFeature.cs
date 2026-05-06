using System;
using DarivaBIM.Revit.Abstractions.Ribbon;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.Features.BatchParameterEditor
{
    /// <summary>
    /// Manifest of the Batch Parameter Editor feature. The modeless window owns
    /// its own <c>ExternalEvent</c> pair (selection + apply), so the DI surface
    /// stays empty for now.
    /// </summary>
    public static class BatchParameterEditorFeature
    {
        public static RibbonCommandId CommandId => RibbonCommandId.OpenBatchParameterEditor;

        public static RibbonButtonDefinition Button => BatchParameterEditorButton.Definition;

        public static Type CommandType => typeof(ShowBatchParameterEditorCommand);

        public static IServiceCollection AddServices(IServiceCollection services)
        {
            return services;
        }
    }
}
