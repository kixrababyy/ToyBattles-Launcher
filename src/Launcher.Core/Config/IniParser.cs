namespace Launcher.Core.Config;

/// <summary>
/// Simple INI file parser. Handles [section] headers, key = value pairs,
/// blank lines, and // comments. Does not modify the format.
/// </summary>
public static class IniParser
{
    /// <summary>
    /// Parse INI content from a string into a section→key→value dictionary.
    /// </summary>
    public static Dictionary<string, Dictionary<string, string>> Parse(string content)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string currentSection = string.Empty;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim('\r', ' ', '\t');

            // Skip blank lines and comments
            if (string.IsNullOrEmpty(line) || line.StartsWith("//") || line.StartsWith(";") || line.StartsWith("#"))
                continue;

            // Section header
            if (line.StartsWith('[') && line.Contains(']'))
            {
                currentSection = line.TrimStart('[').Split(']')[0].Trim();
                if (!result.ContainsKey(currentSection))
                    result[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            // Key = value
            var eqIndex = line.IndexOf('=');
            if (eqIndex > 0 && !string.IsNullOrEmpty(currentSection))
            {
                var key = line[..eqIndex].Trim();
                var value = line[(eqIndex + 1)..].Trim();

                // Strip inline comments (e.g. "2   // 1 Left  2 Center")
                // A real inline comment must be preceded by whitespace: " //"
                // Avoid stripping :// from URLs like http://
                var commentIndex = value.IndexOf(" //");
                if (commentIndex > 0)
                    value = value[..commentIndex].Trim();

                result[currentSection][key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Parse INI content from a file path.
    /// </summary>
    public static Dictionary<string, Dictionary<string, string>> ParseFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return Parse(content);
    }
}
