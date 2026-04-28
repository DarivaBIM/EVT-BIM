using System;
using Autodesk.Revit.UI;
using FamiliesImporterHub.UI;

namespace FamiliesImporterHub.Infrastructure
{
    /// <summary>
    /// Wrapper do <c>ExternalEvent</c> que aciona o
    /// <see cref="PipeInsertionHandler"/>. Expõe um callback
    /// <c>RearmRequested</c> para que o handler se re-agende ao fim de cada
    /// pick (mantendo o ciclo de seleção vivo enquanto a ferramenta está ativa).
    /// </summary>
    public class PipeInsertionExternalEvent
    {
        private readonly PipeInsertionHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public PipeInsertionExternalEvent()
        {
            _handler = new PipeInsertionHandler();
            _externalEvent = ExternalEvent.Create(_handler);
            _handler.RearmRequested = () => _externalEvent.Raise();
        }

        public void Raise(PipeConverterViewModel viewModel)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            _handler.ViewModel = viewModel;
            _externalEvent.Raise();
        }

        /// <summary>
        /// Reagenda manualmente o pick. Usado pela UI quando o usuário altera
        /// parâmetros no WPF e queremos forçar um pick "fresco" com os novos
        /// valores (em conjunto com o ESC enviado para cancelar o pick atual).
        /// </summary>
        public void RaiseIfActive(PipeConverterViewModel viewModel)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            if (!viewModel.IsActive)
                return;

            _handler.ViewModel = viewModel;
            _externalEvent.Raise();
        }
    }
}
