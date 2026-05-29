using System;
using System.Collections.Generic;
using System.Numerics;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using DarivaBIM.Domain.Mep.Classification.Connections;
using DarivaBIM.Domain.Mep.Classification.Ports;
using DarivaBIM.Revit.Adapters.Common.Units;
using DarivaBIM.Revit.Adapters.Features.UtilizationPoints;

namespace DarivaBIM.Revit.Adapters.Common.Mep
{
    /// <summary>
    /// Casca fina (Adapter) que le um <see cref="Element"/> MEP e produz um
    /// <see cref="TopologyReadResult"/>, delegando TODA a logica geometrica ao
    /// motor Domain (<see cref="TopologyInferenceEngine"/>). Responsabilidades:
    /// extrair conectores fisicos (<see cref="ConnectorPhysicalFilter"/>),
    /// converter XYZ->Vector3 em milimetros, blindar o sinal do BasisZ
    /// (<see cref="OutwardNormalGuard"/>) e inferir disciplina/categoria da
    /// BuiltInCategory. PartType vai como string raw (hint fraco D7; sem mapper).
    /// </summary>
    public static class ConnectionTopologyReader
    {
        public static TopologyReadResult Read(Element element)
        {
            var diagnostics = new List<TopologyDiagnostic>();

            if (element is null)
            {
                Add(diagnostics, TopologyDiagnosticCode.NoConnectorManager, DiagnosticSeverity.Error, "Element nulo.");
                return Failure(diagnostics);
            }

            ConnectorManager? manager = RevitConnectorUtilities.GetConnectorManager(element);
            if (manager is null)
            {
                Add(diagnostics, NoManagerCode(element), DiagnosticSeverity.Error,
                    "Sem ConnectorManager (nao e MEPCurve nem FamilyInstance com MEPModel).");
                return Failure(diagnostics);
            }

            IReadOnlyList<Connector> physical = ConnectorPhysicalFilter.Filter(manager, diagnostics);

            var readings = new List<ConnectorReading>(physical.Count);
            foreach (Connector connector in physical)
            {
                ConnectorReading? reading = TryConvert(connector, diagnostics);
                if (reading is not null)
                {
                    readings.Add(reading);
                }
            }

            // Blindagem do sinal outward antes do motor (Codex #2).
            IReadOnlyList<ConnectorReading> guarded = OutwardNormalGuard.EnsureOutward(readings);

            string partTypeRaw = ReadPartTypeRaw(element);
            (Discipline discipline, ProductCategory category) = InferDisciplineCategory(element);

            TopologyReadResult inference = TopologyInferenceEngine.Infer(guarded, partTypeRaw, discipline, category);

            // Mescla os diagnostics de leitura (Adapter) com os do motor (Domain).
            return inference with { Diagnostics = Merge(diagnostics, inference.Diagnostics) };
        }

        private static TopologyReadResult Failure(List<TopologyDiagnostic> diagnostics)
            => new() { Success = false, Topology = null, Diagnostics = diagnostics };

        private static TopologyDiagnosticCode NoManagerCode(Element element)
        {
            // FamilyInstance sem MEPModel -> NoMepModel; qualquer outro -> generico.
            try
            {
                if (element is FamilyInstance fi && fi.MEPModel is null)
                {
                    return TopologyDiagnosticCode.NoMepModel;
                }
            }
            catch
            {
                // cai no generico
            }

            return TopologyDiagnosticCode.NoConnectorManager;
        }

