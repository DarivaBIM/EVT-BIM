using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Markers
{
    /// <summary>
    /// Parâmetros usados ao criar marcadores (placeholders de tubo
    /// taggeados como DBIM_PIPE_MARKER): sistema, tipo, nível host,
    /// elevação alvo e flag que indica se o Z deve vir do CAD ou do
    /// nível+offset.
    ///
    /// <see cref="DefaultDiameterMm"/> é o diâmetro nominal usado para
    /// modo unifilar (onde a geometria não nos diz o diâmetro). No modo
    /// bifilar, o diâmetro é estimado por par de linhas e ajustado para
    /// um valor disponível no tipo — o "default" aqui ainda assim é
    /// usado como fallback se nenhum diâmetro casar.
    /// </summary>
    public sealed class PipeMarkerConfig
    {
        public PipeMarkerConfig(
            ElementId systemTypeId,
            ElementId pipeTypeId,
            ElementId levelId,
            double defaultDiameterMm,
            double levelElevationFeet,
            double offsetMm,
            bool useCadElevation)
        {
            SystemTypeId = systemTypeId;
            PipeTypeId = pipeTypeId;
            LevelId = levelId;
            DefaultDiameterMm = defaultDiameterMm;
            LevelElevationFeet = levelElevationFeet;
            OffsetMm = offsetMm;
            UseCadElevation = useCadElevation;
        }

        public ElementId SystemTypeId { get; }
        public ElementId PipeTypeId { get; }
        public ElementId LevelId { get; }
        public double DefaultDiameterMm { get; }
        public double LevelElevationFeet { get; }
        public double OffsetMm { get; }
        public bool UseCadElevation { get; }
    }
}
