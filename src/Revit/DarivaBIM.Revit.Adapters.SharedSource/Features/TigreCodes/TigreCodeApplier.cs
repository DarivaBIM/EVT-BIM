using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Revit.Adapters.Common.SharedParameters;
using DarivaBIM.Revit.Adapters.Common.Transactions;

namespace DarivaBIM.Revit.Adapters.Features.TigreCodes
{
    /// <summary>
    /// Implementação Revit-side de <see cref="ITigreCodeApplyService"/>.
    /// Recebe os IDs marcados pelo usuário no WPF, refaz o match contra o
    /// <see cref="TigreCatalog"/> e grava o código no parâmetro Tigre:
    /// Código dentro de uma única transação.
    ///
    /// Slice 3.3 — escrita dual-path:
    ///   (a) tenta o parâmetro no INSTANCE (via SharedParameterAccessor.GetParameter,
    ///       que é instance-only por design do Slice 1.5 B2). Caso típico:
    ///       Pipes com binding global Instance, fittings custom com param
    ///       Instance bindado.
    ///   (b) Se instance não disponível, cai no TYPE (mesmo acessor aplicado
    ///       ao Element retornado por Document.GetElement(typeId)). Caso
    ///       típico: famílias catálogo Tigre que embutem Tigre: Código no
    ///       type da família.
    ///   (c) Se ambos indisponíveis, registra <see cref="TigreApplyIssue"/>
    ///       e contabiliza ParameterIssue. NÃO cria param Type novo —
    ///       famílias custom IsTigre sem param precisam ser preparadas
    ///       pelo modelador, e o issue lista qual família.
    ///
    /// Trade-off de escrita no TYPE: afeta TODAS as instances daquele type.
    /// Aceito por design — em famílias catálogo Tigre, 1 type = 1 SKU,
    /// então todas instances DEVEM compartilhar o código.
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

        public TigreSelectiveApplyResult Apply(IReadOnlyList<long> elementIds)
        {
            if (elementIds == null) throw new ArgumentNullException(nameof(elementIds));

            TigreCatalog catalog = _catalogProvider.Load();
            if (catalog.Entries.Count == 0)
            {
                return new TigreSelectiveApplyResult
                {
                    Selected = elementIds.Count,
                    ErrorMessage = "Catálogo Tigre vazio.",
                };
            }

            int totalInProject = TigreElementCollector
                .CollectTigreElements(_doc, catalog).Count;

            int inserted = 0;
            int overwritten = 0;
            int alreadyOk = 0;
            int noMatch = 0;
            int paramIssue = 0;
            List<TigreApplyIssue> issues = new();

            RevitTransactionRunner.Run(_doc, "Tigre — Inserir/Atualizar códigos", () =>
            {
                foreach (long rawId in elementIds)
                {
                    Element? el = _doc.GetElement(new ElementId(rawId));
                    if (el == null)
                    {
                        // Elemento deletado entre o scan e o apply — ignora.
                        continue;
                    }

                    ProcessElement(
                        el,
                        catalog,
                        ref inserted,
                        ref overwritten,
                        ref alreadyOk,
                        ref noMatch,
                        ref paramIssue,
                        issues);
                }
            });

            return new TigreSelectiveApplyResult
            {
                CatalogCount = catalog.Entries.Count,
                ElementsTotalInProject = totalInProject,
                Selected = elementIds.Count,
                Inserted = inserted,
                Overwritten = overwritten,
                AlreadyOk = alreadyOk,
                NoMatch = noMatch,
                ParameterIssue = paramIssue,
                Issues = issues,
            };
        }

        private void ProcessElement(
            Element element,
            TigreCatalog catalog,
            ref int inserted,
            ref int overwritten,
            ref int alreadyOk,
            ref int noMatch,
            ref int paramIssue,
            List<TigreApplyIssue> issues)
        {
            TigreElementData data = TigreElementDataReader.Read(_doc, element);

            if (!data.DiameterMm.HasValue || data.Kinds.Count == 0)
            {
                noMatch++;
                return;
            }

            // Fix smoke pos-Codex 2026-05-27: inclui FamilyName no combined
            // (vide TigreCodeScanner.cs pra rationale detalhado — sem isso,
            // matcher recebia tokens insuficientes e fittings/caps com SKU
            // exata caiam em "Sem correspondencia"). FamilyName carrega o
            // vocabulario distintivo da peca, TypeName tipico = "Standard".
            string combined = TigreTextUtils.Normalize(
                $"{data.Description} {data.Segment} {data.TypeName} {data.FamilyName}");
            TigreCatalogEntry? match = catalog.FindMatch(
                data.Description, data.Segment, data.TypeName, combined,
                data.DiameterMm.Value, kindFilters: data.Kinds);

            if (match == null)
            {
                noMatch++;
                return;
            }

            // (a) instance write — preferencial sempre que disponível
            Parameter? targetInstance = SharedParameterAccessor.GetParameter(
                element, TigreCodesSharedParameters.Code);
            if (targetInstance != null && !targetInstance.IsReadOnly)
            {
                ApplyWrite(targetInstance, match.Code,
                    ref inserted, ref overwritten, ref alreadyOk, ref paramIssue);
                return;
            }

            // (b) fallback type — famílias catálogo Tigre (param mora no type)
            ElementId typeId = element.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                Element? type = _doc.GetElement(typeId);
                if (type != null)
                {
                    Parameter? targetType = SharedParameterAccessor.GetParameter(
                        type, TigreCodesSharedParameters.Code);
                    if (targetType != null && !targetType.IsReadOnly)
                    {
                        ApplyWrite(targetType, match.Code,
                            ref inserted, ref overwritten, ref alreadyOk, ref paramIssue);
                        return;
                    }
                }
            }

            // (c) skip + audit issue
            paramIssue++;
            string familyDisplay = string.IsNullOrWhiteSpace(data.FamilyName)
                ? "(família sem nome)"
                : data.FamilyName;
            issues.Add(new TigreApplyIssue(
                element.Id.Value,
                data.FamilyName,
                $"Tigre: Código não disponível no instance nem no type da família '{familyDisplay}'"));
        }

        private static void ApplyWrite(
            Parameter target,
            int code,
            ref int inserted,
            ref int overwritten,
            ref int alreadyOk,
            ref int paramIssue)
        {
            // Distingue Inserted (vazio → novo valor) de Overwritten (valor
            // pré-existente diferente). Pra Integer, "vazio" é zero.
            bool wasEmpty = IsEmptyCode(target);

            TigreWriteOutcome outcome = TigreCodeWriter.Write(target, code);
            switch (outcome)
            {
                case TigreWriteOutcome.AlreadyOk:
                    alreadyOk++;
                    break;
                case TigreWriteOutcome.Overwritten:
                    if (wasEmpty)
                        inserted++;
                    else
                        overwritten++;
                    break;
                case TigreWriteOutcome.ParameterIssue:
                    paramIssue++;
                    break;
            }
        }

        private static bool IsEmptyCode(Parameter param)
        {
            try
            {
                return param.StorageType switch
                {
                    StorageType.Integer => param.AsInteger() == 0,
                    StorageType.String => string.IsNullOrEmpty(param.AsString()),
                    _ => true,
                };
            }
            catch
            {
                return true;
            }
        }
    }
}
