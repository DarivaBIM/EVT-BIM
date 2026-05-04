using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.V2025.Common.Units
{
    /// <summary>
    /// Atalhos para conversão de unidades nas grandezas mais usadas no
    /// plugin: comprimento em milímetros, metros e o sistema interno do
    /// Revit (<c>feet</c>). Centraliza chamadas a
    /// <see cref="UnitUtils.ConvertToInternalUnits"/>/<see cref="UnitUtils.ConvertFromInternalUnits"/>
    /// para reduzir variações entre features.
    /// </summary>
    public static class RevitUnitConverter
    {
        public static double MillimetersToFeet(double mm)
            => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);

        public static double FeetToMillimeters(double feet)
            => UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);

        public static double MetersToFeet(double meters)
            => UnitUtils.ConvertToInternalUnits(meters, UnitTypeId.Meters);

        public static double FeetToMeters(double feet)
            => UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Meters);
    }
}
