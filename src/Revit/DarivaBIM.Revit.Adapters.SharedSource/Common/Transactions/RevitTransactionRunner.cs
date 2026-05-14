using System;
using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Common.Transactions
{
    /// <summary>
    /// Executa uma <see cref="Action"/> dentro de uma <see cref="Transaction"/>.
    /// Se o documento já estiver modificável (transação aberta pelo caller —
    /// caso comum dentro de outro fluxo), apenas executa a ação. Em caso de
    /// erro, faz <c>RollBack</c> e relança para o caller decidir.
    /// </summary>
    public static class RevitTransactionRunner
    {
        public static void Run(Document doc, string transactionName, Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            Run<object?>(doc, transactionName, () => { action(); return null; });
        }

        public static T Run<T>(Document doc, string transactionName, Func<T> action)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (action == null) throw new ArgumentNullException(nameof(action));

            if (doc.IsModifiable)
                return action();

            using Transaction tx = new Transaction(doc, transactionName);
            TransactionStatus status = tx.Start();
            if (status != TransactionStatus.Started)
                throw new InvalidOperationException($"Não foi possível abrir a transação '{transactionName}'.");

            try
            {
                T result = action();
                tx.Commit();
                return result;
            }
            catch
            {
                if (tx.HasStarted() && !tx.HasEnded())
                    tx.RollBack();
                throw;
            }
        }
    }
}
