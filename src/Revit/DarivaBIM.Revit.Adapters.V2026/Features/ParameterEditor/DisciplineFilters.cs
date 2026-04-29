using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.V2026.Features.ParameterEditor
{
    /// <summary>
    /// Identificadores das disciplinas suportadas pelo filtro de categoria do
    /// editor de parâmetros. A ordem aqui define a ordem dos checkboxes na UI.
    /// </summary>
    public enum Discipline
    {
        Hidraulica,
        Eletrica,
        Mecanica,
        CombateIncendio,
        Estrutura,
        Arquitetura,
        ModelosGenericos,
    }

    /// <summary>
    /// Mapa de cada disciplina para o conjunto de <see cref="BuiltInCategory"/>
    /// que a representa. Exposto como <c>long</c> (valor de
    /// <c>BuiltInCategory</c>) para casar com <c>Category.Id.Value</c> em
    /// Revit 2024+.
    /// </summary>
    public static class DisciplineCategoryMap
    {
        private static readonly HashSet<BuiltInCategory> Plumbing = new()
        {
            // Tubos
            BuiltInCategory.OST_PipeCurves,
            BuiltInCategory.OST_FlexPipeCurves,
            BuiltInCategory.OST_PlaceHolderPipes,
            // Conexões e acessórios
            BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_PipeAccessory,
            // Aparelhos e equipamentos hidráulicos
            BuiltInCategory.OST_PlumbingFixtures,
            BuiltInCategory.OST_PlumbingEquipment,
            // Elementos auxiliares de tubulação
            BuiltInCategory.OST_PipeInsulations,
            BuiltInCategory.OST_PipingSystem,
            BuiltInCategory.OST_PipeSegments,
            // Peças MEP de fabricação
            BuiltInCategory.OST_FabricationPipework,
        };

        private static readonly HashSet<BuiltInCategory> Electrical = new()
        {
            // Equipamentos e dispositivos elétricos
            BuiltInCategory.OST_ElectricalEquipment,
            BuiltInCategory.OST_ElectricalFixtures,
            BuiltInCategory.OST_LightingFixtures,
            BuiltInCategory.OST_LightingDevices,
            // Dispositivos de sistemas especiais
            BuiltInCategory.OST_DataDevices,
            BuiltInCategory.OST_CommunicationDevices,
            BuiltInCategory.OST_TelephoneDevices,
            BuiltInCategory.OST_SecurityDevices,
            BuiltInCategory.OST_NurseCallDevices,
            // Infraestrutura elétrica
            BuiltInCategory.OST_Conduit,
            BuiltInCategory.OST_ConduitFitting,
            BuiltInCategory.OST_ConduitRun,
            BuiltInCategory.OST_CableTray,
            BuiltInCategory.OST_CableTrayFitting,
            BuiltInCategory.OST_CableTrayRun,
            // Fiação e circuitos
            BuiltInCategory.OST_Wire,
            BuiltInCategory.OST_ElectricalCircuit,
            BuiltInCategory.OST_SwitchSystem,
        };

        private static readonly HashSet<BuiltInCategory> Mechanical = new()
        {
            // Dutos
            BuiltInCategory.OST_DuctCurves,
            BuiltInCategory.OST_FlexDuctCurves,
            BuiltInCategory.OST_PlaceHolderDucts,
            // Conexões, acessórios e terminais de ar
            BuiltInCategory.OST_DuctFitting,
            BuiltInCategory.OST_DuctAccessory,
            BuiltInCategory.OST_DuctTerminal,
            // Equipamentos mecânicos
            BuiltInCategory.OST_MechanicalEquipment,
            // Isolamento e revestimento de dutos
            BuiltInCategory.OST_DuctInsulations,
            BuiltInCategory.OST_DuctLinings,
            // Sistemas e zonas HVAC
            BuiltInCategory.OST_DuctSystem,
            BuiltInCategory.OST_HVAC_Zones,
            BuiltInCategory.OST_MEPSpaces,
            // Peças de fabricação MEP
            BuiltInCategory.OST_FabricationDuctwork,
            BuiltInCategory.OST_FabricationHangers,
        };

        private static readonly HashSet<BuiltInCategory> FireProtection = new()
        {
            // Combate a incêndio hidráulico
            BuiltInCategory.OST_Sprinklers,
            // Detecção / alarme
            BuiltInCategory.OST_FireAlarmDevices,
        };

        private static readonly HashSet<BuiltInCategory> Structural = new()
        {
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_StructuralTruss,
            BuiltInCategory.OST_StructuralStiffener,
            BuiltInCategory.OST_StructuralConnections,
            BuiltInCategory.OST_Rebar,
            BuiltInCategory.OST_AreaRein,
            BuiltInCategory.OST_PathRein,
            BuiltInCategory.OST_FabricAreas,
            BuiltInCategory.OST_FabricReinforcement,
        };

        private static readonly HashSet<BuiltInCategory> Architecture = new()
        {
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_Ceilings,
            BuiltInCategory.OST_Doors,
            BuiltInCategory.OST_Windows,
            BuiltInCategory.OST_Stairs,
            BuiltInCategory.OST_StairsRailing,
            BuiltInCategory.OST_Ramps,
            BuiltInCategory.OST_Furniture,
            BuiltInCategory.OST_FurnitureSystems,
            BuiltInCategory.OST_Casework,
            BuiltInCategory.OST_CurtainWallPanels,
            BuiltInCategory.OST_CurtainWallMullions,
            BuiltInCategory.OST_Rooms,
            BuiltInCategory.OST_Parts,
            BuiltInCategory.OST_Topography,
            BuiltInCategory.OST_Site,
            BuiltInCategory.OST_Planting,
            BuiltInCategory.OST_SpecialityEquipment,
        };

        private static readonly HashSet<BuiltInCategory> GenericModels = new()
        {
            BuiltInCategory.OST_GenericModel,
        };

        private static readonly Dictionary<Discipline, HashSet<BuiltInCategory>> Map = new()
        {
            { Discipline.Hidraulica, Plumbing },
            { Discipline.Eletrica, Electrical },
            { Discipline.Mecanica, Mechanical },
            { Discipline.CombateIncendio, FireProtection },
            { Discipline.Estrutura, Structural },
            { Discipline.Arquitetura, Architecture },
            { Discipline.ModelosGenericos, GenericModels },
        };

        /// <summary>
        /// União das categorias das disciplinas informadas. Útil para construir
        /// um <see cref="Autodesk.Revit.UI.Selection.ISelectionFilter"/> a partir
        /// de várias disciplinas marcadas.
        /// </summary>
        public static HashSet<long> UnionCategoryIds(IEnumerable<Discipline> disciplines)
        {
            HashSet<long> result = new();
            foreach (Discipline d in disciplines)
            {
                if (!Map.TryGetValue(d, out HashSet<BuiltInCategory>? cats))
                    continue;

                foreach (BuiltInCategory bic in cats)
                    result.Add((long)bic);
            }
            return result;
        }

        public static HashSet<BuiltInCategory> CategoriesFor(Discipline discipline)
        {
            return Map.TryGetValue(discipline, out HashSet<BuiltInCategory>? cats)
                ? new HashSet<BuiltInCategory>(cats)
                : new HashSet<BuiltInCategory>();
        }
    }

    /// <summary>
    /// Verifica se um elemento pertence a uma disciplina específica, com base em
    /// sua <see cref="Element.Category"/>.
    /// </summary>
    public static class DisciplineClassifier
    {
        public static bool Matches(Element? element, Discipline discipline)
        {
            if (element == null)
                return false;

            Category? category = element.Category;
            if (category == null)
                return false;

            long id = category.Id.Value;
            HashSet<BuiltInCategory> cats = DisciplineCategoryMap.CategoriesFor(discipline);
            foreach (BuiltInCategory bic in cats)
            {
                if (id == (long)bic)
                    return true;
            }
            return false;
        }

        public static bool IsPlumbingElement(Element? element) => Matches(element, Discipline.Hidraulica);
        public static bool IsElectricalElement(Element? element) => Matches(element, Discipline.Eletrica);
        public static bool IsMechanicalElement(Element? element) => Matches(element, Discipline.Mecanica);
        public static bool IsFireProtectionElement(Element? element) => Matches(element, Discipline.CombateIncendio);
        public static bool IsStructuralElement(Element? element) => Matches(element, Discipline.Estrutura);
        public static bool IsArchitectureElement(Element? element) => Matches(element, Discipline.Arquitetura);
        public static bool IsGenericModelElement(Element? element) => Matches(element, Discipline.ModelosGenericos);
    }
}
