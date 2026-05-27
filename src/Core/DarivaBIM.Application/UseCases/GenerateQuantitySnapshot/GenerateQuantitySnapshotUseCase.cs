using System;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Quantifica;

namespace DarivaBIM.Application.UseCases.GenerateQuantitySnapshot
{
    /// <summary>
    /// Caso de uso da feature "Tigre Quantifica": delega a varredura ao
    /// <see cref="IQuantityScanService"/>. Existe como camada fina pra
    /// manter a janela WPF desacoplada do scanner concreto e pra deixar o
    /// pipeline testável headless.
    /// </summary>
    public sealed class GenerateQuantitySnapshotUseCase
    {
        private readonly IQuantityScanService _scanService;

        public GenerateQuantitySnapshotUseCase(IQuantityScanService scanService)
        {
            _scanService = scanService ?? throw new ArgumentNullException(nameof(scanService));
        }

        public QuantitySnapshot Execute()
        {
            return _scanService.Scan();
        }
    }
}
