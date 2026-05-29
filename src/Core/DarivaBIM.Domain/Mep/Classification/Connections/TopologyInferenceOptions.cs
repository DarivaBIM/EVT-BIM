namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Tolerancias angulares e dimensionais do motor de inferencia topologica.
    /// Defaults vindos das secoes 9 e 19 do rulebook canonico. Modelado como
    /// record init-only para permitir override em teste/calibracao sem mutar o
    /// motor. Vide <see cref="TopologyInferenceEngine"/>.
    /// </summary>
    public sealed record TopologyInferenceOptions
    {
        /// <summary>
        /// Angulo minimo (graus) entre OutwardNormals para considerar um par
        /// anti-paralelo (eixo passante). Peca reta ~180 graus.
        /// </summary>
        public double InlineMinDeg { get; init; } = 175.0;

        /// <summary>Faixa de angulo lateral que classifica um ramal como Tee (~90).</summary>
        public double LateralTeeMinDeg { get; init; } = 85.0;

        /// <summary>Faixa de angulo lateral que classifica um ramal como Tee (~90).</summary>
        public double LateralTeeMaxDeg { get; init; } = 95.0;

        /// <summary>Faixa de angulo lateral que classifica um ramal como Wye (~45).</summary>
        public double LateralWyeMinDeg { get; init; } = 40.0;

        /// <summary>Faixa de angulo lateral que classifica um ramal como Wye (~45).</summary>
        public double LateralWyeMaxDeg { get; init; } = 50.0;

        /// <summary>
        /// Passo de arredondamento (graus) do angulo nominal. NAO consumido pelo
        /// motor 1.B-1 (ConnectionTopology nao carrega NominalAngleDeg; a matriz
        /// de angulos e raw); reservado para a derivacao de NominalAngleDeg no
        /// Classify da fase 2.B a partir da matriz raw.
        /// </summary>
        public double AngleSnapDeg { get; init; } = 5.0;

        /// <summary>
        /// Tolerancia (mm) para considerar dois DN iguais (Union vs Reducer,
        /// runs de um Tee vs ramal reduzido).
        /// </summary>
        public int DnEqualToleranceMm { get; init; } = 2;
    }
}
