namespace DarivaBIM.Application.Tools
{
    /// <summary>
    /// Uniform return value for Plugin-side tools (the glue between an
    /// <c>IExternalCommand</c> and the application UseCase). The command shell
    /// is responsible for translating this into Revit's <c>Result</c> and for
    /// presenting <see cref="Message"/> to the user (TaskDialog, status bar,
    /// log, etc.) — the tool itself only declares outcome and intent.
    /// </summary>
    public sealed record ToolResult(
        bool Success,
        string? Message,
        ToolMessageKind Kind = ToolMessageKind.Info)
    {
        public static ToolResult Ok(string? message = null)
            => new(true, message, ToolMessageKind.Info);

        public static ToolResult Cancelled(string? message = null)
            => new(false, message, ToolMessageKind.Info);

        public static ToolResult Warn(string message)
            => new(true, message, ToolMessageKind.Warning);

        public static ToolResult Fail(string message)
            => new(false, message, ToolMessageKind.Error);
    }
}
