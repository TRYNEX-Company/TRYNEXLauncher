using Trynex.Core.Updates;

namespace Trynex.Core.Tests;

public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("0.3.0-preview.2", "0.3.0-preview.1")]
    [InlineData("0.3.0", "0.3.0-preview.99")]
    [InlineData("1.0.0", "0.99.99")]
    public void IsNewer_ReturnsTrueForHigherVersion(string available, string installed)
    {
        Assert.True(UpdateVersionSelector.IsNewer(installed, available));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0-preview.01")]
    public void TryParse_RejectsInvalidSemanticVersion(string value)
    {
        Assert.False(SemanticVersion.TryParse(value, out _));
    }
}
