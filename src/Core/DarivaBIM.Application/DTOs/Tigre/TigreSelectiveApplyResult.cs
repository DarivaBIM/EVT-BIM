namespace DarivaBIM.Application.DTOs.Tigre
{
    /// <summary>
    /// Relatório da operação "Inserir/Atualizar Códigos" sobre os tubos
    /// selecionados pelo usuário no WPF. As contagens são sempre relativas
    /// à seleção, exceto <see cref="CatalogCount"/> e
    /// <see cref="PipesTotalInProject"/>, que dão contexto.
    /// </summary>
    public sealed class TigreSelectiveApplyResult
    {
        public int CatalogCount { get; init; }

        public int PipesTotalInProject { get; init; }

        /// <summary>Total de tubos marcados pelo usuário no WPF.</summary>
        public int Selected { get; init; }

        /// <summary>Tubos que tiveram o parâmetro recém-preenchido.</summary>
        public int Inserted { get; init; }

        /// <summary>Tubos cujo código pré-existente foi sobrescrito.</summary>
        public int Overwritten { get; init; }

        /// <summary>Tubos que já estavam com o código correto e ficaram intactos.</summary>
        public int AlreadyOk { get; init; }

        /// <summary>Tubos sem correspondência no catálogo (foram ignorados).</summary>
        public int NoMatch { get; init; }

        /// <summary>
        /// Tubos sem parâmetro acessível para escrita (ex.: o shared parameter
        /// não foi criado ainda, ou o tubo está em um link/ParameterIsReadOnly).
        /// </summary>
        public int ParameterIssue { get; init; }

        public string? ErrorMessage { get; init; }
    }
}
