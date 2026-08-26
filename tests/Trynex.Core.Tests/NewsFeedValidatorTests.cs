using Trynex.Core.News;
using Trynex.Core.Projects;

namespace Trynex.Core.Tests;

public sealed class NewsFeedValidatorTests
{
    [Fact]
    public void Validate_AcceptsStructuredHttpsArticle()
    {
        var feed = Feed(new NewsArticle(
            "launcher-news",
            "Launcher",
            null,
            DateTimeOffset.UtcNow,
            true,
            Text("Title"),
            Text("Summary"),
            "/Trynex.Launcher;component/Assets/Brand/trynex-mark.png",
            "https://trynex.dev/news"));

        var result = new NewsFeedValidator().Validate(feed);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsDuplicateIdsAndNonHttpsLinks()
    {
        var article = new NewsArticle(
            "duplicate",
            "Project",
            "mr-project",
            DateTimeOffset.UtcNow,
            false,
            Text("Title"),
            Text("Summary"),
            "/Trynex.Launcher;component/Assets/Projects/mr-project.png",
            "javascript:alert(1)");

        var result = new NewsFeedValidator().Validate(Feed(article, article));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "news.article.id.duplicate");
        Assert.Contains(result.Errors, error => error.Code == "news.article.link.invalid");
    }

    [Fact]
    public void Validate_AcceptsStructuredAnnouncement()
    {
        var now = DateTimeOffset.UtcNow;
        var feed = new NewsFeed(
            1,
            now,
            [],
            [
                new SystemAnnouncement(
                    "maintenance",
                    "maintenance",
                    now.AddMinutes(-5),
                    now.AddHours(2),
                    true,
                    Text("Maintenance"),
                    Text("Services may be temporarily unavailable."),
                    "https://trynex.dev/status")
            ]);

        var result = new NewsFeedValidator().Validate(feed);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsInvalidAnnouncementSeverityPeriodAndLink()
    {
        var now = DateTimeOffset.UtcNow;
        var feed = new NewsFeed(
            1,
            now,
            [],
            [
                new SystemAnnouncement(
                    "broken",
                    "unknown",
                    now,
                    now.AddMinutes(-1),
                    false,
                    Text("Broken"),
                    Text("Broken"),
                    "http://trynex.dev")
            ]);

        var result = new NewsFeedValidator().Validate(feed);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "news.announcement.severity.invalid");
        Assert.Contains(result.Errors, error => error.Code == "news.announcement.period.invalid");
        Assert.Contains(result.Errors, error => error.Code == "news.announcement.link.invalid");
    }

    private static NewsFeed Feed(params NewsArticle[] articles) => new(1, DateTimeOffset.UtcNow, articles);

    private static LocalizedProjectText Text(string value) => new(
        new Dictionary<string, string> { ["en-US"] = value });
}
