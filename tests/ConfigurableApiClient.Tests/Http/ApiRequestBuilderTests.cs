using System.Text;
using ConfigurableApiClient.Configuration;
using ConfigurableApiClient.Http;

namespace ConfigurableApiClient.Tests.Http;

public class ApiRequestBuilderTests
{
    private readonly ApiRequestBuilder _builder = new();

    private static ApiCallOptions BaseCall() => new()
    {
        Name = "Call1",
        Enabled = true,
        Method = "GET",
        Url = "https://example.org/api/resource",
        TimeoutSeconds = 30,
    };

    [Fact]
    public void Build_AppendsQueryParameters_WithProperEscaping()
    {
        var call = BaseCall();
        call.QueryParameters = new Dictionary<string, string>
        {
            ["name"] = "john doe",
            ["tag"] = "a&b",
        };

        using var request = _builder.Build(call);

        Assert.Equal(HttpMethod.Get, request.Method);
        var query = request.RequestUri!.Query;
        Assert.Contains("name=john+doe", query);
        Assert.Contains("tag=a%26b", query);
    }

    [Fact]
    public void Build_PreservesExistingQueryValues_WhenAppendingNewOnes()
    {
        var call = BaseCall();
        call.Url = "https://example.org/api/resource?existing=value";
        call.QueryParameters = new Dictionary<string, string> { ["extra"] = "1" };

        using var request = _builder.Build(call);

        var query = request.RequestUri!.Query;
        Assert.Contains("existing=value", query);
        Assert.Contains("extra=1", query);
    }

    [Fact]
    public void Build_WithBasicAuthEnabled_GeneratesCorrectBase64Header()
    {
        var call = BaseCall();
        call.BasicAuth = new BasicAuthOptions { Enabled = true, Username = "user", Password = "pass" };

        using var request = _builder.Build(call);

        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass"));
        Assert.Equal(expected, request.Headers.GetValues("Authorization").First());
    }

    [Fact]
    public void Build_AppliesConfiguredHeaders_IncludingEpicHeaders()
    {
        var call = BaseCall();
        call.Headers = new Dictionary<string, string>
        {
            ["Epic-Client-ID"] = "1234-1234-1234",
            ["Epic-User-IDType"] = "EXTERNAL",
        };

        using var request = _builder.Build(call);

        Assert.Equal("1234-1234-1234", request.Headers.GetValues("Epic-Client-ID").First());
        Assert.Equal("EXTERNAL", request.Headers.GetValues("Epic-User-IDType").First());
    }

    [Fact]
    public async Task Build_ForPost_SendsRawJsonAsUtf8ApplicationJson()
    {
        var call = BaseCall();
        call.Method = "POST";
        call.RawJsonBody = "{\"resourceType\":\"Appointment\"}";

        using var request = _builder.Build(call);

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.NotNull(request.Content);
        Assert.Equal("application/json", request.Content!.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", request.Content.Headers.ContentType?.CharSet);
        var body = await request.Content.ReadAsStringAsync();
        Assert.Equal(call.RawJsonBody, body);
    }

    [Fact]
    public void Build_ForPost_SupportsBothQueryParametersAndRawJsonBody()
    {
        var call = BaseCall();
        call.Method = "POST";
        call.QueryParameters = new Dictionary<string, string> { ["mode"] = "test" };
        call.RawJsonBody = "{\"a\":1}";

        using var request = _builder.Build(call);

        Assert.Contains("mode=test", request.RequestUri!.Query);
        Assert.NotNull(request.Content);
    }
}
