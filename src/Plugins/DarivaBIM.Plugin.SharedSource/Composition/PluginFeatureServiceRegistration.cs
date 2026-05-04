using DarivaBIM.Plugin.Features.FamiliesImporter;
using DarivaBIM.Plugin.Features.ParameterEditor;
using DarivaBIM.Plugin.Features.PipeCadMapper;
using DarivaBIM.Plugin.Features.Prolongador;
using DarivaBIM.Plugin.Features.TigreCodes;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.Composition
{
    /// <summary>
    /// Aggregates the per-feature DI registrations into one extension method
    /// so <c>App.cs</c> only needs a single call. Each feature exposes its own
    /// <c>AddServices</c> entry point in <c>Features/&lt;Tool&gt;/&lt;Tool&gt;Feature.cs</c>.
    /// </summary>
    internal static class PluginFeatureServiceRegistration
    {
        public static IServiceCollection AddPluginFeatures(this IServiceCollection services)
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
