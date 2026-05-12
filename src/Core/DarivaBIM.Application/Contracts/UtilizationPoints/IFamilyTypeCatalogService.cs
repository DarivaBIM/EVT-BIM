using System.Collections.Generic;
using DarivaBIM.Application.DTOs.UtilizationPoints;

namespace DarivaBIM.Application.Contracts.UtilizationPoints
{
    /// <summary>
    /// Lê os <c>FamilySymbol</c> instaláveis do documento ativo e projeta-os
    /// no DTO neutro consumido pela WPF. Implementação concreta em
    /// <c>Revit.Adapters</c>.
    /// </summary>
    public interface IFamilyTypeCatalogService
    {
        /// <summary>
        /// Lista os tipos de família candidatos a ponto de utilização do
        /// documento ativo. A ordenação é definida pelo adaptador; o consumidor
        /// (WPF) pode reordenar/filtrar livremente.
        /// </summary>
        IReadOnlyList<FamilyTypeOptionDto> GetAvailableFamilyTypes();

        /// <summary>
        /// Lista os <c>Level</c> do documento ativo em ordem ascendente de
        /// elevação, com nome e elevação em metros. Alimenta o dropdown
        /// "Nível de referência" da janela.
        /// </summary>
        IReadOnlyList<LevelOptionDto> GetLevels();
    }
}
