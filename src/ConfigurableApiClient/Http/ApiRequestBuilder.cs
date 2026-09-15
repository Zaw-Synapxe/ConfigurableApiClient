using System.Text;
using ConfigurableApiClient.Configuration;

namespace ConfigurableApiClient.Http;

/// <summary>
/// Builds a fresh <see cref="HttpRequestMessage"/> for a single configured API call.
/// </summary>
public interface IApiRequestBuilder
{
    HttpRequestMessage Build(ApiCallOptions call);
}

public sealed class ApiRequestBuilder : IApiRequestBuilder
{
    public HttpRequestMessage Build(ApiCallOptions call)
    {
        ArgumentNullException.ThrowIfNull(call);

        var method = Enum.Parse<ApiCallMethod>(call.Method, ignoreCase: true);
        var uri = BuildUriWithQuery(call.Url, call.QueryParameters);

        var request = new HttpRequestMessage(
            method == ApiCallMethod.GET ? HttpMethod.Get : HttpMethod.Post,
            uri);

        foreach (var (headerName, headerValue) in call.Headers)
        {
            request.Headers.TryAddWithoutValidation(headerName, headerValue);
        }

        if (call.BasicAuth is { Enabled: true } basicAuth)
        {
            var raw = $"{basicAuth.Username}:{basicAuth.Password}";
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
            request.Headers.TryAddWithoutValidation("Authorization", $"Basic {encoded}");
        }

        if (method == ApiCallMethod.POST && !string.IsNullOrWhiteSpace(call.RawJsonBody))
        {
            request.Content = new StringContent(call.RawJsonBody, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static Uri BuildUriWithQuery(string url, IDictionary<string, string> queryParameters)
    {
        var builder = new UriBuilder(url);
        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);

        foreach (var (key, value) in queryParameters)
        {
            query[key] = value;
        }

        builder.Query = query.ToString();
        return builder.Uri;
    }
}
