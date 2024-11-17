using Microsoft.Extensions.Options;
using RedditCodingExercise;
using RedditCodingExercise.App;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure application options.
builder.Services.Configure<ApplicationOptions>(builder.Configuration.GetRequiredSection("ApplicationOptions"));

// Add our typed http client for the reddit api along with our custom message handlers for rate limiting and authentication.
builder.Services.AddSingleton<RateLimitingHttpMessageHandler>();
builder.Services.AddSingleton<AuthenticatingMessageHandler>();
builder.Services.AddHttpClient<IRedditApiClient, RedditApiClient>(
    (provider, httpClient) =>
    {
        var options = provider.GetRequiredService<IOptions<ApplicationOptions>>().Value;
        httpClient.BaseAddress = new Uri(options.RedditApiBaseAddress);
    })
    .AddHttpMessageHandler<RateLimitingHttpMessageHandler>()
    .AddHttpMessageHandler<AuthenticatingMessageHandler>();

// Add dependencies.
builder.Services.AddSingleton<IPostRepository, InMemoryPostRepository>();
builder.Services.AddSingleton<SubRedditListener>();

// Add our cron job and configure its options.
builder.Services.AddCronJob<SubRedditCronJob>(builder.Configuration.GetRequiredSection("CronOptions"));

// Include timestamp in console logging.
builder.Logging.AddSimpleConsole(options => options.TimestampFormat = "[M/d/yy h:mm:ss tt] ");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var subRedditName = app.Configuration["ApplicationOptions:SubRedditName"]
    ?? throw new InvalidOperationException("Configuration setting 'ApplicationOptions:SubRedditName' was not provided.");

// Add endpoint for posts ranked by up votes.
app.MapGet($"{subRedditName}/posts",
    async (IPostRepository postRepository, int? postCount, CancellationToken cancellationToken) =>
        new Listing<Post>(await postRepository.GetPostsOrderedByUpVotesAsync(postCount ?? 10, cancellationToken)))
    .WithName("GetPosts")
    .WithOpenApi();

// Add endpoint for users ranked by post count.
app.MapGet($"{subRedditName}/posts/users",
    async (IPostRepository postRepository, int? userCount, CancellationToken cancellationToken) =>
        new Listing<UserPosts>(await postRepository.GetUserPostsOrderedByPostCountAsync(userCount ?? 10, cancellationToken)))
    .WithName("GetPostsGroupedByUser")
    .WithOpenApi();

app.Run();

class Listing<T>(IReadOnlyList<T> data)
{
    public int Count => Data.Count;
    public IReadOnlyList<T> Data { get; } = data;
}