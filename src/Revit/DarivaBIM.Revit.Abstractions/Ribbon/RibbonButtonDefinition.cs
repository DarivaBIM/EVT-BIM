namespace DarivaBIM.Revit.Abstractions.Ribbon
{
    /// <summary>
    /// Declarative description of a single ribbon button.
    /// </summary>
    public sealed class RibbonButtonDefinition
    {
        public RibbonButtonDefinition(
            string internalName,
            string text,
            RibbonCommandId commandId,
            string? toolTip = null,
            string? longDescription = null,
            string? helpUrl = null,
            string? largeIconResource = null,
            string? smallIconResource = null,
            RibbonButtonStyle style = RibbonButtonStyle.PushButton,
            LicenseRequirement licenseRequirement = LicenseRequirement.Free,
            string? localizationKey = null)
        {
            InternalName = internalName;
            Text = text;
            CommandId = commandId;
            ToolTip = toolTip;
            LongDescription = longDescription;
            HelpUrl = helpUrl;
            LargeIconResource = largeIconResource;
            SmallIconResource = smallIconResource;
            Style = style;
            LicenseRequirement = licenseRequirement;
            LocalizationKey = localizationKey;
        }

        public string InternalName { get; }
        public string Text { get; }
        public RibbonCommandId CommandId { get; }
        public string? ToolTip { get; }
        public string? LongDescription { get; }
        public string? HelpUrl { get; }
        public string? LargeIconResource { get; }
        public string? SmallIconResource { get; }
        public RibbonButtonStyle Style { get; }
        public LicenseRequirement LicenseRequirement { get; }
        public string? LocalizationKey { get; }
    }
}
