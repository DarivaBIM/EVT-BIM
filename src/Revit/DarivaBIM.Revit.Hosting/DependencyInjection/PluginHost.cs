using System;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Revit.Hosting.DependencyInjection
{
    /// <summary>
    /// Process-wide DI host for the plugin. Built once in
    /// <c>IExternalApplication.OnStartup</c> and reused per command via a
    /// scope.
    /// </summary>
    public sealed class PluginHost : IDisposable
    {
        private readonly ServiceProvider _root;

        public PluginHost(IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            _root = services.BuildServiceProvider(validateScopes: false);
        }

        public IServiceProvider Root => _root;

        public IServiceScope CreateScope() => _root.CreateScope();

        public void Dispose() => _root.Dispose();
    }
}
