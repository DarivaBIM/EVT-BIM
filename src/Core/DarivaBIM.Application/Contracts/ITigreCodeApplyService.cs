using DarivaBIM.Application.DTOs.Tigre;

namespace DarivaBIM.Application.Contracts
{
    /// <summary>
    /// Apply Tigre codes to all pipes in the active document.
    /// The Revit-specific implementation lives in DarivaBIM.Revit.Adapters.Vxxxx.
    /// The Application layer never depends on RevitAPI, so the use case talks
    /// to this interface only.
    /// </summary>
    public interface ITigreCodeApplyService
    {
        TigreCodeApplyResult Apply();
    }
}
