using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;

namespace RedditCodingExercise;

public class RedditApiClient(
    HttpClient httpClient,
    IOptionsMonitor<ApplicationOptions> options,
    ILogger<RedditApiClient> logger)
    : IRedditApiClient
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly ILogger<RedditApiClient> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // Public for testing.
    public long ApplicationStartTime { get; } = options.CurrentValue.ApplicationStartTime ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // Paging documentation is here: https://www.reddit.com/dev/api#listings.
    public async IAsyncEnumerable<Post> GetPostsCreatedSinceStartupAsync(
        string subReddit, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(subReddit, nameof(subReddit));

        // Paging parameters.
        int count = 0;
        string? after = null;

        while (true)
        {
            var url = $"/r/{subReddit}/new?show=all";

            // Add paging parameters to the url if they exist.
            if ((count, after) is ( > 0, not null))
                url += $"&count={count}&after={after}";

            // Make the request.
            var response = await _httpClient.GetAsync(url, cancellationToken);

            // Ensure the request was successful.
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Response status code does not indicate success: {StatusCode} ({ReasonPhrase}).",
                    (int)response.StatusCode,
                    response.ReasonPhrase ?? response.StatusCode.ToString());
                break;
            }

            // Unpack the response.
            var newPostsResponse = await response.Content.ReadFromJsonAsync<GetPostsResponse>(cancellationToken)
                ?? throw new InvalidOperationException($"Unexpected null JSON content for {nameof(GetPostsResponse)}.");

            // Track whether the last item was after the start time so we'll know whether to
            // continue and fetch another page from Reddit or break and return our list of posts.
            var lastItemWasAfterStartTime = false;

            foreach (var post in newPostsResponse.Data.Children)
            {
                // Don't include posts that are older than our start time.
                lastItemWasAfterStartTime = post.Data.Created_Utc >= ApplicationStartTime;
                if (!lastItemWasAfterStartTime)
                    break;

                yield return post.Data.AsPost();
                count++;
            }

            after = newPostsResponse.Data.After;

            // A null 'after' indicates that Reddit doesn't have any more posts after this one.
            if (!lastItemWasAfterStartTime || after is null)
                break;
        }
    }

    private class GetPostsResponse
    {
        public ResponseData Data { get; set; } = null!;

        public class ResponseData
        {
            public string After { get; set; } = null!;
            public Post[] Children { get; set; } = null!;
        }

        public class Post
        {
            public PostData Data { get; set; } = null!;

            public class PostData
            {
                public string Name { get; set; } = null!;
                public string Subreddit { get; set; } = null!;
                public string Title { get; set; } = null!;
                public string Author { get; set; } = null!;
                public string Permalink { get; set; } = null!;
                public decimal Created_Utc { get; set; }
                public int Ups { get; set; }
                public double Upvote_Ratio { get; set; }

                public RedditCodingExercise.Post AsPost() =>
                    new()
                    {
                        Name = Name,
                        SubReddit = Subreddit,
                        Author = Author,
                        Permalink = Permalink,
                        Title = Title,
                        UpVotes = (int)(Ups * Upvote_Ratio),
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)Created_Utc),
                    };
            }
        }
    }
}
