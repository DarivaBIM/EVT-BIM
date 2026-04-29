using System;

namespace DarivaBIM.Domain.Tigre
{
    /// <summary>
    /// Pure metadata describing the Tigre shared parameter. Domain-only constant
    /// definition; the Revit-side ensure/binding logic lives in the V2026 adapter.
    /// </summary>
    public static class TigreSharedParameterDefinition
    {
        public const string ParamName = "Tigre: Código";
        public const string SharedGroupName = "Tigre";
        public static readonly Guid ParamGuid = new Guid("71ba9de5-ea50-4906-bbf6-4e86df006f48");
    }
}
