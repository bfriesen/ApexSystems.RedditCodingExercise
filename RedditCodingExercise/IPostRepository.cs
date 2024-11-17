namespace RedditCodingExercise;

public interface IPostRepository
{
    Task AddOrUpdatePostAsync(Post post, CancellationToken cancellationToken);

    Task<IReadOnlyList<Post>> GetPostsOrderedByUpVotesAsync(int numberOfPosts, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserPosts>> GetUserPostsOrderedByPostCountAsync(int numberOfUsers, CancellationToken cancellationToken);
}
