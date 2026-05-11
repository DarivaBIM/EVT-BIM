using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Markers
{
    /// <summary>
    /// Aplica/remove o "look" de marcador (magenta forte + espessura de
    /// linha 5) via overrides gráficos por elemento na view ativa. Não
    /// altera o tipo do tubo nem o sistema; é puramente visual e desfaz
    /// silenciosamente quando o elemento é removido ou convertido.
    /// </summary>
    internal static class PipeMarkerOverrides
    {
        // Magenta forte (255, 0, 255) destaca mesmo sobre paletas claras
        // ou em CAD bem poluído.
        private static readonly Color MarkerColor = new Color(255, 0, 255);

        // Espessura 5 dentro do range 1-16 do Revit é grossa o bastante
        // para sobressair sem ofuscar texto/cotagem.
        private const int MarkerLineWeight = 5;

        public static void Apply(View view, ElementId elementId)
        {
            OverrideGraphicSettings overrides = new();
            overrides.SetProjectionLineColor(MarkerColor);
            overrides.SetProjectionLineWeight(MarkerLineWeight);
            // Pinta também o "preenchimento" do tubo (em vistas que renderizam
            // o pipe como dois traços laterais) para reforçar o destaque.
            overrides.SetSurfaceForegroundPatternColor(MarkerColor);
            overrides.SetCutLineColor(MarkerColor);
            overrides.SetCutLineWeight(MarkerLineWeight);

            try
            {
                view.SetElementOverrides(elementId, overrides);
            }
            catch
            {
                // Algumas views (templates, ou views que não permitem
                // override) lançam exceção — ignorar mantém a criação
                // do marcador, apenas sem o destaque visual.
            }
        }

        public static void Clear(View view, ElementId elementId)
        {
            try
            {
                view.SetElementOverrides(elementId, new OverrideGraphicSettings());
            }
            catch
            {
                // Idem Apply: erro de override não bloqueia a conversão.
            }
        }
    }
}
