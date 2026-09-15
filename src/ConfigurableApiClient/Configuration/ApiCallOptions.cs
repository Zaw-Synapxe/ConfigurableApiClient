namespace ConfigurableApiClient.Configuration;

/// <summary>
/// Configuration for a single API call defined under ApiClient:ApiCalls.
/// </summary>
public sealed class ApiCallOptions
{
    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Raw method string as bound from configuration (e.g. "GET", "POST").
    /// Validated and parsed into <see cref="ApiCallMethod"/> during startup validation.
    /// </summary>
    public string Method { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public BasicAuthOptions? BasicAuth { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new();

    public Dictionary<string, string> QueryParameters { get; set; } = new();

    public string? RawJsonBody { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}
