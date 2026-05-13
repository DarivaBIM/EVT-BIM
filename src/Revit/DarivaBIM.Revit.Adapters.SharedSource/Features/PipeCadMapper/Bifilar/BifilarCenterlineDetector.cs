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
        // Polilinha amostrada de cada Arc/Ellipse/Spline encontrado no layer,
        // com bounding-box pré-calculada (em feet). Usado para rejeitar
        // candidates que atravessam essas curvas: uma reta que cruza um
        // círculo, por exemplo, intersecta a curva amostrada ≥2 vezes — quase
        // certamente uma linha auxiliar ou de chamada, não uma parede de tubo.
        private readonly List<CurveSampling> _curveSamplings = new();
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
            _curveSamplings.Clear();
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

            // 1. Une pedaços colineares sobrepostos/encostados em uma única
            //    linha. CADs com vários "fragmentos" representando a mesma
            //    parede de tubo viravam vários marcadores em cima do mesmo
            //    eixo — depois de unir, viram um par só.
            CoalesceCollinearCandidates();

            // 2. Descarta candidates que ATRAVESSAM uma curva do layer
            //    (intersectam um arco/círculo/elipse/spline em 2+ pontos).
            //    Tangência (1 ponto) ainda passa: pode ser um tubo encostando
            //    em uma peça redonda.
            RejectCandidatesCrossingCurves();

            // 3. Sort por comprimento desc + limite (defesa contra CADs gigantes).
            _candidates.Sort((a, b) => b.LengthMm.CompareTo(a.LengthMm));
            if (_candidates.Count > _params.MaxCandidateSegments)
            {
                _candidates.RemoveRange(_params.MaxCandidateSegments, _candidates.Count - _params.MaxCandidateSegments);
            }

            for (int i = 0; i < _candidates.Count; i++)
                _candidates[i].Index = i;

            ClusterEndpoints();
            BuildSymbolGrid();

            return FindCenterlines();
        }

        /// <summary>
        /// Variante de <see cref="Detect"/> para o picker bifilar: roda o
        /// mesmo scan + coalesce + reject-curves no layer, mas em vez de
        /// procurar TODOS os pares, devolve apenas o melhor par que envolve
        /// a linha que o usuário clicou (definida pelos endpoints da
        /// geometria escolhida). Os endpoints podem cair no MEIO de um
        /// candidate coalescido — por isso o casamento usa proximidade
        /// perpendicular ao midpoint, não igualdade de endpoints.
        /// Retorna <c>null</c> se a linha picada não é paralela a nada
        /// compatível dentro da janela de distância do tipo de tubo.
        /// </summary>
        public BifilarCenterline? DetectForAnchor(
            ImportInstance importInstance,
            string targetLayer,
            XYZ anchorStart,
            XYZ anchorEnd)
        {
            _candidates.Clear();
            _symbolPoints.Clear();
            _curveSamplings.Clear();
            _symbolGrid.Clear();

            Options opts = new()
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = true,
                DetailLevel = ViewDetailLevel.Fine,
            };

            GeometryElement? geomElem = importInstance.get_Geometry(opts);
            if (geomElem == null) return null;

            WalkGeometry(geomElem, targetLayer);
            CoalesceCollinearCandidates();
            RejectCandidatesCrossingCurves();

            _candidates.Sort((a, b) => b.LengthMm.CompareTo(a.LengthMm));
            if (_candidates.Count > _params.MaxCandidateSegments)
                _candidates.RemoveRange(_params.MaxCandidateSegments, _candidates.Count - _params.MaxCandidateSegments);

            for (int i = 0; i < _candidates.Count; i++)
                _candidates[i].Index = i;

            ClusterEndpoints();
            BuildSymbolGrid();

            int anchorIdx = FindCandidateForAnchor(anchorStart, anchorEnd);
            if (anchorIdx < 0) return null;

            Candidate a = _candidates[anchorIdx];
            List<PairCandidate> pairs = new();
            for (int j = 0; j < _candidates.Count; j++)
            {
                if (j == anchorIdx) continue;
                Candidate b = _candidates[j];
                if (AngleDiffDeg(a.AngleDeg, b.AngleDeg) > _params.AngleToleranceDeg)
                    continue;
                PairCandidate? pair = ComputePair(a, b);
                if (pair != null) pairs.Add(pair);
            }

            if (pairs.Count == 0) return null;

            // Mesma ordem de preferência do batch: distância igual a nominal
            // primeiro, depois cluster compartilhado, depois maior overlap.
            pairs.Sort((p, q) =>
            {
                double pScore = DiameterMatchScoreMm(p.EdgeDistanceMm);
                double qScore = DiameterMatchScoreMm(q.EdgeDistanceMm);
                int matchCmp = pScore.CompareTo(qScore);
                if (matchCmp != 0) return matchCmp;

                int sameCmp = (q.SameCluster ? 1 : 0) - (p.SameCluster ? 1 : 0);
                if (sameCmp != 0) return sameCmp;

                return q.OverlapMm.CompareTo(p.OverlapMm);
            });

            PairCandidate best = pairs[0];
            return new BifilarCenterline(best.Start, best.End, best.EdgeDistanceMm);
        }

        // Procura o candidate que MELHOR REPRESENTA a linha picada. Não usa
        // igualdade de endpoints porque o coalesce pode ter unido a linha
        // picada com fragmentos colineares vizinhos. Critério: angulação
        // parecida + midpoint do anchor cai dentro do segmento candidate
        // (t ∈ [-0.05, 1.05]) + distância perpendicular muito pequena.
        private int FindCandidateForAnchor(XYZ anchorStart, XYZ anchorEnd)
        {
            if (_candidates.Count == 0) return -1;

            double dx = anchorEnd.X - anchorStart.X;
            double dy = anchorEnd.Y - anchorStart.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-12) return -1;

            double? anchorAngle = AngleDeg(anchorStart, anchorEnd);
            if (anchorAngle == null) return -1;

            XYZ midpoint = new(
                (anchorStart.X + anchorEnd.X) / 2.0,
                (anchorStart.Y + anchorEnd.Y) / 2.0,
                (anchorStart.Z + anchorEnd.Z) / 2.0);

            double angleTol = Math.Max(_params.AngleToleranceDeg, 2.0);
            double perpTolMm = Math.Max(_params.ClusterSnapMm, 25.0);

            int bestIdx = -1;
            double bestPerpMm = double.MaxValue;
            for (int i = 0; i < _candidates.Count; i++)
            {
                Candidate c = _candidates[i];
                if (AngleDiffDeg(c.AngleDeg, anchorAngle.Value) > angleTol)
                    continue;

                (double dMm, double t) = PointToSegmentDistanceAndT(midpoint, c.P0, c.P1);
                if (t < -0.05 || t > 1.05) continue;
                if (dMm > perpTolMm) continue;
                if (dMm < bestPerpMm)
                {
                    bestPerpMm = dMm;
                    bestIdx = i;
                }
            }
            return bestIdx;
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
            List<XYZ> polyline = new(divisions + 1);
            for (int i = 0; i <= divisions; i++)
            {
                double t = (double)i / divisions;
                XYZ p;
                try { p = curve.Evaluate(t, true); }
                catch { continue; }

                polyline.Add(p);
                _symbolPoints.Add(new SymbolPoint(p, isHard));
            }

            if (polyline.Count < 2) return;

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            foreach (XYZ p in polyline)
            {
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
            _curveSamplings.Add(new CurveSampling(polyline, minX, minY, maxX, maxY));
        }

        // -------------------------------------------------------------
        // Coalescência de candidates colineares
        // -------------------------------------------------------------

        private void CoalesceCollinearCandidates()
        {
            if (_candidates.Count < 2) return;

            // Buckets: linhas no mesmo "trilho infinito". Tolerância angular
            // 1°; distância perpendicular à origem com tolerância 3mm. Linhas
            // dentro do mesmo bucket são considerados a mesma reta infinita.
            const double angleTolDeg = 1.0;
            double distTolFt = 3.0 / MmPerFoot;
            // Gap entre dois fragmentos a unir: pequeno (5mm). Linhas com
            // gap maior representam trechos distintos e devem permanecer
            // separadas para o pareamento.
            double gapTolFt = 5.0 / MmPerFoot;

            Dictionary<(int, int), List<int>> buckets = new();
            for (int i = 0; i < _candidates.Count; i++)
            {
                var key = LineBucket(_candidates[i], angleTolDeg, distTolFt);
                if (!buckets.TryGetValue(key, out var bucket))
                {
                    bucket = new List<int>();
                    buckets[key] = bucket;
                }
                bucket.Add(i);
            }

            List<Candidate> merged = new(_candidates.Count);
            foreach (var pair in buckets)
            {
                List<int> indices = pair.Value;
                if (indices.Count == 1)
                {
                    merged.Add(_candidates[indices[0]]);
                    continue;
                }

                Candidate first = _candidates[indices[0]];
                double dx = first.P1.X - first.P0.X;
                double dy = first.P1.Y - first.P0.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-12)
                {
                    foreach (int idx in indices) merged.Add(_candidates[idx]);
                    continue;
                }
                double ux = dx / len;
                double uy = dy / len;
                double nx = -uy;
                double ny = ux;
                double dPerp = first.P0.X * nx + first.P0.Y * ny;
                double z = first.P0.Z;

                // Projeta cada candidato no eixo da reta e ordena por início.
                List<(double t0, double t1)> ranges = new(indices.Count);
                foreach (int idx in indices)
                {
                    Candidate c = _candidates[idx];
                    double t0 = c.P0.X * ux + c.P0.Y * uy;
                    double t1 = c.P1.X * ux + c.P1.Y * uy;
                    if (t0 > t1) (t0, t1) = (t1, t0);
                    ranges.Add((t0, t1));
                }
                ranges.Sort((a, b) => a.t0.CompareTo(b.t0));

                double curT0 = ranges[0].t0;
                double curT1 = ranges[0].t1;
                List<(double t0, double t1)> unioned = new();
                for (int i = 1; i < ranges.Count; i++)
                {
                    if (ranges[i].t0 <= curT1 + gapTolFt)
                    {
                        if (ranges[i].t1 > curT1) curT1 = ranges[i].t1;
                    }
                    else
                    {
                        unioned.Add((curT0, curT1));
                        curT0 = ranges[i].t0;
                        curT1 = ranges[i].t1;
                    }
                }
                unioned.Add((curT0, curT1));

                foreach (var (t0, t1) in unioned)
                {
                    XYZ q0 = new(t0 * ux + dPerp * nx, t0 * uy + dPerp * ny, z);
                    XYZ q1 = new(t1 * ux + dPerp * nx, t1 * uy + dPerp * ny, z);
                    double newLenFt = q0.DistanceTo(q1);
                    double newLenMm = newLenFt * MmPerFoot;
                    if (newLenMm < _params.MinCandidateLengthMm)
                    {
                        // Mesmo após unir, ainda curto demais — devolve aos
                        // símbolos para o filtro de centerline.
                        if (newLenMm >= 5.0)
                            _symbolPoints.Add(new SymbolPoint(
                                new XYZ((q0.X + q1.X) / 2, (q0.Y + q1.Y) / 2, (q0.Z + q1.Z) / 2),
                                isHard: false));
                        continue;
                    }

                    (XYZ a, XYZ b) = NormalizeSegmentDirection(q0, q1);
                    double? angle = AngleDeg(a, b);
                    if (angle == null) continue;

                    merged.Add(new Candidate
                    {
                        P0 = a,
                        P1 = b,
                        LengthMm = newLenMm,
                        AngleDeg = angle.Value,
                        ClusterId = -1,
                        Index = -1,
                    });
                }
            }

            _candidates.Clear();
            _candidates.AddRange(merged);
        }

        private static (int angleBucket, int distBucket) LineBucket(Candidate c, double angleTolDeg, double distTolFt)
        {
            double dx = c.P1.X - c.P0.X;
            double dy = c.P1.Y - c.P0.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-12) return (0, 0);

            double nx = -dy / len;
            double ny = dx / len;
            double signedDist = c.P0.X * nx + c.P0.Y * ny;

            return (
                (int)Math.Round(c.AngleDeg / angleTolDeg),
                (int)Math.Round(signedDist / distTolFt)
            );
        }

        // -------------------------------------------------------------
        // Rejeição de candidates que atravessam curvas
        // -------------------------------------------------------------

        private void RejectCandidatesCrossingCurves()
        {
            if (_curveSamplings.Count == 0 || _candidates.Count == 0) return;

            List<Candidate> kept = new(_candidates.Count);
            foreach (Candidate c in _candidates)
            {
                if (!CrossesAnyCurveTwiceOrMore(c.P0, c.P1))
                    kept.Add(c);
            }
            _candidates.Clear();
            _candidates.AddRange(kept);
        }

        private bool CrossesAnyCurveTwiceOrMore(XYZ p0, XYZ p1)
        {
            double lineMinX = Math.Min(p0.X, p1.X);
            double lineMaxX = Math.Max(p0.X, p1.X);
            double lineMinY = Math.Min(p0.Y, p1.Y);
            double lineMaxY = Math.Max(p0.Y, p1.Y);

            foreach (CurveSampling cs in _curveSamplings)
            {
                // BB rápida: se as bounding boxes não se cruzam, sem chance
                // de interseção real entre as primitivas.
                if (lineMaxX < cs.MinX || lineMinX > cs.MaxX) continue;
                if (lineMaxY < cs.MinY || lineMinY > cs.MaxY) continue;

                int crossings = 0;
                IReadOnlyList<XYZ> pts = cs.Points;
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    if (SegmentsIntersect2D(p0, p1, pts[i], pts[i + 1]))
                    {
                        crossings++;
                        if (crossings >= 2) return true;
                    }
                }
            }

            return false;
        }

        private static bool SegmentsIntersect2D(XYZ a1, XYZ a2, XYZ b1, XYZ b2)
        {
            double d1 = CrossSign(b1, b2, a1);
            double d2 = CrossSign(b1, b2, a2);
            double d3 = CrossSign(a1, a2, b1);
            double d4 = CrossSign(a1, a2, b2);

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;
            return false;
        }

        private static double CrossSign(XYZ a, XYZ b, XYZ c)
        {
            return (c.X - a.X) * (b.Y - a.Y) - (b.X - a.X) * (c.Y - a.Y);
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

            // Ordenação: PARES CUJA DISTÂNCIA ENTRE PAREDES BATE COM UM
            // DIÂMETRO NOMINAL DO TIPO ganham preferência. Isso evita que
            // um par "errado" (ex.: gap estreito entre dois tubos
            // diferentes, ou dois trechos de polilinha que casam por sorte)
            // venha antes do par CORRETO (que tem distância igual ao
            // diâmetro real do tubo) na hora de travar candidates via
            // LockEdgeAfterPair. Empates por mesmo cluster e maior overlap.
            pairs.Sort((p, q) =>
            {
                double pScore = DiameterMatchScoreMm(p.EdgeDistanceMm);
                double qScore = DiameterMatchScoreMm(q.EdgeDistanceMm);
                int matchCmp = pScore.CompareTo(qScore);
                if (matchCmp != 0) return matchCmp;

                int sameCmp = (q.SameCluster ? 1 : 0) - (p.SameCluster ? 1 : 0);
                if (sameCmp != 0) return sameCmp;

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

        /// <summary>
        /// Distância (em mm) entre <paramref name="edgeMm"/> e o diâmetro
        /// nominal mais próximo da lista do tipo de tubo. Quando a lista vem
        /// vazia (tipo sem routing preferences), todo par recebe score 0 e o
        /// desempate cai para os critérios secundários. Quando a lista existe,
        /// pares cujo edge bate com um nominal vencem qualquer par "errado"
        /// (gap entre tubos paralelos, etc.) — essencial para que LockEdgeAfterPair
        /// não trave o candidate correto antes do par certo aparecer.
        /// </summary>
        private double DiameterMatchScoreMm(double edgeMm)
        {
            IReadOnlyList<double> diams = _params.AvailableDiametersMm;
            if (diams.Count == 0) return 0.0;

            double best = double.MaxValue;
            foreach (double d in diams)
            {
                double diff = Math.Abs(d - edgeMm);
                if (diff < best) best = diff;
            }
            return best;
        }

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

        private sealed class CurveSampling
        {
            public CurveSampling(IReadOnlyList<XYZ> points, double minX, double minY, double maxX, double maxY)
            {
                Points = points;
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }

            public IReadOnlyList<XYZ> Points { get; }
            public double MinX { get; }
            public double MinY { get; }
            public double MaxX { get; }
            public double MaxY { get; }
        }
    }
}
