using Autodesk.Revit.Attributes;
using DarivaBIM.Revit.Hosting.Commands;

namespace DarivaBIM.Plugin.Features.PipeCodes
{
    /// <summary>
    /// Thin <c>IExternalCommand</c> shell for the "Codificar Tubos" tool.
    /// All the wiring (DI scope, document validation, error handling and
    /// dialog) lives in <see cref="RevitCommandBase{TTool}"/>.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public sealed class ApplyPipeCodesCommand : RevitCommandBase<ApplyPipeCodesTool>
    {
        protected override string DialogTitle => "EVT-BIM — Códigos Tigre";

        protected override string NoProjectDocumentMessage =>
            "Abra um projeto Revit para aplicar os códigos Tigre.";
    }
}
