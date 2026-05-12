using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.Features.UtilizationPoints
{
    /// <summary>
    /// Determina o nível de referência usado para calcular a altura relativa
    /// dos conectores hidráulicos livres, seguindo a tríade descrita no
    /// script Python de referência:
    /// <list type="number">
    ///   <item>nível escolhido pelo usuário na UI (quando informado);</item>
    ///   <item>nível do próprio elemento (LevelId, ou parâmetros usuais);</item>
    ///   <item>zero absoluto do projeto.</item>
    /// </list>
    /// </summary>
    internal static class RevitReferenceLevelResolver
    {
        private static readonly BuiltInParameter[] LevelParameterCandidates =
        {
            BuiltInParameter.RBS_START_LEVEL_PARAM,
            BuiltInParameter.FAMILY_LEVEL_PARAM,
            BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
        };

        public sealed class LevelResolution
        {
            public LevelResolution(double elevationFeet, Level? level)
            {
                ElevationFeet = elevationFeet;
                Level = level;
            }

            public double ElevationFeet { get; }
            public Level? Level { get; }
        }

        public static LevelResolution Resolve(Document doc, Element element, Level? userLevel)
        {
            if (userLevel != null)
            {
                try { return new LevelResolution(userLevel.Elevation, userLevel); }
                catch { /* cai no fallback */ }
            }

            Level? elementLevel = GetElementLevel(doc, element);
            if (elementLevel != null)
            {
                try { return new LevelResolution(elementLevel.Elevation, elementLevel); }
                catch { /* cai no zero */ }
            }

            return new LevelResolution(0.0, null);
        }

        public static Level? GetElementLevel(Document doc, Element element)
        {
            if (element == null || doc == null) return null;

            try
            {
                ElementId levelId = element.LevelId;
                if (levelId != null && levelId != ElementId.InvalidElementId)
                {
                    if (doc.GetElement(levelId) is Level lvl) return lvl;
                }
            }
            catch { /* segue */ }

            for (int i = 0; i < LevelParameterCandidates.Length; i++)
            {
                BuiltInParameter bip = LevelParameterCandidates[i];
                try
                {
                    Parameter? p = element.get_Parameter(bip);
                    if (p == null) continue;

                    ElementId id = p.AsElementId();
                    if (id == null || id == ElementId.InvalidElementId) continue;

                    if (doc.GetElement(id) is Level lvl) return lvl;
                }
                catch { /* segue para o próximo */ }
            }

            return null;
        }
    }
}
