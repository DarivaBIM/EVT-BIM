namespace DarivaBIM.Revit.Abstractions.Hosting
{
    /// <summary>
    /// Cancels a pending modeless <c>PickObject</c> / <c>PickObjects</c>
    /// invocation when the WPF window owning the workflow is dismissed
    /// without user interaction. Default implementation uses Win32 to
    /// raise an ESC keystroke focused on the Revit window because the
    /// Revit ExternalEvent queue does not expose a direct cancellation
    /// hook for an in-flight pick.
    /// </summary>
    public interface IRevitPickCancellationService
    {
        /// <summary>
        /// Best-effort attempt to cancel the current pick. Implementations
        /// should swallow any error: a failed cancellation degrades to
        /// the user pressing ESC themselves and does not corrupt state.
        /// </summary>
        void CancelPendingPick();
    }
}
