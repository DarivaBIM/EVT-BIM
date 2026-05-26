using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Revit.Adapters.Common.SharedParameters;

namespace DarivaBIM.Revit.Adapters.Features.TigreCodes
{
    /// <summary>
    /// Implementação Revit-side de <see cref="ITigreCodeScanService"/>:
    /// percorre todos os tubos do projeto, lê descrição/segmento/tipo/diâmetro
    /// e o valor atual do parâmetro Tigre: Código, casa contra o
    /// <see cref="TigreCatalog"/> e devolve um snapshot agrupado por
    /// (TipoNome, Diâmetro, Status). Não abre transação — é leitura pura.
    /// </summary>
    public sealed class TigreCodeScanner : ITigreCodeScanService
    {
        private readonly Document _doc;
        private readonly ITigreCatalogProvider _catalogProvider;

        public TigreCodeScanner(Document doc, ITigreCatalogProvider catalogProvider)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        }

        public TigreScanResult Scan()
        {
            TigreCatalog catalog = _catalogProvider.Load();

            if (catalog.Entries.Count == 0)
            {
                return new TigreScanResult
                {
                    ErrorMessage = "Catálogo Tigre vazio.",
                };
            }

            IList<Pipe> pipes = TigrePipeCollector.CollectPipes(_doc);
            int pipesWithParam = 0;

            // Chave de agrupamento: (TipoNome, Diâmetro nominal mm, Status).
            // Diâmetro nullable: tubo sem diâmetro vai para um bucket próprio
            // (sempre NoMatch, já que o catálogo exige diâmetro pra casar).
            Dictionary<GroupKey, GroupAccumulator> map = new();

            foreach (Pipe pipe in pipes)
            {
                TigrePipeData data = TigrePipeDataReader.Read(_doc, pipe);

                TigreCatalogEntry? match = null;
                if (data.DiameterMm.HasValue)
                {
                    string combined = TigreTextUtils.Normalize(
                        $"{data.Description} {data.Segment} {data.TypeName}");
                    // PipeCodes só processa Pipes (TigrePipeCollector já
                    // filtra), então passamos kindFilter="pipe" defensivo
                    // pra evitar colisão de tokens com fittings do catálogo
                    // expandido (Slice 2B.5).
                    match = catalog.FindMatch(
                        data.Description, data.Segment, data.TypeName, combined,
                        data.DiameterMm.Value, kindFilter: "pipe");
                }

                Parameter? param = SharedParameterAccessor.GetParameter(pipe, TigreCodesSharedParameters.Code);
                if (param != null)
                    pipesWithParam++;

                int? currentCode = ReadCurrentCode(param);
                TigrePipeStatus status = ComputeStatus(match, currentCode);

                GroupKey key = new(data.TypeName, data.DiameterMm, status);
                if (!map.TryGetValue(key, out GroupAccumulator? acc))
                {
                    acc = new GroupAccumulator(match?.Code);
                    map[key] = acc;
                }
                acc.Ids.Add(pipe.Id.Value);
            }

            List<TigreScanGroup> groups = map
                .OrderBy(kv => kv.Key.TypeName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(kv => kv.Key.DiameterMm ?? int.MaxValue)
                .Select(kv => new TigreScanGroup(
                    kv.Key.TypeName,
                    kv.Key.DiameterMm,
                    kv.Key.Status,
                    kv.Value.Ids.ToArray(),
                    kv.Value.MatchedCode))
                .ToList();

            return new TigreScanResult
            {
                CatalogCount = catalog.Entries.Count,
                PipesTotal = pipes.Count,
                PipesWithParameter = pipesWithParam,
                PipesWithoutParameter = pipes.Count - pipesWithParam,
                ParameterIsBound = pipes.Count == 0 || pipesWithParam == pipes.Count,
                Groups = groups,
            };
        }

        private static int? ReadCurrentCode(Parameter? param)
        {
            if (param == null)
                return null;

            try
            {
                switch (param.StorageType)
                {
                    case StorageType.Integer:
                    {
                        int v = param.AsInteger();
                        // Integer parameters always have a value; 0 é o default
                        // do Revit e nunca é um código Tigre válido, então
                        // tratamos como "vazio".
                        return v == 0 ? (int?)null : v;
                    }
                    case StorageType.String:
                    {
                        string? s = param.AsString();
                        if (string.IsNullOrWhiteSpace(s))
                            return null;
                        return int.TryParse(s, System.Globalization.NumberStyles.Integer,
                                            System.Globalization.CultureInfo.InvariantCulture, out int n)
                            ? n
                            : (int?)null;
                    }
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static TigrePipeStatus ComputeStatus(TigreCatalogEntry? match, int? currentCode)
        {
            if (match == null)
                return TigrePipeStatus.NoMatch;

            if (!currentCode.HasValue)
                return TigrePipeStatus.Missing;

            return currentCode.Value == match.Code
                ? TigrePipeStatus.Ok
                : TigrePipeStatus.Divergent;
        }

        private readonly struct GroupKey : IEquatable<GroupKey>
        {
            public GroupKey(string typeName, int? diameterMm, TigrePipeStatus status)
            {
                TypeName = typeName ?? string.Empty;
                DiameterMm = diameterMm;
                Status = status;
            }

            public string TypeName { get; }
            public int? DiameterMm { get; }
            public TigrePipeStatus Status { get; }

            public bool Equals(GroupKey other) =>
                StringComparer.Ordinal.Equals(TypeName, other.TypeName) &&
                DiameterMm == other.DiameterMm &&
                Status == other.Status;

            public override bool Equals(object? obj) => obj is GroupKey k && Equals(k);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = StringComparer.Ordinal.GetHashCode(TypeName);
                    h = (h * 397) ^ (DiameterMm ?? -1);
                    h = (h * 397) ^ (int)Status;
                    return h;
                }
            }
        }

        private sealed class GroupAccumulator
        {
            public GroupAccumulator(int? matchedCode)
            {
                MatchedCode = matchedCode;
            }

            public int? MatchedCode { get; }
            public List<long> Ids { get; } = new();
        }
    }
}
