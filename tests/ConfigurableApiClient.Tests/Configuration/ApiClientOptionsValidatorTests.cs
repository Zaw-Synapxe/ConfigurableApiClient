using ConfigurableApiClient.Configuration;
using Microsoft.Extensions.Options;

namespace ConfigurableApiClient.Tests.Configuration;

public class ApiClientOptionsValidatorTests
{
    private readonly ApiClientOptionsValidator _validator = new();

    private static ApiCallOptions ValidCall(string name = "Call1") => new()
    {
        Name = name,
        Enabled = true,
        Method = "GET",
        Url = "https://example.org/api",
        TimeoutSeconds = 30,
    };

    [Fact]
    public void Validate_WithNoCalls_Fails()
    {
        var options = new ApiClientOptions { ApiCalls = new List<ApiCallOptions>() };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_WithValidCall_Succeeds()
    {
        var options = new ApiClientOptions { ApiCalls = new List<ApiCallOptions> { ValidCall() } };

        var result = _validator.Validate(null, options);

        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_WithMissingName_Fails()
    {
        var call = ValidCall();
        call.Name = "";
        var options = new ApiClientOptions { ApiCalls = new List<ApiCallOptions> { call } };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("Name is required"));
    }

    [Fact]
    public void Validate_WithDuplicateNames_Fails()
    {
        var options = new ApiClientOptions
        {
            ApiCalls = new List<ApiCallOptions> { ValidCall("Dup"), ValidCall("Dup") },
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("duplicate call name"));
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("")]
    public void Validate_WithUnsupportedMethod_Fails(string method)
    {
        var call = ValidCall();
        call.Method = method;
        var options = new ApiClientOptions { ApiCalls = new List<ApiCallOptions> { call } };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.org")]
    [InlineData("")]
    public void Validate_WithInvalidUrl_Fails(string url)
    {
        var call = ValidCall();
        call.Url = url;
        var options = new ApiClientOptions { ApiCalls = new List<ApiCallOptions> { call } };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_WithIncompleteBasicAuth_Fails()
    {
        var call = ValidCall();
        call.BasicAuth = new BasicAuthOptions { Enabled = true, Username = "user", Password = null };
        var options = new ApiClientOptions { ApiCalls = new List<ApiCallOptions> { call } };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("BasicAuth"));
    }

    [Fact]
    public void Validate_WithAuthorizationHeaderAndBasicAuthEnabled_Fails()
    {
        var call = ValidCall();
        call.BasicAuth = new BasicAuthOptions { Enabled = true, Username = "u", Password = "p" };
        call.Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer xyz" };
        var options = new ApiClientOptions { ApiCalls = new List<ApiCallOptions> { call } };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("conflicts with enabled BasicAuth"));
    }

    [Fact]
    public void Validate_WithMalformedJsonBody_Fails()
    {
        var call = ValidCall();
        call.Method = "POST";
        call.RawJsonBody = "{ not valid json";
        var options = new ApiClientOptions { ApiCalls = new List<ApiCallOptions> { call } };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("not valid JSON"));
    }

    [Fact]
    public void Validate_WithGetRequestContainingBody_Fails()
    {
        var call = ValidCall();
        call.Method = "GET";
        call.RawJsonBody = "{\"a\":1}";
        var options = new ApiClientOptions { ApiCalls = new List<ApiCallOptions> { call } };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("GET requests must not include a RawJsonBody"));
    }

    [Fact]
    public void Validate_WithNonPositiveTimeout_Fails()
    {
        var call = ValidCall();
        call.TimeoutSeconds = 0;
        var options = new ApiClientOptions { ApiCalls = new List<ApiCallOptions> { call } };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("TimeoutSeconds must be a positive value"));
    }
}
