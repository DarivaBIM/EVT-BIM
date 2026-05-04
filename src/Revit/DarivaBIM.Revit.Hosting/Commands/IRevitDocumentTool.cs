using Autodesk.Revit.DB;
using DarivaBIM.Application.Tools;

namespace DarivaBIM.Revit.Hosting.Commands
{
    /// <summary>
    /// Tool contract consumed by <see cref="RevitCommandBase{TTool}"/>:
    /// receives the active <see cref="Document"/>, runs the underlying
    /// UseCase and returns a uniform <see cref="ToolResult"/>. The Plugin
    /// command shell is responsible for translating the result into a Revit
    /// <c>Result</c> and for presenting the message to the user.
    /// </summary>
    public interface IRevitDocumentTool
    {
        ToolResult Execute(Document document);
    }
}
