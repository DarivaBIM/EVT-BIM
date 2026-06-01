using System;
using DarivaBIM.Revit.Abstractions.Ribbon;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.Features.TigreQuantifica
{
    /// <summary>
    /// Manifest da feature "Tigre Quantifica": botão da ribbon, comando que
    /// abre o WPF e <see cref="RibbonCommandId"/> estável. A janela cria seu
    /// próprio <c>ExternalEvent</c> de scan; o export é I/O síncrono no
    /// code-behind (sem transação Revit envolvida).
    /// </summary>
    public static class TigreQuantificaFeature
    {
        public static RibbonCommandId CommandId => RibbonCommandId.OpenTigreQuantifica;

        public static RibbonButtonDefinition Button => TigreQuantificaButton.Definition;

        public static Type CommandType => typeof(ShowTigreQuantificaCommand);

        public static IServiceCollection AddServices(IServiceCollection services)
        {
            return services;
        }
    }
}
