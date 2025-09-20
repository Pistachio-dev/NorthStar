using System.Net.Http.Headers;

namespace OrangeGuidanceTomestone.Helpers;

internal static class ServerHelper {
    private static readonly HttpClient Client = new();

    internal static HttpRequestMessage GetRequest(string? apiKey, HttpMethod method, string tail, string? contentType = null, HttpContent? content = null) {
        if (!tail.StartsWith('/')) {
            tail = '/' + tail;
        }

        var url = $"https://tryfingerbuthole.anna.lgbt{tail}";
        var req = new HttpRequestMessage(method, url);
        if (content != null) {
            req.Content = content;

            if (contentType != null) {
                req.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }
        }

        if (apiKey != null) {
            req.Headers.Add("X-Api-Key", apiKey);
        }

        return req;
    }

    internal static async Task<HttpResponseMessage> SendRequest(string? apiKey, HttpMethod method, string tail, string? contentType = null, HttpContent? content = null) {
        var req = GetRequest(apiKey, method, tail, contentType, content);
        return await Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
    }
}
