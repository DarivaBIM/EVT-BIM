using System;
using DarivaBIM.Revit.Abstractions.Ribbon;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.Features.Prolongador
{
    /// <summary>
    /// Manifest of the Prolongador feature. The modeless window owns its own
    /// <c>ExternalEvent</c>, so the DI surface stays empty for now.
    /// </summary>
    public static class ProlongadorFeature
    {
        public static RibbonCommandId CommandId => RibbonCommandId.OpenProlongador;

        public static RibbonButtonDefinition Button => ProlongadorButton.Definition;

        public static Type CommandType => typeof(ShowProlongadorCommand);

        public static IServiceCollection AddServices(IServiceCollection services)
        {
            return services;
        }
    }
}
