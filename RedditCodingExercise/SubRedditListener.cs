using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditCodingExercise;

public class SubRedditListener(
    IRedditApiClient redditApiClient,
    IPostRepository postRepository,
    IOptionsMonitor<ApplicationOptions> options,
    ILogger<SubRedditListener> logger)
{
    private readonly IRedditApiClient _redditApiClient = redditApiClient ?? throw new ArgumentNullException(nameof(redditApiClient));
    private readonly IPostRepository _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
    private readonly IOptionsMonitor<ApplicationOptions> _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<SubRedditListener> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task SynchronizePostsAsync(CancellationToken cancellationToken)
    {
        var subReddit = _options.CurrentValue.SubRedditName;

        try
        {
            _logger.LogDebug("Synchronizing '{SubReddit}' posts...", subReddit);

            var tasks = new List<Task>();
            var posts = _redditApiClient.GetPostsCreatedSinceStartupAsync(subReddit, cancellationToken);

            await foreach (var post in posts)
                tasks.Add(_postRepository.AddOrUpdatePostAsync(post, cancellationToken));

            await Task.WhenAll(tasks);

            _logger.LogInformation("Synchronized {PostCount} '{SubReddit}' posts.", tasks.Count, subReddit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synchronizing '{SubReddit}' posts.", subReddit);
        }
    }
}
