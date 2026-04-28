using System;
using System.IO;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace FamiliesImporterHub.Infrastructure
{
    /// <summary>
    /// Garante que o shared parameter <c>Tigre: Código</c> exista no projeto e
    /// esteja vinculado como instância à categoria <c>Pipe Curves</c>. Reproduz
    /// a lógica do script Dynamo: respeita parâmetros pré-existentes pelo nome
    /// e migra de tipo para instância quando possível.
    /// </summary>
    internal static class TigreSharedParameter
    {
        public const string ParamName = "Tigre: Código";
        public const string SharedGroupName = "Tigre";
        public static readonly Guid ParamGuid = new("71ba9de5-ea50-4906-bbf6-4e86df006f48");
        public const BuiltInCategory TargetCategory = BuiltInCategory.OST_PipeCurves;

        public sealed class EnsureResult
        {
            public string Action { get; set; } = string.Empty;
            public System.Collections.Generic.List<string> Warnings { get; } = new();
        }

        public static EnsureResult Ensure(Document doc)
        {
            EnsureResult result = new();
            Application app = doc.Application;

            ExistingBindingInfo? existing = InspectExistingBinding(doc);

            if (existing == null)
            {
                Definition shared = GetOrCreateSharedDefinition(app, out string? oldSpPath);
                try
                {
                    CategorySet catSet = BuildCategorySet(app, doc, null);
                    InstanceBinding binding = app.Create.NewInstanceBinding(catSet);

                    if (!TryInsertBinding(doc, shared, binding) &&
                        !TryReinsertBinding(doc, shared, binding))
                    {
                        throw new InvalidOperationException(
                            $"Não foi possível criar o parâmetro '{ParamName}' como instância.");
                    }

                    result.Action = "Parâmetro criado como instância em Tubulações.";
                    return result;
                }
                finally
                {
                    if (oldSpPath != null)
                    {
                        try { app.SharedParametersFilename = oldSpPath; }
                        catch { /* best-effort */ }
                    }
                }
            }

            if (!existing.IsShared)
            {
                result.Warnings.Add(
                    "Já existia um parâmetro com esse nome no projeto, mas ele não é shared. " +
                    "O script aproveitou o parâmetro pelo nome.");
            }
            else if (!existing.GuidMatches)
            {
                result.Warnings.Add(
                    "Já existia um shared parameter com esse nome, mas com GUID diferente. " +
                    "O script aproveitou o parâmetro pelo nome.");
            }

            CategorySet existingCats = BuildCategorySet(app, doc, existing.Binding);
            InstanceBinding instBinding = app.Create.NewInstanceBinding(existingCats);

            if (existing.Binding is InstanceBinding)
            {
                if (!TryReinsertBinding(doc, existing.Definition, instBinding) &&
                    !TryInsertBinding(doc, existing.Definition, instBinding))
                {
                    throw new InvalidOperationException(
                        $"Não foi possível atualizar o binding do parâmetro '{ParamName}'.");
                }

                result.Action = "Parâmetro já existia como instância e foi mantido/atualizado.";
                return result;
            }

            // Existia como tipo — tenta migrar para instância.
            if (!TryReinsertBinding(doc, existing.Definition, instBinding) &&
                !TryInsertBinding(doc, existing.Definition, instBinding))
            {
                throw new InvalidOperationException(
                    $"O parâmetro '{ParamName}' existe como tipo e não foi possível convertê-lo automaticamente para instância. " +
                    "Ajuste manualmente em Project Parameters e rode novamente.");
            }

            result.Warnings.Add("O parâmetro existia como tipo e foi convertido para instância.");
            result.Action = "Parâmetro convertido para instância.";
            return result;
        }

        public static Parameter? GetTargetParameter(Element element)
        {
            try
            {
                Parameter? p = element.LookupParameter(ParamName);
                if (p != null)
                    return p;
            }
            catch
            {
                // Ignora — tenta pelo GUID.
            }

            try
            {
                return element.get_Parameter(ParamGuid);
            }
            catch
            {
                return null;
            }
        }

        private sealed class ExistingBindingInfo
        {
            public Definition Definition { get; init; } = null!;
            public ElementBinding Binding { get; init; } = null!;
            public bool IsShared { get; init; }
            public bool GuidMatches { get; init; }
        }

        private static ExistingBindingInfo? InspectExistingBinding(Document doc)
        {
            BindingMap bm = doc.ParameterBindings;
            DefinitionBindingMapIterator it = bm.ForwardIterator();
            it.Reset();

            while (it.MoveNext())
            {
                Definition? definition = it.Key;
                if (definition == null)
                    continue;

                if (definition.Name != ParamName)
                    continue;

                ElementBinding? binding = it.Current as ElementBinding;
                if (binding == null)
                    continue;

                Guid? existingGuid = TryGetGuid(definition);

                return new ExistingBindingInfo
                {
                    Definition = definition,
                    Binding = binding,
                    IsShared = existingGuid.HasValue,
                    GuidMatches = existingGuid.HasValue && existingGuid.Value == ParamGuid,
                };
            }

            return null;
        }

        private static Guid? TryGetGuid(Definition def)
        {
            if (def is ExternalDefinition ext)
                return ext.GUID;
            return null;
        }

        private static Definition GetOrCreateSharedDefinition(Application app, out string? oldSpPath)
        {
            oldSpPath = app.SharedParametersFilename;
            string? sp = oldSpPath;

            if (string.IsNullOrEmpty(sp) || !File.Exists(sp))
            {
                sp = Path.Combine(Path.GetTempPath(), "DarivaBIM_SharedParameters.txt");
                if (!File.Exists(sp))
                {
                    File.WriteAllText(sp, string.Empty);
                }
                app.SharedParametersFilename = sp;
            }

            DefinitionFile? defFile = app.OpenSharedParameterFile()
                ?? throw new InvalidOperationException("Não foi possível abrir/criar o arquivo de Shared Parameters.");

            DefinitionGroup? group = null;
            foreach (DefinitionGroup g in defFile.Groups)
            {
                if (g.Name == SharedGroupName)
                {
                    group = g;
                    break;
                }
            }

            group ??= defFile.Groups.Create(SharedGroupName);

            // Procura definição existente — primeiro pelo GUID; em seguida pelo nome.
            foreach (DefinitionGroup g in defFile.Groups)
            {
                foreach (Definition d in g.Definitions)
                {
                    if (TryGetGuid(d) is Guid guid && guid == ParamGuid)
                        return d;
                    if (d.Name == ParamName)
                        return d;
                }
            }

            ExternalDefinitionCreationOptions options = new(ParamName, SpecTypeId.Int.Integer)
            {
                GUID = ParamGuid,
                Visible = true,
                UserModifiable = true,
            };

            return group.Definitions.Create(options);
        }

        private static CategorySet BuildCategorySet(Application app, Document doc, ElementBinding? existingBinding)
        {
            CategorySet catSet = app.Create.NewCategorySet();
            Category targetCat = doc.Settings.Categories.get_Item(TargetCategory);
            bool hasTarget = false;

            if (existingBinding != null)
            {
                try
                {
                    foreach (Category c in existingBinding.Categories)
                    {
                        catSet.Insert(c);
                        if (c.Id.IntegerValue == targetCat.Id.IntegerValue)
                            hasTarget = true;
                    }
                }
                catch
                {
                    // best-effort — segue com o set vazio
                }
            }

            if (!hasTarget)
                catSet.Insert(targetCat);

            return catSet;
        }

        private static bool TryInsertBinding(Document doc, Definition def, Binding binding)
        {
            try { return doc.ParameterBindings.Insert(def, binding, GroupTypeId.Data); }
            catch
            {
                try { return doc.ParameterBindings.Insert(def, binding); }
                catch { return false; }
            }
        }

        private static bool TryReinsertBinding(Document doc, Definition def, Binding binding)
        {
            try { return doc.ParameterBindings.ReInsert(def, binding, GroupTypeId.Data); }
            catch
            {
                try { return doc.ParameterBindings.ReInsert(def, binding); }
                catch { return false; }
            }
        }
    }
}
