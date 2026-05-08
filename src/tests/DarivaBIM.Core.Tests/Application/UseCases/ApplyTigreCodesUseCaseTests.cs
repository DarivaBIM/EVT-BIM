using System;
using System.Collections.Generic;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.UseCases.ApplyTigreCodes;
using Xunit;

namespace DarivaBIM.Core.Tests.Application.UseCases
{
    public class ApplyTigreCodesUseCaseTests
    {
        [Fact]
        public void Execute_passes_ids_to_service_and_returns_result()
        {
            FakeTigreCodeApplyService fake = new();
            ApplyTigreCodesUseCase useCase = new(fake);

            IReadOnlyList<long> ids = new long[] { 101, 202, 303 };
            TigreSelectiveApplyResult result = useCase.Execute(ids);

            Assert.Equal(7, result.Inserted);
            Assert.Equal(1, fake.CallCount);
            Assert.Equal(ids, fake.LastIds);
        }

        [Fact]
        public void Execute_throws_when_ids_is_null()
        {
            ApplyTigreCodesUseCase useCase = new(new FakeTigreCodeApplyService());

            Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!));
        }

        private sealed class FakeTigreCodeApplyService : ITigreCodeApplyService
        {
            public int CallCount { get; private set; }
            public IReadOnlyList<long>? LastIds { get; private set; }

            public TigreSelectiveApplyResult Apply(IReadOnlyList<long> elementIds)
            {
                CallCount++;
                LastIds = elementIds;
                return new TigreSelectiveApplyResult
                {
                    Selected = elementIds.Count,
                    Inserted = 7,
                };
            }
        }
    }
}
