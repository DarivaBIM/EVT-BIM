// Polyfill obrigatorio para usar o accessor `init` (e records init-only) em
// netstandard2.0. Espelha o mesmo shim ja presente em DarivaBIM.Application —
// pode ser consolidado quando ambos targets multi-targetarem net6+.
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}
