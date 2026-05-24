namespace DarivaBIM.Application.DTOs.Quantifica
{
    /// <summary>
    /// Cabeçalho do relatório "Tigre Quantifica" — lido de
    /// <c>ProjectInformation</c> do Revit pelo <c>ProjectInfoReader</c>. Os
    /// valores chegam aqui como string já formatada; campos não preenchidos
    /// pelo usuário viram o literal "(não preenchido)" (NUNCA fallback pra
    /// <c>Environment.UserName</c> ou <c>DateTime.Now</c> — isso mascararia o
    /// gap pro reviewer).
    /// </summary>
    public sealed class ProjectInfoDto
    {
        /// <summary>Nome do empreendimento (ProjectInformation.Name).</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Cliente (ProjectInformation.ClientName).</summary>
        public string Client { get; init; } = string.Empty;

        /// <summary>Autor do projeto (ProjectInformation.Author).</summary>
        public string Author { get; init; } = string.Empty;

        /// <summary>Data de emissão (ProjectInformation.IssueDate).</summary>
        public string IssueDate { get; init; } = string.Empty;

        /// <summary>Versão do relatório, hard-coded por enquanto.</summary>
        public string Version { get; init; } = "1.0";
    }
}
