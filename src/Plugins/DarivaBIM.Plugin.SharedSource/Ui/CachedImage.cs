using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using DarivaBIM.Infrastructure.Persistence.Cache;

namespace DarivaBIM.Plugin.Ui
{
    /// <summary>
    /// Attached property that loads an <see cref="Image"/>'s source from a URL
    /// through the on-disk thumbnail cache. First access downloads + caches;
    /// every subsequent access (across sessions and across virtualization
    /// recycles) reads from disk synchronously, eliminating the visible flicker
    /// that plain <c>Source="{Binding Url}"</c> produces. Bitmaps are decoded
    /// to a fixed pixel width to keep memory low when many cards are realized.
    /// </summary>
    public static class CachedImage
    {
        // 110 logical px * 2 covers HiDPI displays without holding full-resolution decodes.
        private const int DecodePixelWidth = 220;

        private static readonly ThumbnailCacheService Cache = new();

        public static readonly DependencyProperty SourceUrlProperty =
            DependencyProperty.RegisterAttached(
                "SourceUrl",
                typeof(string),
                typeof(CachedImage),
                new PropertyMetadata(null, OnSourceUrlChanged));

        public static string? GetSourceUrl(DependencyObject obj)
        {
            return (string?)obj.GetValue(SourceUrlProperty);
        }

        public static void SetSourceUrl(DependencyObject obj, string? value)
        {
            obj.SetValue(SourceUrlProperty, value);
        }

        // Tracks the URL the Image is currently bound to. When a virtualized
        // container is recycled to a different family mid-download, the in-flight
        // task checks this value before applying — preventing a stale image from
        // being assigned to a now-unrelated card.
        private static readonly DependencyProperty CurrentUrlProperty =
            DependencyProperty.RegisterAttached(
                "CurrentUrl",
                typeof(string),
                typeof(CachedImage),
                new PropertyMetadata(null));

        private static void OnSourceUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Image image)
            {
                return;
            }

            string? newUrl = e.NewValue as string;
            image.SetValue(CurrentUrlProperty, newUrl);

            if (string.IsNullOrWhiteSpace(newUrl))
            {
                image.Source = null;
                return;
            }

            // Fast path: already cached on disk → load synchronously, no flicker.
            string? cachedPath = Cache.TryGetCachedPath(newUrl);
            if (cachedPath != null)
            {
                BitmapImage? bmp = TryBuildBitmap(cachedPath);
                if (bmp != null)
                {
                    image.Source = bmp;
                    return;
                }
            }

            // Slow path: clear stale source from a recycled container, then
            // download and apply asynchronously.
            image.Source = null;
            _ = LoadAsync(image, newUrl);
        }

        private static async Task LoadAsync(Image image, string url)
        {
            try
            {
                string? path = await Cache.GetOrDownloadAsync(url).ConfigureAwait(true);

                if (path == null)
                {
                    return;
                }

                if (image.GetValue(CurrentUrlProperty) as string != url)
                {
                    return;
                }

                BitmapImage? bmp = TryBuildBitmap(path);

                if (bmp == null)
                {
                    return;
                }

                if (image.GetValue(CurrentUrlProperty) as string != url)
                {
                    return;
                }

                image.Source = bmp;
            }
            catch
            {
                // Swallow — the image stays empty. We don't want a thumbnail
                // outage to crash the gallery.
            }
        }

        private static BitmapImage? TryBuildBitmap(string path)
        {
            try
            {
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.DecodePixelWidth = DecodePixelWidth;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }
}
