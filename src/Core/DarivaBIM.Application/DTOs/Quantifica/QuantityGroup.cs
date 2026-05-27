namespace DarivaBIM.Application.DTOs.Quantifica
{
    /// <summary>
    /// Linha agregada do relatório "Tigre Quantifica" — uma combinação única
    /// de (Categoria, Família, Tipo, Diâmetro, Cód. Tigre, Descrição,
    /// Fabricante, Sistema). O Scanner agrupa todos os elementos do projeto
    /// que compartilham essas 8 chaves em UMA linha, somando
    /// <see cref="Quantity"/> de acordo com <see cref="MeasurementKind"/>:
    /// contagem, comprimento (m) ou área (m²).
    /// </summary>
    public sealed class QuantityGroup
    {
        /// <summary>Categoria do Revit por nome amigável (ex.: "Tubulações", "Conexões de tubulação").</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>Nome da family.</summary>
        public string Family { get; init; } = string.Empty;

        /// <summary>Nome do FamilyType.</summary>
        public string Type { get; init; } = string.Empty;

        /// <summary>
        /// Diâmetro nominal já formatado em mm (ex.: "25 mm"). <c>null</c>
        /// para categorias que não têm diâmetro (paredes, pisos, forros).
        /// </summary>
        public string? Diameter { get; init; }

        /// <summary>
        /// Valor lido do shared parameter <c>Tigre: Código</c> no elemento
        /// (ou no type, se a family Tigre o expõe lá). <c>null</c> quando o
        /// parâmetro não existe ou está vazio.
        /// </summary>
        public string? TigreCode { get; init; }

        /// <summary>Descrição genérica (cascade: instance → type → typeName).</summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// Conteúdo do shared parameter "Tigre: Descrição" lido das famílias
        /// do catálogo Tigre (instance → type fallback). <c>null</c> quando
        /// o parâmetro não existe ou está vazio — desambigua dois casos:
        /// "elemento não é Tigre / família não tem o param" vs "é Tigre mas
        /// modelador não preencheu". O audit Yellow do scanner consome esse
        /// segundo caso. Slice 4.3.A F6-LITE.
        /// </summary>
        public string? TigreDescription { get; init; }

        /// <summary>Fabricante (parâmetro Manufacturer), quando preenchido.</summary>
        public string? Manufacturer { get; init; }

        /// <summary>Sistema (RBS_SYSTEM_NAME_PARAM ou equivalente), quando aplicável.</summary>
        public string? System { get; init; }

        /// <summary>Como o quantitativo é mensurado — dita a unidade exibida.</summary>
        public MeasurementKind MeasurementKind { get; init; } = MeasurementKind.Count;

        /// <summary>
        /// Quantos elementos do projeto foram agregados nesta linha. Sempre
        /// igual ao número de instâncias contadas — vai na coluna "Qtd" do CSV.
        /// </summary>
        public int ElementCount { get; init; }

        /// <summary>
        /// Quantidade agregada. Unidade derivada de <see cref="MeasurementKind"/>
        /// (Count→un, LengthMeters→m, AreaSquareMeters→m²). Para
        /// <see cref="MeasurementKind.Count"/> coincide com <see cref="ElementCount"/>.
        /// </summary>
        public decimal Quantity { get; init; }

        /// <summary>
        /// <c>true</c> se a categoria deste grupo é <c>OST_PipeCurves</c>.
        /// Usado pelo banner "Codificar Tubos antes" da janela (slice 1.6 F1)
        /// e pelos testes de PipesNeedCoding. Não usar nome de categoria
        /// (string) — quebraria em Revit em outros idiomas.
        /// </summary>
        public bool IsPipeCurvesCategory { get; init; }

        /// <summary>
        /// Anotação de auditoria pré-formatada para a coluna "Auditoria" do
        /// CSV. <c>null</c> ou vazio quando não há observação. Mantém a regra
        /// "scanner sabe o domínio, CsvWriter só formata" — assim o writer
        /// (Application) não precisa importar mapas de <c>BuiltInCategory</c>
        /// que moram em Adapters.
        /// </summary>
        public string? AuditNote { get; init; }
    }
}
