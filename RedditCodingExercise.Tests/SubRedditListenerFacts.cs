using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace RedditCodingExercise.Tests;

public class SubRedditListenerFacts
{
    public class SynchronizePostsAsync
    {
        [Fact]
        public async Task GetsPostsFromRedditApiAndAddsThemToDataStore()
        {
            // Arrange
            var applicationOptions = GetApplicationOptions();
            var (post1, post2) = GetPosts();

            var mockRedditApiClient = new Mock<IRedditApiClient>();
            var mockPostRepository = new Mock<IPostRepository>();
            var mockOptionsMonitor = new Mock<IOptionsMonitor<ApplicationOptions>>();
            var mockLogger = new MockLogger<SubRedditListener>();

            mockRedditApiClient
                .Setup(m => m.GetPostsCreatedSinceStartupAsync(applicationOptions.SubRedditName, default))
                .Returns(new[] { post1, post2 }.ToAsyncEnumerable());

            mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(applicationOptions);

            var listener = new SubRedditListener(
                mockRedditApiClient.Object, mockPostRepository.Object, mockOptionsMonitor.Object, mockLogger.Object);

            // Act
            await listener.SynchronizePostsAsync(default);

            // Assert
            mockPostRepository.Verify(m => m.AddOrUpdatePostAsync(post1, default), Times.Once());
            mockPostRepository.Verify(m => m.AddOrUpdatePostAsync(post2, default), Times.Once());
            mockLogger.VerifyLog(log => log.AtError(), Times.Never());
        }

        [Fact]
        public async Task DoesNotThrowIfRedditApiClientThrows()
        {
            // Arrange
            var applicationOptions = GetApplicationOptions();

            var mockRedditApiClient = new Mock<IRedditApiClient>();
            var mockPostRepository = new Mock<IPostRepository>();
            var mockOptionsMonitor = new Mock<IOptionsMonitor<ApplicationOptions>>();
            var mockLogger = new MockLogger<SubRedditListener>();

            var myException = new InvalidOperationException("Z");

            mockRedditApiClient
                .Setup(m => m.GetPostsCreatedSinceStartupAsync(applicationOptions.SubRedditName, default))
                .Throws(myException);

            mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(applicationOptions);

            var listener = new SubRedditListener(
                mockRedditApiClient.Object, mockPostRepository.Object, mockOptionsMonitor.Object, mockLogger.Object);

            // Act
            var act = () => listener.SynchronizePostsAsync(default);

            // Assert
            await act.Should().NotThrowAsync();
            mockPostRepository.Verify(m => m.AddOrUpdatePostAsync(It.IsAny<Post>(), default), Times.Never());
            mockLogger.VerifyLog(log => log.AtError().WithException(ex => ex == myException), Times.Once());
        }

        [Fact]
        public async Task DoesNotThrowIfPostRepositoryThrows()
        {
            // Arrange
            var applicationOptions = GetApplicationOptions();
            var (post1, post2) = GetPosts();

            var mockRedditApiClient = new Mock<IRedditApiClient>();
            var mockPostRepository = new Mock<IPostRepository>();
            var mockOptionsMonitor = new Mock<IOptionsMonitor<ApplicationOptions>>();
            var mockLogger = new MockLogger<SubRedditListener>();

            mockRedditApiClient
                .Setup(m => m.GetPostsCreatedSinceStartupAsync(applicationOptions.SubRedditName, default))
                .Returns(new[] { post1, post2 }.ToAsyncEnumerable());

            var myException = new InvalidOperationException("Z");

            mockPostRepository.Setup(m => m.AddOrUpdatePostAsync(It.IsAny<Post>(), default))
                .ThrowsAsync(myException);

            mockOptionsMonitor.Setup(m => m.CurrentValue).Returns(applicationOptions);

            var listener = new SubRedditListener(
                mockRedditApiClient.Object, mockPostRepository.Object, mockOptionsMonitor.Object, mockLogger.Object);

            // Act
            var act = () => listener.SynchronizePostsAsync(default);

            // Assert
            await act.Should().NotThrowAsync();
            mockPostRepository.Verify(m => m.AddOrUpdatePostAsync(post1, default), Times.Once());
            mockPostRepository.Verify(m => m.AddOrUpdatePostAsync(post2, default), Times.Once());
            mockLogger.VerifyLog(log => log.AtError().WithException(ex => ex == myException), Times.Once());
        }
    }

    private static ApplicationOptions GetApplicationOptions() =>
        new()
        {
            ApplicationName = "A",
            ApplicationStartTime = 0,
            RedditAppClientId = "B",
            RedditAppClientSecret = "C",
            RedditPassword = "D",
            RedditUsername = "E",
            SubRedditName = "XYZ",
        };

    private static (Post, Post) GetPosts() =>
        (
            new Post
            {
                Author = "F",
                Name = "G",
                Permalink = "H",
                SubReddit = "XYZ",
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(5),
                Title = "I",
                UpVotes = 2,
            },
            new Post
            {
                Author = "J",
                Name = "K",
                Permalink = "L",
                SubReddit = "XYZ",
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(10),
                Title = "M",
                UpVotes = 1,
            }
        );
}
