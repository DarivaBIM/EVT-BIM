using System.Collections.Generic;
using DarivaBIM.Application.DTOs.Tigre;

namespace DarivaBIM.Application.Contracts
{
    /// <summary>
    /// Insere/atualiza o parâmetro Tigre: Código nos tubos cujos
    /// <c>ElementId</c> foram passados. Sempre sobrescreve se houver match
    /// no catálogo: a seleção do usuário no WPF já carrega a intenção de
    /// regravar. A implementação Revit-side abre uma única transação e
    /// reaproveita o pipeline de match do <see cref="DarivaBIM.Domain.Tigre.TigreCatalog"/>.
    /// </summary>
    public interface ITigreCodeApplyService
    {
        TigreSelectiveApplyResult Apply(IReadOnlyList<long> elementIds);
    }
}
