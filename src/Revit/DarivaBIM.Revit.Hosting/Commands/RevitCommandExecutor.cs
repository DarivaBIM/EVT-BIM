using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DarivaBIM.Revit.Hosting.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DarivaBIM.Revit.Hosting.Commands
{
    /// <summary>
    /// Standard executor that wraps every <c>IExternalCommand.Execute</c> call.
    /// It builds a fresh DI scope, exposes a <see cref="RevitCommandContext"/>
    /// to the action, and converts thrown exceptions into a
    /// <see cref="Result.Failed"/> with an error message.
    /// </summary>
    public sealed class RevitCommandExecutor
    {
        private readonly PluginHost _host;

        /// <summary>
        /// Process-wide accessor used by version-neutral command shells
        /// (<see cref="RevitCommandBase{TTool}"/>) that cannot take a
        /// dependency on the per-version <c>App</c> static. Set during the
        /// constructor; never reassigned during a session.
        /// </summary>
        public static RevitCommandExecutor? Current { get; private set; }

        public RevitCommandExecutor(PluginHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            Current = this;
        }

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            Func<RevitCommandContext, Result> action)
        {
            try
            {
                using IServiceScope scope = _host.CreateScope();

                Document? doc = commandData.Application.ActiveUIDocument?.Document;
                RevitDocumentContext? docCtx = doc != null ? new RevitDocumentContext(doc) : null;

                RevitCommandContext ctx = new RevitCommandContext(
                    commandData,
                    scope.ServiceProvider,
                    docCtx);

                return action(ctx);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
