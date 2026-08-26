using Trynex.Core.News;

namespace Trynex.Core.Abstractions;

public interface INewsFeedStore
{
    Task<NewsFeed> LoadAsync(CancellationToken cancellationToken = default);
}
