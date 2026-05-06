using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Application.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Revit.Hosting.Commands
{
    /// <summary>
    /// Generic <c>IExternalCommand</c> shell that resolves <typeparamref name="TTool"/>
    /// from the DI scope, validates that a non-family Revit document is
    /// active, runs the tool and translates its <see cref="ToolResult"/> into
    /// the Revit return shape — message and dialog included. Concrete
    /// commands derive from this and inherit a working <c>Execute</c>:
    /// <code>
    /// [Transaction(TransactionMode.Manual)]
    /// public sealed class ApplyPipeCodesCommand
    ///     : RevitCommandBase&lt;ApplyPipeCodesTool&gt; { }
    /// </code>
    /// </summary>
    public abstract class RevitCommandBase<TTool> : IExternalCommand
        where TTool : class, IRevitDocumentTool
    {
        /// <summary>Title used by the default <see cref="TaskDialog"/>.</summary>
        protected virtual string DialogTitle => "EVT-BIM";

        /// <summary>
        /// Message shown when the command runs without an active project
        /// document (or with a family document open).
        /// </summary>
        protected virtual string NoProjectDocumentMessage =>
            "Abra um projeto Revit (.rvt) para usar esta ferramenta.";

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            RevitCommandExecutor? executor = RevitCommandExecutor.Current;
            if (executor == null)
            {
                message = "Plugin ainda não foi inicializado (Executor indisponível).";
                return Result.Failed;
            }

            string outerMessage = message;
            Result result = executor.Execute(commandData, ref outerMessage, ctx =>
            {
                Document? doc = (ctx.Document as RevitDocumentContext)?.RevitDocument;
                if (doc == null || doc.IsFamilyDocument)
                {
                    TaskDialog.Show(DialogTitle, NoProjectDocumentMessage);
                    return Result.Cancelled;
                }

                TTool tool = ctx.Services.GetRequiredService<TTool>();
                ToolResult toolResult = tool.Execute(doc);

                if (!string.IsNullOrEmpty(toolResult.Message))
                {
                    TaskDialog.Show(DialogTitle, toolResult.Message);
                }

                return toolResult.Success
                    ? Result.Succeeded
                    : (toolResult.Kind == ToolMessageKind.Error ? Result.Failed : Result.Cancelled);
            });

            message = outerMessage;
            return result;
        }
    }
}
