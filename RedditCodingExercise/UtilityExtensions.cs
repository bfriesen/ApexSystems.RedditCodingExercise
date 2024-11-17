namespace RedditCodingExercise;

internal static class UtilityExtensions
{
    public static async Task<HttpRequestMessage> CloneAsync(
        this HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpContent? content = null;
        if (request.Content != null)
        {
            var stream = new MemoryStream();
            await request.Content.CopyToAsync(stream, cancellationToken);
            stream.Position = 0;

            content = new StreamContent(stream);
            foreach (var header in content.Headers)
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Content = content,
            Version = request.Version
        };

        foreach (var option in request.Options)
            clone.Options.TryAdd(option.Key, option.Value);

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}
