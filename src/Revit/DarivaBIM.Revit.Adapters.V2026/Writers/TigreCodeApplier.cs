using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Revit.Adapters.V2026.Parameters;
using DarivaBIM.Revit.Adapters.V2026.Filters;
using DarivaBIM.Revit.Adapters.V2026.Mapping;
using DarivaBIM.Revit.Adapters.V2026.Transactions;
using DarivaBIM.Revit.Adapters.V2026.Writers;
using DarivaBIM.Application.DTOs.Family;

namespace DarivaBIM.Revit.Adapters.V2026.Writers
{
    /// <summary>
    /// Revit 2026 implementation of <see cref="ITigreCodeApplyService"/>.
    /// Pulls the catalog from <see cref="ITigreCatalogProvider"/> and writes the
    /// matching code into the Tigre shared parameter on every pipe in the active
    /// document.
    /// </summary>
    public sealed class TigreCodeApplier : ITigreCodeApplyService
    {
        private readonly Document _doc;
        private readonly ITigreCatalogProvider _catalogProvider;

        public TigreCodeApplier(Document doc, ITigreCatalogProvider catalogProvider)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        }

        public TigreCodeApplyResult Apply()
        {
            TigreCodeApplyResult report = new TigreCodeApplyResult();
            TigreCatalog catalog = _catalogProvider.Load();
            report.CatalogCount = catalog.Entries.Count;

            if (catalog.Entries.Count == 0)
                throw new InvalidOperationException("Catálogo Tigre vazio.");

            ExecuteInWriteTransaction(_doc, "Tigre — Aplicar códigos nos tubos", () =>
            {
                TigreSharedParameter.EnsureResult ensure = TigreSharedParameter.Ensure(_doc);
                report.ParameterAction = ensure.Action;
                report.Warnings.AddRange(ensure.Warnings);

                _doc.Regenerate();

                IList<Element> pipes = new FilteredElementCollector(_doc)
                    .OfCategory(BuiltInCategory.OST_PipeCurves)
                    .WhereElementIsNotElementType()
                    .ToElements();

                report.PipesTotal = pipes.Count;

                foreach (Element elem in pipes)
                {
                    if (elem is Pipe pipe)
                        ProcessPipe(_doc, pipe, catalog, report);
                }
            });

            return report;
        }

        private static void ExecuteInWriteTransaction(Document doc, string transactionName, Action action)
        {
            if (doc.IsModifiable)
            {
                action();
                return;
            }

            using Transaction tx = new Transaction(doc, transactionName);
            TransactionStatus status = tx.Start();
            if (status != TransactionStatus.Started)
                throw new InvalidOperationException("Não foi possível abrir transação para aplicar os códigos Tigre.");

            action();
            tx.Commit();
        }

        private static void ProcessPipe(Document doc, Pipe pipe, TigreCatalog catalog, TigreCodeApplyResult report)
        {
            (string description, _) = GetPipeDescriptionText(doc, pipe);
            string segment = ParamToText(doc, pipe.get_Parameter(BuiltInParameter.RBS_PIPE_SEGMENT_PARAM));
            string typeName = GetPipeTypeName(doc, pipe);
            string combined = TigreTextUtils.Normalize($"{description} {segment} {typeName}");

            int? diaMm = GetPipeDiameterMm(pipe);
            if (!diaMm.HasValue)
            {
                RegisterNoMatch(report, pipe, null, description, segment, typeName);
                return;
            }

            TigreCatalogEntry? match = catalog.FindMatch(description, segment, typeName, combined, diaMm.Value);
            if (match == null)
            {
                RegisterNoMatch(report, pipe, diaMm, description, segment, typeName);
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
                        target.Set(code);
                        if (current == code)
                            report.PipesAlreadyOk++;
                        else
                            report.PipesOverwritten++;
                        report.PipesUpdated++;
                        break;
                    }
                    case StorageType.String:
                    {
                        string current = target.AsString() ?? string.Empty;
                        string codeStr = code.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        target.Set(codeStr);
                        if (current == codeStr)
                            report.PipesAlreadyOk++;
                        else
                            report.PipesOverwritten++;
                        report.PipesUpdated++;
                        break;
                    }
                    default:
                        target.Set(code.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        report.PipesUpdated++;
                        report.PipesOverwritten++;
                        break;
                }
            }
            catch
            {
                report.PipesParameterIssue++;
            }
        }

        private static void RegisterNoMatch(TigreCodeApplyResult report, Pipe pipe, int? diaMm, string description, string segment, string typeName)
        {
            report.PipesNoMatch++;
            report.Unmatched.Add(new UnmatchedPipe
            {
                ElementId = pipe.Id.Value,
                DiameterMm = diaMm,
                Description = description,
                Segment = segment,
                TypeName = typeName,
            });
        }

        private static (string Text, string Source) GetPipeDescriptionText(Document doc, Pipe pipe)
        {
            string[] names = { "Descrição", "Description", "Descriçao", "Descricao" };

            string txt = GetParamTextByName(doc, pipe, names);
            if (!string.IsNullOrWhiteSpace(txt))
                return (txt, "instance_name");

            txt = ParamToText(doc, pipe.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION));
            if (!string.IsNullOrWhiteSpace(txt))
                return (txt, "instance_builtin");

            Element? pipeType = doc.GetElement(pipe.GetTypeId());
            txt = GetParamTextByName(doc, pipeType, names);
            if (!string.IsNullOrWhiteSpace(txt))
                return (txt, "type_name");

            txt = ParamToText(doc, pipeType?.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION));
            if (!string.IsNullOrWhiteSpace(txt))
                return (txt, "type_builtin");

            return (pipeType?.Name ?? string.Empty, "type_name_fallback");
        }

        private static string GetParamTextByName(Document doc, Element? element, IEnumerable<string> names)
        {
            if (element == null)
                return string.Empty;

            foreach (string name in names)
            {
                try
                {
                    string txt = ParamToText(doc, element.LookupParameter(name));
                    if (!string.IsNullOrWhiteSpace(txt))
                        return txt;
                }
                catch
                {
                    // continua
                }
            }

            return string.Empty;
        }

        private static string GetPipeTypeName(Document doc, Pipe pipe)
        {
            ElementId typeId = pipe.GetTypeId();
            if (typeId == null || typeId.Value <= 0)
                return string.Empty;

            Element? pipeType = doc.GetElement(typeId);
            return pipeType?.Name ?? string.Empty;
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
                        if (id != null && id.Value > 0)
                            return doc.GetElement(id)?.Name ?? string.Empty;
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
    }
}
