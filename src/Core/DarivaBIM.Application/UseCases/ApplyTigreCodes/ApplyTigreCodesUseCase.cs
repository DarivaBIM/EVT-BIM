using System;
using DarivaBIM.Application.Contracts;
using DarivaBIM.Application.DTOs.Tigre;

namespace DarivaBIM.Application.UseCases.ApplyTigreCodes
{
    /// <summary>
    /// Orchestrates the Tigre code application flow.
    /// Receives the Revit-side service via constructor injection — it never
    /// knows about Revit types.
    /// </summary>
    public sealed class ApplyTigreCodesUseCase
    {
        private readonly ITigreCodeApplyService _service;

        public ApplyTigreCodesUseCase(ITigreCodeApplyService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public TigreCodeApplyResult Execute()
        {
            return _service.Apply();
        }

        public static string FormatReport(TigreCodeApplyResult r)
        {
            string warnings = r.Warnings.Count > 0
                ? "\n\nAvisos:\n - " + string.Join("\n - ", r.Warnings)
                : string.Empty;

            string unmatchedPreview = string.Empty;
            if (r.Unmatched.Count > 0)
            {
                int show = Math.Min(r.Unmatched.Count, 5);
                var lines = new System.Collections.Generic.List<string>();
                for (int i = 0; i < show; i++)
                {
                    UnmatchedPipe u = r.Unmatched[i];
                    lines.Add($" - ID {u.ElementId} | Ø{u.DiameterMm}mm | {u.Description} / {u.Segment} / {u.TypeName}");
                }
                if (r.Unmatched.Count > show)
                    lines.Add($" - … e mais {r.Unmatched.Count - show} tubo(s).");

                unmatchedPreview = "\n\nTubos sem correspondência:\n" + string.Join("\n", lines);
            }

            return
                $"Catálogo: {r.CatalogCount} itens\n" +
                $"Tubos: {r.PipesTotal}\n" +
                $"Atualizados (regravados): {r.PipesUpdated}\n" +
                $"Já estavam corretos: {r.PipesAlreadyOk}\n" +
                $"Sobrescritos: {r.PipesOverwritten}\n" +
                $"Sem correspondência: {r.PipesNoMatch}\n" +
                $"Sem parâmetro acessível: {r.PipesParameterIssue}\n\n" +
                $"Parâmetro: {r.ParameterAction}" +
                warnings +
                unmatchedPreview;
        }
    }
}
