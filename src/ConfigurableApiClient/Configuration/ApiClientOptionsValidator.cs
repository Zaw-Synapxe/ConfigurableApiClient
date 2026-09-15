using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ConfigurableApiClient.Configuration;

/// <summary>
/// Validates <see cref="ApiClientOptions"/> at startup via ValidateOnStart so that
/// no requests are ever executed against an invalid configuration.
/// </summary>
public sealed class ApiClientOptionsValidator : IValidateOptions<ApiClientOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiClientOptions options)
    {
        var failures = new List<string>();

        if (options.ApiCalls is null || options.ApiCalls.Count == 0)
        {
            failures.Add("ApiClient:ApiCalls must contain at least one call definition.");
            return ValidateOptionsResult.Fail(failures);
        }

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < options.ApiCalls.Count; i++)
        {
            var call = options.ApiCalls[i];
            var label = string.IsNullOrWhiteSpace(call.Name) ? $"ApiCalls[{i}]" : call.Name;

            if (string.IsNullOrWhiteSpace(call.Name))
            {
                failures.Add($"ApiCalls[{i}]: Name is required.");
            }
            else if (!seenNames.Add(call.Name))
            {
                failures.Add($"ApiCalls[{i}] ('{call.Name}'): duplicate call name.");
            }

            if (string.IsNullOrWhiteSpace(call.Method) ||
                !Enum.TryParse<ApiCallMethod>(call.Method, ignoreCase: true, out _))
            {
                failures.Add($"{label}: Method '{call.Method}' is not supported. Use GET or POST.");
            }

            if (string.IsNullOrWhiteSpace(call.Url) ||
                !Uri.TryCreate(call.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                failures.Add($"{label}: Url '{call.Url}' is not a valid absolute HTTP/HTTPS URL.");
            }

            if (call.BasicAuth is { Enabled: true } basicAuth)
            {
                if (string.IsNullOrWhiteSpace(basicAuth.Username) || string.IsNullOrWhiteSpace(basicAuth.Password))
                {
                    failures.Add($"{label}: BasicAuth is enabled but Username or Password is missing.");
                }
            }

            if (call.Headers is not null)
            {
                foreach (var (headerName, headerValue) in call.Headers)
                {
                    if (string.IsNullOrWhiteSpace(headerName))
                    {
                        failures.Add($"{label}: a configured header has an empty name.");
                    }

                    if (headerValue is null)
                    {
                        failures.Add($"{label}: header '{headerName}' has a null value.");
                    }

                    if (call.BasicAuth is { Enabled: true } &&
                        string.Equals(headerName, "Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add($"{label}: configured Authorization header conflicts with enabled BasicAuth.");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(call.RawJsonBody))
            {
                try
                {
                    using var _ = JsonDocument.Parse(call.RawJsonBody);
                }
                catch (JsonException ex)
                {
                    failures.Add($"{label}: RawJsonBody is not valid JSON ({ex.Message}).");
                }

                if (string.Equals(call.Method, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"{label}: GET requests must not include a RawJsonBody.");
                }
            }

            if (call.TimeoutSeconds <= 0)
            {
                failures.Add($"{label}: TimeoutSeconds must be a positive value.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
