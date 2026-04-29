using System.Collections.Generic;

namespace DarivaBIM.Revit.Abstractions.Hosting
{
    public interface IRevitSelectionService
    {
        IReadOnlyList<long> GetSelectedElementIds();

        void SetSelectedElementIds(IEnumerable<long> ids);
    }
}
