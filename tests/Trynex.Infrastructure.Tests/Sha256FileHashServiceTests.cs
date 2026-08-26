using Trynex.Infrastructure.Files;

namespace Trynex.Infrastructure.Tests;

public sealed class Sha256FileHashServiceTests
{
    [Fact]
    public async Task ComputeSha256Async_ReturnsExpectedHash()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"trynex-hash-{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(filePath, "abc");

            var hash = await new Sha256FileHashService().ComputeSha256Async(filePath);

            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
