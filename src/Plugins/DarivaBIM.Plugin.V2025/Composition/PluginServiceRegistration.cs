using Autodesk.Revit.UI;
using DarivaBIM.Plugin.V2025.Ribbon;
using DarivaBIM.Revit.Abstractions.Ribbon;
using DarivaBIM.Revit.Hosting.Events;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.V2025.Composition
{
    /// <summary>
    /// Plugin-host bindings: things that only make sense for the V2025 entry
    /// point (UIControlledApplication, the Ribbon command registry, etc.).
    /// Lives next to <c>App.cs</c> so its DI surface stays small and
    /// auditable as new tools land.
    /// </summary>
    internal static class PluginServiceRegistration
    {
        public static IServiceCollection AddPluginV2025(
            this IServiceCollection services,
            UIControlledApplication application)
        {
            services.AddSingleton(new RevitApplicationContext(application));
            services.AddSingleton<ICommandRegistry, CommandRegistry>();
            return services;
        }
    }
}
