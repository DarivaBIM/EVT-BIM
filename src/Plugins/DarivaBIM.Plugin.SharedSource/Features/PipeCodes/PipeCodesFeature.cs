using System;
using DarivaBIM.Revit.Abstractions.Ribbon;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.Features.PipeCodes
{
    /// <summary>
    /// Manifest da feature "Codificar Tigre" (rótulo do Slice 3 — o nome
    /// interno PipeCodes é preservado pra git history + RibbonWiringTests +
    /// CommandRegistry estáveis): botão da ribbon, comando que abre o WPF
    /// e <see cref="RibbonCommandId"/> estável. A janela cria seus próprios
    /// <c>ExternalEvent</c>s para varrer o projeto, criar o shared parameter
    /// Tigre: Código e aplicar/apagar os valores nos elementos Tigre
    /// selecionados pelo usuário, então o DI surface aqui é vazio.
    /// </summary>
    public static class PipeCodesFeature
    {
        public static RibbonCommandId CommandId => RibbonCommandId.WritePipeCodes;

        public static RibbonButtonDefinition Button => PipeCodesButton.Definition;

        public static Type CommandType => typeof(ApplyPipeCodesCommand);

        public static IServiceCollection AddServices(IServiceCollection services)
        {
            return services;
        }
    }
}
