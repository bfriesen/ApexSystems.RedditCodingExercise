namespace RedditCodingExercise;

public interface IRedditApiClient
{
    IAsyncEnumerable<Post> GetPostsCreatedSinceStartupAsync(string subReddit, CancellationToken cancellationToken);
}
