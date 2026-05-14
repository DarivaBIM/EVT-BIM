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
            return SupportsDiameter(diameterMm, GetAvailableDiametersMm(doc, pipeType));
        }

        internal static bool SupportsDiameter(double diameterMm, IReadOnlyList<double> available)
        {
            if (diameterMm <= 0)
                return false;

            foreach (double d in available)
            {
                if (Math.Abs(d - diameterMm) < 0.5)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Cache doc-scoped para <see cref="PipeDiameterDiscoveryService"/>: cada
    /// <see cref="PipeType"/> é resolvido para sua lista de diâmetros nominais
    /// uma única vez por instância. Use durante um scan/load onde o mesmo
    /// PipeType é consultado várias vezes (ex.: FloorDrainExtension faz
    /// <c>SupportsDiameterMm</c> N vezes e depois <c>GetAvailableDiametersMm</c>
    /// outra vez para preencher o dropdown). Não é thread-safe — instancie
    /// dentro do ExternalEvent.Execute e descarte ao final.
    /// </summary>
    public sealed class PipeDiameterDiscoveryCache
    {
        private readonly Document _doc;
        private readonly Dictionary<long, IReadOnlyList<double>> _byPipeType = new();

        public PipeDiameterDiscoveryCache(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public IReadOnlyList<double> GetAvailableDiametersMm(PipeType pipeType)
        {
            if (pipeType == null) throw new ArgumentNullException(nameof(pipeType));

            long key = pipeType.Id.Value;
            if (!_byPipeType.TryGetValue(key, out IReadOnlyList<double>? cached))
            {
                cached = PipeDiameterDiscoveryService.GetAvailableDiametersMm(_doc, pipeType);
                _byPipeType[key] = cached;
            }
            return cached;
        }

        public bool SupportsDiameterMm(PipeType pipeType, double diameterMm)
        {
            return PipeDiameterDiscoveryService.SupportsDiameter(
                diameterMm, GetAvailableDiametersMm(pipeType));
        }
    }
}
