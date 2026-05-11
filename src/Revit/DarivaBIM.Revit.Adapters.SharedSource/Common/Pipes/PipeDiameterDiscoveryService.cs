using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace DarivaBIM.Revit.Adapters.Common.Pipes
{
    /// <summary>
    /// Descobre os diâmetros nominais disponíveis para um <see cref="PipeType"/>
    /// percorrendo as <c>Segments</c> do <c>RoutingPreferenceManager</c> e
    /// extraindo cada <c>MEPSize.NominalDiameter</c>. Compartilhado entre
    /// <c>PipeCadMapper</c> e <c>FloorDrainExtension</c> — qualquer fluxo
    /// que precise saber se determinado diâmetro é roteável em um tipo.
    /// </summary>
    public static class PipeDiameterDiscoveryService
    {
        /// <summary>
        /// Retorna a lista ordenada de diâmetros nominais (em milímetros)
        /// que o <paramref name="pipeType"/> consegue produzir no projeto.
        /// Devolve coleção vazia se o tipo não tiver Routing Preference
        /// Manager ou se a leitura falhar.
        /// </summary>
        public static IReadOnlyList<double> GetAvailableDiametersMm(Document doc, PipeType pipeType)
        {
            HashSet<double> diameters = new();

            try
            {
                RoutingPreferenceManager? manager = pipeType.RoutingPreferenceManager;
                if (manager == null)
                    return Array.Empty<double>();

                int count = manager.GetNumberOfRules(RoutingPreferenceRuleGroupType.Segments);
                for (int i = 0; i < count; i++)
                {
                    RoutingPreferenceRule rule = manager.GetRule(RoutingPreferenceRuleGroupType.Segments, i);
                    if (doc.GetElement(rule.MEPPartId) is not Segment segment)
                        continue;

                    foreach (MEPSize size in segment.GetSizes())
                    {
                        double mm = UnitUtils.ConvertFromInternalUnits(
                            size.NominalDiameter,
                            UnitTypeId.Millimeters);

                        diameters.Add(Math.Round(mm, 2));
                    }
                }
            }
            catch
            {
                // Tipos sem RoutingPreferenceManager utilizável: lista vazia.
            }

            return diameters.OrderBy(d => d).ToList();
        }

        /// <summary>
        /// Indica se o <paramref name="pipeType"/> consegue rotear o
        /// diâmetro nominal (em mm) informado, com tolerância de 0,5 mm
        /// para acomodar arredondamentos do RoutingPreferenceManager.
        /// </summary>
        public static bool SupportsDiameterMm(Document doc, PipeType pipeType, double diameterMm)
        {
            if (diameterMm <= 0)
                return false;

            foreach (double d in GetAvailableDiametersMm(doc, pipeType))
            {
                if (Math.Abs(d - diameterMm) < 0.5)
                    return true;
            }
            return false;
        }
    }
}
