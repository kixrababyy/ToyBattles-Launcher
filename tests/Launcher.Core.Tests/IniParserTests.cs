using Launcher.Core.Config;

namespace Launcher.Core.Tests;

public class IniParserTests
{
    private const string SampleUpdateInfo = @"[update]
addr = http://cdn.toybattles.net/ENG

[FullFile]
addr = http://cdn.toybattles.net/update/ENG/Full/";

    private const string SamplePatchIni = @"[patch]
version = ENG_2.0.4.3
version1 = ENG_2.0.4.2
version2 = ENG_2.0.4.1
exe = bin/MicroVolts.exe";

    private const string SamplePatchLauncherIni = @"[patch]
version = ENG_2.0.1.2";

    [Fact]
    public void Parse_UpdateInfo_ExtractsSections()
    {
        var result = IniParser.Parse(SampleUpdateInfo);

        Assert.True(result.ContainsKey("update"));
        Assert.True(result.ContainsKey("FullFile"));
        Assert.Equal("http://cdn.toybattles.net/ENG", result["update"]["addr"]);
        Assert.Equal("http://cdn.toybattles.net/update/ENG/Full/", result["FullFile"]["addr"]);
    }

    [Fact]
    public void Parse_PatchIni_ExtractsAllVersions()
    {
        var result = IniParser.Parse(SamplePatchIni);

        Assert.True(result.ContainsKey("patch"));
        Assert.Equal("ENG_2.0.4.3", result["patch"]["version"]);
        Assert.Equal("ENG_2.0.4.2", result["patch"]["version1"]);
        Assert.Equal("ENG_2.0.4.1", result["patch"]["version2"]);
        Assert.Equal("bin/MicroVolts.exe", result["patch"]["exe"]);
    }

    [Fact]
    public void Parse_HandlesBlankLinesAndComments()
    {
        var input = @"
; This is a comment
[section]
key1 = value1

// Another comment
key2 = value2

";
        var result = IniParser.Parse(input);
        Assert.Single(result);
        Assert.Equal("value1", result["section"]["key1"]);
        Assert.Equal("value2", result["section"]["key2"]);
    }

    [Fact]
    public void Parse_InlineComments_Stripped()
    {
        var input = @"[section]
align = 2   // 1 Left  2 Center 3 Right";

        var result = IniParser.Parse(input);
        Assert.Equal("2", result["section"]["align"]);
    }

    [Fact]
    public void UpdateInfoConfig_LoadFromContent_Correct()
    {
        var config = UpdateInfoConfig.LoadFromContent(SampleUpdateInfo);

        Assert.Equal("http://cdn.toybattles.net/ENG", config.UpdateAddress);
        Assert.Equal("http://cdn.toybattles.net/update/ENG/Full", config.FullFileAddress);
    }

    [Fact]
    public void PatchConfig_LoadFromContent_CorrectLatestVersion()
    {
        var config = PatchConfig.LoadFromContent(SamplePatchIni);

        Assert.Equal("ENG_2.0.4.3", config.LatestVersion.ToString());
        Assert.Equal(3, config.AllVersions.Count); // version + version1 + version2
        Assert.Equal("bin\\MicroVolts.exe", config.GameExePath);
    }

    [Fact]
    public void Parse_CaseInsensitiveKeys()
    {
        var input = @"[PATCH]
Version = ENG_1.0.0.0";

        var result = IniParser.Parse(input);
        Assert.True(result.ContainsKey("PATCH"));
        Assert.True(result.ContainsKey("patch")); // case-insensitive
        Assert.Equal("ENG_1.0.0.0", result["patch"]["version"]);
    }
}
