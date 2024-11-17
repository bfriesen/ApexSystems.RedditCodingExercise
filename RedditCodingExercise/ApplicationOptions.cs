namespace RedditCodingExercise;

public class ApplicationOptions
{
    /// <summary>
    /// The base address of the reddit api.
    /// </summary>
    public string RedditApiBaseAddress { get; set; } = null!;

    /// <summary>
    /// The url to use when authenticating with the reddit api.
    /// </summary>
    public string RedditApiAuthorizationUrl { get; set; } = null!;

    /// <summary>
    /// The name of the subreddit that this application monitors and reports on.
    /// </summary>
    public string SubRedditName { get; set; } = null!;

    /// <summary>
    /// The name the application, as registered with reddit.
    /// </summary>
    public string ApplicationName { get; set; } = null!;

    /// <summary>
    /// The version of the application.
    /// </summary>
    public string ApplicationVersion { get; } = "v0.0.1";

    /// <summary>
    /// The optional time, in unix time seconds, to use that the start time of the application. This value is used to filter out
    /// posts when listening to a subreddit - any posts before this time will not be processed. When null, the actual startup
    /// time of the application is used instead.
    /// </summary>
    public long? ApplicationStartTime { get; set; }

    /// <summary>
    /// The user name of the reddit user running this application.
    /// </summary>
    public string RedditUsername { get; set; } = null!;

    /// <summary>
    /// The password of the reddit user running this application.
    /// </summary>
    public string RedditPassword { get; set; } = null!;

    /// <summary>
    /// The client id of the reddit api app.
    /// </summary>
    public string RedditAppClientId { get; set; } = null!;

    /// <summary>
    /// The client secret of the reddit api app.
    /// </summary>
    public string RedditAppClientSecret { get; set; } = null!;
}
