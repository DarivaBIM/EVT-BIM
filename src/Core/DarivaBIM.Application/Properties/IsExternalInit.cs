// Polyfill required to use the C# `init` accessor on netstandard2.0.
// Once Application multi-targets net6+ this can move to a single Condition.
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit { }
}
