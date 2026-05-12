namespace DarivaBIM.Presentation.Wpf.UtilizationPoints
{
    /// <summary>
    /// Estado visual da regra na lista do grupo ativo. A janela usa esse enum
    /// para escolher o badge (verde/amarelo) de cada linha.
    /// </summary>
    public enum UtilizationPointRuleStatus
    {
        Ok = 0,
        FamilyTypeMissing = 1,
        FamilyTypeNotFoundInDocument = 2,
        HeightRangeInvalid = 3,
    }
}
