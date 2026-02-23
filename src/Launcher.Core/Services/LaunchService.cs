using System.Diagnostics;

namespace Launcher.Core.Services;

/// <summary>
/// Launches the game executable with the correct working directory.
/// </summary>
public class LaunchService
{
    /// <summary>
    /// Launch MicroVolts.exe from GameRoot\Bin\ with working directory set to GameRoot.
    /// </summary>
    /// <param name="gameRootPath">Root path of the game installation.</param>
    /// <param name="arguments">Optional command-line arguments.</param>
    public static bool Launch(string gameRootPath, string? arguments = null)
    {
        var exePath = Path.Combine(gameRootPath, "Bin", "MicroVolts.exe");
        var workingDir = gameRootPath;

        if (!File.Exists(exePath))
        {
            LogService.LogError($"Game executable not found: {exePath}");
            return false;
        }

        try
        {
            LogService.Log($"Launching game: {exePath}");

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = workingDir,
                UseShellExecute = false
            };

            if (!string.IsNullOrWhiteSpace(arguments))
                psi.Arguments = arguments;

            Process.Start(psi);
            LogService.Log("Game launched successfully.");
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("Failed to launch game", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if the expected game executable exists at the given root path.
    /// </summary>
    public static bool ValidateGameRoot(string gameRootPath)
    {
        if (string.IsNullOrEmpty(gameRootPath))
            return false;

        var exePath = Path.Combine(gameRootPath, "Bin", "MicroVolts.exe");
        return File.Exists(exePath);
    }
}
