using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;

namespace RedditCodingExercise.Tests;

public class RateLimitingHttpMessageHandlerFacts
{
    public class SendAsync
    {
        private class RateLimitingHttpMessageHandlerForTests(
            Action<string> delayCallback,
            ILogger<RateLimitingHttpMessageHandler> logger,
            DateTimeOffset utcNow)
            : RateLimitingHttpMessageHandler(logger)
        {
            private readonly Action<string> _delayCallback = delayCallback;

            // We need to be able to change what "now" means in our tests.
            public override DateTimeOffset UtcNow { get; } = utcNow;

            // We don't actually want to delay when we're running tests.
            public override Task Delay(TimeSpan delay, CancellationToken cancellationToken)
            {
                _delayCallback($"{nameof(Delay)}: {delay.TotalSeconds}");
                return Task.CompletedTask;
            }

            // The SendAsync method that we want to test is protected, so create a public overload without the cancellation token. 
            public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) => SendAsync(request, default);
        }

        [Fact]
        public async Task ResponseIndicatingRateLimitReachedCausesDelayBeforeNextRequest()
        {
            // Arrange
            var response1 = new HttpResponseMessage(HttpStatusCode.OK);
            response1.Headers.TryAddWithoutValidation("X-Ratelimit-Remaining", "1"); // Rate limit not yet reached.
            response1.Headers.TryAddWithoutValidation("X-Ratelimit-Reset", "15");

            var response2 = new HttpResponseMessage(HttpStatusCode.OK);
            response2.Headers.TryAddWithoutValidation("X-Ratelimit-Remaining", "0"); // Rate limit reached.
            response2.Headers.TryAddWithoutValidation("X-Ratelimit-Reset", "10"); // Rate limit resets in 10 seconds.

            var response3 = new HttpResponseMessage(HttpStatusCode.OK);
            response3.Headers.TryAddWithoutValidation("X-Ratelimit-Remaining", "100"); // Rate limit no longer reached.
            response3.Headers.TryAddWithoutValidation("X-Ratelimit-Reset", "60");

            var mockLogger = new MockLogger<RateLimitingHttpMessageHandler>();

            // Collect messages from our test message handlers in this list.
            var callbackMessages = new List<string>();

            // This is the InnerHandler of the handler we're testing. When it "sends" a message,
            // it calls our list's Add method, so we'll know the order messages are sent.
            var testingMessageHandler = new TestingMessageHandler(callbackMessages.Add, response1, response2, response3);

            var now = DateTimeOffset.UtcNow;

            // This is the handler under test. When it "delays", it calls our list's Add method,
            // so we'll know when a message is delayed.
            var handler = new RateLimitingHttpMessageHandlerForTests(callbackMessages.Add, mockLogger.Object, now)
            {
                InnerHandler = testingMessageHandler
            };

            // Act
            await handler.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/test1"));
            await handler.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/test2"));
            await handler.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/test3"));

            // Assert
            callbackMessages.Should().HaveCount(4);

            callbackMessages[0].Should().Be("SendAsync: /test1"); // Should match url from first request.
            callbackMessages[1].Should().Be("SendAsync: /test2"); // Should match url from second request.
            callbackMessages[2].Should().Be("Delay: 10"); // Should match X-Ratelimit-Reset header from second response.
            callbackMessages[3].Should().Be("SendAsync: /test3"); // Should match url from third request.
        }

        [Fact]
        public async Task ResponsesWithStatusCode429AreRetriedAfterDelay()
        {
            // Arrange
            var response1 = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response1.Headers.TryAddWithoutValidation("X-Ratelimit-Remaining", "0"); // Rate limit reached.
            response1.Headers.TryAddWithoutValidation("X-Ratelimit-Reset", "10"); // Rate limit resets in 10 seconds.

            var response2 = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response2.Headers.TryAddWithoutValidation("X-Ratelimit-Remaining", "0"); // Rate limit (still) reached.
            response2.Headers.TryAddWithoutValidation("X-Ratelimit-Reset", "5");

            var response3 = new HttpResponseMessage(HttpStatusCode.OK);
            response3.Headers.TryAddWithoutValidation("X-Ratelimit-Remaining", "100"); // Rate limit no longer reached.
            response3.Headers.TryAddWithoutValidation("X-Ratelimit-Reset", "60");

            var mockLogger = new MockLogger<RateLimitingHttpMessageHandler>();

            // Collect messages from our test message handlers in this list.
            var callbackMessages = new List<string>();

            // This is the InnerHandler of the handler we're testing. When it "sends" a message,
            // it calls our list's Add method, so we'll know the order messages are sent.
            var testingMessageHandler = new TestingMessageHandler(callbackMessages.Add, response1, response2, response3);

            var now = DateTimeOffset.UtcNow;

            // This is the handler under test. When it "delays", it calls our list's Add method,
            // so we'll know when a message is delayed.
            var handler = new RateLimitingHttpMessageHandlerForTests(callbackMessages.Add, mockLogger.Object, now)
            {
                InnerHandler = testingMessageHandler
            };

            // Act
            await handler.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/test1"));

            // Assert
            callbackMessages.Should().HaveCount(5);

            callbackMessages[0].Should().Be("SendAsync: /test1"); // Should match url from request.
            callbackMessages[1].Should().Be("Delay: 10"); // Should match X-Ratelimit-Reset header from first response.
            callbackMessages[2].Should().Be("SendAsync: /test1"); // Should match url from request.
            callbackMessages[3].Should().Be("Delay: 5"); // Should match X-Ratelimit-Reset header from second response.
            callbackMessages[4].Should().Be("SendAsync: /test1"); // Should match url from request.
        }
    }
}
