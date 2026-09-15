# ConfigurableApiClient

A .NET 10 console application that validates and executes multiple API calls defined
in `appsettings.json`, running them sequentially, continuing after failures, printing
each call's response, and returning a meaningful process exit code.

## Solution layout

```
ConfigurableApiClient.slnx
src/ConfigurableApiClient/        Console application
tests/ConfigurableApiClient.Tests/  xUnit test project
```

## Configuration

All calls are defined under the `ApiClient:ApiCalls` array in
`src/ConfigurableApiClient/appsettings.json`.

```json
{
  "ApiClient": {
    "ApiCalls": [
      {
        "Name": "GetPatientById",
        "Enabled": true,
        "Method": "GET",
        "Url": "https://example.org/api/FHIR/R4/Patient/12345",
        "BasicAuth": { "Enabled": true, "Username": "...", "Password": "..." },
        "Headers": { "Epic-Client-ID": "1234-1234-1234", "Epic-User-IDType": "EXTERNAL" },
        "QueryParameters": {},
        "RawJsonBody": null,
        "TimeoutSeconds": 30
      }
    ]
  }
}
```

### Field reference

| Field | Description |
|---|---|
| `Name` | Unique, required identifier for the call (used in output and validation errors). |
| `Enabled` | When `false`, the call is skipped entirely. |
| `Method` | `GET` or `POST` only. |
| `Url` | Absolute `http://` or `https://` URL. |
| `BasicAuth` | Optional. When `Enabled: true`, both `Username` and `Password` are required; a Base64 `Authorization: Basic ...` header is generated. Cannot be combined with a configured `Authorization` header. |
| `Headers` | Arbitrary request headers (e.g. `Epic-Client-ID`, `Epic-User-IDType`). |
| `QueryParameters` | Key/value pairs appended to the URL's query string (existing query values in `Url` are preserved). |
| `RawJsonBody` | Optional raw JSON string sent as `application/json` (UTF-8) for `POST` calls only. Must be valid JSON. Not allowed on `GET`. |
| `TimeoutSeconds` | Per-call timeout; must be a positive number. |

### Adding a new call

Add a new object to the `ApiCalls` array with a unique `Name`. Calls run in the order
they appear in the array.

### Local credential overrides

Because credentials in `appsettings.json` are stored in **clear text**, you can
optionally create a git-ignored `appsettings.Local.json` file next to
`appsettings.json` with the same shape (or just the fields you want to override) and
load it via the standard .NET configuration layering
(`appsettings.{EnvironmentName}.json` / environment variables) if you extend
`Program.cs` to add it as an additional configuration source. Add
`appsettings.Local.json` to `.gitignore` before committing real credentials.

## Running

```bash
dotnet build ConfigurableApiClient.slnx
dotnet run --project src/ConfigurableApiClient
```

## Output

For each enabled call, the tool prints:

- Call name
- Method and URL
- HTTP status code or exception/timeout message
- Duration
- Response body

Passwords and generated `Authorization` header values are never printed. A summary
line (`X succeeded, Y failed, Z total`) is printed at the end.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | All enabled calls succeeded (HTTP 2xx). |
| `1` | One or more enabled calls failed (non-2xx status, timeout, or request exception). |
| `2` | Startup/configuration validation failed; no requests were executed. |

## Validation

Configuration is validated eagerly at startup (`ValidateOnStart`) before any request
is sent. Validation covers: missing/duplicate call names, unsupported HTTP methods,
invalid HTTP/HTTPS URLs, incomplete Basic Auth credentials, invalid headers
(including a configured `Authorization` header conflicting with enabled Basic Auth),
malformed raw JSON bodies, `GET` calls with a body, and non-positive timeouts.

## Testing

```bash
dotnet test ConfigurableApiClient.slnx
```

Tests cover configuration validation, URL/query-string generation, Basic Auth and
custom header application, raw JSON content/media type, sequential execution order,
disabled-call skipping, continuation after HTTP 500/timeout/exception, and
success/failure aggregation.

## Scope (v1)

- `GET` and `POST` only.
- Basic Auth and custom headers (e.g. Epic headers) are configured per call.
- `POST` calls may combine query parameters and a raw JSON body.
- Calls run sequentially and continue after failure.
- Retries, parallel execution, OAuth, file uploads, request chaining, and response
  files are out of scope for v1.
