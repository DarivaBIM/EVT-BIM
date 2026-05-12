using System.Collections.Generic;
using DarivaBIM.Application.DTOs.UtilizationPoints;
using DarivaBIM.Domain.Hydraulics.UtilizationPoints;

namespace DarivaBIM.Application.Contracts.UtilizationPoints
{
    /// <summary>
    /// Executa a inserção de pontos de utilização nos conectores livres dos
    /// elementos informados, dentro de uma única transação Revit. Implementado
    /// pelo adaptador Revit; o use case e a janela WPF só conhecem esta
    /// interface neutra.
    /// </summary>
    public interface IUtilizationPointInsertionService
    {
        /// <summary>
        /// Insere as famílias seguindo o algoritmo descrito no script Python
        /// de referência. Os IDs em <paramref name="elementIds"/> devem
        /// corresponder a tubos/conexões/acessórios hidráulicos do documento
        /// ativo; o adaptador filtra novamente para descartar elementos sem
        /// conector MEP.
        /// </summary>
        /// <param name="elementIds">IDs dos elementos hidráulicos a processar.</param>
        /// <param name="group">Grupo ativo cujas regras definem o que inserir
        /// por faixa de altura.</param>
        /// <param name="referenceLevelId">ID do nível usado como zero das
        /// alturas em metros. <c>null</c> faz o serviço cair no nível do
        /// próprio elemento e, por fim, no zero do projeto, como no script
        /// Python.</param>
        InsertionSummaryDto Insert(
            IReadOnlyList<long> elementIds,
            UtilizationPointGroup group,
            long? referenceLevelId);
    }
}
