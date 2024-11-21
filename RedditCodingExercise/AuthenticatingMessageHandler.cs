using System.Net.Http.Headers;
using System.Text;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace RedditCodingExercise;

// Based on documentation here: https://github.com/reddit-archive/reddit/wiki/OAuth2-Quick-Start-Example.
public class AuthenticatingMessageHandler(
    IOptionsMonitor<ApplicationOptions> options,
    ILogger<AuthenticatingMessageHandler> logger)
    : DelegatingHandler
{
    public const int ExpirationBufferSeconds = 600;

    // Passing one configures the semaphore to allow one thread access to the code it protects.
    private readonly SemaphoreSlim _semaphore = new(1);

    private readonly IOptionsMonitor<ApplicationOptions> _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<AuthenticatingMessageHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private string? _accessToken;
    private DateTimeOffset? _accessTokenExpiration;

    // Public and virtual for testing.
    public virtual DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    // Public for testing.
    public static string GetUserAgent(ApplicationOptions options) =>
        $"{options.ApplicationName}/{options.ApplicationVersion} by {options.RedditUsername}";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Make sure our access token is set and fresh.
        if ((_accessToken, _accessTokenExpiration) is (null, null) || _accessTokenExpiration <= UtcNow)
            await SetAccessToken(cancellationToken);

        // Set required headers.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.Add("User-Agent", GetUserAgent(_options.CurrentValue));

        // Make the actual request.
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task SetAccessToken(CancellationToken cancellationToken)
    {
        // Only let one thread set the access token at a time.
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            // Double check for access token expiration in case the current thread had been blocked
            // while a different thread was setting the access token.
            if ((_accessToken, _accessTokenExpiration) is not (null, null) && _accessTokenExpiration > UtcNow)
                return;

            var options = _options.CurrentValue;
            var authorization = $"{options.RedditAppClientId}:{options.RedditAppClientSecret}";
            var encodedAuthorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(authorization));

            var request = new HttpRequestMessage(HttpMethod.Post, options.RedditApiAuthorizationUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedAuthorization);
            request.Headers.Add("User-Agent", GetUserAgent(options));
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = options.RedditUsername,
                ["password"] = options.RedditPassword,
            });

            var response = await base.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var authenticationResponse = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(cancellationToken)
                ?? throw new InvalidOperationException($"Unexpected null JSON content for {nameof(AuthenticationResponse)}.");

            var expiration = UtcNow.AddSeconds(authenticationResponse.expires_in - ExpirationBufferSeconds);
            _accessTokenExpiration = expiration;
            _accessToken = authenticationResponse.access_token;

            _logger.LogInformation("Access token set, will expire at {AccessTokenExpiration}.", expiration);
        }
        finally
        {
            // Let other threads set the access token again.
            _semaphore.Release();
        }
    }

#pragma warning disable IDE1006
    private class AuthenticationResponse
    {
        public string access_token { get; set; } = null!;
        public int expires_in { get; set; }
    }
}
