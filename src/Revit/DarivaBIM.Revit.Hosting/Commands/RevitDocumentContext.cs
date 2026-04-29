using Autodesk.Revit.DB;
using DarivaBIM.Revit.Abstractions.Hosting;

namespace DarivaBIM.Revit.Hosting.Commands
{
    /// <summary>
    /// Concrete <see cref="IRevitDocumentContext"/> wrapping a Revit
    /// <see cref="Document"/>. Use <see cref="RevitDocument"/> only inside the
    /// hosting/adapter layer; never let it leak to Application/Domain.
    /// </summary>
    public sealed class RevitDocumentContext : IRevitDocumentContext
    {
        public RevitDocumentContext(Document doc)
        {
            RevitDocument = doc;
        }

        public Document RevitDocument { get; }

        public string Title => RevitDocument.Title ?? string.Empty;

        public bool IsFamilyDocument => RevitDocument.IsFamilyDocument;

        public bool IsReadOnly => RevitDocument.IsReadOnly;

        public string PathName => RevitDocument.PathName ?? string.Empty;
    }
}
