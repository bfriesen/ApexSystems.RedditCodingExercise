using Microsoft.Extensions.Logging;
using System.Net;

namespace RedditCodingExercise;

public class RateLimitingHttpMessageHandler(ILogger<RateLimitingHttpMessageHandler> logger)
    : DelegatingHandler
{
    private readonly ILogger<RateLimitingHttpMessageHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private DateTimeOffset _delayUntil = DateTimeOffset.MinValue;

    // public and virtual for testing.
    public virtual DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    // Public and virtual for testing.
    public virtual Task Delay(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, cancellationToken);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Check to see if we need to immediately delay because the server told us we're being rate limited.
        var delayTime = _delayUntil - UtcNow;
        if (delayTime > TimeSpan.Zero)
        {
            _logger.LogInformation(
                "Delaying for {DelayTime} because the server indicated that our previous request was the last one before it "
                    + "would begin rate limiting us.",
                delayTime);
            await Delay(delayTime, cancellationToken);
        }

        // Make the actual request.
        var response = await base.SendAsync(request, cancellationToken);

        // Check the headers to see if we have exceeded our rate limit.
        while (response.Headers.TryGetValues("X-Ratelimit-Remaining", out var remainingRequestsValues)
            && double.TryParse(remainingRequestsValues.First(), out var remainingRequests)
            && remainingRequests < 1
            && response.Headers.TryGetValues("X-Ratelimit-Reset", out var resetSecondsValues)
            && double.TryParse(resetSecondsValues.First(), out var resetSeconds))
        {
            // Set the _delayUntil field so any subsequent requests will know to delay until the rate limit resets.
            _delayUntil = UtcNow.AddSeconds(resetSeconds);

            if (response.StatusCode != HttpStatusCode.TooManyRequests)
                break;

            // If the request we just made failed because we were over the rate limit, wait until
            // the rate limit resets, then try the request again.
            delayTime = TimeSpan.FromSeconds(resetSeconds);
            _logger.LogInformation(
                "Delaying for {DelayTime} because the server returned a 429 (Too Many Requests) response.",
                delayTime);
            await Delay(delayTime, cancellationToken);

            // Sending an HttpRequestMessage more than once results in an exception, so we need
            // to clone our request before retrying it.
            request = await request.CloneAsync(cancellationToken);

            // Retry the cloned request.
            response = await base.SendAsync(request, cancellationToken);
        }

        return response;
    }
}
