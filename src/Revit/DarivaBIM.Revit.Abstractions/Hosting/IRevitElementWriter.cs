namespace DarivaBIM.Revit.Abstractions.Hosting
{
    public interface IRevitElementWriter
    {
        bool DeleteElement(long elementId);

        bool RenameElement(long elementId, string newName);
    }
}
