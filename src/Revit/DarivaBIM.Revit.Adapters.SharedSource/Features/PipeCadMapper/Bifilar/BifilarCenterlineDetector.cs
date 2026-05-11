using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using DarivaBIM.Revit.Adapters.Common.Cad;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Bifilar
{
    /// <summary>
    /// Detector que, dado um <see cref="ImportInstance"/>, um nome de layer
    /// alvo e um conjunto de limiares (<see cref="BifilarDetectionParameters"/>),
    /// retorna a lista de eixos centrais (linha mediana entre as duas
    /// paredes) dos tubos representados de forma bifilar. É uma porta do
    /// algoritmo que rodava no Dynamo: percorre a geometria, descarta
    /// segmentos curtos/curvos como "símbolos" de fundo, agrupa endpoints
    /// próximos via union-find e parea segmentos paralelos com sobreposição
    /// suficiente e poucos símbolos entre eles.
    ///
    /// As distâncias internas são todas em FEET (unidade interna do Revit);
    /// os limiares vêm da config em milímetros e são convertidos sob demanda.
    /// </summary>
    public sealed class BifilarCenterlineDetector
    {
        private const double MmPerFoot = 304.8;

        private readonly Document _doc;
        private readonly BifilarDetectionParameters _params;

        // Geometria coletada na fase de scan.
        private readonly List<Candidate> _candidates = new();
        private readonly List<SymbolPoint> _symbolPoints = new();
        private readonly Dictionary<(int, int), List<int>> _symbolGrid = new();
        private double _symbolGridCellFt;

        public BifilarCenterlineDetector(Document doc, BifilarDetectionParameters parameters)
        {
            _doc = doc;
            _params = parameters;
        }

        public IReadOnlyList<BifilarCenterline> Detect(ImportInstance importInstance, string targetLayer)
        {
            _candidates.Clear();
            _symbolPoints.Clear();
            _symbolGrid.Clear();

            Options opts = new()
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine,
            };

            GeometryElement? geomElem = importInstance.get_Geometry(opts);
            if (geomElem == null)
                return Array.Empty<BifilarCenterline>();

            WalkGeometry(geomElem, targetLayer);

            // Limita candidates aos mais longos (a parte da reta é o que
            // tem maior poder de evidência; segmentos curtos viram ruído).
            _candidates.Sort((a, b) => b.LengthMm.CompareTo(a.LengthMm));
            if (_candidates.Count > _params.MaxCandidateSegments)
            {
                _candidates.RemoveRange(_params.MaxCandidateSegments, _candidates.Count - _params.MaxCandidateSegments);
            }

            // Reindexa para que o índice no candidate bata com a posição
            // na lista (importante para o lookup do union-find e do grid).
            for (int i = 0; i < _candidates.Count; i++)
                _candidates[i].Index = i;

            ClusterEndpoints();
            BuildSymbolGrid();

            return FindCenterlines();
        }

        // -------------------------------------------------------------
        // Coleta de geometria
        // -------------------------------------------------------------

        private void WalkGeometry(GeometryElement geomElem, string targetLayer)
        {
            foreach (GeometryObject obj in geomElem)
            {
                if (obj is GeometryInstance gi)
                {
                    GeometryElement inst = gi.GetInstanceGeometry();
                    if (inst != null)
                        WalkGeometry(inst, targetLayer);
                    continue;
                }

                string? layer = CadLayerScanner.TryReadLayerName(_doc, obj);
                if (!string.Equals(layer, targetLayer, StringComparison.OrdinalIgnoreCase))
                    continue;

                ProcessGeometry(obj);
            }
        }

        private void ProcessGeometry(GeometryObject obj)
        {
            switch (obj)
            {
                case Line line:
                    AddLinearSegment(line.GetEndPoint(0), line.GetEndPoint(1));
                    break;

                case PolyLine pl:
                    IList<XYZ> pts = pl.GetCoordinates();
                    for (int i = 0; i < pts.Count - 1; i++)
                        AddLinearSegment(pts[i], pts[i + 1]);
                    break;

                case Arc arc:
                    AddCurvedSamples(arc, 6, isHard: true);
                    break;

                case Ellipse ellipse:
                    AddCurvedSamples(ellipse, 8, isHard: true);
                    break;

                case NurbSpline nurb:
                    AddCurvedSamples(nurb, 8, isHard: true);
                    break;

                case HermiteSpline hermite:
                    AddCurvedSamples(hermite, 8, isHard: true);
                    break;
            }
        }

        private void AddLinearSegment(XYZ p0, XYZ p1)
        {
            double lengthFt = p0.DistanceTo(p1);
            double lengthMm = lengthFt * MmPerFoot;

            if (lengthMm < _params.MinCandidateLengthMm)
            {
                // Segmento curto não vira candidato, mas seu ponto médio
                // é registrado como "símbolo leve" para o filtro de
                // sobreposição com a centerline (cotas, hachuras, etc.).
                if (lengthMm >= 5.0)
                {
                    XYZ mid = new((p0.X + p1.X) / 2.0, (p0.Y + p1.Y) / 2.0, (p0.Z + p1.Z) / 2.0);
                    _symbolPoints.Add(new SymbolPoint(mid, isHard: false));
                }
                return;
            }

            (XYZ a, XYZ b) = NormalizeSegmentDirection(p0, p1);
            double? angle = AngleDeg(a, b);
            if (angle == null)
                return;

            _candidates.Add(new Candidate
            {
                P0 = a,
                P1 = b,
                LengthMm = lengthMm,
                AngleDeg = angle.Value,
                ClusterId = -1,
                Index = _candidates.Count,
            });
        }

        private void AddCurvedSamples(Curve curve, int divisions, bool isHard)
        {
            for (int i = 0; i <= divisions; i++)
            {
                double t = (double)i / divisions;
                XYZ p;
                try { p = curve.Evaluate(t, true); }
                catch { continue; }

                _symbolPoints.Add(new SymbolPoint(p, isHard));
            }
        }

        // -------------------------------------------------------------
        // Clustering de endpoints (union-find)
        // -------------------------------------------------------------

        private void ClusterEndpoints()
        {
            int n = _candidates.Count;
            if (n == 0) return;

            int endpointCount = n * 2;
            int[] parent = new int[endpointCount];
            int[] rank = new int[endpointCount];
            for (int i = 0; i < endpointCount; i++) parent[i] = i;

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }
                return x;
            }

            void Union(int a, int b)
            {
                int ra = Find(a);
                int rb = Find(b);
                if (ra == rb) return;
                if (rank[ra] < rank[rb]) parent[ra] = rb;
                else if (rank[ra] > rank[rb]) parent[rb] = ra;
                else { parent[rb] = ra; rank[ra]++; }
            }

            // União dos dois endpoints de cada segmento.
            for (int i = 0; i < n; i++) Union(2 * i, 2 * i + 1);

            double snapFt = Math.Max(_params.ClusterSnapMm, 1.0) / MmPerFoot;

            Dictionary<(int, int), List<int>> grid = new();
            XYZ[] endpoints = new XYZ[endpointCount];

            for (int i = 0; i < n; i++)
            {
                endpoints[2 * i] = _candidates[i].P0;
                endpoints[2 * i + 1] = _candidates[i].P1;
            }

            for (int idx = 0; idx < endpointCount; idx++)
            {
                XYZ p = endpoints[idx];
                int cx = (int)Math.Floor(p.X / snapFt);
                int cy = (int)Math.Floor(p.Y / snapFt);

                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    var key = (cx + dx, cy + dy);
                    if (!grid.TryGetValue(key, out var bucket)) continue;

                    foreach (int otherIdx in bucket)
                    {
                        if (p.DistanceTo(endpoints[otherIdx]) * MmPerFoot <= _params.ClusterSnapMm)
                            Union(idx, otherIdx);
                    }
                }

                var ownKey = (cx, cy);
                if (!grid.TryGetValue(ownKey, out var ownBucket))
                {
                    ownBucket = new List<int>();
                    grid[ownKey] = ownBucket;
                }
                ownBucket.Add(idx);
            }

            for (int i = 0; i < n; i++)
                _candidates[i].ClusterId = Find(2 * i);
        }

        // -------------------------------------------------------------
        // Grid de símbolos para lookup rápido
        // -------------------------------------------------------------

        private void BuildSymbolGrid()
        {
            _symbolGrid.Clear();
            _symbolGridCellFt = Math.Max(_params.SymbolBufferMm, 20.0) / MmPerFoot;

            for (int i = 0; i < _symbolPoints.Count; i++)
            {
                XYZ p = _symbolPoints[i].P;
                int cx = (int)Math.Floor(p.X / _symbolGridCellFt);
                int cy = (int)Math.Floor(p.Y / _symbolGridCellFt);
                var key = (cx, cy);
                if (!_symbolGrid.TryGetValue(key, out var bucket))
                {
                    bucket = new List<int>();
                    _symbolGrid[key] = bucket;
                }
                bucket.Add(i);
            }
        }

        private (int hard, int total) CountSymbolsNear(XYZ p0, XYZ p1)
        {
            double lengthFt = p0.DistanceTo(p1);
            double lengthMm = lengthFt * MmPerFoot;
            if (lengthMm <= 0) return (0, 0);

            double ignoreMm = Math.Min(_params.EndpointIgnoreMm, lengthMm * 0.2);
            double tMin = ignoreMm / lengthMm;
            double tMax = 1.0 - tMin;
            double bufferFt = _params.SymbolBufferMm / MmPerFoot;

            double minX = Math.Min(p0.X, p1.X) - bufferFt;
            double maxX = Math.Max(p0.X, p1.X) + bufferFt;
            double minY = Math.Min(p0.Y, p1.Y) - bufferFt;
            double maxY = Math.Max(p0.Y, p1.Y) + bufferFt;

            int minCx = (int)Math.Floor(minX / _symbolGridCellFt);
            int maxCx = (int)Math.Floor(maxX / _symbolGridCellFt);
            int minCy = (int)Math.Floor(minY / _symbolGridCellFt);
            int maxCy = (int)Math.Floor(maxY / _symbolGridCellFt);

            int hard = 0, total = 0;
            HashSet<int> seen = new();

            for (int cx = minCx; cx <= maxCx; cx++)
            for (int cy = minCy; cy <= maxCy; cy++)
            {
                if (!_symbolGrid.TryGetValue((cx, cy), out var bucket)) continue;

                foreach (int symbolIdx in bucket)
                {
                    if (!seen.Add(symbolIdx)) continue;

                    SymbolPoint sp = _symbolPoints[symbolIdx];
                    (double dMm, double t) = PointToSegmentDistanceAndT(sp.P, p0, p1);
                    if (t < tMin || t > tMax) continue;
                    if (dMm > _params.SymbolBufferMm) continue;

                    total++;
                    if (sp.IsHard) hard++;
                }
            }

            return (hard, total);
        }

        // -------------------------------------------------------------
        // Pareamento
        // -------------------------------------------------------------

        private IReadOnlyList<BifilarCenterline> FindCenterlines()
        {
            int n = _candidates.Count;
            if (n == 0) return Array.Empty<BifilarCenterline>();

            // Grid espacial para os próprios candidates (encontrar vizinhos
            // dentro do "tubo": no máximo MaxEdgeDistance + folga).
            double cellFt = _params.SegmentGridCellMm / MmPerFoot;
            if (cellFt <= 0) cellFt = 400.0 / MmPerFoot;

            Dictionary<(int, int), List<int>> grid = new();
            double maxEdgeFt = _params.MaxEdgeDistanceMm / MmPerFoot;

            for (int i = 0; i < n; i++)
            {
                Candidate c = _candidates[i];
                double minX = Math.Min(c.P0.X, c.P1.X) - maxEdgeFt;
                double maxX = Math.Max(c.P0.X, c.P1.X) + maxEdgeFt;
                double minY = Math.Min(c.P0.Y, c.P1.Y) - maxEdgeFt;
                double maxY = Math.Max(c.P0.Y, c.P1.Y) + maxEdgeFt;

                int minCx = (int)Math.Floor(minX / cellFt);
                int maxCx = (int)Math.Floor(maxX / cellFt);
                int minCy = (int)Math.Floor(minY / cellFt);
                int maxCy = (int)Math.Floor(maxY / cellFt);

                for (int cx = minCx; cx <= maxCx; cx++)
                for (int cy = minCy; cy <= maxCy; cy++)
                {
                    var key = (cx, cy);
                    if (!grid.TryGetValue(key, out var bucket))
                    {
                        bucket = new List<int>();
                        grid[key] = bucket;
                    }
                    bucket.Add(i);
                }
            }

            List<PairCandidate> pairs = new();
            HashSet<(int, int)> tested = new();

            for (int i = 0; i < n && pairs.Count < _params.MaxValidPairsStored; i++)
            {
                Candidate a = _candidates[i];

                double minX = Math.Min(a.P0.X, a.P1.X) - maxEdgeFt;
                double maxX = Math.Max(a.P0.X, a.P1.X) + maxEdgeFt;
                double minY = Math.Min(a.P0.Y, a.P1.Y) - maxEdgeFt;
                double maxY = Math.Max(a.P0.Y, a.P1.Y) + maxEdgeFt;

                int minCx = (int)Math.Floor(minX / cellFt);
                int maxCx = (int)Math.Floor(maxX / cellFt);
                int minCy = (int)Math.Floor(minY / cellFt);
                int maxCy = (int)Math.Floor(maxY / cellFt);

                HashSet<int> neighbors = new();
                for (int cx = minCx; cx <= maxCx; cx++)
                for (int cy = minCy; cy <= maxCy; cy++)
                {
                    if (!grid.TryGetValue((cx, cy), out var bucket)) continue;
                    foreach (int idx in bucket)
                        if (idx > i) neighbors.Add(idx);
                }

                foreach (int j in neighbors)
                {
                    var key = (i, j);
                    if (!tested.Add(key)) continue;

                    Candidate b = _candidates[j];
                    if (AngleDiffDeg(a.AngleDeg, b.AngleDeg) > _params.AngleToleranceDeg)
                        continue;

                    PairCandidate? pair = ComputePair(a, b);
                    if (pair == null) continue;

                    pairs.Add(pair);
                    if (pairs.Count >= _params.MaxValidPairsStored) break;
                }
            }

            // Ordena: prioriza pares no mesmo cluster (extremidades coincidem
            // melhor com o tubo real), depois menor distância entre paredes
            // (mais provavelmente o tubo, não duas paredes adjacentes), depois
            // maior sobreposição (mais evidência).
            pairs.Sort((p, q) =>
            {
                int sameCmp = (q.SameCluster ? 1 : 0) - (p.SameCluster ? 1 : 0);
                if (sameCmp != 0) return sameCmp;

                int edgeCmp = p.EdgeDistanceMm.CompareTo(q.EdgeDistanceMm);
                if (edgeCmp != 0) return edgeCmp;

                return q.OverlapMm.CompareTo(p.OverlapMm);
            });

            HashSet<int> usedEdges = new();
            HashSet<(int, int, int, int)> dedupKeys = new();
            List<BifilarCenterline> accepted = new();

            foreach (PairCandidate pair in pairs)
            {
                if (_params.LockEdgeAfterPair &&
                    (usedEdges.Contains(pair.AIndex) || usedEdges.Contains(pair.BIndex)))
                    continue;

                var dedup = (
                    (int)Math.Round(pair.Start.X * MmPerFoot / 10.0),
                    (int)Math.Round(pair.Start.Y * MmPerFoot / 10.0),
                    (int)Math.Round(pair.End.X * MmPerFoot / 10.0),
                    (int)Math.Round(pair.End.Y * MmPerFoot / 10.0));
                if (!dedupKeys.Add(dedup)) continue;

                if (_params.LockEdgeAfterPair)
                {
                    usedEdges.Add(pair.AIndex);
                    usedEdges.Add(pair.BIndex);
                }

                accepted.Add(new BifilarCenterline(pair.Start, pair.End, pair.EdgeDistanceMm));
            }

            return accepted;
        }

        private PairCandidate? ComputePair(Candidate a, Candidate b)
        {
            // Vetor unitário u ao longo de A; n perpendicular no plano XY.
            double dx = a.P1.X - a.P0.X;
            double dy = a.P1.Y - a.P0.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-12) return null;

            double ux = dx / len;
            double uy = dy / len;
            double nx = -uy;
            double ny = ux;

            double Dot(XYZ p, double vx, double vy) => p.X * vx + p.Y * vy;

            double ta0 = Dot(a.P0, ux, uy);
            double ta1 = Dot(a.P1, ux, uy);
            double tb0 = Dot(b.P0, ux, uy);
            double tb1 = Dot(b.P1, ux, uy);

            double aMin = Math.Min(ta0, ta1);
            double aMax = Math.Max(ta0, ta1);
            double bMin = Math.Min(tb0, tb1);
            double bMax = Math.Max(tb0, tb1);

            double overlapStart = Math.Max(aMin, bMin);
            double overlapEnd = Math.Min(aMax, bMax);
            double overlapFt = overlapEnd - overlapStart;
            double overlapMm = overlapFt * MmPerFoot;

            if (overlapMm < _params.MinOverlapMm) return null;

            // Distância normal (eixo n) entre centros.
            XYZ ca = new((a.P0.X + a.P1.X) / 2.0, (a.P0.Y + a.P1.Y) / 2.0, (a.P0.Z + a.P1.Z) / 2.0);
            XYZ cb = new((b.P0.X + b.P1.X) / 2.0, (b.P0.Y + b.P1.Y) / 2.0, (b.P0.Z + b.P1.Z) / 2.0);

            double sa = Dot(ca, nx, ny);
            double sb = Dot(cb, nx, ny);
            double edgeDistanceMm = Math.Abs(sb - sa) * MmPerFoot;

            if (edgeDistanceMm < _params.MinEdgeDistanceMm) return null;
            if (edgeDistanceMm > _params.MaxEdgeDistanceMm) return null;

            // Linha central entre as duas: t corre ao longo de u; s_mid = média
            // dos s das duas paredes; z = média dos z (mantemos o Z do CAD).
            double sMid = (sa + sb) / 2.0;
            double zMid = (ca.Z + cb.Z) / 2.0;

            XYZ start = new(ux * overlapStart + nx * sMid, uy * overlapStart + ny * sMid, zMid);
            XYZ end = new(ux * overlapEnd + nx * sMid, uy * overlapEnd + ny * sMid, zMid);

            double centerLengthMm = start.DistanceTo(end) * MmPerFoot;
            if (centerLengthMm < _params.MinOverlapMm) return null;

            (int hard, int total) = CountSymbolsNear(start, end);
            if (hard > _params.MaxHardSymbolsInside) return null;
            if (total > _params.MaxTotalSymbolsInside) return null;

            return new PairCandidate
            {
                AIndex = a.Index,
                BIndex = b.Index,
                Start = start,
                End = end,
                EdgeDistanceMm = edgeDistanceMm,
                OverlapMm = overlapMm,
                SameCluster = a.ClusterId == b.ClusterId && a.ClusterId != -1,
            };
        }

        // -------------------------------------------------------------
        // Helpers de geometria
        // -------------------------------------------------------------

        private static (XYZ a, XYZ b) NormalizeSegmentDirection(XYZ p0, XYZ p1)
        {
            if (p0.X < p1.X) return (p0, p1);
            if (Math.Abs(p0.X - p1.X) < 1e-9 && p0.Y <= p1.Y) return (p0, p1);
            return (p1, p0);
        }

        private static double? AngleDeg(XYZ p0, XYZ p1)
        {
            double dx = p1.X - p0.X;
            double dy = p1.Y - p0.Y;
            if (Math.Abs(dx) < 1e-12 && Math.Abs(dy) < 1e-12) return null;
            double a = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            while (a < 0) a += 180.0;
            while (a >= 180.0) a -= 180.0;
            return a;
        }

        private static double AngleDiffDeg(double a, double b)
        {
            double d = Math.Abs(a - b);
            if (d > 90.0) d = 180.0 - d;
            return Math.Abs(d);
        }

        private static (double dMm, double t) PointToSegmentDistanceAndT(XYZ p, XYZ a, XYZ b)
        {
            double vx = b.X - a.X;
            double vy = b.Y - a.Y;
            double wx = p.X - a.X;
            double wy = p.Y - a.Y;
            double length2 = vx * vx + vy * vy;
            if (length2 < 1e-18) return (p.DistanceTo(a) * MmPerFoot, 0.0);

            double t = (wx * vx + wy * vy) / length2;

            XYZ closest;
            if (t < 0.0) closest = a;
            else if (t > 1.0) closest = b;
            else closest = new XYZ(a.X + t * vx, a.Y + t * vy, a.Z);

            return (p.DistanceTo(closest) * MmPerFoot, t);
        }

        // -------------------------------------------------------------
        // Tipos internos
        // -------------------------------------------------------------

        private sealed class Candidate
        {
            public XYZ P0 = XYZ.Zero;
            public XYZ P1 = XYZ.Zero;
            public double LengthMm;
            public double AngleDeg;
            public int ClusterId;
            public int Index;
        }

        private sealed class SymbolPoint
        {
            public readonly XYZ P;
            public readonly bool IsHard;
            public SymbolPoint(XYZ p, bool isHard) { P = p; IsHard = isHard; }
        }

        private sealed class PairCandidate
        {
            public int AIndex;
            public int BIndex;
            public XYZ Start = XYZ.Zero;
            public XYZ End = XYZ.Zero;
            public double EdgeDistanceMm;
            public double OverlapMm;
            public bool SameCluster;
        }
    }
}
