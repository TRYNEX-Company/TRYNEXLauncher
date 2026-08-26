using System.Security.Cryptography;
using System.Text.Json;
using Trynex.Core.Updates;

namespace Trynex.ReleaseTool;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                WriteUsage();
                return 1;
            }

            return args[0].ToLowerInvariant() switch
            {
                "keygen" => GenerateKeyPair(ParseOptions(args[1..])),
                "manifest" => await CreateManifestAsync(ParseOptions(args[1..])).ConfigureAwait(false),
                "verify" => await VerifyManifestAsync(ParseOptions(args[1..])).ConfigureAwait(false),
                _ => Fail("Unknown command.")
            };
        }
        catch (Exception exception) when (exception is
            ArgumentException or
            IOException or
            UnauthorizedAccessException or
            CryptographicException or
            JsonException)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 2;
        }
    }

    private static int GenerateKeyPair(IReadOnlyDictionary<string, string> options)
    {
        var privateKeyPath = GetRequiredPath(options, "private");
        var publicKeyPath = GetRequiredPath(options, "public");

        EnsureNewFile(privateKeyPath);
        EnsureNewFile(publicKeyPath);
        Directory.CreateDirectory(Path.GetDirectoryName(privateKeyPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(publicKeyPath)!);

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        WriteNewFile(privateKeyPath, key.ExportPkcs8PrivateKeyPem());
        WriteNewFile(publicKeyPath, key.ExportSubjectPublicKeyInfoPem());

        Console.WriteLine($"Private signing key created: {privateKeyPath}");
        Console.WriteLine($"Public verification key created: {publicKeyPath}");
        Console.WriteLine("Keep the private key outside the repository and back it up securely.");
        return 0;
    }

    private static async Task<int> CreateManifestAsync(IReadOnlyDictionary<string, string> options)
    {
        var packagePath = GetRequiredPath(options, "package");
        var privateKeyPath = GetRequiredPath(options, "private-key");
        var outputPath = GetRequiredPath(options, "output");
        var version = GetRequired(options, "version");
        var channel = GetRequired(options, "channel");
        var objectPath = GetRequired(options, "object-path");
        var minimumBootstrapper = options.GetValueOrDefault("minimum-bootstrapper");
        var mandatory = options.TryGetValue("mandatory", out var mandatoryText) &&
            bool.TryParse(mandatoryText, out var parsedMandatory) &&
            parsedMandatory;

        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("The release package does not exist.", packagePath);
        }

        if (!File.Exists(privateKeyPath))
        {
            throw new FileNotFoundException("The private signing key does not exist.", privateKeyPath);
        }

        EnsureNewFile(outputPath);

        var packageInfo = new FileInfo(packagePath);
        var packageHash = await ComputeSha256Async(packagePath).ConfigureAwait(false);
        var unsignedManifest = new LauncherUpdateManifest(
            LauncherUpdateManifestValidator.SupportedSchemaVersion,
            LauncherUpdateManifestValidator.ExpectedProduct,
            channel,
            version,
            DateTimeOffset.UtcNow,
            objectPath,
            packageInfo.Length,
            packageHash,
            Convert.ToBase64String([1]),
            minimumBootstrapper,
            mandatory);

        var validation = new LauncherUpdateManifestValidator().Validate(unsignedManifest);
        if (!validation.IsValid)
        {
            throw new ArgumentException(string.Join(
                Environment.NewLine,
                validation.Errors.Select(error => $"{error.Code}: {error.Message}")));
        }

        using var signer = ECDsa.Create();
        signer.ImportFromPem(await File.ReadAllTextAsync(privateKeyPath).ConfigureAwait(false));
        var signature = signer.SignData(
            ManifestSigningPayload.Create(unsignedManifest),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);
        var signedManifest = unsignedManifest with
        {
            Signature = Convert.ToBase64String(signature)
        };

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var temporaryPath = outputPath + ".tmp";
        await using (var stream = new FileStream(
                         temporaryPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         16 * 1024,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, signedManifest, JsonOptions).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        File.Move(temporaryPath, outputPath);
        Console.WriteLine($"Signed manifest created: {outputPath}");
        Console.WriteLine($"SHA-256: {packageHash}");
        return 0;
    }

    private static async Task<int> VerifyManifestAsync(IReadOnlyDictionary<string, string> options)
    {
        var manifestPath = GetRequiredPath(options, "manifest");
        var publicKeyPath = GetRequiredPath(options, "public-key");
        var packagePath = options.TryGetValue("package", out var packageValue)
            ? Path.GetFullPath(packageValue)
            : null;

        var manifest = JsonSerializer.Deserialize<LauncherUpdateManifest>(
            await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false),
            JsonOptions) ?? throw new JsonException("The manifest is empty.");

        var validation = new LauncherUpdateManifestValidator().Validate(manifest);
        if (!validation.IsValid)
        {
            throw new ArgumentException(string.Join(
                Environment.NewLine,
                validation.Errors.Select(error => $"{error.Code}: {error.Message}")));
        }

        using var verifier = ECDsa.Create();
        verifier.ImportFromPem(await File.ReadAllTextAsync(publicKeyPath).ConfigureAwait(false));
        var signature = Convert.FromBase64String(manifest.Signature);
        if (!verifier.VerifyData(
                ManifestSigningPayload.Create(manifest),
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence))
        {
            throw new CryptographicException("The manifest signature is not valid for this public key.");
        }

        if (packagePath is not null)
        {
            var packageInfo = new FileInfo(packagePath);
            var packageHash = await ComputeSha256Async(packagePath).ConfigureAwait(false);
            if (packageInfo.Length != manifest.PackageSize ||
                !string.Equals(packageHash, manifest.PackageSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new CryptographicException("The package size or SHA-256 does not match the manifest.");
            }
        }

        Console.WriteLine($"Manifest verified: {manifest.Version} ({manifest.Channel})");
        return 0;
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Options must use the form --name value.");
            }

            var name = args[index][2..];
            if (string.IsNullOrWhiteSpace(name) || !options.TryAdd(name, args[index + 1]))
            {
                throw new ArgumentException($"Invalid or duplicate option: {args[index]}");
            }
        }

        return options;
    }

    private static string GetRequired(IReadOnlyDictionary<string, string> options, string name)
    {
        if (!options.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing required option --{name}.");
        }

        return value;
    }

    private static string GetRequiredPath(IReadOnlyDictionary<string, string> options, string name)
    {
        return Path.GetFullPath(GetRequired(options, name));
    }

    private static void EnsureNewFile(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            throw new IOException($"Refusing to overwrite existing path: {path}");
        }
    }

    private static void WriteNewFile(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        WriteUsage();
        return 1;
    }

    private static void WriteUsage()
    {
        Console.WriteLine("TRYNEX Release Tool");
        Console.WriteLine("  keygen --private <path> --public <path>");
        Console.WriteLine("  manifest --package <zip> --version <semver> --channel <stable|preview>");
        Console.WriteLine("           --object-path <r2/path.zip> --private-key <pem> --output <manifest.json>");
        Console.WriteLine("           [--minimum-bootstrapper <semver>] [--mandatory <true|false>]");
        Console.WriteLine("  verify --manifest <manifest.json> --public-key <pem> [--package <zip>]");
    }
}
