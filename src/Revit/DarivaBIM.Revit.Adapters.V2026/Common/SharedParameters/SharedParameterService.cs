using System;
using Autodesk.Revit.DB;
using RevitApp = Autodesk.Revit.ApplicationServices.Application;

namespace DarivaBIM.Revit.Adapters.V2026.Common.SharedParameters
{
    /// <summary>
    /// Serviço genérico para garantir/acessar shared parameters.
    /// <see cref="Ensure"/> reproduz o fluxo do script Dynamo do TigreCodes:
    ///
    /// 1. Inspeciona o binding existente pelo nome do parâmetro.
    /// 2. Se não existir, cria/abre o arquivo de Shared Parameters, garante
    ///    grupo + ExternalDefinition e insere o binding (Instance ou Type).
    /// 3. Se existir como instância, atualiza o binding (mantém categorias
    ///    pré-existentes; acrescenta o que faltar).
    /// 4. Se existir como type e a definição pede instance, tenta migrar via
    ///    ReInsert/Insert.
    /// 5. Avisa o usuário quando o parâmetro existente tinha GUID diferente
    ///    ou não era shared (para o usuário saber que estamos reaproveitando).
    ///
    /// O <see cref="GetParameter"/> apenas delega para
    /// <see cref="SharedParameterAccessor"/>.
    /// </summary>
    public static class SharedParameterService
    {
        public static SharedParameterEnsureResult Ensure(Document doc, SharedParameterDefinition definition)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            SharedParameterEnsureResult result = new();
            RevitApp app = doc.Application;

            ExistingSharedParameterBindingInfo? existing =
                ProjectParameterBindingService.InspectExistingBinding(doc, definition.Name, definition.Guid);

            if (existing == null)
            {
                Definition shared = GetOrCreateSharedDefinition(app, definition, out string? oldSpPath);
                try
                {
                    CategorySet catSet = ProjectParameterBindingService.BuildCategorySet(
                        app, doc, existingBinding: null, definition.Categories);
                    ElementBinding binding = ProjectParameterBindingService.CreateBinding(
                        app, catSet, definition.BindingKind);

                    if (!ProjectParameterBindingService.TryInsertBinding(doc, shared, binding, definition.ParameterGroupTypeId) &&
                        !ProjectParameterBindingService.TryReinsertBinding(doc, shared, binding, definition.ParameterGroupTypeId))
                    {
                        throw new InvalidOperationException(
                            $"Não foi possível criar o parâmetro '{definition.Name}' como " +
                            $"{DescribeBindingKind(definition.BindingKind)}.");
                    }

                    result.Action =
                        $"Parâmetro criado como {DescribeBindingKind(definition.BindingKind)}.";
                    return result;
                }
                finally
                {
                    SharedParameterFileService.RestorePreviousPath(app, oldSpPath);
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

            CategorySet existingCats = ProjectParameterBindingService.BuildCategorySet(
                app, doc, existing.Binding, definition.Categories);
            ElementBinding desiredBinding = ProjectParameterBindingService.CreateBinding(
                app, existingCats, definition.BindingKind);

            bool existingMatchesKind =
                (definition.BindingKind == SharedParameterBindingKind.Instance && existing.Binding is InstanceBinding) ||
                (definition.BindingKind == SharedParameterBindingKind.Type && existing.Binding is TypeBinding);

            if (existingMatchesKind)
            {
                if (!ProjectParameterBindingService.TryReinsertBinding(doc, existing.Definition, desiredBinding, definition.ParameterGroupTypeId) &&
                    !ProjectParameterBindingService.TryInsertBinding(doc, existing.Definition, desiredBinding, definition.ParameterGroupTypeId))
                {
                    throw new InvalidOperationException(
                        $"Não foi possível atualizar o binding do parâmetro '{definition.Name}'.");
                }

                result.Action =
                    $"Parâmetro já existia como {DescribeBindingKind(definition.BindingKind)} " +
                    "e foi mantido/atualizado.";
                return result;
            }

            // Existia em outro kind — tenta migrar.
            if (!ProjectParameterBindingService.TryReinsertBinding(doc, existing.Definition, desiredBinding, definition.ParameterGroupTypeId) &&
                !ProjectParameterBindingService.TryInsertBinding(doc, existing.Definition, desiredBinding, definition.ParameterGroupTypeId))
            {
                throw new InvalidOperationException(
                    $"O parâmetro '{definition.Name}' existe em outro kind e não foi possível " +
                    $"convertê-lo automaticamente para {DescribeBindingKind(definition.BindingKind)}. " +
                    "Ajuste manualmente em Project Parameters e rode novamente.");
            }

            result.Warnings.Add(
                $"O parâmetro existia em outro kind e foi convertido para " +
                $"{DescribeBindingKind(definition.BindingKind)}.");
            result.Action =
                $"Parâmetro convertido para {DescribeBindingKind(definition.BindingKind)}.";
            return result;
        }

        public static Parameter? GetParameter(Element element, SharedParameterDefinition definition)
        {
            return SharedParameterAccessor.GetParameter(element, definition);
        }

        private static Definition GetOrCreateSharedDefinition(
            RevitApp app,
            SharedParameterDefinition definition,
            out string? oldSpPath)
        {
            DefinitionFile defFile = SharedParameterFileService.OpenOrCreate(app, out oldSpPath);

            DefinitionGroup? group = null;
            foreach (DefinitionGroup g in defFile.Groups)
            {
                if (g.Name == definition.GroupName)
                {
                    group = g;
                    break;
                }
            }

            group ??= defFile.Groups.Create(definition.GroupName);

            // Procura definição existente — primeiro pelo GUID; em seguida pelo nome.
            foreach (DefinitionGroup g in defFile.Groups)
            {
                foreach (Definition d in g.Definitions)
                {
                    if (ProjectParameterBindingService.TryGetGuid(d) is Guid guid && guid == definition.Guid)
                        return d;
                    if (d.Name == definition.Name)
                        return d;
                }
            }

            ExternalDefinitionCreationOptions options = new(definition.Name, definition.SpecTypeId)
            {
                GUID = definition.Guid,
                Visible = definition.Visible,
                UserModifiable = definition.UserModifiable,
            };

            return group.Definitions.Create(options);
        }

        private static string DescribeBindingKind(SharedParameterBindingKind kind) => kind switch
        {
            SharedParameterBindingKind.Type => "tipo",
            _ => "instância",
        };
    }
}
