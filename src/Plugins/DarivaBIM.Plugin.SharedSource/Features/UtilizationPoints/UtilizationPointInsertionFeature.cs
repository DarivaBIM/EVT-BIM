using System;
using DarivaBIM.Revit.Abstractions.Ribbon;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.Features.UtilizationPoints
{
    /// <summary>
    /// Manifest da feature "Inserir Pontos de Utilização". A janela WPF é
    /// modeless e dona dos próprios <c>ExternalEvent</c>, então o DI surface
    /// fica vazio aqui — manteremos o padrão das outras features.
    /// </summary>
    public static class UtilizationPointInsertionFeature
    {
        public static RibbonCommandId CommandId => RibbonCommandId.OpenUtilizationPointInsertion;

        public static RibbonButtonDefinition Button => UtilizationPointInsertionButton.Definition;

        public static Type CommandType => typeof(ShowUtilizationPointInsertionCommand);

        public static IServiceCollection AddServices(IServiceCollection services)
        {
            return services;
        }
    }
}
