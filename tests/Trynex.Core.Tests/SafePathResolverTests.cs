using Trynex.Core.Security;

namespace Trynex.Core.Tests;

public sealed class SafePathResolverTests
{
    [Fact]
    public void ResolveInsideRoot_ReturnsNestedPath_ForValidRelativePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "trynex-root");

        var resolved = SafePathResolver.ResolveInsideRoot(root, "data/files/game.bin");

        Assert.StartsWith(Path.GetFullPath(root), resolved, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("data", "files", "game.bin"), resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../outside.bin")]
    [InlineData("data/../../outside.bin")]
    [InlineData("C:\\Windows\\system.ini")]
    [InlineData("game.bin:secret")]
    [InlineData("./game.bin")]
    [InlineData("data/../game.bin")]
    public void ResolveInsideRoot_RejectsUnsafePath(string relativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "trynex-root");

        Assert.Throws<ArgumentException>(() => SafePathResolver.ResolveInsideRoot(root, relativePath));
    }

    [Fact]
    public void ResolveInsideRoot_WorksWhenRootIsDriveRoot()
    {
        var driveRoot = Path.GetPathRoot(Path.GetTempPath());
        Assert.False(string.IsNullOrWhiteSpace(driveRoot));

        var resolved = SafePathResolver.ResolveInsideRoot(driveRoot, "trynex-root-test/file.bin");

        Assert.StartsWith(driveRoot, resolved, StringComparison.OrdinalIgnoreCase);
    }
}
