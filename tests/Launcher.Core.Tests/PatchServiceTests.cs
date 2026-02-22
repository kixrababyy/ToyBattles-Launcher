using Launcher.Core.Config;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Core.Tests;

public class PatchServiceTests
{
    [Fact]
    public void NeedsUpdate_RemoteNewer_ReturnsTrue()
    {
        var installed = GameVersion.Parse("ENG_2.0.4.1");
        var remote = GameVersion.Parse("ENG_2.0.4.3");

        Assert.True(PatchService.NeedsUpdate(installed, remote));
    }

    [Fact]
    public void NeedsUpdate_SameVersion_ReturnsFalse()
    {
        var installed = GameVersion.Parse("ENG_2.0.4.3");
        var remote = GameVersion.Parse("ENG_2.0.4.3");

        Assert.False(PatchService.NeedsUpdate(installed, remote));
    }

    [Fact]
    public void NeedsUpdate_EmptyInstalled_ReturnsTrue()
    {
        var remote = GameVersion.Parse("ENG_2.0.4.3");

        Assert.True(PatchService.NeedsUpdate(GameVersion.Empty, remote));
    }

    [Fact]
    public void NeedsUpdate_InstalledNewer_ReturnsFalse()
    {
        var installed = GameVersion.Parse("ENG_2.0.5.0");
        var remote = GameVersion.Parse("ENG_2.0.4.3");

        Assert.False(PatchService.NeedsUpdate(installed, remote));
    }

    [Fact]
    public void GetUpgradePath_SingleStep_ReturnsOneStep()
    {
        var installed = GameVersion.Parse("ENG_2.0.4.2");
        var remotePatch = PatchConfig.LoadFromContent(@"[patch]
version = ENG_2.0.4.3
version1 = ENG_2.0.4.2
version2 = ENG_2.0.4.1");

        var steps = PatchService.GetUpgradePath(installed, remotePatch);

        Assert.Single(steps);
        Assert.Equal("ENG_2.0.4.2", steps[0].From.ToString());
        Assert.Equal("ENG_2.0.4.3", steps[0].To.ToString());
    }

    [Fact]
    public void GetUpgradePath_MultipleSteps_ReturnsCorrectSequence()
    {
        var installed = GameVersion.Parse("ENG_2.0.4.1");
        var remotePatch = PatchConfig.LoadFromContent(@"[patch]
version = ENG_2.0.4.3
version1 = ENG_2.0.4.2
version2 = ENG_2.0.4.1");

        var steps = PatchService.GetUpgradePath(installed, remotePatch);

        Assert.Equal(2, steps.Count);
        // Step 1: 2.0.4.1 → 2.0.4.2
        Assert.Equal("ENG_2.0.4.1", steps[0].From.ToString());
        Assert.Equal("ENG_2.0.4.2", steps[0].To.ToString());
        // Step 2: 2.0.4.2 → 2.0.4.3
        Assert.Equal("ENG_2.0.4.2", steps[1].From.ToString());
        Assert.Equal("ENG_2.0.4.3", steps[1].To.ToString());
    }

    [Fact]
    public void GetUpgradePath_AlreadyUpToDate_ReturnsEmpty()
    {
        var installed = GameVersion.Parse("ENG_2.0.4.3");
        var remotePatch = PatchConfig.LoadFromContent(@"[patch]
version = ENG_2.0.4.3
version1 = ENG_2.0.4.2");

        var steps = PatchService.GetUpgradePath(installed, remotePatch);

        Assert.Empty(steps);
    }
}
