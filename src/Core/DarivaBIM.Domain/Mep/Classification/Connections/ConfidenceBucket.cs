namespace DarivaBIM.Domain.Mep.Classification.Connections
{
    /// <summary>
    /// Bucket discreto usado pela UI para colorir/agrupar resultados do
    /// classifier. Derivado do score continuo via thresholds da secao 16 do
    /// rulebook canonico (High >= 0.75, Medium >= 0.45, else Low).
    /// </summary>
    public enum ConfidenceBucket
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }
}
