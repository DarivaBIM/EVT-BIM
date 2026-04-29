using System;
using Autodesk.Revit.UI;
using DarivaBIM.Revit.Abstractions.Hosting;

namespace DarivaBIM.Revit.Hosting.Commands
{
    /// <summary>
    /// Concrete <see cref="IRevitCommandContext"/> built per command execution.
    /// </summary>
    public sealed class RevitCommandContext : IRevitCommandContext
    {
        public RevitCommandContext(
            ExternalCommandData commandData,
            IServiceProvider services,
            IRevitDocumentContext? document)
        {
            CommandData = commandData ?? throw new ArgumentNullException(nameof(commandData));
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Document = document;
        }

        public ExternalCommandData CommandData { get; }

        public IRevitDocumentContext? Document { get; }

        public IServiceProvider Services { get; }
    }
}
