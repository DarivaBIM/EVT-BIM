using Autodesk.Revit.DB;

namespace FamiliesImporterHub.Infrastructure
{
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
