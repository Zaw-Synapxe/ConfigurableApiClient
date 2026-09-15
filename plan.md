Plan: Configurable .NET API Client
Create a .NET 10 console solution at C:\DIM\Zaw\ConfigurableApiClient. It will validate and execute multiple API calls from appsettings.json sequentially, continue after failures, print responses, and return a meaningful exit code.

Steps

1. Scaffold
Verify a .NET 10 SDK is installed with dotnet --list-sdks.
Create:
ConfigurableApiClient.sln
src\ConfigurableApiClient console project
tests\ConfigurableApiClient.Tests xUnit project
Add hosting, configuration, options validation, logging, and IHttpClientFactory dependencies.
2. Configuration
Create strongly typed models:
ApiClientOptions
ApiCallOptions
BasicAuthOptions
Define an ApiClient:ApiCalls array in appsettings.json.
Each call supports:
Name
Enabled
Method (GET or POST)
Url
Per-call BasicAuth
Per-call Headers
QueryParameters
Optional RawJsonBody
Include example GET and POST calls. Each repeats:
Epic-Client-ID: 1234-1234-1234
Epic-User-IDType: EXTERNAL
3. Validation
Add startup validation for:
Missing or duplicate call names
Unsupported methods
Invalid HTTP/HTTPS URLs
Incomplete Basic Auth credentials
Invalid headers
Malformed raw JSON
GET requests containing a body
Non-positive timeout
Use ValidateOnStart so no request runs when configuration is invalid.
4. Request Building
Implement IApiRequestBuilder and ApiRequestBuilder.
Build a fresh HttpRequestMessage for every call.
Append query parameters using proper URI escaping while preserving existing query values.
Generate Basic Auth using UTF-8 Base64 encoding of username:password.
Apply configured request and content headers.
Send POST raw JSON as UTF-8 application/json.
Reject conflicting configured Authorization headers when Basic Auth is enabled.
5. Execution
Implement IApiCallExecutor using IHttpClientFactory.
Run enabled calls sequentially in configured order.
Continue after HTTP errors, timeouts, and request exceptions.
Treat only HTTP 2xx responses as successful.
Print:
Call name
Method and URL
Status code or exception
Duration
Response body
Never print passwords or generated Authorization values.
Support Ctrl+C cancellation.
6. Exit Codes
Return:
0: all enabled calls succeeded
1: one or more calls failed
2: startup or configuration validation failed
7. Testing
Test configuration validation.
Test exact URL and query-string generation.
Test Basic Auth and Epic headers.
Test raw JSON content and media type.
Test sequential execution and disabled calls.
Test continuation after HTTP 500, timeout, or exception.
Test success and failure aggregation.
8. Documentation
Add a README covering configuration fields, adding calls, commands, output, and exit codes.
Document that credentials in appsettings.json are clear text.
Optionally support a git-ignored appsettings.Local.json override for safer local credentials.
Key Files

src\ConfigurableApiClient\Program.cs
src\ConfigurableApiClient\appsettings.json
src\ConfigurableApiClient\Configuration\ApiClientOptions.cs
src\ConfigurableApiClient\Configuration\ApiClientOptionsValidator.cs
src\ConfigurableApiClient\Http\ApiRequestBuilder.cs
src\ConfigurableApiClient\Execution\ApiCallExecutor.cs
src\ConfigurableApiClient\Execution\ApiCallResult.cs
tests\ConfigurableApiClient.Tests\
README.md
Verification

Run dotnet format --verify-no-changes.
Run dotnet build ConfigurableApiClient.sln.
Run dotnet test ConfigurableApiClient.sln.
Verify requests against a mock HTTP server.
Make the first call return 500 and the second return 200; both must execute and the process must exit with 1.
Supply malformed JSON; no requests should run and the process must exit with 2.
Scope Decisions

GET and POST only.
Parameters mean query-string parameters.
Basic Auth and Epic headers are configured per call.
POST may contain both query parameters and raw JSON.
Calls run sequentially and continue after failure.
Retries, parallel execution, OAuth, file uploads, request chaining, and response files are excluded from version 1.