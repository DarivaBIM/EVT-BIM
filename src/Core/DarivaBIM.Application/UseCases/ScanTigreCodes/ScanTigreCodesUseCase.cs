using System;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;

namespace DarivaBIM.Application.UseCases.ScanTigreCodes
{
    /// <summary>
    /// Faz a varredura inicial dos tubos para alimentar a janela
    /// "Codificar Tubos". Não escreve nada no documento; só delega ao
    /// <see cref="ITigreCodeScanService"/> e devolve o snapshot.
    /// </summary>
    public sealed class ScanTigreCodesUseCase
    {
        private readonly ITigreCodeScanService _service;

        public ScanTigreCodesUseCase(ITigreCodeScanService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public TigreScanResult Execute()
        {
            return _service.Scan();
        }
    }
}
