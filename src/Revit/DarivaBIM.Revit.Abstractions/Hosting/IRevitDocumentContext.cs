namespace DarivaBIM.Revit.Abstractions.Hosting
{
    /// <summary>
    /// Neutral handle to "the active Revit document" for a given command
    /// invocation. Implementations live in the per-version adapter and wrap
    /// <c>Autodesk.Revit.DB.Document</c>; consumers in Application/Domain only
    /// see this interface.
    /// </summary>
    public interface IRevitDocumentContext
    {
        string Title { get; }

        bool IsFamilyDocument { get; }

        bool IsReadOnly { get; }

        string PathName { get; }
    }
}
