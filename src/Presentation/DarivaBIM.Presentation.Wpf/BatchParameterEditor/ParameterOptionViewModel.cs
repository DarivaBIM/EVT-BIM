namespace DarivaBIM.Presentation.Wpf.BatchParameterEditor
{
    /// <summary>
    /// View-model representation of a parameter that is editable on the
    /// current Revit selection. Holds the neutral value kind so the
    /// validation logic in <see cref="BatchParameterEditorViewModel"/> stays
    /// independent of <c>Autodesk.Revit.DB.StorageType</c>.
    /// </summary>
    public class ParameterOptionViewModel
    {
        public ParameterOptionViewModel(string name, ParameterValueKind valueKind, bool isInstance)
        {
            Name = name;
            ValueKind = valueKind;
            IsInstance = isInstance;
        }

        public string Name { get; }
        public ParameterValueKind ValueKind { get; }
        public bool IsInstance { get; }

        public string DisplayName => IsInstance
            ? Name
            : $"{Name} (tipo)";

        public override string ToString() => DisplayName;
    }
}
