namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Traducao do Revit PartType (string raw, ja desacoplado em
    /// <see cref="ConnectionTopology.PartType"/>) para um <see cref="BaseKind"/>
    /// canonico. HINT FRACO por design (decisao D7): familias mal parametrizadas
    /// mentem o PartType, entao a GEOMETRIA prevalece sempre que diverge. O motor
    /// usa este hint so para emitir diagnostico (Undefined / MismatchInferred),
    /// nunca para sobrepor a inferencia geometrica. Tabela na secao 7 do rulebook.
    /// </summary>
    public static class PartTypeHints
    {
        /// <summary>
        /// Mapeia o PartType raw para um BaseKind sugerido, ou null quando o
        /// PartType e ausente/generico ("Undefined", "Other", vazio) ou
        /// desconhecido. Mapeamentos ambiguos da secao 7 (ex.: LateralTee pode ser
        /// Tee ou Wye; Transition pode ser Reducer ou Union) colapsam no caso mais
        /// comum porque e apenas um hint — a geometria desempata.
        /// </summary>
        public static BaseKind? ToBaseKindHint(string? partTypeRaw)
        {
            if (partTypeRaw is null)
            {
                return null;
            }

            // Trim e trata vazio/espacos como ausencia de hint. WHY 'is null' +
            // Length em vez de string.IsNullOrWhiteSpace: em netstandard2.0 esse
            // metodo nao carrega [NotNullWhen(false)], entao o compilador nao faz
            // o narrowing e dispara CS8602 no Trim() (e o R4 exige zero avisos).
            var trimmed = partTypeRaw.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }

            return trimmed switch
            {
                "Elbow" => BaseKind.Elbow,
                "Tee" => BaseKind.Tee,
                "LateralTee" => BaseKind.Tee,
                "TapPerpendicular" => BaseKind.Tee,
                "TapAdjustable" => BaseKind.Tee,
                "SpudPerpendicular" => BaseKind.Tee,
                "SpudAdjustable" => BaseKind.Tee,
                "Wye" => BaseKind.Wye,
                "Cross" => BaseKind.Cross,
                "LateralCross" => BaseKind.Cross,
                "Union" => BaseKind.Union,
                "PipeFlange" => BaseKind.Union,
                "Transition" => BaseKind.Reducer,
                "Offset" => BaseKind.Elbow,
                "MultiPort" => BaseKind.MultiPort,
                "Cap" => BaseKind.Cap,
                "ValveNormal" => BaseKind.Valve,
                "ValveBreaksInto" => BaseKind.Valve,
                "InlineSensor" => BaseKind.Valve,
                "Sensor" => BaseKind.Valve,
                _ => null,
            };
        }
    }
}
