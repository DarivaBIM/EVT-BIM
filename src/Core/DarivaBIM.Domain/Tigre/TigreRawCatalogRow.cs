namespace DarivaBIM.Domain.Tigre
{
    /// <summary>
    /// Linha bruta do catálogo Tigre como deserializada do JSON. Campos
    /// originais (Description, DiameterMm, Code) permanecem obrigatórios pra
    /// compatibilidade com PipeCodes; campos novos do schema do Slice 2A
    /// (ProductLine, Kind, Dn1, Dn2) são opcionais — JSON antigo sem esses
    /// campos continua deserializando.
    /// </summary>
    public sealed class TigreRawCatalogRow
    {
        public string Description { get; set; } = string.Empty;
        public int DiameterMm { get; set; }
        public int Code { get; set; }

        /// <summary>
        /// Linha de produto Tigre: SR, SN, REDUX, Soldável, Registros,
        /// ClicPEX, AQUATHERM, TIGREFire ou PPR. Null em entradas legadas.
        /// </summary>
        public string? ProductLine { get; set; }

        /// <summary>
        /// Tipo do item: pipe, cap, elbow, tee, reducer, fitting, valve,
        /// accessory, fixture. Null em entradas legadas.
        /// </summary>
        public string? Kind { get; set; }

        /// <summary>
        /// Diâmetro nominal principal em mm. Igual a <see cref="DiameterMm"/>
        /// pra entradas com diâmetro único, e o "maior" em reduções.
        /// Null quando o item não tem DN (ex.: ralo linear medido em cm).
        /// </summary>
        public int? Dn1 { get; set; }

        /// <summary>
        /// Diâmetro nominal secundário em mm. Preenchido em reduções, tês
        /// de redução e bifurcações (sempre menor ou igual a Dn1). Null
        /// pra itens de DN único.
        /// </summary>
        public int? Dn2 { get; set; }
    }
}
