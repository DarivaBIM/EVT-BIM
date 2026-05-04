using DarivaBIM.Plugin.V2025.Features.FamiliesImporter;
using DarivaBIM.Plugin.V2025.Features.ParameterEditor;
using DarivaBIM.Plugin.V2025.Features.PipeCadMapper;
using DarivaBIM.Plugin.V2025.Features.Prolongador;
using DarivaBIM.Plugin.V2025.Features.TigreCodes;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.V2025.Composition
{
    /// <summary>
    /// Aggregates the per-feature DI registrations into one extension method
    /// so <c>App.cs</c> only needs a single call. Each feature exposes its own
    /// <c>AddServices</c> entry point in <c>Features/&lt;Tool&gt;/&lt;Tool&gt;Feature.cs</c>.
    /// </summary>
    internal static class PluginFeatureServiceRegistration
    {
        public static IServiceCollection AddPluginFeaturesV2025(this IServiceCollection services)
        {
            TigreCodesFeature.AddServices(services);
            PipeCadMapperFeature.AddServices(services);
            ProlongadorFeature.AddServices(services);
            ParameterEditorFeature.AddServices(services);
            FamiliesImporterFeature.AddServices(services);

            return services;
        }
    }
}
