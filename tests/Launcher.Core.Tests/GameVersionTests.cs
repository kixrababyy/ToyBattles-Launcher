using Launcher.Core.Models;

namespace Launcher.Core.Tests;

public class GameVersionTests
{
    [Theory]
    [InlineData("ENG_2.0.4.3", "ENG", 2, 0, 4, 3)]
    [InlineData("ENG_2.0.0.0", "ENG", 2, 0, 0, 0)]
    [InlineData("ENG_1.0.0.1", "ENG", 1, 0, 0, 1)]
    [InlineData("KR_3.1.2.5", "KR", 3, 1, 2, 5)]
    public void Parse_ValidVersion_ReturnsCorrectComponents(
        string input, string prefix, int major, int minor, int build, int rev)
    {
        var v = GameVersion.Parse(input);

        Assert.Equal(prefix, v.Prefix);
        Assert.Equal(major, v.Major);
        Assert.Equal(minor, v.Minor);
        Assert.Equal(build, v.Build);
        Assert.Equal(rev, v.Revision);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("ENG_2.0.4")]       // only 3 parts
    [InlineData("ENG_2.0.4.3.1")]   // 5 parts
    [InlineData("2.0.4.3")]          // no prefix
    public void Parse_InvalidVersion_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => GameVersion.Parse(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invalid")]
    public void TryParse_InvalidVersion_ReturnsNull(string? input)
    {
        Assert.Null(GameVersion.TryParse(input));
    }

    [Fact]
    public void Compare_NewerVersion_IsGreater()
    {
        var older = GameVersion.Parse("ENG_2.0.4.1");
        var newer = GameVersion.Parse("ENG_2.0.4.3");

        Assert.True(newer > older);
        Assert.True(older < newer);
        Assert.False(older > newer);
    }

    [Fact]
    public void Compare_SameVersion_AreEqual()
    {
        var a = GameVersion.Parse("ENG_2.0.4.3");
        var b = GameVersion.Parse("ENG_2.0.4.3");

        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void Compare_DifferentBuild_CorrectOrder()
    {
        var v1 = GameVersion.Parse("ENG_2.0.3.9");
        var v2 = GameVersion.Parse("ENG_2.0.4.0");

        Assert.True(v2 > v1);
    }

    [Fact]
    public void ToString_RoundTrip_MatchesOriginal()
    {
        var original = "ENG_2.0.4.3";
        var version = GameVersion.Parse(original);
        Assert.Equal(original, version.ToString());
    }

    [Fact]
    public void Empty_Version_IsEmpty()
    {
        Assert.True(GameVersion.Empty.IsEmpty);
    }

    [Fact]
    public void Parsed_Version_IsNotEmpty()
    {
        var v = GameVersion.Parse("ENG_2.0.0.1");
        Assert.False(v.IsEmpty);
    }

    [Fact]
    public void GetHashCode_EqualVersions_SameHash()
    {
        var a = GameVersion.Parse("ENG_2.0.4.3");
        var b = GameVersion.Parse("ENG_2.0.4.3");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
