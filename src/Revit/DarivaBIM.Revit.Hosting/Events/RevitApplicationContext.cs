using System;
using Autodesk.Revit.UI;

namespace DarivaBIM.Revit.Hosting.Events
{
    /// <summary>
    /// Holds the <see cref="UIControlledApplication"/> received in
    /// <c>OnStartup</c>. Other hosting services depend on this rather than on
    /// the Revit app directly so the app is replaceable in tests if needed.
    /// </summary>
    public sealed class RevitApplicationContext
    {
        public RevitApplicationContext(UIControlledApplication application)
        {
            Application = application ?? throw new ArgumentNullException(nameof(application));
        }

        public UIControlledApplication Application { get; }
    }
}
