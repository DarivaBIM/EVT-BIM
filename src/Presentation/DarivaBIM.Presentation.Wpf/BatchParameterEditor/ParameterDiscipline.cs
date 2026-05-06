namespace DarivaBIM.Presentation.Wpf.BatchParameterEditor
{
    /// <summary>
    /// Revit-agnostic mirror of the discipline enum used by the Parameter
    /// Editor view (originally lived in
    /// <c>DarivaBIM.Revit.Adapters.V20XX.Features.BatchParameterEditor.Discipline</c>).
    /// Mapping between the two enums happens in the plugin/adapter layer; the
    /// ViewModel only deals with this neutral definition.
    /// </summary>
    public enum ParameterDiscipline
    {
        Hidraulica,
        Eletrica,
        Mecanica,
        CombateIncendio,
        Estrutura,
        Arquitetura,
        ModelosGenericos,
    }
}
