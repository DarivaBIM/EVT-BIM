using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Features.PipeCadMapper.Markers
{
    /// <summary>
    /// Marcação que identifica um <see cref="Autodesk.Revit.DB.Plumbing.Pipe"/>
    /// placeholder como sendo um "marcador" criado pelo PipeCADMapper. Ela é
    /// armazenada em <see cref="BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS"/>
    /// (parâmetro de instância "Comentários") porque é editável em qualquer
    /// versão do Revit e sobrevive ao undo, à edição manual da linha e à
    /// troca de view.
    ///
    /// O conteúdo previamente existente do parâmetro é descartado durante a
    /// criação porque os marcadores nunca compartilham comentários com tubos
    /// reais do usuário — são elementos temporários.
    /// </summary>
    internal static class PipeMarkerTag
    {
        public const string Value = "DBIM_PIPE_MARKER";

        public static void Apply(Element element)
        {
            element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(Value);
        }

        public static bool IsTagged(Element element)
        {
            Parameter? p = element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (p == null)
                return false;

            string? value = p.AsString();
            return string.Equals(value, Value, System.StringComparison.Ordinal);
        }

        public static void Clear(Element element)
        {
            Parameter? p = element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (p != null && !p.IsReadOnly)
            {
                p.Set(string.Empty);
            }
        }
    }
}
