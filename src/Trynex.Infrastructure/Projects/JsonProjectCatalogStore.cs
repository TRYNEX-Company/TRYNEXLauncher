using System.Text.Json;
using System.Text.Json.Serialization;
using Trynex.Core.Abstractions;
using Trynex.Core.Projects;

namespace Trynex.Infrastructure.Projects;

public sealed class JsonProjectCatalogStore : IProjectCatalogStore
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly string _catalogPath;
    private readonly ProjectCatalogValidator _validator;

    public JsonProjectCatalogStore(
        string catalogPath,
        ProjectCatalogValidator? validator = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        _catalogPath = Path.GetFullPath(catalogPath);
        _validator = validator ?? new(new ProjectManifestValidator());
    }

    public async Task<ProjectCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = new FileStream(
                _catalogPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            var catalog = await JsonSerializer
                .DeserializeAsync<ProjectCatalog>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (catalog is null)
            {
                throw new InvalidDataException("Project catalog is empty.");
            }

            var validation = _validator.Validate(catalog);
            if (!validation.IsValid)
            {
                var details = string.Join(
                    "; ",
                    validation.Errors.Select(error => $"{error.Code}: {error.RelativePath ?? error.Message}"));
                throw new InvalidDataException($"Project catalog is invalid. {details}");
            }

            return catalog;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Project catalog contains invalid JSON.", exception);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
