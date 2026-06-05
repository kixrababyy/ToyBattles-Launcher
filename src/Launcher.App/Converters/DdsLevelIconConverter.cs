using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pfim;
using Launcher.Core.Models;

namespace Launcher.App.Converters;

/// <summary>
/// Converts a player level integer to a DDS image source from the game files.
/// Path: GameRootPath\data\ui\icon\ENG\icon_user_grade_{Level:D2}.dds
/// </summary>
public class DdsLevelIconConverter : IValueConverter
{
    private static BitmapSource? _cachedSpritesheet;
    private const int Columns = 16;
    private const int IconWidth = 32;
    private const int IconHeight = 32;

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int level)
            return null;

        try
        {
            var sheet = GetSpritesheet();
            if (sheet == null)
                return null;

            int col = level % Columns;
            int row = level / Columns;

            // Ensure we don't crop outside the image bounds
            if (col * IconWidth >= sheet.PixelWidth || row * IconHeight >= sheet.PixelHeight)
                return null;

            return new CroppedBitmap(sheet, new System.Windows.Int32Rect(col * IconWidth, row * IconHeight, IconWidth, IconHeight));
        }
        catch (Exception ex)
        {
            Launcher.Core.Services.LogService.LogError($"Failed to load level icon for level {level}", ex);
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static BitmapSource? GetSpritesheet()
    {
        if (_cachedSpritesheet != null)
            return _cachedSpritesheet;

        var state = LocalState.Load();
        if (string.IsNullOrEmpty(state.GameRootPath))
            return null; // Game not installed

        string filePath = Path.Combine(state.GameRootPath, "data", "ui", "icon", "ENG", "icon_user_grade_01.dds");
        if (!File.Exists(filePath))
            return null;

        using var image = Pfimage.FromFile(filePath);
        
        PixelFormat format;
        switch (image.Format)
        {
            case ImageFormat.Rgba32:
                format = PixelFormats.Bgra32;
                break;
            case ImageFormat.Rgb24:
                format = PixelFormats.Bgr24;
                break;
            default:
                throw new Exception($"Unsupported DDS format: {image.Format}");
        }

        var pinnedArray = GCHandle.Alloc(image.Data, GCHandleType.Pinned);
        try
        {
            var bitmap = BitmapSource.Create(
                image.Width,
                image.Height,
                96.0,
                96.0,
                format,
                null,
                pinnedArray.AddrOfPinnedObject(),
                image.DataLen,
                image.Stride);

            bitmap.Freeze(); // Allow crossing threads safely
            _cachedSpritesheet = bitmap;
            return bitmap;
        }
        finally
        {
            pinnedArray.Free();
        }
    }
}
