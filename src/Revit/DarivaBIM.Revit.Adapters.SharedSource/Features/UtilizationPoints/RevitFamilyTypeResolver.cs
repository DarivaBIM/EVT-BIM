using System;
using Autodesk.Revit.DB;
using DarivaBIM.Domain.Hydraulics.UtilizationPoints;

namespace DarivaBIM.Revit.Adapters.Features.UtilizationPoints
{
    /// <summary>
    /// Resolve uma <see cref="FamilyTypeReference"/> persistida (chave por
    /// nome) contra o <c>FamilySymbol</c> correspondente do documento ativo.
    /// Tenta, em ordem: <c>UniqueId</c>, <c>ElementId</c> e por fim
    /// <c>FamilyName</c> + <c>TypeName</c>. Devolve <c>null</c> quando o tipo
    /// não existe mais no documento — a janela WPF usa isso para marcar a
    /// regra como "Tipo não encontrado".
    /// </summary>
    internal static class RevitFamilyTypeResolver
    {
        public static FamilySymbol? Resolve(Document doc, FamilyTypeReference reference)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (reference == null || reference.IsEmpty) return null;

            FamilySymbol? bySpecificHint = ResolveByHints(doc, reference);
            if (bySpecificHint != null) return bySpecificHint;

            return ResolveByName(doc, reference.FamilyName, reference.TypeName);
        }

        private static FamilySymbol? ResolveByHints(Document doc, FamilyTypeReference reference)
        {
            if (!string.IsNullOrEmpty(reference.UniqueId))
            {
                try
                {
                    Element? byUid = doc.GetElement(reference.UniqueId);
                    if (byUid is FamilySymbol s) return s;
                }
                catch { /* ignora — cai no próximo */ }
            }

            if (reference.ElementId.HasValue)
            {
                try
                {
                    Element? byId = doc.GetElement(new ElementId(reference.ElementId.Value));
                    if (byId is FamilySymbol s) return s;
                }
                catch { /* ignora — cai no próximo */ }
            }

            return null;
        }

        private static FamilySymbol? ResolveByName(Document doc, string familyName, string typeName)
        {
            if (string.IsNullOrWhiteSpace(familyName) || string.IsNullOrWhiteSpace(typeName))
                return null;

            FilteredElementCollector collector = new(doc);
            collector.OfClass(typeof(FamilySymbol));

            foreach (Element element in collector)
            {
                if (element is not FamilySymbol symbol) continue;

                string actualType;
                try { actualType = symbol.Name ?? string.Empty; } catch { actualType = string.Empty; }
                if (!string.Equals(actualType, typeName, StringComparison.Ordinal)) continue;

                string actualFamily;
                try { actualFamily = symbol.Family?.Name ?? string.Empty; } catch { actualFamily = string.Empty; }
                if (!string.Equals(actualFamily, familyName, StringComparison.Ordinal)) continue;

                return symbol;
            }

            return null;
        }
    }
}
