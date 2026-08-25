using Trynex.Core.Updates;

namespace Trynex.Core.News;

public sealed class NewsFeedValidator
{
    private const int MaximumArticleCount = 100;
    private const int MaximumAnnouncementCount = 20;
    private static readonly HashSet<string> SupportedSeverities = new(
        ["info", "maintenance", "warning", "critical"],
        StringComparer.OrdinalIgnoreCase);

    public ManifestValidationResult Validate(NewsFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);

        var errors = new List<ManifestValidationError>();
        if (feed.SchemaVersion != 1)
        {
            errors.Add(new("news.schema.unsupported", "The news feed schema is not supported."));
        }

        if (feed.Articles is null)
        {
            errors.Add(new("news.articles.required", "The news article list is required."));
            return new(errors);
        }

        if (feed.Articles.Count > MaximumArticleCount)
        {
            errors.Add(new("news.articles.too_many", $"A news feed may contain at most {MaximumArticleCount} articles."));
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var article in feed.Articles)
        {
            if (article is null)
            {
                errors.Add(new("news.article.required", "The news feed contains an empty article."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(article.Id))
            {
                errors.Add(new("news.article.id.required", "A news article id is required."));
            }
            else if (!ids.Add(article.Id))
            {
                errors.Add(new("news.article.id.duplicate", "The same news article id appears more than once.", article.Id));
            }

            if (string.IsNullOrWhiteSpace(article.Category))
            {
                errors.Add(new("news.article.category.required", "A news category is required.", article.Id));
            }

            if (article.Title?.Values is null || article.Title.Values.Count == 0)
            {
                errors.Add(new("news.article.title.required", "A localized news title is required.", article.Id));
            }

            if (article.Summary?.Values is null || article.Summary.Values.Count == 0)
            {
                errors.Add(new("news.article.summary.required", "A localized news summary is required.", article.Id));
            }

            if (string.IsNullOrWhiteSpace(article.ArtworkPath) ||
                !article.ArtworkPath.StartsWith(
                    "/Trynex.Launcher;component/Assets/",
                    StringComparison.Ordinal))
            {
                errors.Add(new(
                    "news.article.artwork.invalid",
                    "News artwork must use a bundled TRYNEX package resource.",
                    article.Id));
            }

            if (!string.IsNullOrWhiteSpace(article.Link) &&
                (!Uri.TryCreate(article.Link, UriKind.Absolute, out var link) || link.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add(new("news.article.link.invalid", "News links must use an absolute HTTPS address.", article.Id));
            }
        }

        if (feed.Announcements is null)
        {
            return new(errors);
        }

        if (feed.Announcements.Count > MaximumAnnouncementCount)
        {
            errors.Add(new(
                "news.announcements.too_many",
                $"A news feed may contain at most {MaximumAnnouncementCount} system announcements."));
        }

        var announcementIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var announcement in feed.Announcements)
        {
            if (announcement is null)
            {
                errors.Add(new("news.announcement.required", "The feed contains an empty system announcement."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(announcement.Id))
            {
                errors.Add(new("news.announcement.id.required", "A system announcement id is required."));
            }
            else if (!announcementIds.Add(announcement.Id))
            {
                errors.Add(new(
                    "news.announcement.id.duplicate",
                    "The same system announcement id appears more than once.",
                    announcement.Id));
            }

            if (!SupportedSeverities.Contains(announcement.Severity))
            {
                errors.Add(new(
                    "news.announcement.severity.invalid",
                    "System announcement severity must be info, maintenance, warning or critical.",
                    announcement.Id));
            }

            if (announcement.EndsAtUtc <= announcement.StartsAtUtc)
            {
                errors.Add(new(
                    "news.announcement.period.invalid",
                    "A system announcement must end after it starts.",
                    announcement.Id));
            }

            if (announcement.Title?.Values is null || announcement.Title.Values.Count == 0)
            {
                errors.Add(new(
                    "news.announcement.title.required",
                    "A localized system announcement title is required.",
                    announcement.Id));
            }

            if (announcement.Message?.Values is null || announcement.Message.Values.Count == 0)
            {
                errors.Add(new(
                    "news.announcement.message.required",
                    "A localized system announcement message is required.",
                    announcement.Id));
            }

            if (!string.IsNullOrWhiteSpace(announcement.Link) &&
                (!Uri.TryCreate(announcement.Link, UriKind.Absolute, out var link) || link.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add(new(
                    "news.announcement.link.invalid",
                    "System announcement links must use an absolute HTTPS address.",
                    announcement.Id));
            }
        }

        return new(errors);
    }
}
