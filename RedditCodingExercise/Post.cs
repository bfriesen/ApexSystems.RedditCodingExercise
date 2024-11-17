namespace RedditCodingExercise;

public class Post
{
    public string Name { get; set; } = null!;

    public string SubReddit { get; set; } = null!;

    public string Permalink { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Author { get; set; } = null!;

    public DateTimeOffset Timestamp { get; set; }

    public int UpVotes { get; set; }
}
