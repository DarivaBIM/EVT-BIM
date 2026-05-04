using Autodesk.Revit.DB;

namespace DarivaBIM.Plugin.Features.PipeCadMapper.Tools
{
    /// <summary>
    /// Boundary helpers between the neutral <see cref="long"/> ids carried by
    /// the WPF view models in <c>Presentation.Wpf</c> and Revit's
    /// <see cref="ElementId"/>. Revit 2026 exposes the underlying value as
    /// <c>ElementId.Value</c> (long); pre-2024 versions used
    /// <c>ElementId.IntegerValue</c> (int) — that conversion stays in each
    /// versioned plugin/adapter so the presentation layer never needs to know.
    /// </summary>
    internal static class RevitElementIdConversions
    {
        public static long ToLong(ElementId id) => id.Value;

        public static ElementId ToElementId(long value) => new ElementId(value);
    }
}
