using System.Collections.Generic;

namespace DarivaBIM.Presentation.Wpf.Models
{
    /// <summary>
    /// Neutral view-model option representing a Revit pipe type, including the
    /// list of nominal diameters available for routing.
    /// </summary>
    public sealed class PipeTypeOptionViewModel
    {
        public PipeTypeOptionViewModel(long id, string name, IReadOnlyList<double> availableDiametersMm)
        {
            Id = id;
            Name = name;
            AvailableDiametersMm = availableDiametersMm;
        }

        public long Id { get; }
        public string Name { get; }
        public IReadOnlyList<double> AvailableDiametersMm { get; }

        public override string ToString() => Name;
    }
}
