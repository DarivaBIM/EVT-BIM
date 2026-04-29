using System;
using DarivaBIM.Revit.Abstractions.Ribbon;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Plugin.V2026.Features.FamiliesImporter
{
    /// <summary>
    /// Manifest of the FamiliesImporterHub feature. The dockable pane owns its
    /// own <c>ExternalEvent</c>, so the DI surface stays empty for now.
    /// </summary>
    public static class FamiliesImporterFeature
    {
        public static RibbonCommandId CommandId => RibbonCommandId.ShowFamiliesPane;

        public static RibbonButtonDefinition Button => FamiliesImporterHubButton.Definition;

        public static Type CommandType => typeof(ShowFamiliesPaneCommand);

        public static IServiceCollection AddServices(IServiceCollection services)
        {
            return services;
        }
    }
}
