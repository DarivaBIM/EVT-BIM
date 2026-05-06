using DarivaBIM.Plugin.Features.BatchParameterEditor;
using DarivaBIM.Plugin.Features.FamiliesImporter;
using DarivaBIM.Plugin.Features.FloorDrainExtension;
using DarivaBIM.Plugin.Features.PipeCadMapper;
using DarivaBIM.Plugin.Features.PipeCodes;
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
            PipeCodesFeature.AddServices(services);
            PipeCadMapperFeature.AddServices(services);
            FloorDrainExtensionFeature.AddServices(services);
            BatchParameterEditorFeature.AddServices(services);
            FamiliesImporterFeature.AddServices(services);

            return services;
        }
    }
}
