namespace DarivaBIM.Presentation.Wpf.Models
{
    /// <summary>
    /// Neutral view-model option representing a Revit level. Elevation is
    /// stored in feet (Revit's internal unit) so the Plugin/Adapter side does
    /// not need to round-trip the value.
    /// </summary>
    public sealed class LevelOptionViewModel
    {
        public LevelOptionViewModel(long id, string name, double elevationFeet)
        {
            Id = id;
            Name = name;
            ElevationFeet = elevationFeet;
        }

        public long Id { get; }
        public string Name { get; }
        public double ElevationFeet { get; }

        public override string ToString() => Name;
    }
}
