using System;
using DarivaBIM.Revit.Abstractions.Ribbon;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.Features.PipeCadMapper
{
    /// <summary>
    /// Manifest of the PipeCADMapper feature. The window owns its own
    /// <c>ExternalEvent</c> instances today, so the DI surface stays empty.
    /// </summary>
    public static class PipeCadMapperFeature
    {
        public static RibbonCommandId CommandId => RibbonCommandId.OpenPipeConverter;

        public static RibbonButtonDefinition Button => PipeCadMapperButton.Definition;

        public static Type CommandType => typeof(ShowPipeConverterCommand);

        public static IServiceCollection AddServices(IServiceCollection services)
        {
            return services;
        }
    }
}
