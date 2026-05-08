using System;
using System.Collections.Generic;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;

namespace DarivaBIM.Application.UseCases.ClearTigreCodes
{
    /// <summary>
    /// Apaga (zera) o valor do parâmetro Tigre: Código nos tubos selecionados.
    /// Não remove o binding do shared parameter — apenas o valor.
    /// </summary>
    public sealed class ClearTigreCodesUseCase
    {
        private readonly ITigreCodeClearService _service;

        public ClearTigreCodesUseCase(ITigreCodeClearService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public TigreClearResult Execute(IReadOnlyList<long> elementIds)
        {
            if (elementIds == null) throw new ArgumentNullException(nameof(elementIds));
            return _service.Clear(elementIds);
        }
    }
}
