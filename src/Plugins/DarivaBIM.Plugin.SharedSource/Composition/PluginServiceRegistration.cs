using Autodesk.Revit.UI;
using DarivaBIM.Plugin.Ribbon;
using DarivaBIM.Revit.Abstractions.Ribbon;
using DarivaBIM.Revit.Hosting.Events;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.Composition
{
    /// <summary>
    /// Plugin-host bindings shared between Plugin.V2025 and Plugin.V2026
    /// (the only difference between them is which RevitAPI version they
    /// link to). Registers the things that only make sense at the plugin
    /// entry point — <c>UIControlledApplication</c> and the Ribbon command
    /// registry — and keeps the DI surface small and auditable as new tools
    /// land.
    /// </summary>
    internal static class PluginServiceRegistration
    {
        public static IServiceCollection AddPluginShared(
            this IServiceCollection services,
            UIControlledApplication application)
        {
            services.AddSingleton(new RevitApplicationContext(application));
            services.AddSingleton<ICommandRegistry, CommandRegistry>();
            return services;
        }
    }
}
