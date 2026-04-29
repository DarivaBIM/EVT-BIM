using Autodesk.Revit.DB;
using DarivaBIM.Revit.Adapters.V2026.Filters;
using DarivaBIM.Revit.Adapters.V2026.Mapping;
using DarivaBIM.Revit.Adapters.V2026.Parameters;
using DarivaBIM.Revit.Adapters.V2026.Transactions;
using DarivaBIM.Revit.Adapters.V2026.Writers;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Application.DTOs.Family;
using DarivaBIM.Application.DTOs.Tigre;
using DarivaBIM.Application.Contracts;

namespace DarivaBIM.Revit.Adapters.V2026.Mapping
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
