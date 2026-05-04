using System;
using Autodesk.Revit.DB;
using RevitApp = Autodesk.Revit.ApplicationServices.Application;

namespace DarivaBIM.Revit.Adapters.V2025.Common.SharedParameters
{
    /// <summary>
    /// Encapsula a manipulação de <see cref="BindingMap"/> do documento:
    /// inspeciona binding pré-existente pelo nome, monta <see cref="CategorySet"/>
    /// preservando categorias já vinculadas, cria
    /// <see cref="InstanceBinding"/>/<see cref="TypeBinding"/> conforme
    /// <see cref="SharedParameterBindingKind"/> e tenta Insert/ReInsert com
    /// fallback para o overload sem <see cref="ForgeTypeId"/> de grupo
    /// (compatibilidade com versões antigas do Revit que não conhecem
    /// <c>GroupTypeId</c>).
    /// </summary>
    internal static class ProjectParameterBindingService
    {
        public static ExistingSharedParameterBindingInfo? InspectExistingBinding(
            Document doc,
            string parameterName,
            Guid expectedGuid)
        {
            BindingMap bm = doc.ParameterBindings;
            DefinitionBindingMapIterator it = bm.ForwardIterator();
            it.Reset();

            while (it.MoveNext())
            {
                Definition? definition = it.Key;
                if (definition == null)
                    continue;

                if (definition.Name != parameterName)
                    continue;

                ElementBinding? binding = it.Current as ElementBinding;
                if (binding == null)
                    continue;

                Guid? existingGuid = TryGetGuid(definition);

                return new ExistingSharedParameterBindingInfo
                {
                    Definition = definition,
                    Binding = binding,
                    IsShared = existingGuid.HasValue,
                    GuidMatches = existingGuid.HasValue && existingGuid.Value == expectedGuid,
                };
            }

            return null;
        }

        public static CategorySet BuildCategorySet(
            RevitApp app,
            Document doc,
            ElementBinding? existingBinding,
            System.Collections.Generic.IReadOnlyList<BuiltInCategory> targetCategories)
        {
            CategorySet catSet = app.Create.NewCategorySet();
            System.Collections.Generic.HashSet<long> alreadyIn = new();

            if (existingBinding != null)
            {
                try
                {
                    foreach (Category c in existingBinding.Categories)
                    {
                        catSet.Insert(c);
                        alreadyIn.Add(c.Id.Value);
                    }
                }
                catch
                {
                    // best-effort — segue com o set vazio
                }
            }

            foreach (BuiltInCategory bic in targetCategories)
            {
                Category cat = doc.Settings.Categories.get_Item(bic);
                if (alreadyIn.Add(cat.Id.Value))
                    catSet.Insert(cat);
            }

            return catSet;
        }

        public static ElementBinding CreateBinding(
            RevitApp app,
            CategorySet categorySet,
            SharedParameterBindingKind kind)
        {
            return kind switch
            {
                SharedParameterBindingKind.Type => app.Create.NewTypeBinding(categorySet),
                _ => app.Create.NewInstanceBinding(categorySet),
            };
        }

        public static bool TryInsertBinding(
            Document doc,
            Definition def,
            Binding binding,
            ForgeTypeId parameterGroup)
        {
            try { return doc.ParameterBindings.Insert(def, binding, parameterGroup); }
            catch
            {
                try { return doc.ParameterBindings.Insert(def, binding); }
                catch { return false; }
            }
        }

        public static bool TryReinsertBinding(
            Document doc,
            Definition def,
            Binding binding,
            ForgeTypeId parameterGroup)
        {
            try { return doc.ParameterBindings.ReInsert(def, binding, parameterGroup); }
            catch
            {
                try { return doc.ParameterBindings.ReInsert(def, binding); }
                catch { return false; }
            }
        }

        public static Guid? TryGetGuid(Definition def)
        {
            if (def is ExternalDefinition ext)
                return ext.GUID;
            return null;
        }
    }
}
