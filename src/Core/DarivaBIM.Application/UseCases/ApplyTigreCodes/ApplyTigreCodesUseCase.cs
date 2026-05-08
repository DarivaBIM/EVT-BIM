using System;
using System.Collections.Generic;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;

namespace DarivaBIM.Application.UseCases.ApplyTigreCodes
{
    /// <summary>
    /// Insere/atualiza o código Tigre nos tubos selecionados pelo usuário no
    /// WPF. A janela passa só os <c>ElementId</c> marcados; este use case
    /// repassa ao serviço Revit-side e devolve o relatório.
    /// </summary>
    public sealed class ApplyTigreCodesUseCase
    {
        private readonly ITigreCodeApplyService _service;

        public ApplyTigreCodesUseCase(ITigreCodeApplyService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public TigreSelectiveApplyResult Execute(IReadOnlyList<long> elementIds)
        {
            if (elementIds == null) throw new ArgumentNullException(nameof(elementIds));
            return _service.Apply(elementIds);
        }
    }
}
