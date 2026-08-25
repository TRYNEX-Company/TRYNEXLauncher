using Trynex.Core.Projects;

namespace Trynex.Core.News;

public sealed record SystemAnnouncement(
    string Id,
    string Severity,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    bool IsDismissible,
    LocalizedProjectText Title,
    LocalizedProjectText Message,
    string? Link);
