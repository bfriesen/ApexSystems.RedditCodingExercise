using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Text;
using Xunit.Sdk;

namespace RedditCodingExercise.Tests;

public class AuthenticatingMessageHandlerFacts
{
    public class SendAsync
    {
        private class AuthenticatingMessageHandlerForTests(
            IOptionsMonitor<ApplicationOptions> options,
            ILogger<AuthenticatingMessageHandler> logger,
            params DateTimeOffset[] nowTimestamps)
            : AuthenticatingMessageHandler(options, logger)
        {
            private readonly DateTimeOffset[] _nowTimestamps = nowTimestamps;
            private int _index = -1;

            // We need to be able to change what "now" means in our tests.
            public override DateTimeOffset UtcNow => _nowTimestamps[++_index];

            // The SendAsync method that we want to test is protected, so create a public overload without the cancellation token. 
            public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) => SendAsync(request, default);
        }

        [Fact]
        public async Task AccessTokenIsSetOnFirstCallButNotOnSubsequentCalls()
        {
            // Arrange

            // Our access token will expire in 10 seconds after the first request.
            var expiresIn = AuthenticatingMessageHandler.ExpirationBufferSeconds + 10;
            var accessToken = "abc";

            // The first request is made to authenticate. This will be its response.
            var response1Data = $$"""{"expires_in":{{expiresIn}},"access_token":"{{accessToken}}"}""";
            using var stream1 = new MemoryStream();
            await stream1.WriteAsync(Encoding.UTF8.GetBytes(response1Data));
            stream1.Position = 0;
            using var response1 = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream1) };

            // This will be the actual response to the first request.
            using var response2 = new HttpResponseMessage(HttpStatusCode.OK);

            // This will be the response to the second request.
            using var response3 = new HttpResponseMessage(HttpStatusCode.OK);

            var applicationOptions = new ApplicationOptions
            {
                ApplicationName = "A",
                RedditApiAuthorizationUrl = "B",
                RedditAppClientId = "C",
                RedditAppClientSecret = "D",
                RedditPassword = "E",
                RedditUsername = "F",
            };

            var mockOptionsMonitor = new Mock<IOptionsMonitor<ApplicationOptions>>();
            mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(applicationOptions);

            var mockLogger = new MockLogger<AuthenticatingMessageHandler>();

            var testingMessageHandler = new TestingMessageHandler(response1, response2, response3);

            // This is the time of the first request.
            var firstRequestTime = DateTimeOffset.UtcNow;

            // The time of the second request is 5 seconds after the first, but before
            // the access token expiration which is 10 seconds after the first request,
            // so an authentication request won't need to be made.
            var secondRequestTime = firstRequestTime.AddSeconds(5);

            var handler = new AuthenticatingMessageHandlerForTests(
                mockOptionsMonitor.Object, mockLogger.Object,
                firstRequestTime, secondRequestTime)
            {
                InnerHandler = testingMessageHandler,
            };

            // Act
            var actualResponse1 = await handler.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/test1"));
            var actualResponse2 = await handler.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/test2"));

            // Assert
            actualResponse1.Should().Be(response2);
            actualResponse2.Should().Be(response3);

            testingMessageHandler.SentRequests.Should().HaveCount(3);

            var expectedAuthorizationParameter = $"{applicationOptions.RedditAppClientId}:{applicationOptions.RedditAppClientSecret}";
            string expectedUserAgent = AuthenticatingMessageHandler.GetUserAgent(applicationOptions);

            // The first sent request should be to authenticate.
            var sentRequest1 = testingMessageHandler.SentRequests[0];
            sentRequest1.RequestUri!.ToString().Should().Be(applicationOptions.RedditApiAuthorizationUrl);
            sentRequest1.Headers.Authorization!.Scheme.Should().Be("Basic");
            GetAuthorizationParameter(sentRequest1).Should().Be(expectedAuthorizationParameter);
            GetUserAgent(sentRequest1).Should().Be(expectedUserAgent);
            sentRequest1.Content.Should().BeOfType<FormUrlEncodedContent>();

            // The second sent request should be our first request, with authorization and user-agent headers set.
            HttpRequestMessage sentRequest2 = testingMessageHandler.SentRequests[1];
            sentRequest2.RequestUri!.ToString().Should().Be("/test1");
            sentRequest2.Headers.Authorization!.Scheme.Should().Be("Bearer");
            sentRequest2.Headers.Authorization!.Parameter.Should().Be(accessToken);
            GetUserAgent(sentRequest2).Should().Be(expectedUserAgent);

            // The third sent request should be our second request, with same authorization and user-agent headers.
            HttpRequestMessage sentRequest3 = testingMessageHandler.SentRequests[2];
            sentRequest3.RequestUri!.ToString().Should().Be("/test2");
            sentRequest3.Headers.Authorization!.Scheme.Should().Be("Bearer");
            sentRequest3.Headers.Authorization!.Parameter.Should().Be(accessToken);
            GetUserAgent(sentRequest3).Should().Be(expectedUserAgent);
        }

        [Fact]
        public async Task AccessTokenIsRefreshedWhenExpired()
        {
            // Arrange
            
            // Our access token will expire in 10 seconds after the first request.
            var expiresIn1 = AuthenticatingMessageHandler.ExpirationBufferSeconds + 10;
            var accessToken1 = "abc";
            
            // The first request is made to authenticate. This will be its response.
            var response1Data = $$"""{"expires_in":{{expiresIn1}},"access_token":"{{accessToken1}}"}""";
            using var stream1 = new MemoryStream();
            await stream1.WriteAsync(Encoding.UTF8.GetBytes(response1Data));
            stream1.Position = 0;
            using var response1 = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream1) };

            // This will be the actual response to the first request.
            using var response2 = new HttpResponseMessage(HttpStatusCode.OK);

            // Our next access token will expire in 10 seconds after the second request.
            var expiresIn2 = AuthenticatingMessageHandler.ExpirationBufferSeconds + 10;
            var accessToken2 = "xyz";

            // The third request is made to authenticate again. This will be its response.
            var response3Data = $$"""{"expires_in":{{expiresIn2}},"access_token":"{{accessToken2}}"}""";
            using var stream3 = new MemoryStream();
            await stream3.WriteAsync(Encoding.UTF8.GetBytes(response3Data));
            stream3.Position = 0;
            using var response3 = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream3) };

            // This will be the response to the second request.
            using var response4 = new HttpResponseMessage(HttpStatusCode.OK);

            var applicationOptions = new ApplicationOptions
            {
                ApplicationName = "A",
                RedditApiAuthorizationUrl = "B",
                RedditAppClientId = "C",
                RedditAppClientSecret = "D",
                RedditPassword = "E",
                RedditUsername = "F",
            };

            var mockOptionsMonitor = new Mock<IOptionsMonitor<ApplicationOptions>>();
            mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(applicationOptions);

            var mockLogger = new MockLogger<AuthenticatingMessageHandler>();

            var testingMessageHandler = new TestingMessageHandler(response1, response2, response3, response4);

            // This is the time of the first request.
            var firstRequestTime = DateTimeOffset.UtcNow;

            // This time of the second request is 20 seconds, which is after the access
            // token expiration which is 10 seconds after the first request, so an
            // authentication request will need to be made for the second request.
            var secondRequestTime = firstRequestTime.AddSeconds(20);

            var handler = new AuthenticatingMessageHandlerForTests(
                mockOptionsMonitor.Object, mockLogger.Object,
                firstRequestTime, secondRequestTime, secondRequestTime)
            {
                InnerHandler = testingMessageHandler,
            };

            // Act
            var actualResponse1 = await handler.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/test1"));
            var actualResponse2 = await handler.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/test2"));

            // Assert
            actualResponse1.Should().Be(response2);
            actualResponse2.Should().Be(response4);

            testingMessageHandler.SentRequests.Should().HaveCount(4);

            var expectedAuthorizationParameter = $"{applicationOptions.RedditAppClientId}:{applicationOptions.RedditAppClientSecret}";
            var expectedUserAgent = AuthenticatingMessageHandler.GetUserAgent(applicationOptions);

            // The first sent request should be to authenticate.
            var sentRequest1 = testingMessageHandler.SentRequests[0];
            sentRequest1.RequestUri!.ToString().Should().Be(applicationOptions.RedditApiAuthorizationUrl);
            sentRequest1.Headers.Authorization!.Scheme.Should().Be("Basic");
            GetAuthorizationParameter(sentRequest1).Should().Be(expectedAuthorizationParameter);
            GetUserAgent(sentRequest1).Should().Be(expectedUserAgent);
            sentRequest1.Content.Should().BeOfType<FormUrlEncodedContent>();

            // The second sent request should be our first request, with authorization and user-agent headers set.
            HttpRequestMessage sentRequest2 = testingMessageHandler.SentRequests[1];
            sentRequest2.RequestUri!.ToString().Should().Be("/test1");
            sentRequest2.Headers.Authorization!.Scheme.Should().Be("Bearer");
            sentRequest2.Headers.Authorization!.Parameter.Should().Be(accessToken1);
            GetUserAgent(sentRequest2).Should().Be(expectedUserAgent);

            // The third sent request should be to authenticate.
            HttpRequestMessage sentRequest3 = testingMessageHandler.SentRequests[2];
            sentRequest3.RequestUri!.ToString().Should().Be(applicationOptions.RedditApiAuthorizationUrl);
            sentRequest3.Headers.Authorization!.Scheme.Should().Be("Basic");
            GetAuthorizationParameter(sentRequest1).Should().Be(expectedAuthorizationParameter);
            GetUserAgent(sentRequest3).Should().Be(expectedUserAgent);
            sentRequest1.Content.Should().BeOfType<FormUrlEncodedContent>();

            // The fourth sent request should be our second request, with different authorization, but same user-agent headers.
            HttpRequestMessage sentRequest4 = testingMessageHandler.SentRequests[3];
            sentRequest4.RequestUri!.ToString().Should().Be("/test2");
            sentRequest4.Headers.Authorization!.Scheme.Should().Be("Bearer");
            sentRequest4.Headers.Authorization!.Parameter.Should().Be(accessToken2);
            GetUserAgent(sentRequest4).Should().Be(expectedUserAgent);
        }

        private static string GetAuthorizationParameter(HttpRequestMessage request)
        {
            var encodedAuthorization = request.Headers.Authorization!.Parameter;
            return Encoding.UTF8.GetString(Convert.FromBase64String(encodedAuthorization!));
        }

        private static string GetUserAgent(HttpRequestMessage request)
        {
            if (!request.Headers.TryGetValues("User-Agent", out var userAgentValues))
                throw new XunitException("Request does not contain User-Agent header.");

            return string.Join(" ", userAgentValues);
        }
    }
}
