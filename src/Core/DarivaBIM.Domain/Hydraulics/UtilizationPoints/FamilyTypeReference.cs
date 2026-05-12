using System;

namespace DarivaBIM.Domain.Hydraulics.UtilizationPoints
{
    /// <summary>
    /// Identificador estável de um <c>FamilySymbol</c> Revit usado pelas
    /// regras de inserção de pontos de utilização. <see cref="FamilyName"/>
    /// + <see cref="TypeName"/> é a chave canônica que sobrevive entre
    /// documentos; <see cref="UniqueId"/> e <see cref="ElementId"/> são pistas
    /// auxiliares válidas apenas no contexto de um documento específico, úteis
    /// para reidentificar rapidamente o tipo após reabrir o mesmo projeto.
    /// </summary>
    public sealed class FamilyTypeReference : IEquatable<FamilyTypeReference>
    {
        public FamilyTypeReference(
            string familyName,
            string typeName,
            string? categoryName = null,
            long? elementId = null,
            string? uniqueId = null)
        {
            FamilyName = familyName ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            CategoryName = categoryName;
            ElementId = elementId;
            UniqueId = uniqueId;
        }

        public string FamilyName { get; }
        public string TypeName { get; }
        public string? CategoryName { get; }
        public long? ElementId { get; }
        public string? UniqueId { get; }

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(FamilyName) && string.IsNullOrWhiteSpace(TypeName);

        public bool Equals(FamilyTypeReference? other)
        {
            if (other is null) return false;
            return string.Equals(FamilyName, other.FamilyName, StringComparison.Ordinal)
                && string.Equals(TypeName, other.TypeName, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => Equals(obj as FamilyTypeReference);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (FamilyName?.GetHashCode() ?? 0);
                hash = (hash * 31) + (TypeName?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public override string ToString() => $"{FamilyName} : {TypeName}";
    }
}
