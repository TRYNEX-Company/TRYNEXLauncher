using Trynex.Core.Updates;

namespace Trynex.Core.Projects;

public sealed class ProjectCatalogValidator
{
    private readonly ProjectManifestValidator _projectValidator;

    public ProjectCatalogValidator(ProjectManifestValidator projectValidator)
    {
        _projectValidator = projectValidator;
    }

    public ManifestValidationResult Validate(ProjectCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var errors = new List<ManifestValidationError>();
        if (catalog.SchemaVersion != 1)
        {
            errors.Add(new("catalog.schema.unsupported", "The project catalog schema is not supported."));
        }

        if (catalog.Projects is null)
        {
            errors.Add(new("catalog.projects.required", "Project catalog list is required."));
            return new(errors);
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in catalog.Projects)
        {
            if (project is null)
            {
                errors.Add(new("catalog.project.required", "Catalog contains an empty project."));
                continue;
            }

            if (!ids.Add(project.Id))
            {
                errors.Add(new("catalog.project.duplicate", "Catalog contains the same project id more than once.", project.Id));
            }

            foreach (var error in _projectValidator.Validate(project).Errors)
            {
                errors.Add(error with { RelativePath = error.RelativePath ?? project.Id });
            }
        }

        return new(errors);
    }
}
