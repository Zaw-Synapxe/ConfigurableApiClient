namespace ConfigurableApiClient.Execution;

/// <summary>
/// The outcome of executing a single configured API call.
/// </summary>
public sealed class ApiCallResult
{
    public required string Name { get; init; }

    public required string Method { get; init; }

    public required string Url { get; init; }

    public bool Success { get; init; }

    public int? StatusCode { get; init; }

    public string? ErrorMessage { get; init; }

    public string? ResponseBody { get; init; }

    public TimeSpan Duration { get; init; }
}
