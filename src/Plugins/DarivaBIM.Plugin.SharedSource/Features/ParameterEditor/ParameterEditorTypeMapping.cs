using Autodesk.Revit.DB;
using DarivaBIM.Presentation.Wpf.ParameterEditor;
#if REVIT2026
using DarivaBIM.Revit.Adapters.V2026.Features.ParameterEditor;
#elif REVIT2025
using DarivaBIM.Revit.Adapters.V2025.Features.ParameterEditor;
#endif

namespace DarivaBIM.Plugin.Features.ParameterEditor
{
    /// <summary>
    /// Boundary mappings between RevitAPI/adapter enums and the neutral
    /// ViewModel types declared in <c>Presentation.Wpf.ParameterEditor</c>.
    /// Lives here (not in Presentation.Wpf) because Presentation.Wpf must
    /// stay free of any reference to <c>Autodesk.Revit.*</c> per ADR-0010.
    /// </summary>
    internal static class ParameterEditorTypeMapping
    {
        public static ParameterValueKind ToValueKind(StorageType storageType)
        {
            return storageType switch
            {
                StorageType.String => ParameterValueKind.Text,
                StorageType.Integer => ParameterValueKind.Integer,
                StorageType.Double => ParameterValueKind.Decimal,
                StorageType.ElementId => ParameterValueKind.ElementReference,
                _ => ParameterValueKind.Unknown,
            };
        }

        public static ParameterDiscipline ToNeutral(Discipline discipline)
        {
            return discipline switch
            {
                Discipline.Hidraulica => ParameterDiscipline.Hidraulica,
                Discipline.Eletrica => ParameterDiscipline.Eletrica,
                Discipline.Mecanica => ParameterDiscipline.Mecanica,
                Discipline.CombateIncendio => ParameterDiscipline.CombateIncendio,
                Discipline.Estrutura => ParameterDiscipline.Estrutura,
                Discipline.Arquitetura => ParameterDiscipline.Arquitetura,
                Discipline.ModelosGenericos => ParameterDiscipline.ModelosGenericos,
                _ => ParameterDiscipline.ModelosGenericos,
            };
        }

        public static Discipline ToAdapter(ParameterDiscipline discipline)
        {
            return discipline switch
            {
                ParameterDiscipline.Hidraulica => Discipline.Hidraulica,
                ParameterDiscipline.Eletrica => Discipline.Eletrica,
                ParameterDiscipline.Mecanica => Discipline.Mecanica,
                ParameterDiscipline.CombateIncendio => Discipline.CombateIncendio,
                ParameterDiscipline.Estrutura => Discipline.Estrutura,
                ParameterDiscipline.Arquitetura => Discipline.Arquitetura,
                ParameterDiscipline.ModelosGenericos => Discipline.ModelosGenericos,
                _ => Discipline.ModelosGenericos,
            };
        }
    }
}
