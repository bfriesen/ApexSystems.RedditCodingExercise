using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace RedditCodingExercise;

public class InMemoryPostRepository(ILogger<InMemoryPostRepository> logger)
    : IPostRepository
{
    private readonly ConcurrentDictionary<string, Post> _posts = new();
    private readonly ILogger<InMemoryPostRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // Public for testing.
    public IReadOnlyDictionary<string, Post> Posts => _posts.AsReadOnly();

    public Task AddOrUpdatePostAsync(Post post, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(post, nameof(post));

        _posts.AddOrUpdate(
            post.Name,
            (name, t) =>
            {
                t.Logger.LogInformation(
                    "Added post '{PostName}'. Author: {Author}, Up Votes: {UpVotes}.",
                    name, t.Post.Author, t.Post.UpVotes);

                return t.Post;
            },
            (name, previousPost, t) =>
            {
                if (t.Post.UpVotes != previousPost.UpVotes)
                {
                    t.Logger.LogInformation(
                        "Updated post '{PostName}'. Author: {Author}, Up Votes: {Previous} -> {UpVotes}.",
                        name, t.Post.Author, previousPost.UpVotes, t.Post.UpVotes);

                    previousPost.UpVotes = t.Post.UpVotes;
                }

                return previousPost;
            },
            (Post: post, Logger: _logger));

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Post>> GetPostsOrderedByUpVotesAsync(int numberOfPosts, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numberOfPosts, nameof(numberOfPosts));

        IReadOnlyList<Post> posts = _posts.Values
            .OrderByDescending(p => p.UpVotes)
            .Take(numberOfPosts)
            .ToArray();

        return Task.FromResult(posts);
    }

    public Task<IReadOnlyList<UserPosts>> GetUserPostsOrderedByPostCountAsync(int numberOfUsers, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numberOfUsers, nameof(numberOfUsers));

        IReadOnlyList<UserPosts> users = _posts.Values
            .GroupBy(p => p.Author)
            .OrderByDescending(g => g.Count())
            .Take(numberOfUsers)
            .Select(g => new UserPosts { Author = g.Key, Posts = [.. g] })
            .ToArray();

        return Task.FromResult(users);
    }
}
