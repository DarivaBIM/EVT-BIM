using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;
using DarivaBIM.Plugin.Ui;

namespace DarivaBIM.Plugin.Features.TigreQuantifica
{
    /// <summary>
    /// Abre <see cref="PipeCodesWindow"/> (janela "Codificar Tigre") com
    /// prefilter opcional de IDs, rodando dentro do contexto Revit API
    /// válido (ExternalEvent.Execute).
    ///
    /// <para>
    /// Por que precisa de ExternalEvent: <c>PipeCodesWindow</c> ctor cria
    /// 4 ExternalEvents via <c>ExternalEvent.Create(...)</c> — chamada que
    /// PRECISA estar dentro de execução API standard (IExternalCommand OU
    /// outro ExternalEvent.Execute). Click direto de WPF modeless da
    /// TigreQuantificaWindow chamando <c>ShowSingleton</c> ficava fora
    /// desse contexto, gerando:
    /// </para>
    /// <para>
    /// <c>"Attempting to create an ExternalEvent outside of a standard API execution"</c>
    /// </para>
    /// <para>
    /// Slice 4.3.A F1 "Corrigir agora" → este event → ShowSingleton dentro
    /// do Execute (contexto válido). Fix do crash detectado no smoke
    /// pós-Codex em 2026-05-27.
    /// </para>
    /// </summary>
    public sealed class OpenPipeCodesWindowExternalEvent
    {
        private readonly OpenPipeCodesWindowHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public OpenPipeCodesWindowExternalEvent()
        {
            _handler = new OpenPipeCodesWindowHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public void Raise(IReadOnlyCollection<long>? prefilterIds)
        {
            // Snapshot defensivo — chamador pode mutar a lista entre Raise
            // e Execute (Execute roda no idle do Revit, não inline).
            _handler.PendingPrefilterIds = prefilterIds?.ToArray();
            _externalEvent.Raise();
        }
    }

    internal sealed class OpenPipeCodesWindowHandler : IExternalEventHandler
    {
        public IReadOnlyList<long>? PendingPrefilterIds { get; set; }

        public string GetName() => "EvtBim.OpenPipeCodesWindowHandler";

        public void Execute(UIApplication app)
        {
            try
            {
                // ShowSingleton ja tem try/catch interno com TaskDialog
                // amigavel; aqui defensive contra qualquer exception
                // residual que poderia escapar e crashar o Idle do Revit.
                PipeCodesWindow.ShowSingleton(PendingPrefilterIds);
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "EVT-BIM — Codificar Tigre",
                    "Erro ao abrir janela Codificar Tigre.\n\n" + ex.Message);
            }
        }
    }
}
