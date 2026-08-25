using Trynex.Core.Projects;

namespace Trynex.Core.News;

public sealed record NewsArticle(
    string Id,
    string Category,
    string? ProjectId,
    DateTimeOffset PublishedAtUtc,
    bool IsFeatured,
    LocalizedProjectText Title,
    LocalizedProjectText Summary,
    string ArtworkPath,
    string? Link);
