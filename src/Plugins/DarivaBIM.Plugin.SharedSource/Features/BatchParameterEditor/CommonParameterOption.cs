using Autodesk.Revit.DB;

namespace DarivaBIM.Plugin.Features.BatchParameterEditor
{
    /// <summary>
    /// Boundary type carried between the RevitAPI-side parameter scan
    /// (<see cref="BatchParameterEditorService"/>) and the WPF window. Holds the
    /// adapter-facing <see cref="StorageType"/> so the apply path can pick the
    /// correct <c>Parameter.Set</c> overload; the ViewModel only sees the
    /// neutral <c>ParameterOptionViewModel</c>. Lives here (not in
    /// Presentation.Wpf) because Presentation.Wpf must stay free of
    /// <c>Autodesk.Revit.*</c> per ADR-0010.
    /// </summary>
    public class CommonParameterOption
    {
        public CommonParameterOption(string name, StorageType storageType, bool isInstance)
        {
            Name = name;
            StorageType = storageType;
            IsInstance = isInstance;
        }

        public string Name { get; }
        public StorageType StorageType { get; }
        public bool IsInstance { get; }

        public string DisplayName => IsInstance
            ? Name
            : $"{Name} (tipo)";

        public override string ToString() => DisplayName;
    }
}
