using Trynex.Core.Updates;

namespace Trynex.Core.Tests;

public sealed class LauncherInstallStateTests
{
    [Fact]
    public void Activate_PreservesPreviousVersionUntilHealthConfirmation()
    {
        var state = new LauncherInstallState("0.2.0-preview.1");

        var activated = state.Activate("0.3.0-preview.1");

        Assert.Equal("0.3.0-preview.1", activated.ActiveVersion);
        Assert.Equal("0.2.0-preview.1", activated.PreviousVersion);
        Assert.Equal("0.3.0-preview.1", activated.PendingVersion);
        Assert.Null(activated.FailedVersion);
    }

    [Fact]
    public void Rollback_RestoresPreviousVersion()
    {
        var state = new LauncherInstallState(
            "0.3.0-preview.1",
            "0.2.0-preview.1",
            "0.3.0-preview.1");

        var rolledBack = state.Rollback();

        Assert.Equal("0.2.0-preview.1", rolledBack.ActiveVersion);
        Assert.Null(rolledBack.PreviousVersion);
        Assert.Null(rolledBack.PendingVersion);
        Assert.Equal("0.3.0-preview.1", rolledBack.FailedVersion);
    }

    [Fact]
    public void Rollback_WithoutPreviousVersion_DoesNotKeepFailedVersionActive()
    {
        var state = new LauncherInstallState(
            "0.3.0-preview.1",
            null,
            "0.3.0-preview.1");

        var rolledBack = state.Rollback();

        Assert.Null(rolledBack.ActiveVersion);
        Assert.Null(rolledBack.PreviousVersion);
        Assert.Null(rolledBack.PendingVersion);
        Assert.Equal("0.3.0-preview.1", rolledBack.FailedVersion);
    }

    [Fact]
    public void ConfirmHealthy_ClearsPendingAndFailedVersions()
    {
        var state = new LauncherInstallState(
            "0.4.0-preview.1",
            "0.3.0-preview.1",
            "0.4.0-preview.1",
            "0.3.0-preview.2");

        var confirmed = state.ConfirmHealthy();

        Assert.Null(confirmed.PendingVersion);
        Assert.Null(confirmed.FailedVersion);
    }

    [Fact]
    public void IsFailedVersion_MatchesCaseInsensitively()
    {
        var state = new LauncherInstallState(FailedVersion: "0.3.0-PREVIEW.2");

        Assert.True(state.IsFailedVersion("0.3.0-preview.2"));
        Assert.False(state.IsFailedVersion("0.3.0-preview.3"));
    }
}
