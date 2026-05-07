using DarivaBIM.Plugin.Composition;
using DarivaBIM.Plugin.V2026.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.V2026
{
    /// <summary>
    /// Entry point referenciado pelo manifest <c>EVT-BIM.V2026.addin</c> em
    /// <c>FullClassName</c>. Toda a lógica de bootstrap (DI, ribbon, pane,
    /// ViewActivated) vive em <see cref="DarivaBimAppBase"/>; aqui cabe só
    /// o registro do adapter da Revit API 2026.
    /// </summary>
    public class App : DarivaBimAppBase
    {
        protected override IServiceCollection AddRevitAdapters(IServiceCollection services)
            => services.AddRevitAdaptersV2026();
    }
}
