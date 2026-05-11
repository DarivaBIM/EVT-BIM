using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.V2025.Composition
{
    /// <summary>
    /// Bindings for the V2025 Revit adapter (<c>Autodesk.Revit.*</c> dependent).
    /// Empty for now because the current adapters either need an active
    /// <c>Document</c> at construction time (so they are built per-command in
    /// each feature's Tool) or are still consumed via static helpers
    /// (e.g. <c>PipeMarkerCreator</c>/<c>PipeMarkerConverter</c> for the
    /// PipeCADMapper flow).
    ///
    /// As <c>IRevitTransactionRunner</c>, <c>IRevitParameterWriter</c>,
    /// <c>IRevitElementWriter</c> and <c>IRevitSelectionService</c> ship,
    /// they are registered here.
    /// </summary>
    internal static class RevitAdapterServiceRegistration
    {
        public static IServiceCollection AddRevitAdaptersV2025(this IServiceCollection services)
        {
            return services;
        }
    }
}
