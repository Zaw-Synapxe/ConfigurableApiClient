using ConfigurableApiClient.Configuration;
using ConfigurableApiClient.Execution;
using ConfigurableApiClient.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace ConfigurableApiClient;

/// <summary>
/// Exit codes:
/// 0 - all enabled calls succeeded
/// 1 - one or more calls failed
/// 2 - startup or configuration validation failed
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Anchor the working directory to the executable's own folder. Without this, when the
        // exe is launched from a different working directory (shortcuts, scripts, Task
        // Scheduler, etc.), Host.CreateApplicationBuilder fails to find appsettings.json and
        // Serilog's relative "logs/..." file paths resolve elsewhere, so no log files (including
        // Info logs) are produced.
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        // Bootstrap logger captures any failures that occur before the host (and its
        // configuration-driven Serilog pipeline) has finished initializing.
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            return await RunAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "ConfigurableApiClient terminated unexpectedly.");
            return 2;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services
            .AddOptions<ApiClientOptions>()
            .Bind(builder.Configuration.GetSection(ApiClientOptions.SectionName))
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<ApiClientOptions>, ApiClientOptionsValidator>();
        builder.Services.AddHttpClient(nameof(ConfigurableApiClient));
        builder.Services.AddSingleton<IApiRequestBuilder, ApiRequestBuilder>();
        builder.Services.AddSingleton<IApiCallExecutor, ApiCallExecutor>();

        builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services));

        using var host = builder.Build();

        IOptions<ApiClientOptions> options;
        try
        {
            // Resolving IOptions here triggers ValidateOnStart eagerly, before any request runs.
            options = host.Services.GetRequiredService<IOptions<ApiClientOptions>>();
            _ = options.Value;
        }
        catch (OptionsValidationException ex)
        {
            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
            foreach (var failure in ex.Failures)
            {
                logger.LogError("Configuration error: {Failure}", failure);
            }

            return 2;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var executor = host.Services.GetRequiredService<IApiCallExecutor>();

        IReadOnlyList<ApiCallResult> results;
        try
        {
            results = await executor.ExecuteAllAsync(options.Value.ApiCalls, cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Execution cancelled by user.");
            return 1;
        }

        PrintResults(results, host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Results"));

        return results.Count > 0 && results.All(r => r.Success) ? 0 : 1;
    }

    private static void PrintResults(IReadOnlyList<ApiCallResult> results, Microsoft.Extensions.Logging.ILogger logger)
    {
        foreach (var result in results)
        {
            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"Call: {result.Name}");
            Console.WriteLine($"{result.Method} {result.Url}");
            Console.WriteLine(result.StatusCode is not null
                ? $"Status: {result.StatusCode}"
                : $"Error: {result.ErrorMessage}");
            Console.WriteLine($"Duration: {result.Duration.TotalMilliseconds:F0} ms");

            if (!string.IsNullOrEmpty(result.ResponseBody))
            {
                Console.WriteLine("Response body:");
                Console.WriteLine(result.ResponseBody);
            }

            logger.LogInformation(
                "Result - Call: {Name} | {Method} {Url} | Status: {StatusCode} | Error: {ErrorMessage} | Duration: {ElapsedMs} ms",
                result.Name,
                result.Method,
                result.Url,
                result.StatusCode?.ToString() ?? "N/A",
                result.ErrorMessage ?? "None",
                result.Duration.TotalMilliseconds);
        }

        Console.WriteLine("----------------------------------------");

        var succeeded = results.Count(r => r.Success);
        var failed = results.Count - succeeded;
        Console.WriteLine($"Summary: {succeeded} succeeded, {failed} failed, {results.Count} total.");
        logger.LogInformation(
            "Summary: {Succeeded} succeeded, {Failed} failed, {Total} total.",
            succeeded,
            failed,
            results.Count);
    }
}
