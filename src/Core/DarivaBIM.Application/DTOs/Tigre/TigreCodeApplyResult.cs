using System.Collections.Generic;

namespace DarivaBIM.Application.DTOs.Tigre
{
    public sealed class TigreCodeApplyResult
    {
        public int CatalogCount { get; set; }
        public int PipesTotal { get; set; }
        public int PipesUpdated { get; set; }
        public int PipesAlreadyOk { get; set; }
        public int PipesOverwritten { get; set; }
        public int PipesNoMatch { get; set; }
        public int PipesParameterIssue { get; set; }
        public string ParameterAction { get; set; } = string.Empty;
        public List<string> Warnings { get; } = new List<string>();
        public List<UnmatchedPipe> Unmatched { get; } = new List<UnmatchedPipe>();
    }

    public sealed class UnmatchedPipe
    {
        public long ElementId { get; set; }
        public int? DiameterMm { get; set; }
        public string? Description { get; set; }
        public string? Segment { get; set; }
        public string? TypeName { get; set; }
    }
}
