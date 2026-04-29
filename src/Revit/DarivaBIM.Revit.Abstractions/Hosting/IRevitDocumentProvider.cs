namespace DarivaBIM.Revit.Abstractions.Hosting
{
    public interface IRevitDocumentProvider
    {
        IRevitDocumentContext? Active { get; }
    }
}
