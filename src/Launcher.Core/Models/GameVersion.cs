namespace Launcher.Core.Models;

/// <summary>
/// Represents a game version in the format "ENG_X.Y.Z.W".
/// Supports parsing, comparison, and string formatting.
/// </summary>
public class GameVersion : IComparable<GameVersion>, IEquatable<GameVersion>
{
    public static readonly GameVersion Empty = new("", 0, 0, 0, 0);

    public string Prefix { get; }  // e.g. "ENG"
    public int Major { get; }
    public int Minor { get; }
    public int Build { get; }
    public int Revision { get; }

    public GameVersion(string prefix, int major, int minor, int build, int revision)
    {
        Prefix = prefix;
        Major = major;
        Minor = minor;
        Build = build;
        Revision = revision;
    }

    /// <summary>
    /// Parse a version string like "ENG_2.0.4.3".
    /// Throws FormatException if invalid.
    /// </summary>
    public static GameVersion Parse(string versionString)
    {
        var result = TryParse(versionString);
        if (result == null)
            throw new FormatException($"Invalid version format: '{versionString}'");
        return result;
    }

    /// <summary>
    /// Try to parse a version string. Returns null if invalid.
    /// </summary>
    public static GameVersion? TryParse(string? versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return null;

        versionString = versionString.Trim();

        // Split on underscore: "ENG_2.0.4.3" → ["ENG", "2.0.4.3"]
        var underscoreIndex = versionString.IndexOf('_');
        if (underscoreIndex < 0)
            return null;

        var prefix = versionString[..underscoreIndex];
        var versionPart = versionString[(underscoreIndex + 1)..];

        var parts = versionPart.Split('.');
        if (parts.Length != 4)
            return null;

        if (!int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var build) ||
            !int.TryParse(parts[3], out var rev))
            return null;

        return new GameVersion(prefix, major, minor, build, rev);
    }

    public int CompareTo(GameVersion? other)
    {
        if (other == null) return 1;

        var c = Major.CompareTo(other.Major);
        if (c != 0) return c;

        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;

        c = Build.CompareTo(other.Build);
        if (c != 0) return c;

        return Revision.CompareTo(other.Revision);
    }

    public bool Equals(GameVersion? other)
    {
        if (other == null) return false;
        return string.Equals(Prefix, other.Prefix, StringComparison.OrdinalIgnoreCase) &&
               Major == other.Major && Minor == other.Minor &&
               Build == other.Build && Revision == other.Revision;
    }

    public override bool Equals(object? obj) => Equals(obj as GameVersion);

    public override int GetHashCode() =>
        HashCode.Combine(Prefix.ToUpperInvariant(), Major, Minor, Build, Revision);

    public override string ToString() => $"{Prefix}_{Major}.{Minor}.{Build}.{Revision}";

    public bool IsEmpty => string.IsNullOrEmpty(Prefix) && Major == 0 && Minor == 0 && Build == 0 && Revision == 0;

    public static bool operator >(GameVersion a, GameVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(GameVersion a, GameVersion b) => a.CompareTo(b) < 0;
    public static bool operator >=(GameVersion a, GameVersion b) => a.CompareTo(b) >= 0;
    public static bool operator <=(GameVersion a, GameVersion b) => a.CompareTo(b) <= 0;
    public static bool operator ==(GameVersion? a, GameVersion? b) =>
        a is null ? b is null : a.Equals(b);
    public static bool operator !=(GameVersion? a, GameVersion? b) => !(a == b);
}
