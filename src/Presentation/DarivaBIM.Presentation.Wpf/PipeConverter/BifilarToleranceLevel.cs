namespace DarivaBIM.Presentation.Wpf.PipeConverter
{
    /// <summary>
    /// Cinco níveis discretos de tolerância para o detector bifilar.
    /// Cada nível mapeia para um percentual fixo (0/25/50/75/100) que
    /// alimenta <c>BifilarDetectionParameters.FromTolerance</c>. A UI
    /// expõe estes níveis em um ComboBox em vez de um slider contínuo,
    /// porque na prática os usuários trabalham bem com poucos
    /// "presets" e a curva é não-linear demais para um valor exato
    /// ter significado claro.
    /// </summary>
    public enum BifilarToleranceLevel
    {
        VeryLow = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        VeryHigh = 4,
    }
}
