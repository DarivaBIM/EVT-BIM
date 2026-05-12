namespace DarivaBIM.Domain.Hydraulics.UtilizationPoints
{
    /// <summary>
    /// Resultado por conector processado pelo serviço de inserção. Os
    /// estados refletem os caminhos descritos no algoritmo Python de
    /// referência, sem qualquer dependência de Revit API.
    /// </summary>
    public enum UtilizationPointInsertionOutcome
    {
        InsertedAndConnected = 0,
        InsertedNotConnected = 1,
        NoMatchingRange = 2,
        FamilyMissing = 3,
        CreationError = 4,
        ElementWithoutFreeConnectors = 5,
    }
}
