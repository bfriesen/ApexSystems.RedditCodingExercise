using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Text;

namespace RedditCodingExercise.Tests;

public class RedditApiClientFacts
{
    public class GetPostsCreatedSinceStartupAsync
    {
        [Theory]
        [InlineData(3, 3)] // Verifies that we stop making requests because we found a post that was too old.
        [InlineData(0, 4)] // Verifies that we stop making requests because 'after' is null.
        public async Task ReturnsPostsMappedFromHttpRequests(int applicationStartTime, int expectedPostCount)
        {
            // Arrange
            var response1Data =
                """
                {
                  "data": {
                    "after":"A",
                    "children": [
                      {
                        "data": {
                          "name": "B",
                          "subreddit": "C",
                          "title": "D",
                          "author": "E",
                          "permalink": "F",
                          "created_utc": 8,
                          "ups": 10,
                          "upvote_ratio": 0.5
                        }
                      },
                      {
                        "data": {
                          "name": "G",
                          "subreddit": "H",
                          "title": "I",
                          "author": "J",
                          "permalink": "K",
                          "created_utc": 6,
                          "ups": 8,
                          "upvote_ratio": 0.75
                        }
                      }
                    ]
                  }
                }
                """;

            var response2Data =
                """
                {
                  "data": {
                    "children": [
                      {
                        "data": {
                          "name": "L",
                          "subreddit": "M",
                          "title": "N",
                          "author": "O",
                          "permalink": "P",
                          "created_utc": 4,
                          "ups": 9,
                          "upvote_ratio": 0.34
                        }
                      },
                      {
                        "data": {
                          "name": "Q",
                          "subreddit": "R",
                          "title": "S",
                          "author": "T",
                          "permalink": "U",
                          "created_utc": 2,
                          "ups": 2,
                          "upvote_ratio": -2.5
                        }
                      }
                    ]
                  }
                }
                """;

            using var stream1 = new MemoryStream();
            await stream1.WriteAsync(Encoding.UTF8.GetBytes(response1Data));
            stream1.Position = 0;
            using var testingResponse1 = new HttpResponseMessage { Content = new StreamContent(stream1), StatusCode = HttpStatusCode.OK };

            using var stream2 = new MemoryStream();
            await stream2.WriteAsync(Encoding.UTF8.GetBytes(response2Data));
            stream2.Position = 0;
            using var testingResponse2 = new HttpResponseMessage { Content = new StreamContent(stream2), StatusCode = HttpStatusCode.OK };

            var applicationOptions = new ApplicationOptions { ApplicationStartTime = applicationStartTime };

            using var testingMessageHandler = new TestingMessageHandler(testingResponse1, testingResponse2);
            using var httpClient = new HttpClient(testingMessageHandler) { BaseAddress = new Uri("https://example.com") };

            var mockOptionsMonitor = new Mock<IOptionsMonitor<ApplicationOptions>>();
            mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(applicationOptions);

            var mockLogger = new MockLogger<RedditApiClient>();

            var client = new RedditApiClient(httpClient, mockOptionsMonitor.Object, mockLogger.Object);
            var subReddit = "test";

            // Act
            var posts = await client.GetPostsCreatedSinceStartupAsync(subReddit, default).ToListAsync();

            // Assert
            try
            {
                posts.Should().HaveCount(expectedPostCount);

                posts[0].Name.Should().Be("B");
                posts[0].SubReddit.Should().Be("C");
                posts[0].Title.Should().Be("D");
                posts[0].Author.Should().Be("E");
                posts[0].Permalink.Should().Be("F");
                posts[0].Timestamp.Should().Be(DateTimeOffset.FromUnixTimeSeconds(8));
                posts[0].UpVotes.Should().Be(5);

                posts[1].Name.Should().Be("G");
                posts[1].SubReddit.Should().Be("H");
                posts[1].Title.Should().Be("I");
                posts[1].Author.Should().Be("J");
                posts[1].Permalink.Should().Be("K");
                posts[1].Timestamp.Should().Be(DateTimeOffset.FromUnixTimeSeconds(6));
                posts[1].UpVotes.Should().Be(6);

                posts[2].Name.Should().Be("L");
                posts[2].SubReddit.Should().Be("M");
                posts[2].Title.Should().Be("N");
                posts[2].Author.Should().Be("O");
                posts[2].Permalink.Should().Be("P");
                posts[2].Timestamp.Should().Be(DateTimeOffset.FromUnixTimeSeconds(4));
                posts[2].UpVotes.Should().Be(3);

                if (expectedPostCount > 3)
                {
                    posts[3].Name.Should().Be("Q");
                    posts[3].SubReddit.Should().Be("R");
                    posts[3].Title.Should().Be("S");
                    posts[3].Author.Should().Be("T");
                    posts[3].Permalink.Should().Be("U");
                    posts[3].Timestamp.Should().Be(DateTimeOffset.FromUnixTimeSeconds(2));
                    posts[3].UpVotes.Should().Be(-5);
                }

                testingMessageHandler.SentRequests.Should().HaveCount(2);

                testingMessageHandler.SentRequests[0].RequestUri?.PathAndQuery.Should().Be($"/r/{subReddit}/new?show=all");
                testingMessageHandler.SentRequests[1].RequestUri?.PathAndQuery.Should().Be($"/r/{subReddit}/new?show=all&count=2&after=A");
            }
            finally
            {
                foreach (var sentRequest in testingMessageHandler.SentRequests)
                    sentRequest.Dispose();
            }
        }

