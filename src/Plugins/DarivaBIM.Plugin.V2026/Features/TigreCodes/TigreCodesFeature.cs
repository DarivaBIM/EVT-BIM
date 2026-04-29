using System;
using DarivaBIM.Revit.Abstractions.Ribbon;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.V2026.Features.TigreCodes
{
    /// <summary>
    /// Manifest of the "Códigos Tigre" feature: groups the ribbon button, the
    /// command type, the stable <see cref="RibbonCommandId"/> and the DI
    /// registration of feature-local services. Consumed by the panel
    /// (button), the command registry (command type) and the composition root
    /// (services), so all three views of the same feature stay in sync.
    /// </summary>
    public static class TigreCodesFeature
    {
        public static RibbonCommandId CommandId => RibbonCommandId.WriteTigreCodes;

        public static RibbonButtonDefinition Button => TigreCodesButton.Definition;

        public static Type CommandType => typeof(ApplyTigreCodesCommand);

        public static IServiceCollection AddServices(IServiceCollection services)
        {
            services.AddTransient<ApplyTigreCodesTool>();
            return services;
        }
    }
}
