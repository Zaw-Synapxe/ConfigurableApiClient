namespace ConfigurableApiClient.Configuration;

/// <summary>
/// Root configuration bound from the "ApiClient" configuration section.
/// </summary>
public sealed class ApiClientOptions
{
    public const string SectionName = "ApiClient";

    public List<ApiCallOptions> ApiCalls { get; set; } = new();
}
