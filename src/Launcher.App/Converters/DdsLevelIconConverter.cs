using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pfim;
using Launcher.Core.Config;

namespace Launcher.App.Converters;

/// <summary>
/// Converts a player level integer to a DDS image source from the game files.
/// Path: GameRootPath\data\ui\icon\ENG\icon_user_grade_{Level:D2}.dds
/// </summary>
public class DdsLevelIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int level)
            return null;

        try
        {
            var state = LocalState.Load();
            if (string.IsNullOrEmpty(state.GameRootPath))
                return null; // Game not installed

            string filename = $"icon_user_grade_{level:D2}.dds";
            string filePath = Path.Combine(state.GameRootPath, "data", "ui", "icon", "ENG", filename);

            if (!File.Exists(filePath))
                return null;

            return LoadDDS(filePath);
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

    private static BitmapSource LoadDDS(string file)
    {
        using var image = Pfimage.FromFile(file);
        
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

            bitmap.Freeze(); // Allow crossing threads safely if needed
            return bitmap;
        }
        finally
        {
            pinnedArray.Free();
        }
    }
}
