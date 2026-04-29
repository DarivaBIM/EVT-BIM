namespace DarivaBIM.Presentation.Wpf.Models
{
    /// <summary>
    /// Neutral view-model option representing a Revit piping system. The Id is
    /// kept as a plain <see cref="long"/> so this assembly stays free from any
    /// reference to RevitAPI types (ElementId is converted at the Plugin/Adapter
    /// boundary).
    /// </summary>
    public sealed class PipingSystemOptionViewModel
    {
        public PipingSystemOptionViewModel(long id, string name)
        {
            Id = id;
            Name = name;
        }

        public long Id { get; }
        public string Name { get; }

        public override string ToString() => Name;
    }
}
