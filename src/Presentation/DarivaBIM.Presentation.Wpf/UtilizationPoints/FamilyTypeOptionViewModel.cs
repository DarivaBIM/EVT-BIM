using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DarivaBIM.Application.DTOs.UtilizationPoints;
using DarivaBIM.Presentation.Wpf.Common;

namespace DarivaBIM.Presentation.Wpf.UtilizationPoints
{
    /// <summary>
    /// Linha do dropdown de tipos de família. Encapsula o
    /// <see cref="FamilyTypeOptionDto"/> e expõe propriedades amigáveis para
    /// data templates (nome composto, categoria visível, thumbnail).
    /// </summary>
    public class FamilyTypeOptionViewModel : ObservableObject
    {
        private ImageSource? _thumbnail;
        private bool _thumbnailDecoded;

        public FamilyTypeOptionViewModel(FamilyTypeOptionDto dto)
        {
            Dto = dto;
        }

        public FamilyTypeOptionDto Dto { get; }

        public long ElementId => Dto.ElementId;
        public string UniqueId => Dto.UniqueId;
        public string FamilyName => Dto.FamilyName;
        public string TypeName => Dto.TypeName;
        public string? CategoryName => Dto.CategoryName;
        public string DisplayName => Dto.DisplayName;

        public string SearchKey => $"{FamilyName} {TypeName} {CategoryName}".ToLowerInvariant();

        /// <summary>
        /// Preview do Revit decodificada sob demanda. Mantemos o resultado em
        /// cache e retornamos <c>null</c> quando o adapter não conseguiu
        /// capturar a imagem (família in-place, símbolo corrompido, etc.) —
        /// a UI cai no badge "F" nesses casos.
        /// </summary>
        public ImageSource? Thumbnail
        {
            get
            {
                if (_thumbnailDecoded) return _thumbnail;
                _thumbnailDecoded = true;
                _thumbnail = TryDecode(Dto.ThumbnailPng);
                return _thumbnail;
            }
        }

        public bool HasThumbnail => Thumbnail != null;
        public bool HasNoThumbnail => Thumbnail == null;

        private static ImageSource? TryDecode(byte[]? png)
        {
            if (png == null || png.Length == 0) return null;

            try
            {
                using MemoryStream stream = new(png);
                BitmapImage image = new();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}
