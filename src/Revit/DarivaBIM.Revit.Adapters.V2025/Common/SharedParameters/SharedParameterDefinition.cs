using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace DarivaBIM.Revit.Adapters.V2025.Common.SharedParameters
{
    /// <summary>
    /// Dado declarativo de um shared parameter: nome, grupo, GUID, tipo,
    /// categorias-alvo, kind do binding e flags de visibilidade. Cada
    /// ferramenta declara seus parâmetros como instâncias dessa classe e
    /// passa para o <see cref="SharedParameterService"/>; nenhuma feature
    /// reimplementa a lógica de criação/binding.
    /// </summary>
    public sealed class SharedParameterDefinition
    {
        public SharedParameterDefinition(
            string name,
            string groupName,
            Guid guid,
            ForgeTypeId specTypeId,
            ForgeTypeId parameterGroupTypeId,
            IReadOnlyList<BuiltInCategory> categories,
            SharedParameterBindingKind bindingKind,
            bool visible = true,
            bool userModifiable = true)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));
            if (string.IsNullOrWhiteSpace(groupName))
                throw new ArgumentException("GroupName is required.", nameof(groupName));
            if (guid == Guid.Empty)
                throw new ArgumentException("Guid is required.", nameof(guid));
            if (specTypeId == null)
                throw new ArgumentNullException(nameof(specTypeId));
            if (parameterGroupTypeId == null)
                throw new ArgumentNullException(nameof(parameterGroupTypeId));
            if (categories == null || categories.Count == 0)
                throw new ArgumentException("At least one category is required.", nameof(categories));

            Name = name;
            GroupName = groupName;
            Guid = guid;
            SpecTypeId = specTypeId;
            ParameterGroupTypeId = parameterGroupTypeId;
            Categories = categories;
            BindingKind = bindingKind;
            Visible = visible;
            UserModifiable = userModifiable;
        }

        public string Name { get; }
        public string GroupName { get; }
        public Guid Guid { get; }
        public ForgeTypeId SpecTypeId { get; }
        public ForgeTypeId ParameterGroupTypeId { get; }
        public IReadOnlyList<BuiltInCategory> Categories { get; }
        public SharedParameterBindingKind BindingKind { get; }
        public bool Visible { get; }
        public bool UserModifiable { get; }
    }
}
