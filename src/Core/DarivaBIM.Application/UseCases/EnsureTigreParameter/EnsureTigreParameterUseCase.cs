using System;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;

namespace DarivaBIM.Application.UseCases.EnsureTigreParameter
{
    /// <summary>
    /// Garante que o shared parameter Tigre: Código exista no projeto e esteja
    /// vinculado às tubulações como instância. Idempotente.
    /// </summary>
    public sealed class EnsureTigreParameterUseCase
    {
        private readonly ITigreParameterBindingService _service;

        public EnsureTigreParameterUseCase(ITigreParameterBindingService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public TigreEnsureParameterResult Execute()
        {
            return _service.Ensure();
        }
    }
}
