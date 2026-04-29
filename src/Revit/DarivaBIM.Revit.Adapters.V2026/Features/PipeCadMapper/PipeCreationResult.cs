namespace DarivaBIM.Revit.Adapters.V2026.Features.PipeCadMapper
{
    /// <summary>
    /// Resultado de uma operação de conversão de linha CAD em tubos via
    /// <see cref="PipeCreator"/>. Em caso de falha, <see cref="Success"/> é
    /// <c>false</c> e <see cref="ErrorMessage"/> traz a explicação para o
    /// usuário; em caso de sucesso, traz contagens auxiliares.
    /// </summary>
    public sealed class PipeCreationResult
    {
        private PipeCreationResult(
            bool success,
            int createdCount,
            int skippedCount,
            int arcsAsChordCount,
            string? errorMessage)
        {
            Success = success;
            CreatedCount = createdCount;
            SkippedCount = skippedCount;
            ArcsAsChordCount = arcsAsChordCount;
            ErrorMessage = errorMessage;
        }

        public bool Success { get; }
        public int CreatedCount { get; }
        public int SkippedCount { get; }
        public int ArcsAsChordCount { get; }
        public string? ErrorMessage { get; }

        public static PipeCreationResult Ok(int created, int skipped, int arcsAsChord = 0)
            => new(true, created, skipped, arcsAsChord, null);

        public static PipeCreationResult Failed(string message)
            => new(false, 0, 0, 0, message);
    }
}
