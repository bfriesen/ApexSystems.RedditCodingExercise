namespace RedditCodingExercise.Tests;

internal class TestingMessageHandler(Action<string>? sendAsyncCallback, params HttpResponseMessage[] responses)
    : DelegatingHandler
{
    private readonly Action<string>? _sendAsyncCallback = sendAsyncCallback;
    private readonly HttpResponseMessage[] _responses = responses;
    private readonly List<HttpRequestMessage> _sentRequests = [];

    public TestingMessageHandler(params HttpResponseMessage[] responses)
        : this(null, responses)
    {
    }

    public IReadOnlyList<HttpRequestMessage> SentRequests => _sentRequests;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = _responses[_sentRequests.Count];
        _sentRequests.Add(request);
        _sendAsyncCallback?.Invoke($"{nameof(SendAsync)}: {request.RequestUri}");
        return Task.FromResult(response);
    }
}
