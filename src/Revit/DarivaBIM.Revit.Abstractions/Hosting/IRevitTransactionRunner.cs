using System;

namespace DarivaBIM.Revit.Abstractions.Hosting
{
    /// <summary>
    /// Runs an action inside a Revit transaction. Adapters wrap
    /// <c>Autodesk.Revit.DB.Transaction</c> here.
    /// </summary>
    public interface IRevitTransactionRunner
    {
        void Run(string transactionName, Action action);

        T Run<T>(string transactionName, Func<T> action);
    }
}
