using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace Launcher.App.Views;

/// <summary>
/// Loads images from disk first (allowing user overrides),
/// then falls back to embedded resources inside the exe.
/// </summary>
internal static class EmbeddedImageLoader
{
    private static readonly Assembly Asm = Assembly.GetExecutingAssembly();

    /// <summary>
    /// Try to load an image from disk, then from embedded resources.
    /// Returns null if not found anywhere.
    /// </summary>
    public static BitmapImage? Load(string diskPath, string resourceName)
    {
        // 1) Disk file takes priority (user override)
        if (File.Exists(diskPath))
            return LoadFromFile(diskPath);

        // 2) Fall back to embedded resource
        return LoadFromResource(resourceName);
    }

    /// <summary>
    /// Get all embedded resource names matching a prefix (e.g. "wallpapers/").
    /// </summary>
    public static string[] GetResourceNames(string prefix)
    {
        return Asm.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n)
            .ToArray();
    }

    public static BitmapImage? LoadFromResource(string resourceName)
    {
        using var stream = Asm.GetManifestResourceStream(resourceName);
        if (stream == null) return null;

        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = stream;
        img.EndInit();
        img.Freeze();
        return img;
    }

    public static BitmapImage LoadFromFile(string path)
    {
        var img = new BitmapImage();
        img.BeginInit();
        img.UriSource = new Uri(path, UriKind.Absolute);
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        img.EndInit();
        img.Freeze();
        return img;
    }
}
