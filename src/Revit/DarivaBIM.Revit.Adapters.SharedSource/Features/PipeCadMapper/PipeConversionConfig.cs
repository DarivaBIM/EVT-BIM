using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper
{
    /// <summary>
    /// Parâmetros usados pelo <see cref="PipeCreator"/> ao converter uma
    /// linha de vínculo CAD em tubos: sistema, tipo, nível, diâmetro nominal
    /// (mm), elevação do nível (já em feet, evita reconverter) e offset (mm)
    /// acima do nível.
    /// </summary>
    public class PipeConversionConfig
    {
        public PipeConversionConfig(
            ElementId systemTypeId,
            ElementId pipeTypeId,
            ElementId levelId,
            double diameterMm,
            double levelElevationFeet,
            double offsetMm)
        {
            SystemTypeId = systemTypeId;
            PipeTypeId = pipeTypeId;
            LevelId = levelId;
            DiameterMm = diameterMm;
            LevelElevationFeet = levelElevationFeet;
            OffsetMm = offsetMm;
        }

        public ElementId SystemTypeId { get; }
        public ElementId PipeTypeId { get; }
        public ElementId LevelId { get; }
        public double DiameterMm { get; }
        public double LevelElevationFeet { get; }
        public double OffsetMm { get; }
    }
}
