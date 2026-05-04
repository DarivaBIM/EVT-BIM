using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.V2025.Composition
{
    /// <summary>
    /// Bindings for the <c>DarivaBIM.Infrastructure.*</c> assemblies. Empty
    /// for now: API clients, telemetry sinks and licensing services are still
    /// instantiated directly by the consuming UI components. As they migrate
    /// behind interfaces, the registrations land here.
    /// </summary>
    internal static class InfrastructureServiceRegistration
    {
        public static IServiceCollection AddDarivaInfrastructure(this IServiceCollection services)
        {
            return services;
        }
    }
}
