using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using DarivaBIM.Revit.Abstractions.Ribbon;

namespace DarivaBIM.Revit.Hosting.Ribbon
{
    /// <summary>
    /// Translates a <see cref="RibbonDefinition"/> into Revit ribbon objects.
    /// The actual <c>IExternalCommand</c> Type for each button is resolved
    /// through the <see cref="ICommandRegistry"/>, so the ribbon is fully
    /// declarative and version-agnostic.
    /// </summary>
    public sealed class RibbonBuilder
    {
        private readonly ICommandRegistry _commands;
        private readonly string _pluginAssemblyPath;

        public RibbonBuilder(ICommandRegistry commands, string pluginAssemblyPath)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _pluginAssemblyPath = pluginAssemblyPath ?? throw new ArgumentNullException(nameof(pluginAssemblyPath));
        }

        public void Build(UIControlledApplication application, RibbonDefinition ribbon)
        {
            TryCreateRibbonTab(application, ribbon.TabName);

            foreach (RibbonPanelDefinition panelDef in ribbon.Panels)
            {
                RibbonPanel panel = GetOrCreateRibbonPanel(application, ribbon.TabName, panelDef.Name);

                foreach (RibbonButtonDefinition buttonDef in panelDef.Buttons)
                {
                    AddButton(panel, buttonDef);
                }
            }
        }

        private void AddButton(RibbonPanel panel, RibbonButtonDefinition def)
        {
            if (!_commands.TryGetCommandType(def.CommandId, out Type? commandType) || commandType == null)
            {
                // Unknown commands are skipped silently; log via telemetry once wired.
                return;
            }

            PushButtonData data = new PushButtonData(
                def.InternalName,
                def.Text,
                _pluginAssemblyPath,
                commandType.FullName!);

            if (panel.AddItem(data) is PushButton button)
            {
                button.ToolTip = def.ToolTip;
                button.LongDescription = def.LongDescription;

                BitmapSource? large = TryLoadIcon(def.LargeIconResource);
                if (large != null) button.LargeImage = large;

                BitmapSource? small = TryLoadIcon(def.SmallIconResource);
                if (small != null) button.Image = small;

                if (!string.IsNullOrWhiteSpace(def.HelpUrl))
                {
                    button.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, def.HelpUrl));
                }
            }
        }

        private BitmapSource? TryLoadIcon(string? resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return null;

            try
            {
                string baseDir = Path.GetDirectoryName(_pluginAssemblyPath) ?? string.Empty;
                string fullPath = Path.IsPathRooted(resourcePath)
                    ? resourcePath
                    : Path.Combine(baseDir, resourcePath);

                if (!File.Exists(fullPath)) return null;

                BitmapFrame frame;
                using (FileStream stream = File.OpenRead(fullPath))
                {
                    BitmapDecoder decoder = BitmapDecoder.Create(
                        stream,
                        BitmapCreateOptions.None,
                        BitmapCacheOption.OnLoad);
                    frame = decoder.Frames[0];
                }

                // Revit's ribbon sizes icons in DPI-aware logical units. PNGs
                // exported at 72 DPI render ~33% larger than their pixel size
                // and get cropped in the 32x32 button slot, so re-emit the
                // bitmap at 96 DPI (logical size == pixel size).
                int stride = (frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8;
                byte[] pixels = new byte[stride * frame.PixelHeight];
                frame.CopyPixels(pixels, stride, 0);

                BitmapSource image = BitmapSource.Create(
                    frame.PixelWidth,
                    frame.PixelHeight,
                    96,
                    96,
                    frame.Format,
                    frame.Palette,
                    pixels,
                    stride);
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private static void TryCreateRibbonTab(UIControlledApplication application, string tabName)
        {
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch
            {
                // Tab already exists.
            }
        }

        private static RibbonPanel GetOrCreateRibbonPanel(
            UIControlledApplication application,
            string tabName,
            string panelName)
        {
            foreach (RibbonPanel panel in application.GetRibbonPanels(tabName))
            {
                if (string.Equals(panel.Name, panelName, StringComparison.OrdinalIgnoreCase))
                    return panel;
            }

            return application.CreateRibbonPanel(tabName, panelName);
        }
    }
}
