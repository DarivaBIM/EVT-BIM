using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace DarivaBIM.Plugin.Ui.Models
{
    /// <summary>
    /// Carregador único e cacheado dos PNGs de sistema. Compartilhado entre
    /// o chip de filtro (<c>TagFilterOption</c>) e os badges miniaturizados
    /// dos cards — assim cada arquivo é decodificado uma vez por processo
    /// independente de quantos consumidores o referenciem.
    /// </summary>
    public static class SistemaIconLoader
    {
        // Caminho relativo ao .dll do plugin. O projitems linka os PNGs em
        // "Ribbon\Resources\FilterIcons\<arquivo>.png" no output.
        private const string FilterIconsRelativeFolder = "Ribbon\\Resources\\FilterIcons";

        // Cache global por filename. Misses (arquivo ausente) ficam como
        // null para evitar restats em cada criação de chip/badge.
        private static readonly Dictionary<string, BitmapImage?> Cache =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly object Lock = new();

        /// <param name="decodePixelWidth">
        /// Largura em pixels para o decode. Use ~2× o tamanho lógico de
        /// renderização para cobrir HiDPI sem segurar a textura full-res.
        /// 56 cobre os chips 28×28; 40 cobre os badges 20×20.
        /// </param>
        public static BitmapImage? Load(string? fileName, int decodePixelWidth)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            string cacheKey = $"{fileName}|{decodePixelWidth}";

            lock (Lock)
            {
                if (Cache.TryGetValue(cacheKey, out BitmapImage? cached))
                {
                    return cached;
                }
            }

            BitmapImage? image = null;
            try
            {
                string assemblyLocation = typeof(SistemaIconLoader).Assembly.Location;
                string baseDir = Path.GetDirectoryName(assemblyLocation) ?? string.Empty;
                string fullPath = Path.Combine(baseDir, FilterIconsRelativeFolder, fileName!);

                if (File.Exists(fullPath))
                {
                    image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = decodePixelWidth;
                    image.UriSource = new Uri(fullPath, UriKind.Absolute);
                    image.EndInit();
                    image.Freeze();
                }
            }
            catch
            {
                // PNG ausente/corrompido: callers tratam null como "sem
                // ícone" e caem no fallback (glyph ou ausência total).
                image = null;
            }

            lock (Lock)
            {
                Cache[cacheKey] = image;
            }

            return image;
        }
    }
}
