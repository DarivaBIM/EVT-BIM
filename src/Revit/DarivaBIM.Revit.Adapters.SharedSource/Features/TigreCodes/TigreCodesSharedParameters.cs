using System;
using Autodesk.Revit.DB;
using DarivaBIM.Domain.Tigre;
using DarivaBIM.Revit.Adapters.Common.SharedParameters;

namespace DarivaBIM.Revit.Adapters.Features.TigreCodes
{
    /// <summary>
    /// Declaração dos shared parameters utilizados pela feature "Códigos
    /// Tigre". A lógica de criação/binding vive em
    /// <see cref="SharedParameterService"/> — esta classe só descreve o dado.
    /// O nome, grupo e GUID vêm de <see cref="TigreSharedParameterDefinition"/>
    /// no Domain (constantes puras, sem RevitAPI), de forma que o lado
    /// Domain/Application e o lado Adapter compartilhem a mesma identidade.
    /// </summary>
    public static class TigreCodesSharedParameters
    {
        public static readonly SharedParameterDefinition Code = new SharedParameterDefinition(
            name: TigreSharedParameterDefinition.ParamName,
            groupName: TigreSharedParameterDefinition.SharedGroupName,
            guid: TigreSharedParameterDefinition.ParamGuid,
            specTypeId: SpecTypeId.Int.Integer,
            parameterGroupTypeId: GroupTypeId.Data,
            categories: new[] { BuiltInCategory.OST_PipeCurves },
            bindingKind: SharedParameterBindingKind.Instance,
            visible: true,
            userModifiable: true);
    }
}
