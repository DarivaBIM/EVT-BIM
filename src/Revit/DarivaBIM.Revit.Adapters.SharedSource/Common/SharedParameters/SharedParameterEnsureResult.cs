using System.Collections.Generic;

namespace DarivaBIM.Revit.Adapters.Common.SharedParameters
{
    /// <summary>
    /// Resultado da operação de garantir um shared parameter no projeto.
    /// <c>Action</c> descreve o que foi feito ("criado", "mantido",
    /// "convertido para instância"...). <c>Warnings</c> reúne avisos não-fatais
    /// para o usuário (parâmetro existente com GUID diferente, conversão de
    /// type→instance etc.).
    /// </summary>
    public sealed class SharedParameterEnsureResult
    {
        public string Action { get; set; } = string.Empty;
        public List<string> Warnings { get; } = new();
    }
}
