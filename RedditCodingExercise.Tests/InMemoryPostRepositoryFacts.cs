using FluentAssertions;
using Moq;

namespace RedditCodingExercise.Tests;

public class InMemoryPostRepositoryFacts
{
    public class AddOrUpdatePostAsync
    {
        [Fact]
        public async Task GivenNewPostsAddsThemToDictionary()
        {
            // Arrange
            var mockLogger = new MockLogger<InMemoryPostRepository>();

            var repository = new InMemoryPostRepository(mockLogger.Object);

            var (post1, post2) = GetPosts();

            // Act
            await repository.AddOrUpdatePostAsync(post1, default);
            await repository.AddOrUpdatePostAsync(post2, default);

            // Assert
            repository.Posts.Should().HaveCount(2);
            repository.Posts.Should().ContainKey(post1.Name).WhoseValue.Should().Be(post1);
            repository.Posts.Should().ContainKey(post2.Name).WhoseValue.Should().Be(post2);
        }

        [Fact]
        public async Task GivenExistingPostUpdatesDictionary()
        {
            // Arrange
            var mockLogger = new MockLogger<InMemoryPostRepository>();

            var repository = new InMemoryPostRepository(mockLogger.Object);

            var (post1, post2) = GetPosts();

            await repository.AddOrUpdatePostAsync(post1, default);
            await repository.AddOrUpdatePostAsync(post2, default);

            var post3 = new Post
            {
                Author = post1.Author,
                Name = post1.Name,
                Permalink = post1.Permalink,
                SubReddit = post1.SubReddit,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(15),
                Title = post1.Title,
                UpVotes = 7,
            };

            // Act
            await repository.AddOrUpdatePostAsync(post3, default);

            // Assert
            repository.Posts.Should().HaveCount(2);
            repository.Posts.Should().ContainKey(post1.Name).WhoseValue.Should().Be(post1);
            repository.Posts.Should().ContainKey(post2.Name).WhoseValue.Should().Be(post2);
            post1.UpVotes.Should().Be(post3.UpVotes);
        }
    }

    public class GetPostsOrderedByUpVotesAsync
    {
        [Theory]
        [InlineData(2, 2)]
        [InlineData(5, 3)]
        public async Task ReturnsCorrectNumberOfPostsInCorrectOrder(int numberOfPosts, int expectedNumberOfPosts)
        {
            // Arrange
            var mockLogger = new MockLogger<InMemoryPostRepository>();

            var repository = new InMemoryPostRepository(mockLogger.Object);

            var (post1, post2) = GetPosts();
            var post3 = new Post
            {
                Author = "I",
                Name = "J",
                Permalink = "K",
                SubReddit = "XYZ",
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(15),
                Title = "L",
                UpVotes = 1,
            };

            await repository.AddOrUpdatePostAsync(post1, default);
            await repository.AddOrUpdatePostAsync(post2, default);
            await repository.AddOrUpdatePostAsync(post3, default);

            // Act
            var posts = await repository.GetPostsOrderedByUpVotesAsync(numberOfPosts, default);

            // Assert
            posts.Should().HaveCount(expectedNumberOfPosts);
            posts[0].Should().Be(post2);
            posts[1].Should().Be(post1);
            if (expectedNumberOfPosts > 2)
                posts[2].Should().Be(post3);
        }
    }

    public class GetUserPostsOrderedByPostCountAsync
    {
        [Theory]
        [InlineData(1, 1)]
        [InlineData(5, 2)]
        public async Task ReturnsCorrectNumberOfUserPostsInCorrectOrder(int numberOfUsers, int expectedNumberOfUsers)
        {
            // Arrange
            var mockLogger = new MockLogger<InMemoryPostRepository>();

            var repository = new InMemoryPostRepository(mockLogger.Object);

            var (post1, post2) = GetPosts();
            var post3 = new Post
            {
                Author = post1.Author,
                Name = "I",
                Permalink = "J",
                SubReddit = "XYZ",
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(15),
                Title = "K",
                UpVotes = 1,
            };

            await repository.AddOrUpdatePostAsync(post1, default);
            await repository.AddOrUpdatePostAsync(post2, default);
            await repository.AddOrUpdatePostAsync(post3, default);

            // Act
            var userPosts = await repository.GetUserPostsOrderedByPostCountAsync(numberOfUsers, default);

            // Assert
            userPosts.Should().HaveCount(expectedNumberOfUsers);
            userPosts[0].Author.Should().Be(post1.Author).And.Be(post3.Author);
            userPosts[0].Posts.Should().HaveCount(2);
            userPosts[0].Posts.Should().BeEquivalentTo([post1, post3]);
            if (expectedNumberOfUsers > 1)
            {
                userPosts[1].Author.Should().Be(post2.Author);
                userPosts[1].Posts.Should().HaveCount(1);
                userPosts[1].Posts.Should().BeEquivalentTo([post2]);
            }
        }
    }

    private static (Post, Post) GetPosts() =>
        (
            new Post
            {
                Author = "A",
                Name = "B",
                Permalink = "C",
                SubReddit = "XYZ",
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(5),
                Title = "D",
                UpVotes = 2,
            },
            new Post
            {
                Author = "E",
                Name = "F",
                Permalink = "G",
                SubReddit = "XYZ",
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(10),
                Title = "H",
                UpVotes = 3,
            }
        );
}
