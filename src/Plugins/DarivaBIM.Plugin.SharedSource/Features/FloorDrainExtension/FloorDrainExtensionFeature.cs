using System;
using DarivaBIM.Revit.Abstractions.Ribbon;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.Features.FloorDrainExtension
{
    /// <summary>
    /// Manifest of the FloorDrainExtension feature. The modeless window owns its
    /// own <c>ExternalEvent</c>, so the DI surface stays empty for now.
    /// </summary>
    public static class FloorDrainExtensionFeature
    {
        public static RibbonCommandId CommandId => RibbonCommandId.OpenFloorDrainExtension;

        public static RibbonButtonDefinition Button => FloorDrainExtensionButton.Definition;

        public static Type CommandType => typeof(ShowFloorDrainExtensionCommand);

        public static IServiceCollection AddServices(IServiceCollection services)
        {
            return services;
        }
    }
}
