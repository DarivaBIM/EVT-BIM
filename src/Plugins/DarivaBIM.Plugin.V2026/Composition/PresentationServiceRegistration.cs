using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.V2026.Composition
{
    /// <summary>
    /// Reserved hook for Presentation.Wpf bindings (view models with
    /// dependencies, dialog services, etc.). Today the WPF view models are
    /// instantiated directly by the windows because they have no
    /// dependencies, so this method is intentionally a no-op.
    /// </summary>
    internal static class PresentationServiceRegistration
    {
        public static IServiceCollection AddDarivaPresentation(this IServiceCollection services)
        {
            return services;
        }
    }
}
