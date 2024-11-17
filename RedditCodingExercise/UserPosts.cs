namespace RedditCodingExercise;

public class UserPosts
{
    public string Author { get; set; } = null!;

    public int PostCount => Posts.Count;

    public IReadOnlyList<Post> Posts { get; set; } = null!;
}
