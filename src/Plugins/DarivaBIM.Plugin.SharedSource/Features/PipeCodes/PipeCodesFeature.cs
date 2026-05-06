using System;
using DarivaBIM.Revit.Abstractions.Ribbon;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.Features.PipeCodes
{
    /// <summary>
    /// Manifest of the "Codificar Tubos" feature: groups the ribbon button, the
    /// command type, the stable <see cref="RibbonCommandId"/> and the DI
    /// registration of feature-local services. Today this dispatches to the
    /// Tigre-specific applier (<see cref="ApplyPipeCodesTool"/>); future
    /// catalogues plug in alongside.
    /// </summary>
    public static class PipeCodesFeature
    {
        public static RibbonCommandId CommandId => RibbonCommandId.WritePipeCodes;

        public static RibbonButtonDefinition Button => PipeCodesButton.Definition;

        public static Type CommandType => typeof(ApplyPipeCodesCommand);

        public static IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddTransient<ApplyPipeCodesTool>();
            return services;
        }
    }
}
