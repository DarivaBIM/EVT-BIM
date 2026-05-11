using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Bifilar
{
    /// <summary>
    /// Resultado do detector bifilar: um par de linhas paralelas do CAD
    /// foi identificado como um tubo e este record carrega o eixo central
    /// e o diâmetro nominal estimado (já em milímetros, antes do snap
    /// para a lista de diâmetros disponíveis).
    /// </summary>
    public sealed class BifilarCenterline
    {
        public BifilarCenterline(XYZ start, XYZ end, double measuredDiameterMm)
        {
            Start = start;
            End = end;
            MeasuredDiameterMm = measuredDiameterMm;
        }

        public XYZ Start { get; }
        public XYZ End { get; }
        public double MeasuredDiameterMm { get; }
    }
}
