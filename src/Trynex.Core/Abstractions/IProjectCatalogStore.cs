using Trynex.Core.Projects;

namespace Trynex.Core.Abstractions;

public interface IProjectCatalogStore
{
    Task<ProjectCatalog> LoadAsync(CancellationToken cancellationToken = default);
}
