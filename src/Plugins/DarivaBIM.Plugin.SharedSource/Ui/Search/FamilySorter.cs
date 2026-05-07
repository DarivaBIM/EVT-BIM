using System;
using System.Collections.Generic;
using System.Linq;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Infrastructure.Persistence.Preferences;

namespace DarivaBIM.Plugin.Ui.Search
{
    /// <summary>
    /// Ordenação aplicada à galeria de famílias. Mantém a aba "recent" como
    /// caso especial porque ela respeita a ordem do histórico de import,
    /// não a do toolbar (sort por nome / data).
    /// </summary>
    internal static class FamilySorter
    {
        // UpdatedAt/CreatedAt podem ser null para famílias antigas — null é
        // tratado como mais antigo que qualquer data conhecida.
        public static IEnumerable<FamilyItem> ApplySort(IEnumerable<FamilyItem> source, string sort) =>
            sort switch
            {
                "name" => source.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
                "newest" => source.OrderByDescending(f => f.CreatedAt ?? DateTime.MinValue),
                _ => source.OrderByDescending(f => f.UpdatedAt ?? DateTime.MinValue),
            };

        // Empareia famílias do catálogo com entradas do histórico de import,
        // preservando a ordem do histórico (mais recente primeiro). Famílias
        // que estavam no histórico mas saíram do catálogo (excluídas no
        // backend) são silenciosamente ignoradas.
        public static List<FamilyItem> OrderByRecency(
            List<FamilyItem> snapshot,
            IReadOnlyList<RecentFamilyEntry> recents)
        {
            Dictionary<int, FamilyItem> byId = new Dictionary<int, FamilyItem>(snapshot.Count);
            for (int i = 0; i < snapshot.Count; i++)
            {
                byId[snapshot[i].Id] = snapshot[i];
            }

            List<FamilyItem> ordered = new List<FamilyItem>(recents.Count);
            for (int i = 0; i < recents.Count; i++)
            {
                if (byId.TryGetValue(recents[i].FamilyId, out FamilyItem? family))
                {
                    ordered.Add(family);
                }
            }

            return ordered;
        }
    }
}
