using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace FamiliesImporterHub.Infrastructure
{
    /// <summary>
    /// Percorre os tubos do projeto e preenche o parâmetro <c>Tigre: Código</c>
    /// com base no <see cref="TigreCodeCatalog"/> (descrição+segmento × diâmetro).
    /// </summary>
    public sealed class TigreCodeApplyResult
    {
        public int CatalogCount { get; set; }
        public int PipesTotal { get; set; }
        public int PipesUpdated { get; set; }
        public int PipesAlreadyOk { get; set; }
        public int PipesNoMatch { get; set; }
        public int PipesParameterIssue { get; set; }
        public string ParameterAction { get; set; } = string.Empty;
        public List<string> Warnings { get; } = new();
        public List<UnmatchedPipe> Unmatched { get; } = new();
    }

    public sealed class UnmatchedPipe
    {
        public int ElementId { get; set; }
        public int? DiameterMm { get; set; }
        public string? Description { get; set; }
        public string? Segment { get; set; }
    }

    internal static class TigreCodeApplier
    {
        public static TigreCodeApplyResult Run(Document doc)
        {
            TigreCodeApplyResult report = new();
            IReadOnlyList<TigreCatalogEntry> catalog = TigreCodeCatalog.Entries;
            report.CatalogCount = catalog.Count;

            if (catalog.Count == 0)
                throw new InvalidOperationException("Catálogo Tigre vazio.");

            // 1) Garante o parâmetro shared como instância em PipeCurves.
            using (Transaction tx1 = new(doc, "Tigre — Garantir parâmetro 'Tigre: Código'"))
            {
                tx1.Start();
                TigreSharedParameter.EnsureResult ensure = TigreSharedParameter.Ensure(doc);
                report.ParameterAction = ensure.Action;
                report.Warnings.AddRange(ensure.Warnings);
                tx1.Commit();
            }

            doc.Regenerate();

            // 2) Atualiza os tubos.
            IList<Element> pipes = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_PipeCurves)
                .WhereElementIsNotElementType()
                .ToElements();

            report.PipesTotal = pipes.Count;

            using Transaction tx2 = new(doc, "Tigre — Aplicar códigos nos tubos");
            tx2.Start();

            foreach (Element elem in pipes)
            {
                if (elem is not Pipe pipe)
                    continue;

                ProcessPipe(doc, pipe, report);
            }

            tx2.Commit();
            return report;
        }

        private static void ProcessPipe(Document doc, Pipe pipe, TigreCodeApplyResult report)
        {
            string descText = ParamToText(doc, pipe.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION));
            string segText = ParamToText(doc, pipe.get_Parameter(BuiltInParameter.RBS_PIPE_SEGMENT_PARAM));
            string combined = TigreTextUtils.Normalize(descText + " " + segText);

            int? diaMm = GetPipeDiameterMm(pipe);
            if (!diaMm.HasValue)
            {
                report.PipesNoMatch++;
                report.Unmatched.Add(new UnmatchedPipe
                {
                    ElementId = pipe.Id.IntegerValue,
                    DiameterMm = null,
                    Description = descText,
                    Segment = segText,
                });
                return;
            }

            TigreCatalogEntry? match = TigreCodeCatalog.FindMatch(combined, diaMm.Value);
            if (match == null)
            {
                report.PipesNoMatch++;
                report.Unmatched.Add(new UnmatchedPipe
                {
                    ElementId = pipe.Id.IntegerValue,
                    DiameterMm = diaMm,
                    Description = descText,
                    Segment = segText,
                });
                return;
            }

            Parameter? target = TigreSharedParameter.GetTargetParameter(pipe);
            if (target == null || target.IsReadOnly)
            {
                report.PipesParameterIssue++;
                return;
            }

            int code = match.Code;

            try
            {
                switch (target.StorageType)
                {
                    case StorageType.Integer:
                    {
                        int current = target.AsInteger();
                        if (current != code)
                        {
                            target.Set(code);
                            report.PipesUpdated++;
                        }
                        else
                        {
                            report.PipesAlreadyOk++;
                        }
                        break;
                    }

                    case StorageType.String:
                    {
                        string current = target.AsString() ?? string.Empty;
                        string codeStr = code.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        if (current != codeStr)
                        {
                            target.Set(codeStr);
                            report.PipesUpdated++;
                        }
                        else
                        {
                            report.PipesAlreadyOk++;
                        }
                        break;
                    }

                    default:
                        target.Set(code.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        report.PipesUpdated++;
                        break;
                }
            }
            catch
            {
                report.PipesParameterIssue++;
            }
        }

        private static int? GetPipeDiameterMm(Pipe pipe)
        {
            Parameter? p = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
            if (p == null)
                return null;

            try
            {
                double feet = p.AsDouble();
                double mm = UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
                return (int)Math.Round(mm);
            }
            catch
            {
                return null;
            }
        }

        private static string ParamToText(Document doc, Parameter? p)
        {
            if (p == null)
                return string.Empty;

            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String:
                        return p.AsString() ?? string.Empty;

                    case StorageType.ElementId:
                    {
                        ElementId id = p.AsElementId();
                        if (id != null && id.IntegerValue > 0)
                        {
                            Element? el = doc.GetElement(id);
                            if (el != null)
                                return el.Name ?? string.Empty;
                        }
                        break;
                    }
                }

                string? vs = p.AsValueString();
                if (!string.IsNullOrEmpty(vs))
                    return vs;

                return p.StorageType switch
                {
                    StorageType.Integer => p.AsInteger().ToString(System.Globalization.CultureInfo.InvariantCulture),
                    StorageType.Double => p.AsDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _ => string.Empty,
                };
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string FormatReport(TigreCodeApplyResult r)
        {
            string warnings = r.Warnings.Count > 0
                ? "\n\nAvisos:\n - " + string.Join("\n - ", r.Warnings)
                : string.Empty;

            string unmatchedPreview = string.Empty;
            if (r.Unmatched.Count > 0)
            {
                int show = Math.Min(r.Unmatched.Count, 5);
                List<string> lines = new();
                for (int i = 0; i < show; i++)
                {
                    UnmatchedPipe u = r.Unmatched[i];
                    lines.Add($" - ID {u.ElementId} | Ø{u.DiameterMm}mm | {u.Description} / {u.Segment}");
                }
                if (r.Unmatched.Count > show)
                    lines.Add($" - … e mais {r.Unmatched.Count - show} tubo(s).");

                unmatchedPreview = "\n\nTubos sem correspondência:\n" + string.Join("\n", lines);
            }

            return
                $"Catálogo: {r.CatalogCount} itens\n" +
                $"Tubos: {r.PipesTotal}\n" +
                $"Atualizados: {r.PipesUpdated}\n" +
                $"Já estavam corretos: {r.PipesAlreadyOk}\n" +
                $"Sem correspondência: {r.PipesNoMatch}\n" +
                $"Sem parâmetro acessível: {r.PipesParameterIssue}\n\n" +
                $"Parâmetro: {r.ParameterAction}" +
                warnings +
                unmatchedPreview;
        }
    }
}
