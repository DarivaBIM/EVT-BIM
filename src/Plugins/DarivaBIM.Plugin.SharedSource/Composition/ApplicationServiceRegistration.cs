using DarivaBIM.Application.Contracts;
using DarivaBIM.Infrastructure.Persistence.TigreCatalog;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.Composition
{
    /// <summary>
    /// Application/Domain bindings shared by Plugin.V2025 and Plugin.V2026:
    /// catalogue providers, use-case factories, and any other Revit-agnostic
    /// service that the Plugin can resolve eagerly. Concrete implementations
    /// that need a Revit <c>Document</c> are NOT registered here — they live
    /// in the per-command tools to avoid leaking the active document into
    /// the process-wide root provider.
    /// </summary>
    internal static class ApplicationServiceRegistration
    {
        public static IServiceCollection AddDarivaApplication(this IServiceCollection services)
        {
            services.AddSingleton<ITigreCatalogProvider>(_ => new TigreCatalogJsonLoader());
            return services;
        }
    }
}