        private static ConnectorReading? TryConvert(Connector connector, List<TopologyDiagnostic> diagnostics)
        {
            // try/catch POR conector: Connector.Origin lanca p/ NonEnd em Revit 2025.
            try
            {
                XYZ basisZ = connector.CoordinateSystem.BasisZ.Normalize();
                XYZ origin = connector.Origin;
                double diameterMm = RevitUnitConverter.FeetToMillimeters(connector.Radius * 2.0);

                return new ConnectorReading
                {
                    NativeIndex = connector.Id,
                    OutwardNormal = new Vector3((float)basisZ.X, (float)basisZ.Y, (float)basisZ.Z),
                    Origin = new Vector3(
                        (float)RevitUnitConverter.FeetToMillimeters(origin.X),
                        (float)RevitUnitConverter.FeetToMillimeters(origin.Y),
                        (float)RevitUnitConverter.FeetToMillimeters(origin.Z)),
                    DnMm = (int)Math.Round(diameterMm),
                    Shape = MapShape(connector),
                    IsConnected = SafeIsConnected(connector),
                };
            }
            catch
            {
                Add(diagnostics, TopologyDiagnosticCode.BasisZIncoherent, DiagnosticSeverity.Warning,
                    "Falha convertendo geometria do conector.");
                return null;
            }
        }

        private static ConnectorShape MapShape(Connector connector)
        {
            try
            {
                return connector.Shape switch
                {
                    ConnectorProfileType.Round => ConnectorShape.Round,
                    ConnectorProfileType.Rectangular => ConnectorShape.Rectangular,
                    ConnectorProfileType.Oval => ConnectorShape.Oval,
                    _ => ConnectorShape.Unknown,
                };
            }
            catch
            {
                return ConnectorShape.Unknown;
            }
        }

        private static bool SafeIsConnected(Connector connector)
        {
            try
            {
                return connector.IsConnected;
            }
            catch
            {
                return false;
            }
        }

        private static string ReadPartTypeRaw(Element element)
        {
            // PartType via MechanicalFitting (cobre pipe/duct fittings). Pipes e
            // accessories que nao sao MechanicalFitting devolvem "" -> PartTypeHints
            // retorna null -> o motor classifica por geometria (hint fraco D7).
            try
            {
                if (element is FamilyInstance fi && fi.MEPModel is MechanicalFitting fitting)
                {
                    return fitting.PartType.ToString();
                }
            }
            catch
            {
                // sem PartType -> string vazia
            }

            return string.Empty;
        }

        private static (Discipline, ProductCategory) InferDisciplineCategory(Element element)
        {
            BuiltInCategory bic = ResolveBuiltInCategory(element);
            return bic switch
            {
                BuiltInCategory.OST_PipeFitting => (Discipline.Plumbing, ProductCategory.PipeFitting),
                BuiltInCategory.OST_PipeAccessory => (Discipline.Plumbing, ProductCategory.PipeAccessory),
                BuiltInCategory.OST_PlumbingFixtures => (Discipline.Plumbing, ProductCategory.PlumbingFixture),
                // Pipe reto e Plumbing mas nao e fitting/accessory/fixture -> Category Unknown.
                BuiltInCategory.OST_PipeCurves => (Discipline.Plumbing, ProductCategory.Unknown),
                _ => (Discipline.Unknown, ProductCategory.Unknown),
            };
        }

        private static BuiltInCategory ResolveBuiltInCategory(Element element)
        {
            Category? cat = element.Category;
            if (cat is null)
            {
                return BuiltInCategory.INVALID;
            }
#if REVIT2024_OR_GREATER || REVIT2025 || REVIT2026
            return cat.BuiltInCategory;
#else
            return (BuiltInCategory)cat.Id.IntegerValue;
#endif
        }

        private static IReadOnlyList<TopologyDiagnostic> Merge(
            List<TopologyDiagnostic> readDiagnostics,
            IReadOnlyList<TopologyDiagnostic> engineDiagnostics)
        {
            if (engineDiagnostics.Count == 0)
            {
                return readDiagnostics;
            }

            var merged = new List<TopologyDiagnostic>(readDiagnostics.Count + engineDiagnostics.Count);
            merged.AddRange(readDiagnostics);
            merged.AddRange(engineDiagnostics);
            return merged;
        }

        private static void Add(
            List<TopologyDiagnostic> diagnostics,
            TopologyDiagnosticCode code,
            DiagnosticSeverity severity,
            string detail)
        {
            diagnostics.Add(new TopologyDiagnostic { Code = code, Severity = severity, Detail = detail });
        }
    }
}
