using System;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Quantifica;
using DarivaBIM.Application.UseCases.GenerateQuantitySnapshot;
using Xunit;

namespace DarivaBIM.Core.Tests.Application.UseCases
{
    public class GenerateQuantitySnapshotUseCaseTests
    {
        [Fact]
        public void Execute_delegates_to_scan_service_and_returns_snapshot()
        {
            QuantitySnapshot stub = new()
            {
                ProjectInfo = new ProjectInfoDto { Name = "Obra X" },
            };
            FakeQuantityScanService fake = new(stub);

            GenerateQuantitySnapshotUseCase useCase = new(fake);
            QuantitySnapshot result = useCase.Execute();

            Assert.Same(stub, result);
            Assert.Equal(1, fake.CallCount);
        }

        [Fact]
        public void Ctor_throws_when_service_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => new GenerateQuantitySnapshotUseCase(null!));
        }

        private sealed class FakeQuantityScanService : IQuantityScanService
        {
            private readonly QuantitySnapshot _result;

            public FakeQuantityScanService(QuantitySnapshot result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public QuantitySnapshot Scan()
            {
                CallCount++;
                return _result;
            }
        }
    }
}
