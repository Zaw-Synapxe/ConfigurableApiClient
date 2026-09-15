using System.Diagnostics;
using ConfigurableApiClient.Configuration;
using ConfigurableApiClient.Http;
using Microsoft.Extensions.Logging;

namespace ConfigurableApiClient.Execution;

/// <summary>
/// Executes enabled API calls sequentially, in configured order, continuing after failures.
/// </summary>
public interface IApiCallExecutor
{
    Task<IReadOnlyList<ApiCallResult>> ExecuteAllAsync(
        IEnumerable<ApiCallOptions> calls,
        CancellationToken cancellationToken);
}

public sealed class ApiCallExecutor : IApiCallExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApiRequestBuilder _requestBuilder;
    private readonly ILogger<ApiCallExecutor> _logger;
    private readonly ILogger _performanceLogger;

    public ApiCallExecutor(
        IHttpClientFactory httpClientFactory,
        IApiRequestBuilder requestBuilder,
        ILogger<ApiCallExecutor> logger,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _requestBuilder = requestBuilder;
        _logger = logger;
        // Dedicated logger category so performance entries can be routed to their own log file.
        _performanceLogger = loggerFactory.CreateLogger("Performance");
    }

    public async Task<IReadOnlyList<ApiCallResult>> ExecuteAllAsync(
        IEnumerable<ApiCallOptions> calls,
        CancellationToken cancellationToken)
    {
        var results = new List<ApiCallResult>();

        foreach (var call in calls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!call.Enabled)
            {
                _logger.LogInformation("Skipping disabled call '{Name}'.", call.Name);
                continue;
            }

            results.Add(await ExecuteOneAsync(call, cancellationToken));
        }

        return results;
    }

    private async Task<ApiCallResult> ExecuteOneAsync(ApiCallOptions call, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(call.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        _logger.LogInformation("Starting API call '{Name}' {Method} {Url}.", call.Name, call.Method, call.Url);

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(ConfigurableApiClient));
            using var request = _requestBuilder.Build(call);

            using var response = await client.SendAsync(request, linkedCts.Token);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            var result = new ApiCallResult
            {
                Name = call.Name,
                Method = call.Method,
                Url = call.Url,
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ResponseBody = body,
                Duration = stopwatch.Elapsed,
            };

            if (result.Success)
            {
                _logger.LogInformation(
                    "Call '{Name}' completed with status {StatusCode} in {ElapsedMs} ms.",
                    call.Name,
                    result.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogError(
                    "Call '{Name}' returned unsuccessful status {StatusCode} in {ElapsedMs} ms.",
                    call.Name,
                    result.StatusCode,
                    stopwatch.ElapsedMilliseconds);
            }

            LogPerformance(call, result);
            return result;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            var result = new ApiCallResult
            {
                Name = call.Name,
                Method = call.Method,
                Url = call.Url,
                Success = false,
                ErrorMessage = $"Request timed out after {call.TimeoutSeconds}s.",
                Duration = stopwatch.Elapsed,
            };

            _logger.LogError(
                "Call '{Name}' timed out after {TimeoutSeconds}s ({ElapsedMs} ms elapsed).",
                call.Name,
                call.TimeoutSeconds,
                stopwatch.ElapsedMilliseconds);
            LogPerformance(call, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            // User-requested cancellation (e.g. Ctrl+C): propagate so the run stops.
            throw;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            var result = new ApiCallResult
            {
                Name = call.Name,
                Method = call.Method,
                Url = call.Url,
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed,
            };

            _logger.LogError(ex, "Call '{Name}' failed: {Message}", call.Name, ex.Message);
            LogPerformance(call, result);
            return result;
        }
    }

    private void LogPerformance(ApiCallOptions call, ApiCallResult result)
    {
        _performanceLogger.LogInformation(
            "{Name} {Method} {Url} | Status: {StatusCode} | Success: {Success} | Duration: {ElapsedMs} ms",
            call.Name,
            call.Method,
            call.Url,
            result.StatusCode?.ToString() ?? "N/A",
            result.Success,
            result.Duration.TotalMilliseconds);
    }
}
