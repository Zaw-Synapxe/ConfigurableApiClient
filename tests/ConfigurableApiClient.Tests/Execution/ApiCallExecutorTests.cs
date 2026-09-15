using System.Net;
using ConfigurableApiClient.Configuration;
using ConfigurableApiClient.Execution;
using ConfigurableApiClient.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConfigurableApiClient.Tests.Execution;

/// <summary>
/// A minimal HttpMessageHandler stub that returns pre-programmed responses per call order,
/// or invokes a custom responder function keyed by request URI.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    public List<HttpRequestMessage> Requests { get; } = new();

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_responder(request));
    }
}

internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}

public class ApiCallExecutorTests
{
    private static ApiCallOptions Call(string name, bool enabled = true, string method = "GET") => new()
    {
        Name = name,
        Enabled = enabled,
        Method = method,
        Url = "https://example.org/api/" + name,
        TimeoutSeconds = 5,
    };

    [Fact]
    public async Task ExecuteAllAsync_RunsEnabledCallsSequentially_InConfiguredOrder()
    {
        var order = new List<string>();
        var handler = new StubHttpMessageHandler(req =>
        {
            order.Add(req.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var executor = new ApiCallExecutor(
            new StubHttpClientFactory(handler), new ApiRequestBuilder(), NullLogger<ApiCallExecutor>.Instance, NullLoggerFactory.Instance);

        var calls = new List<ApiCallOptions> { Call("First"), Call("Second"), Call("Third") };

        var results = await executor.ExecuteAllAsync(calls, CancellationToken.None);

        Assert.Equal(new[] { "/api/First", "/api/Second", "/api/Third" }, order);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task ExecuteAllAsync_SkipsDisabledCalls()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var executor = new ApiCallExecutor(
            new StubHttpClientFactory(handler), new ApiRequestBuilder(), NullLogger<ApiCallExecutor>.Instance, NullLoggerFactory.Instance);

        var calls = new List<ApiCallOptions> { Call("Enabled"), Call("Disabled", enabled: false) };

        var results = await executor.ExecuteAllAsync(calls, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("Enabled", results[0].Name);
    }

    [Fact]
    public async Task ExecuteAllAsync_ContinuesAfterHttp500_AndReportsFailure()
    {
        var responses = new Queue<HttpStatusCode>(new[] { HttpStatusCode.InternalServerError, HttpStatusCode.OK });
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(responses.Dequeue()));
        var executor = new ApiCallExecutor(
            new StubHttpClientFactory(handler), new ApiRequestBuilder(), NullLogger<ApiCallExecutor>.Instance, NullLoggerFactory.Instance);

        var calls = new List<ApiCallOptions> { Call("First"), Call("Second") };

        var results = await executor.ExecuteAllAsync(calls, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.False(results[0].Success);
        Assert.Equal(500, results[0].StatusCode);
        Assert.True(results[1].Success);
    }

    [Fact]
    public async Task ExecuteAllAsync_ContinuesAfterRequestException()
    {
        var callCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new HttpRequestException("network down");
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var executor = new ApiCallExecutor(
            new StubHttpClientFactory(handler), new ApiRequestBuilder(), NullLogger<ApiCallExecutor>.Instance, NullLoggerFactory.Instance);

        var calls = new List<ApiCallOptions> { Call("First"), Call("Second") };

        var results = await executor.ExecuteAllAsync(calls, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.False(results[0].Success);
        Assert.NotNull(results[0].ErrorMessage);
        Assert.True(results[1].Success);
    }

    [Fact]
    public async Task ExecuteAllAsync_ContinuesAfterTimeout()
    {
        var callCount = 0;
        var handler = new DelegatingStubHandler(async (req, ct) =>
        {
            callCount++;
            if (callCount == 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var executor = new ApiCallExecutor(
            new StubHttpClientFactory(handler), new ApiRequestBuilder(), NullLogger<ApiCallExecutor>.Instance, NullLoggerFactory.Instance);

        var first = Call("First");
        first.TimeoutSeconds = 1;
        var calls = new List<ApiCallOptions> { first, Call("Second") };

        var results = await executor.ExecuteAllAsync(calls, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.False(results[0].Success);
        Assert.Contains("timed out", results[0].ErrorMessage);
        Assert.True(results[1].Success);
    }

    [Fact]
    public async Task ExecuteAllAsync_AggregatesSuccessAndFailureCounts()
    {
        var responses = new Queue<HttpStatusCode>(new[]
        {
            HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.OK,
        });
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(responses.Dequeue()));
        var executor = new ApiCallExecutor(
            new StubHttpClientFactory(handler), new ApiRequestBuilder(), NullLogger<ApiCallExecutor>.Instance, NullLoggerFactory.Instance);

        var calls = new List<ApiCallOptions> { Call("A"), Call("B"), Call("C") };

        var results = await executor.ExecuteAllAsync(calls, CancellationToken.None);

        Assert.Equal(2, results.Count(r => r.Success));
        Assert.Equal(1, results.Count(r => !r.Success));
    }
}

internal sealed class DelegatingStubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

    public DelegatingStubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => _responder(request, cancellationToken);
}