        [Fact]
        public async Task NonSuccessStatusCodeFromHttpClientTerminatesGettingPostsFromRedditApi()
        {
            // Arrange
            var response1Data =
                """
                {
                  "data": {
                    "after":"A",
                    "children": [
                      {
                        "data": {
                          "name": "B",
                          "subreddit": "C",
                          "title": "D",
                          "author": "E",
                          "permalink": "F",
                          "created_utc": 8,
                          "ups": 10,
                          "upvote_ratio": 0.5
                        }
                      },
                      {
                        "data": {
                          "name": "G",
                          "subreddit": "H",
                          "title": "I",
                          "author": "J",
                          "permalink": "K",
                          "created_utc": 6,
                          "ups": 8,
                          "upvote_ratio": 0.75
                        }
                      }
                    ]
                  }
                }
                """;

            using var stream1 = new MemoryStream();
            await stream1.WriteAsync(Encoding.UTF8.GetBytes(response1Data));
            stream1.Position = 0;
            using var testingResponse1 = new HttpResponseMessage { Content = new StreamContent(stream1), StatusCode = HttpStatusCode.OK };

            using var testingResponse2 = new HttpResponseMessage { StatusCode = HttpStatusCode.NotAcceptable, ReasonPhrase = "Not Acceptable" };

            var applicationOptions = new ApplicationOptions { ApplicationStartTime = 0 };

            using var testingMessageHandler = new TestingMessageHandler(testingResponse1, testingResponse2);
            using var httpClient = new HttpClient(testingMessageHandler) { BaseAddress = new Uri("https://example.com") };

            var mockOptionsMonitor = new Mock<IOptionsMonitor<ApplicationOptions>>();
            mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(applicationOptions);

            var mockLogger = new MockLogger<RedditApiClient>();

            var client = new RedditApiClient(httpClient, mockOptionsMonitor.Object, mockLogger.Object);
            var subReddit = "test";

            // Act
            var posts = await client.GetPostsCreatedSinceStartupAsync(subReddit, default).ToListAsync();

            // Assert
            posts.Should().HaveCount(2);

            posts[0].Name.Should().Be("B");
            posts[0].SubReddit.Should().Be("C");
            posts[0].Title.Should().Be("D");
            posts[0].Author.Should().Be("E");
            posts[0].Permalink.Should().Be("F");
            posts[0].Timestamp.Should().Be(DateTimeOffset.FromUnixTimeSeconds(8));
            posts[0].UpVotes.Should().Be(5);

            posts[1].Name.Should().Be("G");
            posts[1].SubReddit.Should().Be("H");
            posts[1].Title.Should().Be("I");
            posts[1].Author.Should().Be("J");
            posts[1].Permalink.Should().Be("K");
            posts[1].Timestamp.Should().Be(DateTimeOffset.FromUnixTimeSeconds(6));
            posts[1].UpVotes.Should().Be(6);

            testingMessageHandler.SentRequests.Should().HaveCount(2);

            testingMessageHandler.SentRequests[0].RequestUri?.PathAndQuery.Should().Be($"/r/{subReddit}/new?show=all");
            testingMessageHandler.SentRequests[1].RequestUri?.PathAndQuery.Should().Be($"/r/{subReddit}/new?show=all&count=2&after=A");

            mockLogger.VerifyLog(log => log.AtWarning(), Times.Once());
        }
    }

    public class StartTime
    {
        [Fact]
        public void IsSetInConstructorFromOptionsApplicationStartTimeWhenProvided()
        {
            // Arrange
            var applicationOptions = new ApplicationOptions { ApplicationStartTime = 123456 };

            using var testingMessageHandler = new TestingMessageHandler();
            using var httpClient = new HttpClient(testingMessageHandler);

            var mockOptionsMonitor = new Mock<IOptionsMonitor<ApplicationOptions>>();
            mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(applicationOptions);

            var mockLogger = new MockLogger<RedditApiClient>();

            // Act
            var client = new RedditApiClient(httpClient, mockOptionsMonitor.Object, mockLogger.Object);

            // Assert
            client.ApplicationStartTime.Should().Be(applicationOptions.ApplicationStartTime);
        }

        [Fact]
        public void IsSetInConstructorToNowWhenOptionsApplicationStartTimeNotProvided()
        {
            // Arrange
            var applicationOptions = new ApplicationOptions { ApplicationStartTime = null };

            using var testingMessageHandler = new TestingMessageHandler();
            using var httpClient = new HttpClient(testingMessageHandler);

            var mockOptionsMonitor = new Mock<IOptionsMonitor<ApplicationOptions>>();
            mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(applicationOptions);

            var mockLogger = new MockLogger<RedditApiClient>();

            var nowUnixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Act
            var client = new RedditApiClient(httpClient, mockOptionsMonitor.Object, mockLogger.Object);

            // Assert
            client.ApplicationStartTime.Should().BeCloseTo(nowUnixTimeSeconds, 1);
        }
    }
}
