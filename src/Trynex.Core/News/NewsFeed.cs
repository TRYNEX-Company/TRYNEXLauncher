namespace Trynex.Core.News;

public sealed record NewsFeed(
    int SchemaVersion,
    DateTimeOffset PublishedAtUtc,
    IReadOnlyList<NewsArticle> Articles,
    IReadOnlyList<SystemAnnouncement>? Announcements = null);
