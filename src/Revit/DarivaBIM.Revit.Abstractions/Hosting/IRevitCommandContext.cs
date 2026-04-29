using System;

namespace DarivaBIM.Revit.Abstractions.Hosting
{
    /// <summary>
    /// Per-invocation context for a Revit external command.
    /// Holds the active document handle and the DI scope's service provider.
    /// </summary>
    public interface IRevitCommandContext
    {
        IRevitDocumentContext? Document { get; }

        IServiceProvider Services { get; }
    }
}
